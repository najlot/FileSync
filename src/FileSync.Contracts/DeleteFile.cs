namespace FileSync.Contracts
{
	public class DeleteFile
	{
		public DeleteFile(string fileName)
		{
			FileName = fileName;
		}

		public string FileName { get; private set; }
	}
}
