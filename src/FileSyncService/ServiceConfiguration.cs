using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileSyncService
{
	public class ServiceConfiguration
	{
		public string Secret { get; set; } = "";
		public string BasePath { get; set; } = "";
	}
}
