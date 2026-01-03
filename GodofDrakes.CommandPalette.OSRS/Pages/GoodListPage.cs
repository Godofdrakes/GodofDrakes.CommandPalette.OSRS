using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS.Pages;

public sealed partial class GoodListPage : ListPage
{
	private readonly IListItem[] _listItems;

	public GoodListPage()
	{
		this.Name = nameof(GoodListPage);
		this.Title = "Good List Page";

		_listItems =
		[
			new ListItem()
			{
				Title = nameof(GoodDynamicListPage),
				Command = new GoodDynamicListPage(),
			},
			new ListItem()
			{
				Title = nameof(BadDynamicListPage),
				Command = new BadDynamicListPage(),
			}
		];
	}

	public override IListItem[] GetItems()
	{
		return _listItems;
	}
}