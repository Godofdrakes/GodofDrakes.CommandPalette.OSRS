using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NReco.Logging.File;

namespace GodofDrakes.CommandPalette.OSRS.Extensions;

public static class HostBuilderExtensions
{
	internal static IHostBuilder AddLogFile( this IHostBuilder builder )
	{
		builder.ConfigureServices( services =>
		{
			services.AddLogging( log =>
			{
				var logFile = Program.GetConfigFile( "Extension.log" );

				log.AddFile( logFile, options =>
				{
					options.Append = false;
					options.UseUtcTimestamp = false;
				} );
			} );
		} );

		return builder;
	}
}