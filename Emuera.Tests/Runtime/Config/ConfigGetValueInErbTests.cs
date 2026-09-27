using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

// 命名空间不带 .Config:那会在 Emuera.Tests.Runtime 下造出 Config 命名空间,
// 遮蔽 using 引入的 Config 类型(同目录既有用例同此处理)
namespace Emuera.Tests.Runtime;

/// <summary>
/// ConfigData.GetConfigValueInERB(ERB 的 GETCONFIG/GETCONFIGS 取值入口)测试。
/// seam 取这对新入口本身:探测版 TryGetConfigValueInERB 如实回答「有没有」,
/// 求值版 GetConfigValueInERB 把同一个 errMes 抛成 CodeEE——原先这对信息被塞在
/// `ref string? errMes` 里,唯一的调用点必须自己写 `if (errMes != null) throw` 再 `term!`。
/// ConfigData 是进程内单例,写过的项在 Dispose 里还原(见 Emuera.Tests/README.md)。
/// </summary>
public class ConfigGetValueInErbTests : IDisposable
{
	private const string UnknownKey = "NO_SUCH_CONFIG";

	private readonly string _prevFontName;
	private readonly int _prevWindowY;

	public ConfigGetValueInErbTests()
	{
		TestBootstrap.Initialize();
		_prevFontName = ConfigData.Instance.GetRequiredConfigItem<string>(ConfigCode.FontName).Value;
		_prevWindowY = ConfigData.Instance.GetRequiredConfigItem<int>(ConfigCode.WindowY).Value;
	}

	public void Dispose()
	{
		ConfigData.Instance.GetRequiredConfigItem<string>(ConfigCode.FontName).Value = _prevFontName;
		ConfigData.Instance.GetRequiredConfigItem<int>(ConfigCode.WindowY).Value = _prevWindowY;
	}

	[Fact]
	public void TryGet_UnknownName_ReportsInvalidConfigName()
	{
		Assert.False(ConfigData.TryGetConfigValueInERB(UnknownKey, out SingleTerm? term, out string? errMes));

		Assert.Null(term);
		Assert.Equal(string.Format(trerror.InvalidConfigName.Text, UnknownKey), errMes);
	}

	[Fact]
	public void TryGet_StringConfig_ReturnsItsValueInAStrTerm()
	{
		ConfigData.Instance.GetRequiredConfigItem<string>(ConfigCode.FontName).Value = "PROBE_FONT";

		Assert.True(ConfigData.TryGetConfigValueInERB("FONT NAME", out SingleTerm? term, out string? errMes));

		Assert.Null(errMes);
		Assert.Equal("PROBE_FONT", Assert.IsType<SingleStrTerm>(term).Str);
	}

	[Fact]
	public void TryGet_ConfigOutsideTheSwitch_RoundTripsThroughItsStringForm()
	{
		// WINDOW HEIGHT 不在 GetConfigValueInERB 的 switch 里,走 default 分支:ValueToString → long 解析
		ConfigData.Instance.GetRequiredConfigItem<int>(ConfigCode.WindowY).Value = 12345;

		Assert.True(ConfigData.TryGetConfigValueInERB("WINDOW HEIGHT", out SingleTerm? term, out string? errMes));

		Assert.Null(errMes);
		Assert.Equal(12345L, Assert.IsType<SingleLongTerm>(term).Int);
	}

	[Fact]
	public void Get_UnknownName_ThrowsCodeEEWithTheTryGetMessage()
	{
		var e = Assert.Throws<CodeEE>(() => ConfigData.GetConfigValueInERB(UnknownKey));

		Assert.Equal(string.Format(trerror.InvalidConfigName.Text, UnknownKey), e.Message);
	}

	[Fact]
	public void Get_KnownName_ReturnsTheResolvedTerm()
	{
		ConfigData.Instance.GetRequiredConfigItem<string>(ConfigCode.FontName).Value = "PROBE_FONT";

		Assert.Equal("PROBE_FONT", Assert.IsType<SingleStrTerm>(ConfigData.GetConfigValueInERB("FONT NAME")).Str);
	}
}
