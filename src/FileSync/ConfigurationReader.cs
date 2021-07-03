using Najlot.Log;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileSync
{
	public static class ConfigurationReader
	{
		private static readonly Logger _logger = LogAdministrator.Instance
				.GetLogger(typeof(ConfigurationReader));

		public async static Task<T> ReadConfigurationAsync<T>() where T : class, new()
		{
			var configDir = "config";
			var configPath = Path.Combine(configDir, typeof(T).Name + ".json");
			configPath = Path.GetFullPath(configPath);

			if (!File.Exists(configPath))
			{
				_logger.Info(configPath + " not found.");

				if (!File.Exists(configPath + ".example"))
				{
					_logger.Info("Writing " + configPath + ".example...");

					if (!Directory.Exists(configDir))
					{
						Directory.CreateDirectory(configDir);
					}

					await File.WriteAllTextAsync(configPath + ".example", JsonSerializer.Serialize(new T()));
				}

				return null;
			}

			var configContent = await File.ReadAllTextAsync(configPath);
			return JsonSerializer.Deserialize<T>(configContent, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
		}
	}
}
