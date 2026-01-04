// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using GodofDrakes.CommandPalette.OSRS.Views;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS.Commands;

internal sealed partial class AnonymousFallbackHandler : IFallbackHandler
{
	private readonly Action<string> _action;

	public AnonymousFallbackHandler( Action<string> action )
	{
		ArgumentNullException.ThrowIfNull( action );

		_action = action;
	}

	public void UpdateQuery( string query )
	{
		_action.Invoke( query );
	}
}

internal sealed partial class ExtensionCommandProvider : CommandProvider
{
	private readonly OpenSearchView _openSearch;

	private readonly IFallbackCommandItem[] _fallbackCommands;

	public ExtensionCommandProvider( IServiceProvider serviceProvider )
	{
		this.Id = "godofdrakes.commandpalette.osrs";
		this.Icon = Icons.Wiki;
		this.DisplayName = "CmdPal Commands for Old School Runescape";

		_openSearch = new OpenSearchView( serviceProvider );

		_fallbackCommands =
		[
			new FallbackCommandItem( _openSearch, string.Empty )
			{
				Icon = Icons.Wiki,
				Title = "Search the OSRS Wiki",
				Subtitle = nameof(FallbackCommandItem),
				FallbackHandler = new AnonymousFallbackHandler( str =>
				{
					_openSearch.SearchText = str;
				} ),
			}
		];
	}

	public override ICommandItem[] TopLevelCommands()
	{
		return [];
	}

	public override IFallbackCommandItem[] FallbackCommands()
	{
		return _fallbackCommands;
	}

	public override void Dispose()
	{
		_openSearch.Dispose();

		base.Dispose();
	}
}