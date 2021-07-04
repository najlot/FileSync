using Cosei.Service.Base;
using FileSync.Contracts;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FileSyncService.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class FileSyncController : ControllerBase
	{
		private readonly ServiceConfiguration _configuration;
		private readonly IPublisher _publisher;

		public FileSyncController(ServiceConfiguration configuration, IPublisher publisher)
		{
			_configuration = configuration;
			_publisher = publisher;
		}

		[HttpGet("[action]")]
		public PathInfo[] GetPathInfos()
		{
			var fullPath = _configuration.BasePath;

			var fileInfos = Directory
				.GetFiles(fullPath, "*", SearchOption.AllDirectories)
				.Select(p => new PathInfo
				{
					FileName = Path.GetRelativePath(fullPath, p),
					LastWriteTimeUtc = System.IO.File.GetLastWriteTimeUtc(p)
				})
				.ToArray();

			return fileInfos;
		}

		[HttpPost("[action]")]
		public async Task CreateFile(CreateFile command)
		{
			var basePath = _configuration.BasePath;
			var fullPath = Path.Combine(basePath, command.FileName);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

			if (command.IsDirectory)
			{
				Directory.CreateDirectory(fullPath);
				Directory.SetLastWriteTimeUtc(fullPath, command.LastWriteTimeUtc);
			}
			else
			{
				await System.IO.File.WriteAllBytesAsync(fullPath, command.Content);
				System.IO.File.SetLastWriteTimeUtc(fullPath, command.LastWriteTimeUtc);
			}
			
			var message = new FileCreated(
				command.FileName,
				command.LastWriteTimeUtc,
				command.IsDirectory);

			await _publisher.PublishAsync(message);
		}

		[HttpPost("[action]")]
		public async Task UpdateFile(UpdateFile command)
		{
			var fullPath = Path.Combine(_configuration.BasePath, command.FileName);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
			await System.IO.File.WriteAllBytesAsync(fullPath, command.Content);
			System.IO.File.SetLastWriteTimeUtc(fullPath, command.LastWriteTimeUtc);

			var message = new FileUpdated(
				command.FileName,
				command.LastWriteTimeUtc);

			await _publisher.PublishAsync(message);
		}

		[HttpPost("[action]")]
		public async Task DeleteFile(DeleteFile command)
		{
			var fullPath = Path.Combine(_configuration.BasePath, command.FileName);

			if (System.IO.File.Exists(fullPath))
			{
				System.IO.File.Delete(fullPath);
			}
			else if (Directory.Exists(fullPath))
			{
				Directory.Delete(fullPath, true);
			}

			var message = new FileDeleted(command.FileName);
			await _publisher.PublishAsync(message);
		}

		[HttpPost("[action]")]
		public async Task RenameFile(RenameFile command)
		{
			var basePath = _configuration.BasePath;
			var fullPath = Path.Combine(basePath, command.FileName);
			var newFullPath = Path.Combine(basePath, command.NewFileName);

			if (System.IO.File.Exists(fullPath))
			{
				System.IO.File.Move(fullPath, newFullPath);
			}
			else if (Directory.Exists(fullPath))
			{
				Directory.Move(fullPath, newFullPath);
			}
			
			var message = new FileRenamed(
				command.FileName,
				command.NewFileName);

			await _publisher.PublishAsync(message);
		}

		[HttpPost("[action]")]
		public async Task<FileContent> GetFileContent(PathInfo info)
		{
			var basePath = _configuration.BasePath;
			var fullPath = Path.Combine(basePath, info.FileName);
			return new FileContent { Content = await System.IO.File.ReadAllBytesAsync(fullPath) };
		}
	}
}
