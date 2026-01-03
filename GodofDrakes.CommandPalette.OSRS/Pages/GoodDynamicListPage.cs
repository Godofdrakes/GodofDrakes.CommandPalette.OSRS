using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS.Pages;

public sealed partial class GoodDynamicListPage : DynamicListPage, IDisposable
{
	private readonly BehaviorSubject<string> _searchTextChanged = new( string.Empty );

	private readonly ReadOnlyObservableCollection<IListItem> _listItems;

	public GoodDynamicListPage()
	{
		this.Name = nameof(GoodDynamicListPage);
		this.Title = "Good Dynamic List Page";

		var changes = ObservableChangeSet.Create( CreateChangeSet, ( IListItem item ) => item );

		changes.Bind( out _listItems )
			.Do( _ => RaiseItemsChanged() )
			.Subscribe();
	}

	private IDisposable CreateChangeSet( ISourceCache<IListItem, IListItem> cache )
	{
		return _searchTextChanged
			.Select( IListItem ( text ) =>
			{
				return new ListItem()
				{
					Title = $"Search Text: \"{text}\"",
					Subtitle = nameof(BadDynamicListPage),
				};
			} )
			.Subscribe( item =>
			{
				cache.Edit( list =>
				{
					list.Clear();

					list.AddOrUpdate( item );
				} );
			} );
	}

	public override IListItem[] GetItems()
	{
		return _listItems.ToArray();
	}

	public override void UpdateSearchText( string oldSearch, string newSearch )
	{
		_searchTextChanged.OnNext( newSearch );
	}

	public void Dispose()
	{
		_searchTextChanged.Dispose();
	}
}