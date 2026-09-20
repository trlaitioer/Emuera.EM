using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Script;
using Xunit;

namespace Emuera.Tests;

/// <summary>
/// 目录根与路径常量必须跟随 SetDirPaths,而不是在所属类型首次被触碰时固化。
/// 这些都是进程级静态,用例不得并行(AssemblyInfo 已禁用并行)。
/// </summary>
public class ProgramDirectoriesTests
{
	[Fact]
	public void EveryRootIsANamedEntryOfTheGameRoot()
	{
		string root = GameRootScope.UseTempRoot("directories-roots");

		Assert.Equal(root, Program.GameRoot.FullName);
		Assert.Equal(Path.Combine(root, "csv"), Program.CsvRoot.FullName);
		Assert.Equal(Path.Combine(root, "erb"), Program.ErbRoot.FullName);
		Assert.Equal(Path.Combine(root, "debug"), Program.DebugRoot.FullName);
		Assert.Equal(Path.Combine(root, "dat"), Program.DatRoot.FullName);
		Assert.Equal(Path.Combine(root, "resources"), Program.ContentRoot.FullName);
		Assert.Equal(Path.Combine(root, "sound"), Program.SoundRoot.FullName);
		Assert.Equal(Path.Combine(root, "font"), Program.FontRoot.FullName);
		Assert.Equal(Path.Combine(root, "emuera.log"), Program.DefaultLogFile.FullName);
	}

	[Fact]
	public void PathConstantsFollowTheGameDirectoryChosenAfterTheTypeWasFirstUsed()
	{
		GameRootScope.UseTempRoot("directories-first");
		// Touch every type that resolves a path before the game directory changes again.
		JSONConfig.Data = new JSONConfigData();
		JSONConfig.Save();
		_ = KeyMacro.MacroPath;
		Assert.True(ConfigData.Instance.SaveConfig());

		string second = GameRootScope.UseTempRoot("directories-second");
		JSONConfig.Save();
		Assert.True(ConfigData.Instance.SaveConfig());

		Assert.Equal(Path.Combine(second, "macro.txt"), KeyMacro.MacroPath.FullName);
		Assert.True(File.Exists(Path.Combine(second, "setting.json")));
		Assert.True(File.Exists(Path.Combine(second, "emuera.config")));
	}
}
