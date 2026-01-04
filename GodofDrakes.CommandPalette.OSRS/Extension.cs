// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using GodofDrakes.CommandPalette.Hosting;
using Microsoft.CommandPalette.Extensions;

namespace GodofDrakes.CommandPalette.OSRS;

[Guid( "e2905630-80e8-4e23-a35b-ef3cfeb11a4b" )]
internal sealed partial class Extension : HostedExtension
{
	private readonly Commands.ExtensionCommandProvider _provider;

	public Extension( Commands.ExtensionCommandProvider provider )
	{
		ArgumentNullException.ThrowIfNull( provider );

		_provider = provider;
	}

	public override object? GetProvider( ProviderType providerType )
	{
		if ( providerType == ProviderType.Commands )
		{
			return _provider;
		}

		return null;
	}
}