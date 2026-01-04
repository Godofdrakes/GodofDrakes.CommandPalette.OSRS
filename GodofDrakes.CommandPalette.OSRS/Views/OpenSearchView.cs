using System;
using System.Reactive.Disposables;
using GodofDrakes.CommandPalette.OSRS.ViewModels;
using GodofDrakes.CommandPalette.Reactive.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GodofDrakes.CommandPalette.OSRS.Views;

public sealed partial class OpenSearchView : DynamicListView<OpenSearchViewModel>
{
	private readonly OpenSearchViewModel _defaultViewModel;

	public OpenSearchView( IServiceProvider serviceProvider )
	{
		this.Id = "godofdrakes.commandpalette.osrs.opensearch";
		this.Name = nameof(OpenSearchView);
		this.Title = "Open Search View";

		_defaultViewModel = ActivatorUtilities.CreateInstance<OpenSearchViewModel>( serviceProvider );

		this.ViewModel = _defaultViewModel;
	}

	protected override void Dispose( bool disposing )
	{
		if ( disposing )
		{
			_defaultViewModel.Dispose();
		}

		base.Dispose( disposing );
	}
}