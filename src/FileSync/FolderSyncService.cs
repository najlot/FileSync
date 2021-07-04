using Cosei.Client.Base;
using FileSync.Contracts;
using Najlot.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileSync
{
	class FolderSyncService
	{
		private readonly ILogger _logger = LogAdministrator.Instance.GetLogger(typeof(ServerListenerService));

		private readonly ClientConfiguration _config;
		private readonly TokenProvider _tokenProvider;
		private readonly IRequestClient _client;

		public FolderSyncService(ClientConfiguration config, TokenProvider tokenProvider, IRequestClient client)
		{
			_config = config;
			_tokenProvider = tokenProvider;
			_client = client;
		}

		public async Task SyncFolderAsync()
		{
			_logger.Info("Init Sync...");

			var token = await _tokenProvider.GetToken();

			var headers = new Dictionary<string, string>
			{
				{ "Authorization", $"Bearer {token}" }
			};

			var localPath = _config.BaseDirectory;
			Directory.CreateDirectory(localPath);
			var localFiles = Directory
				.GetFiles(localPath, "*", SearchOption.AllDirectories)
				.Select(f => Path.GetRelativePath(localPath, f))
				.ToArray();

			var pathInfos = await _client.GetAsync<PathInfo[]>("/api/FileSync/GetPathInfos", headers);

			var serverPaths = pathInfos
				.Select(p => p.FileName)
				.ToArray();

			string defaultAnswer = null;

			foreach (var file in localFiles)
			{
				if (!serverPaths.Contains(file))
				{
					var fullpath = Path.Combine(localPath, file);
					string answer = defaultAnswer;

					if (string.IsNullOrEmpty(answer))
					{
						Console.WriteLine($"'{fullpath}' does not exist on the server.");
						Console.WriteLine("Push the file to the server(p), push all(P),");
						Console.WriteLine("delete the local file(d), delete all local files that do not exist on the server(D)? (default 'p'):");
						answer = Console.ReadLine();
					}
					
					bool postToTheServer = true;

					switch (answer.Trim())
					{
						case "P":
							defaultAnswer = "p";
							postToTheServer = true;
							break;
						case "p":
							postToTheServer = true;
							break;

						case "D":
							defaultAnswer = "d";
							postToTheServer = false;
							break;
						case "d":
							postToTheServer = false;
							break;
					}

					if (postToTheServer)
					{
						var time = File.GetLastWriteTimeUtc(fullpath);
						var content = await File.ReadAllBytesAsync(fullpath);
						var command = new CreateFile(file, time, content, false);

						_logger.Debug("Posting file '{Path}' to create", fullpath);

						await _client.PostAsync("/api/FileSync/CreateFile", command, headers);
					}
					else
					{
						_logger.Debug("Deleting file '{Path}'...", fullpath);
						File.Delete(fullpath);
					}
				}
			}

			foreach (var pathInfo in pathInfos)
			{
				var fullPath = Path.Combine(_config.BaseDirectory, pathInfo.FileName);
				var time = File.GetLastWriteTimeUtc(fullPath);

				if (localFiles.Contains(pathInfo.FileName))
				{
					if (time > pathInfo.LastWriteTimeUtc)
					{
						_logger.Debug("Posting file '{Path}' to update", fullPath);

						var content = await File.ReadAllBytesAsync(fullPath);
						var command = new UpdateFile(pathInfo.FileName, time, content);
						await _client.PostAsync("/api/FileSync/UpdateFile", command, headers);
					}
					else if (time < pathInfo.LastWriteTimeUtc)
					{
						_logger.Debug("Requesting file '{Path}' to update", fullPath);

						var info = new PathInfo { FileName = pathInfo.FileName };
						var fileContent = await _client.PostAsync<FileContent, PathInfo>("/api/FileSync/GetFileContent", info, headers);
						Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
						await File.WriteAllBytesAsync(fullPath, fileContent.Content);
						File.SetLastWriteTimeUtc(fullPath, pathInfo.LastWriteTimeUtc);
					}
				}
				else
				{
					_logger.Debug("Requesting file '{Path}' to create", fullPath);

					var info = new PathInfo { FileName = pathInfo.FileName };
					var fileContent = await _client.PostAsync<FileContent, PathInfo>("/api/FileSync/GetFileContent", info, headers);
					Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
					await File.WriteAllBytesAsync(fullPath, fileContent.Content);
					File.SetLastWriteTimeUtc(fullPath, pathInfo.LastWriteTimeUtc);
				}
			}
		}
	}
}
