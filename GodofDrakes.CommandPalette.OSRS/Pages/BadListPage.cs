using GodofDrakes.CommandPalette.Reactive.ViewModels;
using GodofDrakes.CommandPalette.Reactive.Views;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS.Pages;

public sealed partial class BadListPage : ListView<ListViewModel>
{
	public BadListPage()
	{
		this.Name = nameof(BadListPage);
		this.Title = "Bad List Page";

		this.ViewModel = new ReadOnlyListViewModel()
		{
			ListItems =
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
			],
		};
	}
}