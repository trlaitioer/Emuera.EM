//新設したコンフィグ設定のロード、セーブ、公開を担当する。

using MinorShift.Emuera.Runtime.Utils.PathUtils;
using System.IO;
using System.Text.Json;


namespace MinorShift.Emuera.Runtime.Config.JSON;

internal static class JSONConfig
{
	public static JSONConfigData Data = null!;

	private const string _configFileName = "setting.json";

	// Resolved on every read: the game directory is only known once SetDirPaths has run.
	private static GamePath ConfigFilePath => Program.GameRoot.CombineName(_configFileName);

	public static void Load()
	{
		if (!File.Exists(ConfigFilePath.FullName))
		{
			JSONConfigData defaultData = new();
			string defaultJson = JsonSerializer.Serialize(defaultData);
			File.WriteAllText(ConfigFilePath.FullName, defaultJson);
		}

		string json = File.ReadAllText(ConfigFilePath.FullName);

		Data = JsonSerializer.Deserialize<JSONConfigData>(json)!;
	}

	public static void Save()
	{
		string json = JsonSerializer.Serialize(Data);
		File.WriteAllText(ConfigFilePath.FullName, json);
	}
}
