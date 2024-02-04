using System.Text.Json;
using System.Text.Json.Serialization;

namespace MRCP;

/// <summary>App config object.</summary>
internal class Config
{
	/// <summary>Singleton path to the config file.</summary>
	private static readonly string s_filePath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MRCP", "Settings.json");
	/// <summary>Port to listen on for requests.</summary>
	public required int Port { get; init; }
	/// <summary>Steam account name to use.</summary>
	public required string AccountName { get; init; }
	/// <summary>Access/refresh token for Steam account.</summary>
	public required string Token { get; init; }

	/// <summary>Writes current object state to the file.</summary>
	public void SaveToFile()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(s_filePath)!);
		byte[] data = JsonSerializer.SerializeToUtf8Bytes(this, ConfigJsonContext.Default.Config);
		using var fileHandle = File.OpenHandle(s_filePath, FileMode.Create, FileAccess.Write, preallocationSize: data.Length);
		RandomAccess.Write(fileHandle, data, 0);
	}
	/// <summary>Loads config from the file.</summary>
	/// <returns>Config object loaded from the file.</returns>
	public static Config Load()
	{
		if (!File.Exists(s_filePath))
			throw new FileNotFoundException("Config file not found. Run the app with --setup flag.");
		byte[] buffer;
		using (var fileHandle = File.OpenHandle(s_filePath))
		{
			buffer = new byte[(int)RandomAccess.GetLength(fileHandle)];
			RandomAccess.Read(fileHandle, buffer, 0);
		}
		return JsonSerializer.Deserialize(buffer, ConfigJsonContext.Default.Config)!;
	}
}
[JsonSourceGenerationOptions(PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate)]
[JsonSerializable(typeof(Config))]
partial class ConfigJsonContext : JsonSerializerContext { }