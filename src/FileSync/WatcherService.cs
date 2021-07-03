using Cosei.Client.Base;
using FileSync.Contracts;
using Najlot.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FileSync
{
	sealed class WatcherService : IDisposable
	{
		private readonly ILogger _logger = LogAdministrator.Instance.GetLogger(typeof(WatcherService));

		private readonly ClientConfiguration _config;
		private readonly TokenProvider _tokenProvider;
		private readonly Func<IRequestClient> _clientFactory;

		private FileSystemWatcher _watcher = null;
		
		public WatcherService(ClientConfiguration config, TokenProvider tokenProvider, Func<IRequestClient> clientFactory)
		{
			_config = config;
			_tokenProvider = tokenProvider;
			_clientFactory = clientFactory;
		}

		public void Init()
		{
			_logger.Info("Init WatcherService...");

			_watcher = new FileSystemWatcher(_config.BaseDirectory, "*");

			_watcher.Created += FileCreated;
			_watcher.Changed += FileChanged;
			_watcher.Deleted += FileDeleted;
			_watcher.Renamed += FileRenamed;

			_watcher.IncludeSubdirectories = true;
			_watcher.EnableRaisingEvents = true;
		}

		private async void FileRenamed(object sender, RenamedEventArgs args)
		{
			try
			{
				await Task.Delay(250);

				var token = await _tokenProvider.GetToken();

				var headers = new Dictionary<string, string>
				{
					{ "Authorization", $"Bearer {token}" }
				};

				var command = new RenameFile(args.OldName, args.Name);
				var client = _clientFactory();

				_logger.Debug("Posting file '{Path}' renamed to '{NewName}'...", args.OldName, args.Name);

				await client.PostAsync("/api/FileSync/RenameFile", command, headers);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on delete: ");
			}
		}

		private async void FileDeleted(object sender, FileSystemEventArgs args)
		{
			try
			{
				await Task.Delay(250);

				var token = await _tokenProvider.GetToken();

				var headers = new Dictionary<string, string>
				{
					{ "Authorization", $"Bearer {token}" }
				};

				var command = new DeleteFile(args.Name);
				var client = _clientFactory();

				_logger.Debug("Posting file '{Path}' deleted...", args.Name);

				await client.PostAsync("/api/FileSync/DeleteFile", command, headers);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on delete: ");
			}
		}

		private async void FileChanged(object sender, FileSystemEventArgs args)
		{
			try
			{
				await Task.Delay(250);

				if (!File.Exists(args.FullPath))
				{
					// Directory
					return;
				}

				var token = await _tokenProvider.GetToken();

				var headers = new Dictionary<string, string>
				{
					{ "Authorization", $"Bearer {token}" }
				};

				var time = File.GetLastWriteTimeUtc(args.FullPath);
				var content = await File.ReadAllBytesAsync(args.FullPath);
				var command = new UpdateFile(args.Name, time, content);
				var client = _clientFactory();

				_logger.Debug("Posting file '{Path}' updated...", args.Name);

				await client.PostAsync("/api/FileSync/UpdateFile", command, headers);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on change: ");
			}
		}

		private async void FileCreated(object sender, FileSystemEventArgs args)
		{
			try
			{
				await Task.Delay(250);

				var token = await _tokenProvider.GetToken();

				var headers = new Dictionary<string, string>
				{
					{ "Authorization", $"Bearer {token}" }
				};

				if (File.Exists(args.FullPath))
				{
					var time = File.GetLastWriteTimeUtc(args.FullPath);
					var content = await File.ReadAllBytesAsync(args.FullPath);
					var command = new CreateFile(args.Name, time, content, false);
					var client = _clientFactory();

					_logger.Debug("Posting file '{Path}' created...", args.Name);

					await client.PostAsync("/api/FileSync/CreateFile", command, headers);
				}
				else if (Directory.Exists(args.FullPath))
				{
					var time = File.GetLastWriteTimeUtc(args.FullPath);
					var command = new CreateFile(args.Name, time, null, true);
					var client = _clientFactory();

					_logger.Debug("Posting directory '{Path}' created...", args.Name);

					await client.PostAsync("/api/FileSync/CreateFile", command, headers);
				}
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on create: ");
			}
		}

		public void Dispose()
		{
			if (_watcher == null)
			{
				return;
			}

			_watcher.Created += FileCreated;
			_watcher.Changed += FileChanged;
			_watcher.Deleted += FileDeleted;
			_watcher.Renamed += FileRenamed;

			_watcher.EnableRaisingEvents = false;
		}
	}
}
