using System;

namespace FileSync.Contracts
{
	public class FileCreated
	{
		public FileCreated(string fileName, DateTime lastWriteTimeUtc, bool isDirectory)
		{
			FileName = fileName;
			LastWriteTimeUtc = lastWriteTimeUtc;
			IsDirectory = isDirectory;
		}

		public string FileName { get; private set; }
		public DateTime LastWriteTimeUtc { get; private set; }
		public bool IsDirectory { get; private set; }
	}
}
