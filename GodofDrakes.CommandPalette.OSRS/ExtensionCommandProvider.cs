// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reactive.Disposables;
using GodofDrakes.CommandPalette.OSRS.Views;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS;

internal sealed partial class ExtensionCommandProvider : CommandProvider
{
	private readonly CompositeDisposable _onDispose = [];

	private readonly ICommandItem[] _commands;

	public ExtensionCommandProvider( IServiceProvider serviceProvider )
	{
		this.Id = "godofdrakes.commandpalette.osrs";
		this.Icon = Icons.Wiki;
		this.DisplayName = "CmdPal Commands for Old School Runescape";

		var openSearch = new OpenSearchView( serviceProvider );

		_onDispose.Add( openSearch );

		_commands =
		[
			new CommandItem()
			{
				Title = "Search the OSRS Wiki",
				Command = openSearch,
			}
		];
	}

	public override ICommandItem[] TopLevelCommands()
	{
		return _commands;
	}

	public override void Dispose()
	{
		_onDispose.Dispose();

		base.Dispose();
	}
}