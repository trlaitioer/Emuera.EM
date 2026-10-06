using Emuera.Tests.Runtime.Script.Loader;
using MinorShift.Emuera;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Statements;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// BREAK 在循环头未执行时(nullable-migration-27):GOTO 跳进 REPEAT/FOR 体后 LoopCounter 仍为 null,
/// 旧行为在该断言上抛 NullReferenceException。缝:ErbLoader.LoadErbList(共享夹具见 ErbLoaderFixture)
/// + 直接调 BREAK_Instruction.DoInstruction 并观察 ProcessState.CurrentLine。
/// </summary>
public class BreakLoopCounterTests
{
	public BreakLoopCounterTests()
	{
		TestBootstrap.Initialize();
	}

	private static async Task<List<InstructionLine>> LoadFixture(params string[] lines)
		=> (await ErbLoaderFixture.LoadSystemTitleBody("breakloop", lines)).Body;

	private static InstructionLine LineWith(List<InstructionLine> body, FunctionCode code)
		=> Assert.Single(body, line => line.FunctionCode == code);

	/// <summary>GOTO 跳进 REPEAT 体:循环头从未执行,故 BREAK 没有计数器可旋转,应直接跳出该循环。</summary>
	[Fact]
	public async Task Break_WithSkippedRepeatHeader_JumpsOutWithoutACounter()
	{
		List<InstructionLine> body = await LoadFixture("GOTO L1", "REPEAT 3", "$L1", "BREAK", "REND");

		InstructionLine breakLine = LineWith(body, FunctionCode.BREAK);
		var repeatLine = Assert.IsType<InstructionLine>(breakLine.JumpTo);
		Assert.Equal(FunctionCode.REPEAT, repeatLine.Function.Code);
		Assert.Null(repeatLine.LoopCounter); // 跳过了循环头,计数器从未赋值(缺陷前提)

		var state = new ProcessState(null!);
		breakLine.Function.Instruction!.DoInstruction(GlobalStatic.EMediator, breakLine, state);

		Assert.Same(repeatLine.JumpTo, state.CurrentLine); // 跳到 REND,与 CONTINUE/REND 的「无视并跳出」一致
	}

	/// <summary>WHILE 与 DO 本就没有计数器,同形态的跳入不得回归(修复前也走这条短路)。</summary>
	[Fact]
	public async Task Break_WithSkippedWhileHeader_JumpsOut()
	{
		List<InstructionLine> body = await LoadFixture("GOTO L1", "WHILE 1", "$L1", "BREAK", "WEND");

		InstructionLine breakLine = LineWith(body, FunctionCode.BREAK);
		var whileLine = Assert.IsType<InstructionLine>(breakLine.JumpTo);
		Assert.Equal(FunctionCode.WHILE, whileLine.Function.Code);

		var state = new ProcessState(null!);
		breakLine.Function.Instruction!.DoInstruction(GlobalStatic.EMediator, breakLine, state);

		Assert.Same(whileLine.JumpTo, state.CurrentLine);
	}
}
