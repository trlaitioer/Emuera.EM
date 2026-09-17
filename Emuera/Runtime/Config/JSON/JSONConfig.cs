#nullable enable
//新設したコンフィグ設定のロード、セーブ、公開を担当する。
using System.IO;
using System.Text.Json;


namespace MinorShift.Emuera.Runtime.Config.JSON;

internal static class JSONConfig
{
	public static JSONConfigData Data = null!;

	private const string _configFileName = "setting.json";
	private static string _configFilePath = Program.ExeDir + _configFileName;

	public static void Load()
	{
		if (!File.Exists(_configFilePath))
		{
			JSONConfigData defaultData = new();
			string defaultJson = JsonSerializer.Serialize(defaultData);
			File.WriteAllText(_configFilePath, defaultJson);
		}

		string json = File.ReadAllText(_configFilePath);

		Data = JsonSerializer.Deserialize<JSONConfigData>(json)!;
	}

	public static void Save()
	{
		string json = JsonSerializer.Serialize(Data);
		File.WriteAllText(_configFilePath, json);
	}
}