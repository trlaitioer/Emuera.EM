using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Config;
using System.Reflection;
using Xunit;

// 命名空间不能取 Emuera.Tests.Runtime.Config:那会让同项目其它文件里的 Config 解析成命名空间而非类型。
namespace Emuera.Tests.Runtime;

/// <summary>
/// 配置项「写入方写出的键」必须落在「读取方认的键」集合内(backlog-01)。
/// 写入侧只有两种形态(Emuera/Runtime/Config/ConfigItem.cs 的 WrittenKey),读取侧三条路径
/// (Emuera/Runtime/Config/ConfigData.cs 的 GetConfigItem/GetReplaceItem/GetDebugItem)同源于 FindByKey;
/// 本文件锁住这条闭合关系。
/// 不调 Config.SetConfig(会连带改写其它用例依赖的静态);Config.SetDebugConfig 只写 7 个 Debug* 静态
/// (Emuera/Runtime/Config/Config.cs 的 SetDebugConfig),本文件把它们快照并在 Dispose 还原,
/// 因此 debug.config 可以走 LoadDebugConfig 真入口。
/// </summary>
public class ConfigKeySymmetryTests : IDisposable
{
	/// <summary>Config.SetDebugConfig 写的全部静态,快照/还原用。</summary>
	private static readonly string[] DebugStaticNames = [
		nameof(Config.DebugShowWindow),
		nameof(Config.DebugWindowTopMost),
		nameof(Config.DebugWindowWidth),
		nameof(Config.DebugWindowHeight),
		nameof(Config.DebugSetWindowPos),
		nameof(Config.DebugWindowPosX),
		nameof(Config.DebugWindowPosY),
	];

	private static readonly PropertyInfo EnglishConfigOutputProperty =
		typeof(Config).GetProperty(nameof(Config.EnglishConfigOutput), BindingFlags.Public | BindingFlags.Static)
		?? throw new InvalidOperationException("Config.EnglishConfigOutput not found");

	private readonly ConfigItem<bool> debugShowWindow = ConfigData.Instance.GetRequiredDebugItem<bool>(ConfigCode.DebugShowWindow);
	private readonly ConfigItem<int> debugWindowWidth = ConfigData.Instance.GetRequiredDebugItem<int>(ConfigCode.DebugWindowWidth);
	private readonly ConfigItem<int> windowY = ConfigData.Instance.GetRequiredConfigItem<int>(ConfigCode.WindowY);
	private readonly ConfigItem<string> moneyLabel = (ConfigItem<string>)ConfigData.Instance.GetRequiredItem(ConfigCode.MoneyLabel);

	private readonly bool prevEnglishConfigOutput = Config.EnglishConfigOutput;
	private readonly object?[] prevDebugStatics;
	private readonly ItemState prevDebugShowWindow;
	private readonly ItemState prevDebugWindowWidth;
	private readonly ItemState prevWindowY;
	private readonly ItemState prevMoneyLabel;

	public ConfigKeySymmetryTests()
	{
		prevDebugStatics = [.. DebugStaticNames.Select(ReadDebugStatic)];
		prevDebugShowWindow = Snapshot(debugShowWindow);
		prevDebugWindowWidth = Snapshot(debugWindowWidth);
		prevWindowY = Snapshot(windowY);
		prevMoneyLabel = Snapshot(moneyLabel);
	}

	/// <summary>ConfigData 是进程内单例,写过的项与静态按 Emuera.Tests/README.md 还原。</summary>
	public void Dispose()
	{
		SetEnglishConfigOutput(prevEnglishConfigOutput);
		Restore(debugShowWindow, prevDebugShowWindow);
		Restore(debugWindowWidth, prevDebugWindowWidth);
		Restore(windowY, prevWindowY);
		Restore(moneyLabel, prevMoneyLabel);

		for (int i = 0; i < DebugStaticNames.Length; i++)
		{
			DebugStaticProperty(DebugStaticNames[i]).SetValue(null, prevDebugStatics[i]);
		}
	}

