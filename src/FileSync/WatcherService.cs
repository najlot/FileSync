using Cosei.Client.Base;
using FileSync.Contracts;
using Najlot.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileSync
{
	enum FileAction
	{
		Create,
		Change,
		Delete,
		Rename
	}

	sealed class WatcherService : IDisposable
	{
		private readonly ILogger _logger = LogAdministrator.Instance.GetLogger(typeof(WatcherService));

		private readonly ClientConfiguration _config;
		private readonly TokenProvider _tokenProvider;
		private readonly Func<IRequestClient> _clientFactory;

		private FileSystemWatcher _watcher = null;

		private Thread _thread;
		private readonly ConcurrentQueue<(FileAction Action, string FileName, string NewFileName)> _queue = new ();
		private CancellationTokenSource _cancellationTokenSource;

		public WatcherService(ClientConfiguration config, TokenProvider tokenProvider, Func<IRequestClient> clientFactory)
		{
			_config = config;
			_tokenProvider = tokenProvider;
			_clientFactory = clientFactory;
		}

		public void Init()
		{
			_cancellationTokenSource = new CancellationTokenSource();
			_thread = new Thread(ThreadAction) { IsBackground = true };

			_logger.Info("Init WatcherService...");

			_watcher = new FileSystemWatcher(_config.BaseDirectory, "*");

			_watcher.Created += FileCreated;
			_watcher.Changed += FileChanged;
			_watcher.Deleted += FileDeleted;
			_watcher.Renamed += FileRenamed;

			_watcher.IncludeSubdirectories = true;
			_watcher.EnableRaisingEvents = true;

			_thread.Start(_cancellationTokenSource.Token);
		}

		private async void ThreadAction(object param)
		{
			try
			{
				var cancelationToken = (CancellationToken)param;
				var taskList = new List<(FileAction Action, string FileName, string NewFileName)>();

				while (!cancelationToken.IsCancellationRequested)
				{
					SpinWait.SpinUntil(() => _queue.TryPeek(out _) || cancelationToken.IsCancellationRequested);

					if (cancelationToken.IsCancellationRequested)
					{
						return;
					}

					await Task.Delay(250, cancelationToken);

					while (_queue.TryDequeue(out var taskInfo))
					{
						taskList.Add(taskInfo);
					}

					if (cancelationToken.IsCancellationRequested)
					{
						return;
					}

					await ExecuteTasks(taskList);

					taskList.Clear();
				}
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Thread failed: ");
			}
		}

		private async Task ExecuteTasks(List<(FileAction Action, string FileName, string NewFileName)> taskList)
		{
			var token = await _tokenProvider.GetToken();

			var headers = new Dictionary<string, string>
			{
				{ "Authorization", $"Bearer {token}" }
			};

			var client = _clientFactory();

			var fileNames = taskList.Select(t => t.FileName).Distinct();

			foreach (var fileName in fileNames)
			{
				var lastTask = taskList.Last(t => t.FileName == fileName);

				switch (lastTask.Action)
				{
					case FileAction.Create:
						await CreateFileAsync(client, lastTask.FileName, headers);
						break;
					case FileAction.Change:
						await ChangeFileAsync(client, lastTask.FileName, headers);
						break;
					case FileAction.Delete:
						await DeleteFileAsync(client, lastTask.FileName, headers);
						break;
					case FileAction.Rename:
						await RenameFileAsync(client, lastTask.FileName, lastTask.NewFileName, headers);
						break;
				}
			}
		}

		private void FileRenamed(object sender, RenamedEventArgs args)
		{
			_queue.Enqueue((FileAction.Rename, args.OldName, args.Name));
		}

		private async Task RenameFileAsync(IRequestClient client, string fileName, string newFileName, Dictionary<string, string> headers)
		{
			try
			{
				_logger.Debug("Posting file '{Path}' renamed to '{NewName}'...", fileName, newFileName);

				var command = new RenameFile(fileName, newFileName);
				await client.PostAsync("/api/FileSync/RenameFile", command, headers);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on delete: ");
			}
		}

		private void FileDeleted(object sender, FileSystemEventArgs args)
		{
			_queue.Enqueue((FileAction.Delete, args.Name, null));
		}

		private async Task DeleteFileAsync(IRequestClient client, string fileName, Dictionary<string, string> headers)
		{
			try
			{
				_logger.Debug("Posting file '{Path}' deleted...", fileName);

				var command = new DeleteFile(fileName);
				await client.PostAsync("/api/FileSync/DeleteFile", command, headers);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on delete: ");
			}
		}

		private void FileChanged(object sender, FileSystemEventArgs args)
		{
			_queue.Enqueue((FileAction.Change, args.Name, null));
		}

		private async Task ChangeFileAsync(IRequestClient client, string fileName, Dictionary<string, string> headers)
		{
			try
			{
				var fullPath = Path.Combine(_config.BaseDirectory, fileName);

				if (!File.Exists(fullPath))
				{
					// Directory
					return;
				}

				var time = File.GetLastWriteTimeUtc(fullPath);
				var content = await File.ReadAllBytesAsync(fullPath);
				var command = new UpdateFile(fileName, time, content);
				
				_logger.Debug("Posting file '{Path}' updated...", fileName);

				await client.PostAsync("/api/FileSync/UpdateFile", command, headers);
			}
			catch (Exception ex)
			{
				_logger.Error(ex, "Error on change: ");
			}
		}

		private void FileCreated(object sender, FileSystemEventArgs args)
		{
			_queue.Enqueue((FileAction.Create, args.Name, null));
		}

		private async Task CreateFileAsync(IRequestClient client, string fileName, Dictionary<string, string> headers)
		{
			try
			{
				var fullPath = Path.Combine(_config.BaseDirectory, fileName);

				if (File.Exists(fullPath))
				{
					var time = File.GetLastWriteTimeUtc(fullPath);
					var content = await File.ReadAllBytesAsync(fullPath);
					var command = new CreateFile(fileName, time, content, false);
					
					_logger.Debug("Posting file '{Path}' created...", fileName);

					await client.PostAsync("/api/FileSync/CreateFile", command, headers);
				}
				else if (Directory.Exists(fullPath))
				{
					var time = File.GetLastWriteTimeUtc(fullPath);
					var command = new CreateFile(fileName, time, null, true);
					
					_logger.Debug("Posting directory '{Path}' created...", fileName);

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
			if (_watcher != null)
			{
				_watcher.Created -= FileCreated;
				_watcher.Changed -= FileChanged;
				_watcher.Deleted -= FileDeleted;
				_watcher.Renamed -= FileRenamed;

				_watcher.EnableRaisingEvents = false;
			}

			_cancellationTokenSource?.Cancel();
			_thread?.Join();

			_cancellationTokenSource?.Dispose();
		}
	}
}
