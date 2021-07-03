using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Najlot.Log;
using Najlot.Log.Destinations;
using Najlot.Log.Extensions.Logging;
using Najlot.Log.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSyncService
{
	public class Program
	{
		public static void Main(string[] args)
		{
			LogAdministrator
				.Instance
				.AddConsoleDestination(true)
				.SetCollectMiddleware<ConcurrentCollectMiddleware, ConsoleDestination>();

			CreateHostBuilder(args).Build().Run();

			LogAdministrator.Instance.Dispose();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureLogging(builder =>
				{
					builder.ClearProviders();
					builder.AddNajlotLog(LogAdministrator.Instance);
				})
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseStartup<Startup>();
				});
	}
}