	/// <summary>
	/// 写入方写 EngText 还是 Text 由 EnglishConfigOutput 决定,两种都可能出现在文件里
	/// ⇒ 两种拼写都必须是可读键。用 Assert.Same 而不是 NotNull:命中「另一个项」同样是坏键。
	/// </summary>
	[Fact]
	public void EveryItemIsReachableByBothKeySpellingsTheWriterCanEmit()
	{
		int checkedCount = 0;

		foreach (ConfigCode code in Enum.GetValues<ConfigCode>())
		{
			AConfigItem? item = ConfigData.Instance.GetItem(code);
			if (item == null)
			{
				continue;
			}

			Assert.Same(item, ConfigData.Instance.GetItem(item.Text));
			Assert.Same(item, ConfigData.Instance.GetItem(item.EngText));
			checkedCount++;
		}

		// 遍历没取到任何项时上面的循环会静默全过。
		Assert.True(checkedCount > 100, $"只检查了 {checkedCount} 个配置项,枚举遍历疑似失效");
	}

	/// <summary>
	/// debug.config 的自写自读走真入口:EnglishConfigOutput=true 下 SaveDebugConfig 写出的英文键,
	/// 必须被 LoadDebugConfig 原样读回(含文件解析、TryParse 与 SetDebugConfig)。
	/// 逐行的键闭合另由 EveryItemIsReachableByBothKeySpellings 兜住,此处锁的是端到端行为。
	/// </summary>
	[Fact]
	public void DebugConfigWrittenInEnglishSurvivesASaveAndReload()
	{
		GameRootScope.UseTempRoot("config-key-symmetry-debug-file");
		Directory.CreateDirectory(Program.DebugRoot.FullName);
		SetEnglishConfigOutput(true);

		debugShowWindow.Fixed = false;
		debugWindowWidth.Fixed = false;
		debugShowWindow.Value = false;
		debugWindowWidth.Value = 357;
		Assert.True(ConfigData.Instance.SaveDebugConfig());

		// 扰动:读侧若不认英文键,LoadDebugConfig 会静默保持下面这两个值。
		debugShowWindow.Value = true;
		debugWindowWidth.Value = 400;

		Assert.True(ConfigData.Instance.LoadDebugConfig());

		Assert.False(debugShowWindow.Value);
		Assert.Equal(357, debugWindowWidth.Value);
	}

	/// <summary>configArray 那半边今天已经闭合,固定住两种输出形态下的既有行为。</summary>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ConfigFileKeysAndValuesWrittenBySaveConfigAreAllReadable(bool englishOutput)
	{
		SetEnglishConfigOutput(englishOutput);
		string root = GameRootScope.UseTempRoot("config-key-symmetry-config-file");
		Assert.True(ConfigData.Instance.SaveConfig());

		string[] lines = File.ReadAllLines(Path.Combine(root, "emuera.config"));
		Assert.NotEmpty(lines);

		foreach (string line in lines)
		{
			int separator = line.IndexOf(':');
			Assert.True(separator > 0, $"emuera.config 的行没有键值分隔符:{line}");

			string key = line[..separator];
			AConfigItem? item = ConfigData.Instance.GetConfigItem(key);
			Assert.NotNull(item);
			// 文件由当前状态写出:值也必须回得去(loadConfig 用的是同一个 TryParse)。
			Assert.Equal(line[(separator + 1)..], item.ValueToString());
		}
	}

