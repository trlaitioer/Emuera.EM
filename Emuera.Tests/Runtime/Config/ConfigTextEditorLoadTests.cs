using MinorShift.Emuera.Runtime.Config;
using Xunit;

// 命名空间不能取 Emuera.Tests.Runtime.Config:那会让同项目其它文件里的 Config 解析成命名空间而非类型。
namespace Emuera.Tests.Runtime;

/// <summary>
/// TextEditor 的值是路径:配置文件里只有第一个 ":" 分隔键与值,盘符里的 ":" 不能把值截断。
/// 键用该项的标签(SaveConfig 写出的形态),不是枚举名。
/// 直接调 internal loadConfig,避免 LoadConfig 连带 SetConfig/CheckUpdate 改写其它用例依赖的静态。
/// </summary>
public class ConfigTextEditorLoadTests
{
	private const string Key = "TEXT EDITOR";

	/// <summary>只有正斜杠盘符这一例是修复前的失败形态,其余三例是必须保持可用的既有形态。</summary>
	[Theory]
	[InlineData(@"C:\tools\ed.exe")]
	[InlineData("C:/tools/ed.exe")]
	[InlineData(@"\\server\share\ed.exe")]
	[InlineData(@"C:\tools\ed:itor.exe")]
	public void Loadconfig_TextEditorValueHoldingColons_KeepsTheWholeValue(string editor)
	{
		ConfigItem<string> item = Prepare();

		Assert.Equal(editor, LoadTextEditor(Key + ":" + editor, item));
	}

	/// <summary>ConfigItem.TryParse 本来就 Trim,此处只保证换实现后仍然成立。</summary>
	[Fact]
	public void Loadconfig_TextEditorValueWithSurroundingSpaces_IsTrimmed()
	{
		ConfigItem<string> item = Prepare();

		Assert.Equal(@"C:\tools\ed.exe", LoadTextEditor(Key + @": C:\tools\ed.exe ", item));
	}

	/// <summary>该实例项可能被别的用例置为 Fixed,写值会被静默丢弃,故先复位。</summary>
	private static ConfigItem<string> Prepare()
	{
		ConfigItem<string> item = ConfigData.Instance.GetRequiredConfigItem<string>(ConfigCode.TextEditor);
		item.Fixed = false;
		item.Value = "";
		return item;
	}

	private static string LoadTextEditor(string configLine, ConfigItem<string> item)
	{
		string root = GameRootScope.UseTempRoot("config-text-editor");
		string path = Path.Combine(root, "emuera.config");
		File.WriteAllText(path, configLine + Environment.NewLine);

		Assert.True(ConfigData.Instance.loadConfig(path, false));

		return item.Value;
	}
}
