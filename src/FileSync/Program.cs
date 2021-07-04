using Cosei.Client.Base;
using Cosei.Client.Http;
using Najlot.Log;
using Najlot.Log.Destinations;
using Najlot.Log.Middleware;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FileSync
{
	class Program
	{
		static void InitLogging()
		{
			LogErrorHandler.Instance.ErrorOccured += (sender, args) =>
			{
				var message = $"{args.Message} {args.Exception}";
				Console.WriteLine(message);
				Debug.WriteLine(message);
			};

			LogAdministrator
				.Instance
				.AddConsoleDestination(true)
				.SetCollectMiddleware<ConcurrentCollectMiddleware, ConsoleDestination>();
		}

		static async Task Main(string[] args)
		{
			InitLogging();

			try
			{
				await MainInternal(args);
			}
			catch (Exception ex)
			{
				var logger = LogAdministrator.Instance.GetLogger(typeof(Program));
				logger.Error(ex, "Error: ");
			}
			finally
			{
				LogAdministrator.Instance.Dispose();
			}
		}

		static async Task MainInternal(string[] args)
		{
			var logger = LogAdministrator.Instance.GetLogger(typeof(Program));
			
			var config = await ConfigurationReader.ReadConfigurationAsync<ClientConfiguration>();

			if (config == null)
			{
				logger.Fatal(nameof(ClientConfiguration) + " not found!");
				return;
			}

			IRequestClient CreateRequestClient() => new HttpRequestClient(config.ServerUri);

			var client = CreateRequestClient();

			var tokenProvider = new TokenProvider(
				CreateRequestClient,
				config.Username,
				config.Password);

			var serverListenerService = new ServerListenerService(tokenProvider, config, CreateRequestClient);
			await serverListenerService.InitAsync();

			var folderSyncService = new FolderSyncService(
				config,
				tokenProvider,
				client);
			await folderSyncService.SyncFolderAsync();

			var watcher = new WatcherService(config, tokenProvider, CreateRequestClient);
			watcher.Init();

			logger.Info("Listening for changes, press any key to exit...");
			Console.ReadKey();

			watcher.Dispose();
			serverListenerService.Dispose();
		}
	}
}