	/// <summary>
	/// 未知键静默是既定语义(Emuera/Runtime/Config/ConfigData.cs 的 loadConfig 注释):不抛异常、返回成功、
	/// 不命中任何项。断言必须非空转——文件里放一行已知键作正控(证明文件确实被解析并生效),
	/// 再放一行近似误配键(WINDOW HEIGH 与 WINDOW HEIGHT 只差一个字母),它排在正控之后:
	/// 一旦被部分命中就会把正控写入的值改掉。
	/// 不额外断言 loadConfig 的返回值:它在 catch 之后同样 return true,返回值不携带信息。
	/// </summary>
	[Fact]
	public void UnknownAndNearMissKeysAreIgnoredWhileAKnownKeyOnTheSameFileIsApplied()
	{
		string root = GameRootScope.UseTempRoot("config-key-symmetry-unknown-key");
		windowY.Fixed = false;

		int applied = windowY.Value + 7;
		string path = Path.Combine(root, "emuera.config");
		File.WriteAllLines(path, [
			"NO SUCH CONFIG ITEM:12345",
			"WINDOW HEIGHT:" + applied,
			"WINDOW HEIGH:999",
		]);

		ConfigData.Instance.loadConfig(path, false);
		Assert.Equal(applied, windowY.Value);
	}

	/// <summary>
	/// _Replace.csv 那半边没有写入方(游戏作者手写),未命中同样静默:正控行用该项的英文标签,
	/// 证明文件被解析且读侧认 EngText;近似键排在正控之后,一旦部分命中就会改掉正控写入的值。
	/// </summary>
	[Fact]
	public void UnknownReplaceKeyIsIgnoredWhileAKnownKeyOnTheSameFileIsApplied()
	{
		string root = GameRootScope.UseTempRoot("config-key-symmetry-replace-file");
		moneyLabel.Fixed = false;

		string path = Path.Combine(root, "replace.csv");
		File.WriteAllLines(path, [
			"NO SUCH REPLACE ITEM,%",
			"CURRENCY SYMBOL,@",
			"CURRENCY SYMBOLX,%",
		]);

		ConfigData.Instance.LoadReplaceFile(path);
		Assert.Equal("@", moneyLabel.Value);
	}

	/// <summary>
	/// debug.config 的未命中同样静默:正控行证明文件被解析(返回 true 在此非空转,LoadDebugConfig
	/// 在文件不存在/解析异常时返回 false),近似键排在正控之后,一旦部分命中就会改掉正控写入的值。
	/// </summary>
	[Fact]
	public void UnknownDebugKeyIsIgnoredWhileAKnownKeyOnTheSameFileIsApplied()
	{
		GameRootScope.UseTempRoot("config-key-symmetry-debug-unknown-key");
		Directory.CreateDirectory(Program.DebugRoot.FullName);
		debugWindowWidth.Fixed = false;

		File.WriteAllLines(Program.DebugRoot.CombineName("debug.config").FullName, [
			"NO SUCH DEBUG ITEM:999",
			"DEBUG WINDOW WIDTH:321",
			"DEBUG WINDOW WIDTHX:999",
		]);

		Assert.True(ConfigData.Instance.LoadDebugConfig());
		Assert.Equal(321, debugWindowWidth.Value);
	}

	private static void SetEnglishConfigOutput(bool value)
	{
		EnglishConfigOutputProperty.SetValue(null, value);
	}

	private static PropertyInfo DebugStaticProperty(string name)
	{
		return typeof(Config).GetProperty(name, BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException($"Config.{name} not found");
	}

	private static object? ReadDebugStatic(string name)
	{
		return DebugStaticProperty(name).GetValue(null);
	}

	/// <summary>项的值与 Fixed 位:还原写过的共享状态用。</summary>
	private sealed record ItemState(string Value, bool Fixed);

	private static ItemState Snapshot(AConfigItem item)
	{
		return new ItemState(item.ValueToString(), item.Fixed);
	}

	private static void Restore(AConfigItem item, ItemState state)
	{
		// TryParse 与 Value 的 setter 在 Fixed 时都会静默丢弃写入。
		item.Fixed = false;
		item.TryParse(state.Value);
		item.Fixed = state.Fixed;
	}
}
