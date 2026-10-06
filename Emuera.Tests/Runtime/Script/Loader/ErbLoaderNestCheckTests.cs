using MinorShift.Emuera;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Loader;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Loader;

/// <summary>
/// ErbLoader.nestCheck 的配对不变量回归网:配对失败时,读 JumpTo 的行必须在解析期标错
/// (Process.ScriptProc 对 IsError 行抛 CodeEE),否则执行期的 JumpTo 断言会走进空引用。
/// 缝:ErbLoader.LoadErbList(装载入口)+ 遍历 FunctionLabelLine.NextLine 链的解析产物。
/// </summary>
public class ErbLoaderNestCheckTests
{
	public ErbLoaderNestCheckTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>
	/// 夹具放在 Depth==0 的系统标签里:默认 Config.IgnoreUncalledFunction 会跳过未被调用的
	/// 普通函数,nestCheck 根本不会运行。
	/// </summary>
	private static async Task<List<InstructionLine>> LoadFixture(params string[] lines)
		=> (await ErbLoaderFixture.LoadSystemTitleBody("nestcheck", lines)).Body;

	private static InstructionLine LineWith(List<InstructionLine> body, FunctionCode code)
		=> Assert.Single(LinesWith(body, code));

	private static List<InstructionLine> LinesWith(List<InstructionLine> body, FunctionCode code)
		=> [.. body.Where(line => line.FunctionCode == code)];

	/// <summary>
	/// ENDSELECT 恢复分支:SELECTCASE 与 ENDSELECT 之间夹着未闭合的 IF 时,被弹出的 IF 无对应
	/// ENDIF 可配对,必须在解析期标错;CASE 行随后会被 SELECTCASE 跳转到,必须拿到 ENDSELECT 作
	/// 为目标(旧行为是整段配对被丢弃,CASE 行拿到 null 后执行期空跳转)。
	/// </summary>
	[Fact]
	public async Task EndselectRecovery_PairsTheSelectcaseAndMarksTheDroppedLine()
	{
		List<InstructionLine> body = await LoadFixture(
			"SELECTCASE 1",
			"CASE 1",
			"PRINTFORM \"a\"",
			"CASE 2",
			"PRINTFORM \"b\"",
			"IF 0",
			"ENDSELECT");

		InstructionLine select = LineWith(body, FunctionCode.SELECTCASE);
		InstructionLine endSelect = LineWith(body, FunctionCode.ENDSELECT);
		InstructionLine droppedIf = LineWith(body, FunctionCode.IF);

		Assert.Same(endSelect, select.JumpTo);
		foreach (InstructionLine caseLine in LinesWith(body, FunctionCode.CASE))
		{
			Assert.Same(endSelect, caseLine.JumpTo);
		}

		Assert.True(droppedIf.IsError);
		Assert.True(endSelect.IsError);
	}

	/// <summary>
	/// CASE 恢复分支:未闭合的 IF 挡在 CASE 之前时,CASE 行自身被标错(既有行为),被丢弃的 IF 也
	/// 必须标错——此形态下门禁并不生效:后续 ENDSELECT 会正常配对 SELECTCASE,块内顺次执行到该 IF
	/// 行时 IF_Instruction 以 null JumpTo 空跳转。
	/// </summary>
	[Fact]
	public async Task CaseRecovery_MarksTheDroppedLineSoTheBlockIsNotEnterable()
	{
		List<InstructionLine> body = await LoadFixture(
			"SELECTCASE 1",
			"CASE 1",
			"PRINTFORM \"a\"",
			"IF 0",
			"CASE 2",
			"ENDSELECT");

		InstructionLine select = LineWith(body, FunctionCode.SELECTCASE);
		InstructionLine endSelect = LineWith(body, FunctionCode.ENDSELECT);
		InstructionLine droppedIf = LineWith(body, FunctionCode.IF);

		Assert.False(select.IsError);
		Assert.Same(endSelect, select.JumpTo);
		Assert.True(droppedIf.IsError);
	}

	/// <summary>
	/// 循环外的 BREAK/CONTINUE 必须按「不在循环内」报错,不能沿用上一条指令留下的配对目标:
	/// 旧实现把目标记在一个跨行共享的局部里,CATCH 行残留使其 `JumpTo` 指向一个 JumpTo 为 null
	/// 的行(执行期取 LoopCounter 时抛),已结束循环行残留则把它静默地重新进入。
	/// </summary>
	[Fact]
	public async Task BreakOutsideLoop_DoesNotInheritATargetFromAnotherLine()
	{
		//CATCH 行残留:CATCH 的 JumpTo 从不赋值
		AssertBreakOutsideLoopIsMarked(await LoadFixture("TRYCCALL X", "CATCH", "ENDCATCH", "BREAK"));

		//循环行残留:REND 留下的目标指向一个已结束的循环
		AssertBreakOutsideLoopIsMarked(await LoadFixture("REPEAT 3", "BREAK", "REND", "BREAK"));

		static void AssertBreakOutsideLoopIsMarked(List<InstructionLine> body)
		{
			InstructionLine lastBreak = LinesWith(body, FunctionCode.BREAK)[^1];
			Assert.True(lastBreak.IsError);
			Assert.Null(lastBreak.JumpTo);
		}
	}

	/// <summary>
	/// 正控制:正常配对的脚本不得被标错、配对目标保持两端一致。恢复分支新增的标记若越界到正常路径,
	/// 这里会失败(该用例在修复前后都应通过,是护栏而非红绿驱动)。
	/// </summary>
	[Fact]
	public async Task WellFormedBlocks_ArePairedAndNotMarked()
	{
		List<InstructionLine> body = await LoadFixture(
			"REPEAT 3",
			"BREAK",
			"REND",
			"SELECTCASE 1",
			"CASE 1",
			"PRINTFORM \"a\"",
			"ENDSELECT");

		InstructionLine repeat = LineWith(body, FunctionCode.REPEAT);
		InstructionLine rend = LineWith(body, FunctionCode.REND);
		InstructionLine select = LineWith(body, FunctionCode.SELECTCASE);
		InstructionLine endSelect = LineWith(body, FunctionCode.ENDSELECT);

		Assert.Same(repeat, LineWith(body, FunctionCode.BREAK).JumpTo);
		Assert.Same(rend, repeat.JumpTo);
		Assert.Same(repeat, rend.JumpTo);
		Assert.Same(endSelect, select.JumpTo);
		Assert.Same(endSelect, LineWith(body, FunctionCode.CASE).JumpTo);
		Assert.DoesNotContain(body, line => line.IsError);
	}
}
