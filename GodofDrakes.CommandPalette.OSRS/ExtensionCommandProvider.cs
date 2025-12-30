// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using GodofDrakes.CommandPalette.OSRS.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace GodofDrakes.CommandPalette.OSRS;

internal sealed partial class ExtensionCommandProvider : CommandProvider
{
	private readonly OSRSPage _osrsPage;

	private readonly ICommandItem[] _commands;

	public ExtensionCommandProvider( OSRSPage osrsPage )
	{
		ArgumentNullException.ThrowIfNull( osrsPage );

		this.DisplayName = "Old School Runescape";
		this.Icon = IconHelpers.FromRelativePath( "Assets\\StoreLogo.png" );

		_osrsPage = osrsPage;

		_commands =
		[
			new CommandItem()
			{
				Title = DisplayName,
				Command = _osrsPage,
			},
		];
	}

	public override ICommandItem[] TopLevelCommands()
	{
		return _commands;
	}
}