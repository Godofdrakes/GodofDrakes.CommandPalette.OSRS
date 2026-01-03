// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.RateLimiting;
using GodofDrakes.CommandPalette.Hosting.Extensions;
using GodofDrakes.CommandPalette.OSRS.Extensions;
using GodofDrakes.CommandPalette.OSRS.ViewModels;
using GodofDrakes.CommandPalette.Reactive.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace GodofDrakes.CommandPalette.OSRS;

public static class Program
{
	[MTAThread]
	public static void Main( string[] args )
	{
		var builder = Host.CreateDefaultBuilder( args )
			.UseHostedExtensionLifetime( args )
			.AddHostedExtension()
			.AddLogFile()
			.ConfigureSplat();

		var host = builder.Build()
			// Since MS DI container is a different type,
			// we need to re-register the built container with Splat again
			.InitializeSplat();

		host.Run();
	}

	private static IHostBuilder AddHostedExtension( this IHostBuilder hostBuilder )
	{
		return hostBuilder.ConfigureServices( ( context, services ) =>
		{
			services.AddHostedExtension<Extension>();

			services.AddSingleton<ExtensionCommandProvider>();

			services.AddResiliencePipeline( typeof(OpenSearchViewModel), builder =>
			{
				var options = new SlidingWindowRateLimiterOptions()
				{
					PermitLimit = 60,
					SegmentsPerWindow = 4,
					Window = TimeSpan.FromSeconds( 60 ),
				};

				builder
					.AddRateLimiter( new SlidingWindowRateLimiter( options ) )
					.AddConcurrencyLimiter( 1, 10 );
			} );

			services.AddSingleton( s =>
			{
				var loggingFactory = s.GetRequiredService<ILoggerFactory>();

				return new WikiClient
				{
					ClientUserAgent = "GodofDrakes.CommandPalette.OSRS/1.0 (github.com/GodofDrakes)",
					Logger = loggingFactory.CreateLogger<WikiClient>(),
				};
			} );

			services.AddTransient( s =>
			{
				var loggingFactory = s.GetRequiredService<ILoggerFactory>();
				var wikiClient = s.GetRequiredService<WikiClient>();

				return new WikiSite( wikiClient, "https://oldschool.runescape.wiki/api.php" )
				{
					Logger = loggingFactory.CreateLogger<WikiSite>(),
				};
			} );
		} );
	}

	internal static string GetConfigDirectory()
	{
		var appData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
		return Path.Combine( appData, "GodofDrakes.CommandPalette", "OSRS" );
	}

	internal static string GetConfigFile( string fileName )
	{
		return Path.Combine( GetConfigDirectory(), fileName );
	}
}