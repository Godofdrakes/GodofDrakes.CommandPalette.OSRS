// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using GodofDrakes.CommandPalette.OSRS.ViewModel;
using GodofDrakes.CommandPalette.Reactive;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using ReactiveUI;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;

namespace GodofDrakes.CommandPalette.OSRS.Pages;

internal sealed partial class WikiSearchPage : DynamicListPage, IDisposable
{
	private readonly CompositeDisposable _onDispose = [];
	private readonly ReadOnlyObservableCollection<IListItem> _listItems;
	private readonly ILogger<WikiSearchPage> _logger;
	private readonly ResiliencePipeline _pipeline;
	private readonly WikiSite _wiki;

	private readonly BehaviorSubject<string> _searchText = new( string.Empty );

	public WikiSearchPage( ILogger<WikiSearchPage> logger, ResiliencePipelineProvider<string> provider, WikiSite wiki )
	{
		ArgumentNullException.ThrowIfNull( logger );
		ArgumentNullException.ThrowIfNull( provider );
		ArgumentNullException.ThrowIfNull( wiki );

		this.Icon = IconHelpers.FromRelativePath( "Assets\\StoreLogo.png" );

		this.EmptyContent = new CommandItem()
		{
			Title = "Type To Search",
		};

		_logger = logger;
		_pipeline = provider.GetPipeline( "OpenSearch" );
		_wiki = wiki;

		var results = ObservableChangeSet.Create( cache =>
			{
				var onDispose = new CompositeDisposable();

				_searchText
					.Throttle( TimeSpan.FromMilliseconds( 1000 ) )
					.SelectMany( OpenSearchAsync )
					.Subscribe( list =>
					{
						cache.Edit( source =>
						{
							source.Load( list );
						} );
					} )
					.DisposeWith( onDispose );

				return onDispose;
			}, ( WikiPageViewModel entry ) => entry.Title! )
			.Publish()
			.RefCount();

		results
			.Subscribe( _ => { }, ex =>
			{
				LogException( ex );

				var error = new StatusMessage()
				{
					Message = ex.Message,
					State = MessageState.Error,
				};

				ExtensionHost.ShowStatus( error, StatusContext.Page );
			} )
			.DisposeWith( _onDispose );

		results
			.Transform( CreateSearchItemView )
			.DisposeMany()
			.Bind( out _listItems )
			.Do( _ => RaiseItemsChanged() )
			.Subscribe()
			.DisposeWith( _onDispose );
	}

	private async Task<IList<OpenSearchResultEntry>> PageSearchAsync( string text, CancellationToken t )
	{
		ArgumentNullException.ThrowIfNullOrWhiteSpace( text );

		return await _wiki.OpenSearchAsync( text, 20, OpenSearchOptions.None, t );
	}

	private async Task<IEnumerable<WikiPageViewModel>> OpenSearchAsync( string text, CancellationToken token )
	{
		if ( string.IsNullOrWhiteSpace( text ) )
		{
			return [];
		}

		await _wiki.Initialization;

		var entries = await _pipeline.ExecuteAsync( async t => await PageSearchAsync( text, t ), token );

		var list = new List<WikiPageViewModel>();

		var lookup = new Dictionary<string, WikiPageViewModel>();

		foreach ( var entry in entries )
		{
			if ( string.IsNullOrWhiteSpace( entry.Title ) )
			{
				LogInvalidEntry( entry.Title, entry.Url );

				continue;
			}

			var item = new WikiPageViewModel()
			{
				Title = entry.Title,
				Description = entry.Description,
				Url = entry.Url,
				ThumbnailUrl = null,
			};

			list.Add( item );
			lookup.Add( entry.Title, item );
		}

		// Kick off a thumbnail search in the background, but don't wait for it to finish.
		// The view models will notify the view when the thumbnail is updated.
		_ = _pipeline.ExecuteAsync( ThumbnailSearchAsync, token ).AsTask();

		return list;

		async ValueTask ThumbnailSearchAsync( CancellationToken t )
		{
			var pages = entries
				.Select( entry => new WikiPage( _wiki, entry.Title ) )
				.ToList();

			await pages.RefreshAsync( new WikiPageQueryProvider()
			{
				Properties =
				{
					new PageInfoPropertyProvider(),
					new PageImagesPropertyProvider()
					{
						ThumbnailSize = 200,
					}
				}
			}, cancellationToken: t );

			foreach ( var page in pages )
			{
				if ( string.IsNullOrEmpty( page.Title ) )
				{
					throw new InvalidOperationException();
				}

				if ( !lookup.TryGetValue( page.Title, out var item ) )
				{
					continue;
				}

				using (item.DelayChangeNotifications())
				{
					item.IsRedirector = page.IsRedirect;

					var thumbnailInfo = page.GetPropertyGroup<PageImagesPropertyGroup>();

					if ( thumbnailInfo is not null )
					{
						item.ThumbnailUrl = thumbnailInfo.ThumbnailImage.Url;
					}
				}
			}
		}
	}

	[SuppressMessage( "Trimming", "IL2026" )]
	[SuppressMessage( "AOT", "IL3050" )]
	private static IListItem CreateSearchItemView( WikiPageViewModel viewModel )
	{
		return ListItemView.Create( view =>
		{
			var onDispose = new CompositeDisposable();

			viewModel
				.WhenAnyValue<WikiPageViewModel, string>( nameof(viewModel.Title) )
				.Subscribe( text => view.Title = text )
				.DisposeWith( onDispose );

			viewModel
				.WhenAnyValue<WikiPageViewModel, string?>( nameof(viewModel.Description) )
				.Subscribe( text => view.Subtitle = text ?? string.Empty )
				.DisposeWith( onDispose );

			viewModel
				.WhenAnyValue<WikiPageViewModel, string?>( nameof(viewModel.Url) )
				.Subscribe( url =>
				{
					if ( string.IsNullOrEmpty( url ) )
					{
						view.Command = new NoOpCommand();
						return;
					}

					view.Command = new OpenUrlCommand( url )
					{
						Result = CommandResult.Dismiss(),
					};
				} )
				.DisposeWith( onDispose );

			var whenThumbnailChanged = viewModel
				.WhenAnyValue<WikiPageViewModel, string?>( nameof(viewModel.ThumbnailUrl) );

			var whenRedirectorChanged = viewModel
				.WhenAnyValue<WikiPageViewModel, bool>( nameof(viewModel.IsRedirector) );

			whenThumbnailChanged
				.CombineLatest( whenRedirectorChanged, ( url, isRedirector ) => (url, isRedirector) )
				.Subscribe( pair =>
				{
					if ( !string.IsNullOrWhiteSpace( pair.url ) )
					{
						view.Icon = new IconInfo( pair.url );

						return;
					}

					if ( pair.isRedirector )
					{
						view.Icon = Icons.Link;

						return;
					}

					view.Icon = Icons.OpenInExternalWindow;
				} )
				.DisposeWith( onDispose );

			return onDispose;
		} );
	}

	public override IListItem[] GetItems()
	{
		return _listItems.ToArray();
	}

	public void Dispose()
	{
		_onDispose.Dispose();
	}

	public override void UpdateSearchText( string oldSearch, string newSearch )
	{
		_searchText.OnNext( newSearch );
	}

	[LoggerMessage( LogLevel.Error )]
	private partial void LogException( Exception exception );

	[LoggerMessage( LogLevel.Warning, "Invalid OpenSearchResultEntry {{ Title: {Title}, Url: {Url} }}" )]
	private partial void LogInvalidEntry( string? title, string? url );
}