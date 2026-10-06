using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Loader;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Loader;

/// <summary>
/// 首个 @函数 之前的语句(nullable-migration-24)。VARI/VARS 需要父标签来登记私有变量,
/// 而 ErbLoader 对函数定义之前的行只警告后继续解析,故该断言曾以 NullReferenceException
/// 崩在装载期。缝:ErbLoader.LoadErbList(共享夹具见 ErbLoaderFixture)。
/// </summary>
public class ErbLoaderPreFunctionLineTests : IDisposable
{
	private readonly bool _prevDeferParseErrorSound;

	public ErbLoaderPreFunctionLineTests()
	{
		TestBootstrap.Initialize();
		// 解析报错路径会经 NotifyParseError 播放提示音,用例中抑制
		_prevDeferParseErrorSound = ParserMediator.DeferParseErrorSound;
		ParserMediator.DeferParseErrorSound = true;
	}

	public void Dispose()
	{
		ParserMediator.DeferParseErrorSound = _prevDeferParseErrorSound;
	}

	/// <summary>首个 @函数 之前的 VARI:装载期不得抛 NullReferenceException,该行按解析错误处置。</summary>
	[Fact]
	public async Task VariBeforeFirstFunction_IsReportedAsAParseError()
	{
		(bool loaded, _) = await ErbLoaderFixture.LoadErb("prefunc", "VARI X", "@SYSTEM_TITLE", "PRINTL \"a\"");

		Assert.False(loaded);
	}

	/// <summary>VARS 与 VARI 同根因(同一分支的两处 parentLine 断言)。</summary>
	[Fact]
	public async Task VarsBeforeFirstFunction_IsReportedAsAParseError()
	{
		(bool loaded, _) = await ErbLoaderFixture.LoadErb("prefunc", "VARS X", "@SYSTEM_TITLE", "PRINTL \"a\"");

		Assert.False(loaded);
	}

	/// <summary>正控制:去掉函数定义之前的那一行即可正常装载(夹具本身不恒为失败)。</summary>
	[Fact]
	public async Task FunctionWithoutPrecedingStatement_LoadsCleanly()
	{
		(bool loaded, _) = await ErbLoaderFixture.LoadErb("prefunc", "@SYSTEM_TITLE", "PRINTL \"a\"");

		Assert.True(loaded);
	}
}
