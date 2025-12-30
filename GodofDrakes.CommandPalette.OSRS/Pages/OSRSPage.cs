// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS.Pages;

internal sealed partial class OSRSPage : ListPage
{
	private readonly WikiSearchPage _wikiSearchPage;

	public OSRSPage( WikiSearchPage wikiSearchPage )
	{
		ArgumentNullException.ThrowIfNull( wikiSearchPage );

		this.Icon = IconHelpers.FromRelativePath( "Assets\\StoreLogo.png" );
		this.Title = "Old School Runescape";

		_wikiSearchPage = wikiSearchPage;
	}

	public override IListItem[] GetItems()
	{
		return
		[
			new ListItem()
			{
				Title = "Search oldschool.runescape.wiki",
				Command = _wikiSearchPage,
			}
		];
	}
}