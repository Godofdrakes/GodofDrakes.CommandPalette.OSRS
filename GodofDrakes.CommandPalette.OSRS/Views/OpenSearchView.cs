using System;
using GodofDrakes.CommandPalette.OSRS.ViewModels;
using GodofDrakes.CommandPalette.Reactive.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GodofDrakes.CommandPalette.OSRS.Views;

public sealed partial class OpenSearchView : DynamicListView<OpenSearchViewModel>, IDisposable
{
	private readonly OpenSearchViewModel _defaultViewModel;

	public OpenSearchView( IServiceProvider serviceProvider )
	{
		this.Name = nameof(OpenSearchView);
		this.Title = "Open Search View";

		_defaultViewModel = ActivatorUtilities.CreateInstance<OpenSearchViewModel>( serviceProvider );

		this.ViewModel = _defaultViewModel;
	}

	public void Dispose()
	{
		_defaultViewModel.Dispose();
	}
}