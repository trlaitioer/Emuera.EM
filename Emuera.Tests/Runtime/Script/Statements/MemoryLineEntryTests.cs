using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// MemoryLineEntry 测试。seam 取该类本身:它是 DebugCommand 与 PluginManager.ExecuteLine 共用的单行入口,
/// 公开侧的等价护栏在 PluginManagerTests。本类针对新增的抛异常出口 Build()——它替掉两个调用点各自
/// 手写的 `throw new CodeEE(errMes)`(Try + 抛异常兄弟的收窄),故必须与 TryBuild 放弃同一批行、同一批文案。
/// </summary>
public class MemoryLineEntryTests : IDisposable
{
	private readonly bool _prevDeferParseErrorSound;

	public MemoryLineEntryTests()
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

	private static ScriptPosition Pos => new("test.erb", 1);

	private static LogicalLine? ParseLine(string text)
		=> LogicalLineParser.ParseLine(new CharStream(text), Pos, null);

	[Fact]
	public void Build_NothingToRun_ThrowsCodeEEWithTryBuildMessage()
	{
		var e = Assert.Throws<CodeEE>(() => MemoryLineEntry.Build(null));

		Assert.Equal(trerror.CanNotInterpretedLine.Text, e.Message);
	}

	[Fact]
	public void Build_InvalidLine_ThrowsCodeEEWithLineError()
	{
		var e = Assert.Throws<CodeEE>(() => MemoryLineEntry.Build(new InvalidLine(Pos, "boom")));

		Assert.Equal("boom", e.Message);
	}

	[Fact]
	public void Build_FlowControlInstruction_ThrowsCodeEE()
	{
		LogicalLine line = ParseLine("RESTART")!;

		var e = Assert.Throws<CodeEE>(() => MemoryLineEntry.Build(line));

		Assert.Equal(trerror.CanNotUseFlowInstruction.Text, e.Message);
	}

	[Fact]
	public void Build_RunnableInstruction_ReturnsThatBuiltInstruction()
	{
		var line = Assert.IsType<InstructionLine>(ParseLine("PRINTPLAIN \"x\""));

		InstructionLine built = MemoryLineEntry.Build(line);

		Assert.Same(line, built);
		Assert.NotNull(built.Argument);
	}
}
