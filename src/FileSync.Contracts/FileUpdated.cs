using System;

namespace FileSync.Contracts
{
	public class FileUpdated
	{
		public FileUpdated(string fileName, DateTime lastWriteTimeUtc)
		{
			FileName = fileName;
			LastWriteTimeUtc = lastWriteTimeUtc;
		}

		public string FileName { get; private set; }
		public DateTime LastWriteTimeUtc { get; private set; }
	}
}
