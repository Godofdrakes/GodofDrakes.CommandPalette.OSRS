using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;
using GodofDrakes.CommandPalette.OSRS.Extensions;
using GodofDrakes.CommandPalette.Reactive.Interfaces;
using GodofDrakes.CommandPalette.Reactive.ViewModels;
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

namespace GodofDrakes.CommandPalette.OSRS.ViewModels;

public sealed partial class OpenSearchViewModel : DynamicListViewModel, INotifyLoading, IDisposable
{
	private readonly CompositeDisposable _onDispose = [];

	private readonly WikiSite _wikiSite;
	private readonly ILogger<OpenSearchViewModel> _logger;
	private readonly ResiliencePipeline? _pipeline;
	private readonly ReactiveCommand<string, IList<OpenSearchResultEntry>> _searchCommand;
	private readonly ObservableAsPropertyHelper<bool> _isLoading;

	public bool IsLoading => _isLoading.Value;

	public OpenSearchViewModel(
		WikiSite wikiSite,
		ILogger<OpenSearchViewModel> logger,
		ResiliencePipelineProvider<Type>? pipelineProvider )
	{
		ArgumentNullException.ThrowIfNull( wikiSite );
		ArgumentNullException.ThrowIfNull( logger );

		_wikiSite = wikiSite;
		_logger = logger;
		_pipeline = pipelineProvider?.GetPipeline( typeof(OpenSearchViewModel) );

		_searchCommand = ReactiveCommand.CreateFromTask( async ( string s, CancellationToken token ) =>
			{
				return await OpenSearchAsync( s, token );
			} )
			.DisposeWith( _onDispose );

		_searchCommand.ThrownExceptions
			.Subscribe( exception =>
			{
				var status = new StatusMessage()
				{
					Message = exception.Message,
					State = MessageState.Error,
				};

				ExtensionHost.ShowStatus( status, StatusContext.Page );
			} ).DisposeWith( _onDispose );

		_isLoading = _searchCommand.IsExecuting
			.ToProperty( this, x => x.IsLoading )
			.DisposeWith( _onDispose );

		this.WhenAnyValue( x => x.SearchText )
			.Throttle( TimeSpan.FromMilliseconds( 1000 ) )
			.DistinctUntilChanged()
			.InvokeCommand( _searchCommand )
			.DisposeWith( _onDispose );
	}

	public override IObservable<IChangeSet<IListItem, string>> Connect()
	{
		var searchResults = ObservableChangeSet.Create<WikiPage, string>( cache =>
		{
			var onDispose = new CompositeDisposable();

			_searchCommand
				.Select( entries =>
				{
					return entries.Select( entry =>
					{
						return new WikiPage( _wikiSite, entry.Title );
					} );
				} )
				.Subscribe( pages =>
				{
					cache.Edit( updater =>
					{
						updater.Load( pages );
					} );
				} )
				.DisposeWith( onDispose );

			return onDispose;
		}, page => page.Title! );

		return searchResults
			.TransformOnObservable( sourcePage =>
			{
				return Observable.Create<WikiPage>( async ( o, token ) =>
				{
					try
					{
						var workingPage = sourcePage;

						await RefreshPageInfoAsync( workingPage, token );

						if ( workingPage.IsRedirect )
						{
							var target = await ResolveRedirectsAsync( workingPage, token );

							if ( target is not null )
							{
								workingPage = target;
							}
						}

						o.OnNext( workingPage );
					}
					catch ( OperationCanceledException )
					{
						o.OnCompleted();
					}
					catch ( Exception exception )
					{
						o.OnError( exception );
					}
				} );
			} )
			.AutoRefreshOnObservable( page =>
			{
				return Observable.FromAsync( token =>
				{
					return RefreshPageDetailsAsync( page, token );
				} );
			} )
			.TransformWithInlineUpdate( CreateListItem, UpdateListItem, transformOnRefresh: true )
			.Cast( IListItem ( item ) => item );
	}

	private Task<WikiPage> RefreshPageInfoAsync( WikiPage page, CancellationToken cancellationToken )
	{
		ArgumentException.ThrowIfNullOrWhiteSpace( page.Title, nameof(page) );

		return Run( ImplAsync, cancellationToken );

		async Task<WikiPage> ImplAsync( CancellationToken token )
		{
			_logger.LogDebug( "Refreshing info for {Page}", page.Title! );

			await page.RefreshAsync( new WikiPageQueryProvider()
			{
				Properties =
				[
					new PageInfoPropertyProvider(),
				],
			}, token );

			_logger.LogDebug( "Refreshed info for {Page} ({IsRedirect})", page.Title!, page.IsRedirect );

			return page;
		}
	}

