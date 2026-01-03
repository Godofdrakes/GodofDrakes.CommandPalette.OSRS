using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using GodofDrakes.CommandPalette.Reactive.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Polly;
using Polly.Registry;
using ReactiveUI;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;
using WikiPageAndUrl = (WikiClientLibrary.Pages.WikiPage Page, string Url);

namespace GodofDrakes.CommandPalette.OSRS.ViewModels;

public sealed partial class OpenSearchViewModel : DynamicListViewModel, IDisposable
{
	private readonly CompositeDisposable _onDispose = [];

	private readonly WikiSite _wikiSite;
	private readonly ResiliencePipeline? _pipeline;
	private readonly ReactiveCommand<string, IList<IListItem>> _searchCommand;
	private readonly ObservableAsPropertyHelper<bool> _isLoading;

	public bool IsLoading => _isLoading.Value;

	public OpenSearchViewModel( WikiSite wikiSite, ResiliencePipelineProvider<Type>? pipelineProvider )
	{
		ArgumentNullException.ThrowIfNull( wikiSite );

		_wikiSite = wikiSite;
		_pipeline = pipelineProvider?.GetPipeline( typeof(OpenSearchViewModel) );

		_searchCommand = ReactiveCommand.CreateFromObservable( ( string s ) =>
		{
			return Observable.Return( s )
				.SelectMany( OpenSearchAsync )
				.SelectMany( CreateWikiPages )
				.Select( CreateListItems );
		} );

		_isLoading = _searchCommand.IsExecuting.ToProperty( this, x => x.IsLoading );

		this.WhenAnyValue( x => x.SearchText )
			.Throttle( TimeSpan.FromMilliseconds( 1000 ) )
			.DistinctUntilChanged()
			.InvokeCommand( _searchCommand )
			.DisposeWith( _onDispose );

		_onDispose.Add( _searchCommand );
		_onDispose.Add( _isLoading );
	}

	public override IObservable<IChangeSet<IListItem, IListItem>> Connect()
	{
		return ObservableChangeSet.Create( CreateChangeSet, ( IListItem item ) => item );
	}

	private IDisposable CreateChangeSet( ISourceCache<IListItem, IListItem> cache )
	{
		var dispose = new CompositeDisposable();

		_searchCommand
			.Subscribe( list =>
			{
				cache.Edit( updater =>
				{
					updater.Load( list );
				} );
			} )
			.DisposeWith( dispose );

		return dispose;
	}

	private Task<IList<OpenSearchResultEntry>> OpenSearchAsync( string s, CancellationToken token )
	{
		if ( string.IsNullOrWhiteSpace( s ) )
		{
			// Empty string causes OpenSearch to stall.
			// Also avoids utilizing the pipeline for trivial work.

			return Task.FromResult<IList<OpenSearchResultEntry>>( [] );
		}

		return Run( Impl, token );

		Task<IList<OpenSearchResultEntry>> Impl( CancellationToken t )
		{
			var task = _wikiSite.OpenSearchAsync( s, OpenSearchOptions.None );

			return task.WaitAsync( t );
		}
	}

	private Task<List<WikiPageAndUrl>> CreateWikiPages( IList<OpenSearchResultEntry> search, CancellationToken token )
	{
		if ( search.Count < 1 )
		{
			// Avoids utilizing the pipeline for trivial work

			return Task.FromResult<List<WikiPageAndUrl>>( [] );
		}

		return Run( Impl, token );

		async Task<List<WikiPageAndUrl>> Impl( CancellationToken t )
		{
			var list = search
				.Where( entry => !string.IsNullOrWhiteSpace( entry.Title ) )
				.Select( WikiPageAndUrl ( entry ) => (new WikiPage( _wikiSite, entry.Title ), entry.Url) )
				.ToList();

			await list.Select( pair => pair.Page ).RefreshAsync( new WikiPageQueryProvider()
			{
				Properties =
				[
					new PageInfoPropertyProvider(),
					new PageImagesPropertyProvider()
					{
						ThumbnailSize = 200,
					},
				],
			}, t );

			return list;
		}
	}

	private static IList<IListItem> CreateListItems( List<WikiPageAndUrl> list )
	{
		return list.Select( IListItem ( pair ) =>
			{
				return new ListItem()
				{
					Title = pair.Page.Title ?? string.Empty,
					Subtitle = pair.Url,
					Command = new OpenUrlCommand( pair.Url )
					{
						Result = CommandResult.Dismiss(),
					},
				};
			} )
			.ToList();
	}

	private Task<T> Run<T>( Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken )
	{
		ArgumentNullException.ThrowIfNull( work );

		if ( _pipeline is not null )
		{
			return RunAsync();

			async Task<T> RunAsync()
			{
				var task = _pipeline.ExecuteAsync( async token => await work( token ), cancellationToken );

				return await task;
			}
		}

		return work( cancellationToken );
	}

	public void Dispose()
	{
		_onDispose.Dispose();
	}
}