namespace FileSync.Contracts
{
	public class RenameFile
	{
		public RenameFile(string fileName, string newFileName)
		{
			FileName = fileName;
			NewFileName = newFileName;
		}

		public string FileName { get; private set; }
		public string NewFileName { get; }
	}
}
