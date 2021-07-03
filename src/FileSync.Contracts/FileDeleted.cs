namespace FileSync.Contracts
{
	public class FileDeleted
	{
		public FileDeleted(string fileName)
		{
			FileName = fileName;
		}

		public string FileName { get; private set; }
	}
}
