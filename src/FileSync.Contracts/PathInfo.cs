using System;

namespace FileSync.Contracts
{
	public class PathInfo
	{
		public string FileName { get; set; }
		public DateTime LastWriteTimeUtc { get; set; }
	}
}
