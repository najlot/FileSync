using System;

namespace FileSync.Contracts
{
	public class UpdateFile
	{
		public UpdateFile(string fileName, DateTime lastWriteTimeUtc, byte[] content)
		{
			FileName = fileName;
			LastWriteTimeUtc = lastWriteTimeUtc;
			Content = content;
		}

		public string FileName { get; private set; }
		public DateTime LastWriteTimeUtc { get; private set; }
		public byte[] Content { get; private set; }
	}
}
