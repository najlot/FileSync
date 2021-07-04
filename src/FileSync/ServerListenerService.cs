using Cosei.Client.Base;
using Cosei.Client.Http;
using FileSync.Contracts;
using Najlot.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FileSync
{
	sealed class ServerListenerService : IDisposable
	{
		private readonly ILogger _logger = LogAdministrator.Instance.GetLogger(typeof(ServerListenerService));

		private readonly TokenProvider _tokenProvider;
		private readonly ClientConfiguration _configuration;
		private readonly Func<IRequestClient> _createRequestClient;
		private SignalRSubscriber _subscriber = null;

		public ServerListenerService(TokenProvider tokenProvider, ClientConfiguration configuration, Func<IRequestClient> createRequestClient)
		{
			_tokenProvider = tokenProvider;
			_configuration = configuration;
			_createRequestClient = createRequestClient;
		}

		public async Task InitAsync()
		{
			_logger.Info("Init Listener...");

			var serverUri = new Uri(_configuration.ServerUri);
			var signalRUri = new Uri(serverUri, "/cosei");

			_subscriber = new SignalRSubscriber(
				signalRUri.AbsoluteUri,
				async options =>
				{
					var token = await _tokenProvider.GetToken();
					options.Headers.Add("Authorization", $"Bearer {token}");
				},
				exception =>
				{
					_logger.Error(exception, "Error consuming message: ");
				});

			_subscriber.Register<FileCreated>(Handle);
			_subscriber.Register<FileUpdated>(Handle);
			_subscriber.Register<FileDeleted>(Handle);

			_logger.Debug("Starting subscriber...");
			await _subscriber.StartAsync();
		}

		public async Task Handle(FileCreated message)
		{
			var path = Path.Combine(_configuration.BaseDirectory, message.FileName);

			Directory.CreateDirectory(Path.GetDirectoryName(path));

			if (File.GetLastWriteTimeUtc(path) < message.LastWriteTimeUtc)
			{
				_logger.Debug("Creating '{Path}'", path);

				var token = await _tokenProvider.GetToken();

				var headers = new Dictionary<string, string>
				{
					{ "Authorization", $"Bearer {token}" }
				};

				var client = _createRequestClient();
				var info = new PathInfo { FileName = message.FileName };
				var fileContent = await client.PostAsync<FileContent, PathInfo>("/api/FileSync/GetFileContent", info, headers);
				await File.WriteAllBytesAsync(path, fileContent.Content);
				File.SetLastWriteTimeUtc(path, message.LastWriteTimeUtc);
			}
		}

		public async Task Handle(FileUpdated message)
		{
			var path = Path.Combine(_configuration.BaseDirectory, message.FileName);

			Directory.CreateDirectory(Path.GetDirectoryName(path));

			var lastWriteTimeUtc = DateTime.MinValue;

			if (File.Exists(path))
			{
				lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
				_ = new FileInfo(path) { IsReadOnly = false };
			}

			if (lastWriteTimeUtc < message.LastWriteTimeUtc)
			{
				_logger.Debug("Updating '{Path}'", path);

				var token = await _tokenProvider.GetToken();

				var headers = new Dictionary<string, string>
				{
					{ "Authorization", $"Bearer {token}" }
				};

				var client = _createRequestClient();
				var info = new PathInfo { FileName = message.FileName };
				var fileContent = await client.PostAsync<FileContent, PathInfo>("/api/FileSync/GetFileContent", info, headers);

				await File.WriteAllBytesAsync(path, fileContent.Content);
				File.SetLastWriteTimeUtc(path, message.LastWriteTimeUtc);
			}
		}

		public void Handle(FileDeleted message)
		{
			var path = Path.Combine(_configuration.BaseDirectory, message.FileName);

			if (File.Exists(path))
			{
				_logger.Debug("Deleting file '{Path}'", path);

				_ = new FileInfo(path) { IsReadOnly = false };
				File.Delete(path);
			}
			else if (Directory.Exists(path))
			{
				_logger.Debug("Deleting directory '{Path}'", path);

				Directory.Delete(path, true);
			}
		}

		public void Handle(FileRenamed message)
		{
			var path = Path.Combine(_configuration.BaseDirectory, message.FileName);
			var newPath = Path.Combine(_configuration.BaseDirectory, message.FileName);

			_logger.Debug("Renaming file '{Path}' -> '{NewPath}'", path, newPath);

			_ = new FileInfo(path) { IsReadOnly = false };
			File.Move(path, newPath);
		}

		public void Dispose()
		{
			_subscriber?.Dispose();
		}
	}
}
