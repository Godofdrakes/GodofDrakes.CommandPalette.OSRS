using System;
using System.Reactive.Linq;

namespace GodofDrakes.CommandPalette.OSRS.Extensions;

public static class ObservableExtensions
{
	public static IObservable<T> DefaultIfNull<T>( this IObservable<T?> observable, T defaultValue )
	{
		return observable.Select( value => value ?? defaultValue );
	}
}