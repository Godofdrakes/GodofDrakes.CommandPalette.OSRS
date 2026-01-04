// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using GodofDrakes.CommandPalette.Hosting.Extensions;
using GodofDrakes.CommandPalette.OSRS.Extensions;
using GodofDrakes.CommandPalette.OSRS.ViewModels;
using GodofDrakes.CommandPalette.Reactive.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.RateLimiting;
using Polly.Retry;
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

			services.AddSingleton<Commands.ExtensionCommandProvider>();

			services.AddResiliencePipeline( typeof(OpenSearchViewModel), builder =>
			{
				var rateLimiterOptions = new SlidingWindowRateLimiterOptions()
				{
					PermitLimit = 60,
					SegmentsPerWindow = 6,
					Window = TimeSpan.FromSeconds( 60 ),
				};

				var retryOptions = new RetryStrategyOptions()
				{
					BackoffType = DelayBackoffType.Exponential,
					MaxRetryAttempts = int.MaxValue,
					ShouldHandle = args =>
					{
						if ( args.Outcome.Exception is OperationCanceledException )
						{
							return ValueTask.FromResult( false );
						}

						if ( args.Outcome.Exception is RateLimiterRejectedException )
						{
							return ValueTask.FromResult( true );
						}

						return ValueTask.FromResult( false );
					},
					DelayGenerator = arguments =>
					{
						TimeSpan? delay = null;

						if ( arguments.Outcome.Result is RateLimiterRejectedException rateLimit )
						{
							delay = rateLimit.RetryAfter;
						}

						return new ValueTask<TimeSpan?>( delay );
					},
				};

				builder
					.AddRateLimiter( new SlidingWindowRateLimiter( rateLimiterOptions ) )
					.AddRetry( retryOptions );
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