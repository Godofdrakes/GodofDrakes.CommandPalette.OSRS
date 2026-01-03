using System;
using System.Reactive.Linq;
using DynamicData;
using GodofDrakes.CommandPalette.Reactive.ViewModels;
using GodofDrakes.CommandPalette.Reactive.Views;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using ReactiveUI;

namespace GodofDrakes.CommandPalette.OSRS.Pages;

public sealed partial class BadDynamicListViewModel : DynamicListViewModel
{
	public override IObservable<IChangeSet<IListItem, IListItem>> Connect()
	{
		return ObservableChangeSet.Create( CreateChangeSet, ( IListItem item ) => item );
	}

	private IDisposable CreateChangeSet( ISourceCache<IListItem, IListItem> cache )
	{
		return this.WhenAnyValue( x => x.SearchText )
			.Select( CreateListItem )
			.Subscribe( item =>
			{
				cache.Edit( list =>
				{
					list.Clear();
					list.AddOrUpdate( item );
				} );
			} );
	}

	private static IListItem CreateListItem( string searchText )
	{
		return new ListItem()
		{
			Title = $"Search Text: \"{searchText}\"",
			Subtitle = nameof(BadDynamicListViewModel),
		};
	}
}

public sealed partial class BadDynamicListPage : DynamicListView<BadDynamicListViewModel>
{
	public BadDynamicListPage()
	{
		this.Name = nameof(BadDynamicListPage);
		this.Title = "Bad Dynamic List Page";
		this.ViewModel = new BadDynamicListViewModel();
	}
}