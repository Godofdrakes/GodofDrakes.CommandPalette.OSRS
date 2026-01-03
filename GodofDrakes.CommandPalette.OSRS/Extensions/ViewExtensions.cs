using System;
using System.Diagnostics.CodeAnalysis;
using ReactiveUI;

namespace GodofDrakes.CommandPalette.OSRS.Extensions;

public static class ViewExtensions
{
	[SuppressMessage( "AOT", "IL3050" )]
	[SuppressMessage( "Trimming", "IL2026" )]
	public static IObservable<TViewModel?> WhenViewModel<TViewModel>( this IViewFor<TViewModel> view )
		where TViewModel : class
	{
		return view.WhenAnyValue<IViewFor<TViewModel>, TViewModel?>( nameof(view.ViewModel) );
	}
}