	private Task<WikiPage?> ResolveRedirectsAsync( WikiPage page, CancellationToken cancellationToken )
	{
		ArgumentException.ThrowIfNullOrWhiteSpace( page.Title, nameof(page) );

		if ( !page.IsRedirect )
		{
			throw new ArgumentException( "Page is not a redirector", nameof(page) );
		}

		return Run( ImplAsync, cancellationToken );

		async Task<WikiPage?> ImplAsync( CancellationToken token )
		{
			_logger.LogDebug( "Resolving redirects for {Page}", page.Title! );

			var targetPage = new WikiPage( page.Site, page.Title! );

			await targetPage.RefreshAsync( PageQueryOptions.ResolveRedirects, token );

			if ( targetPage.RedirectPath.Count <= 0 )
			{
				_logger.LogDebug( "Failed to resolve redirects for {Page}", page.Title! );

				return null;
			}

			_logger.LogDebug( "Resolved redirects for {Page}. Found {Target}.", page.Title!, targetPage.Title! );

			return targetPage;
		}
	}

	private Task<WikiPage> RefreshPageDetailsAsync( WikiPage page, CancellationToken cancellationToken )
	{
		return Run( ImplAsync, cancellationToken );

		async Task<WikiPage> ImplAsync( CancellationToken token )
		{
			_logger.LogDebug( "Refreshing details for {Page}", page.Title! );

			await page.RefreshAsync( new WikiPageQueryProvider()
			{
				Properties =
				[
					new PageImagesPropertyProvider()
					{
						ThumbnailSize = 200,
					},
					new ExtractsPropertyProvider()
					{
						AsPlainText = true,
						IntroductionOnly = true,
						MaxSentences = 1,
					},
				],
			}, token );

			_logger.LogDebug( "Refreshed details for {Page}", page.Title! );

			return page;
		}
	}

	private Task<IList<OpenSearchResultEntry>> OpenSearchAsync( string s, CancellationToken token )
	{
		if ( string.IsNullOrWhiteSpace( s ) )
		{
			// Empty string causes OpenSearch to stall.
			// Also avoids utilizing the pipeline for trivial work.

			return Task.FromResult<IList<OpenSearchResultEntry>>( [] );
		}

		return Run( ImplAsync, token );

		async Task<IList<OpenSearchResultEntry>> ImplAsync( CancellationToken t )
		{
			const int maxCount = 10;
			const OpenSearchOptions options = OpenSearchOptions.None;
			return await _wikiSite.OpenSearchAsync( s, maxCount, options, t );
		}
	}

	private static ListItem CreateListItem( WikiPage page )
	{
		var url = page.Site.SiteInfo.MakeArticleUrl( page.Title! );

		return new ListItem()
		{
			Icon = Icons.OpenInExternalWindow,
			Title = page.Title!,
			Subtitle = url,
			Command = new OpenUrlCommand( url )
			{
				Result = CommandResult.Dismiss(),
			},
		};
	}

	private static void UpdateListItem( ListItem item, WikiPage page )
	{
		if ( page.TryGetPropertyGroup( out PageImagesPropertyGroup? pageImages ) )
		{
			item.Icon = new IconInfo( pageImages.ThumbnailImage.Url );
		}

		if ( !string.IsNullOrWhiteSpace( page.Title ) )
		{
			var url = page.Site.SiteInfo.MakeArticleUrl( page.Title! );

			item.Title = page.Title;

			// This may get overridden below
			item.Subtitle = url;

			item.Command = new OpenUrlCommand( url )
			{
				Result = CommandResult.Dismiss(),
			};
		}

		if ( page.TryGetPropertyGroup( out ExtractsPropertyGroup? pageExtracts ) )
		{
			item.Subtitle = pageExtracts.Extract;
		}
	}

	private Task Run(
		Func<CancellationToken, Task> work,
		CancellationToken cancellationToken,
		[CallerMemberName] string memberName = "" )
	{
		ArgumentNullException.ThrowIfNull( work );

		if ( _pipeline is not null )
		{
			return RunAsync();

			[DebuggerNonUserCode]
			async Task RunAsync()
			{
				var context = ResilienceContextPool.Shared.Get( memberName, cancellationToken );

				try
				{
					await _pipeline.ExecuteAsync( ImplAsync, context );

					[DebuggerNonUserCode]
					async ValueTask ImplAsync( ResilienceContext c )
					{
						await work( c.CancellationToken );
					}
				}
				finally
				{
					ResilienceContextPool.Shared.Return( context );
				}
			}
		}

		return work( cancellationToken );
	}

	private Task<T> Run<T>(
		Func<CancellationToken, Task<T>> work,
		CancellationToken cancellationToken,
		[CallerMemberName] string memberName = "" )
	{
		ArgumentNullException.ThrowIfNull( work );

		if ( _pipeline is not null )
		{
			return RunAsync();

			async Task<T> RunAsync()
			{
				var context = ResilienceContextPool.Shared.Get( memberName, cancellationToken );

				try
				{
					return await _pipeline.ExecuteAsync( ImplAsync, context );

					[DebuggerNonUserCode]
					async ValueTask<T> ImplAsync( ResilienceContext c )
					{
						return await work( c.CancellationToken );
					}
				}
				finally
				{
					ResilienceContextPool.Shared.Return( context );
				}
			}
		}

		return work( cancellationToken );
	}

	public void Dispose()
	{
		_onDispose.Dispose();
	}
}