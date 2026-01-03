using System;
using System.Diagnostics.CodeAnalysis;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries.Properties;

namespace GodofDrakes.CommandPalette.OSRS.Extensions;

public static class WikiPageExtensions
{
	public static bool TryGetPropertyGroup<T>(
		this WikiPage wikiPage,
		[NotNullWhen( true )] out T? propertyGroup )
		where T : IWikiPagePropertyGroup
	{
		ArgumentNullException.ThrowIfNull( wikiPage );

		propertyGroup = wikiPage.GetPropertyGroup<T>();

		return propertyGroup is not null;
	}
}