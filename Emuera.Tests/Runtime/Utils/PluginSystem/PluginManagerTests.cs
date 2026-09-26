using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.PluginSystem;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Utils.PluginSystem;

/// <summary>
/// PluginManager(插件 API)测试。
/// 调用栈文本只由进程状态推导、不写控制台,故可在无 EmueraConsole 的夹具下覆盖。
/// </summary>
public class PluginManagerTests
{
	public PluginManagerTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>把插件管理器挂到一个「未运行脚本」的进程状态上,或挂到指定的当前行。</summary>
	private static void SetUp(LogicalLine? currentLine)
	{
		PluginManager.GetInstance().SetParent(GlobalStatic.Process, new ProcessState(null!) { CurrentLine = currentLine },
			GlobalStatic.EMediator);
	}

	[Fact]
	public void GetStackTrace_WithoutRunningLine_IsEmpty()
	{
		SetUp(null);

		Assert.Empty(PluginManager.GetInstance().GetStackTrace());
	}

	[Fact]
	public void GetStackTrace_ReportsPositionAndFunction()
	{
		var label = new FunctionLabelLine(new ScriptPosition("def.erb", 2), "FOO", new WordCollection());
		SetUp(new GotoLabelLine(new ScriptPosition("call.erb", 6), "SITE") { ParentLabelLine = label });

		Assert.Equal("call.erb:7@FOO", Assert.Single(PluginManager.GetInstance().GetStackTrace()));
	}

	[Fact]
	public void ExecuteLine_NotAnInstruction_ThrowsCodeEE()
	{
		SetUp(null);

		// 空行:ParseLine 返回 null
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine(""));
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("   \t "));
		// @/$ 行与无法解释的行:InvalidLine,返回该行自身的错误(此前是无效引用转换直接崩)
		Assert.NotEqual(trerror.InvalidDebugCommand.Text,
			Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("@FOO")).Message);
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("__NO_SUCH_COMMAND__"));
	}

	[Fact]
	public void ExecuteLine_FlowControlInstruction_ThrowsCodeEE()
	{
		SetUp(null);

		// 内存行没有父标签、也没有正在执行的函数,故不接受改变流程的指令
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("RESTART"));
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("GOTO @FOO"));
	}

	[Fact]
	public void ExecuteLine_InstructionRequiringRunningFunction_ThrowsCodeEE()
	{
		SetUp(null);

		// QUIT 不是 METHOD_SAFE,PRINTW 会等待输入;单行入口两者都不具备
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("QUIT"));
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("PRINTW x"));
	}

	[Fact]
	public void ExecuteLine_UnparsableArgument_ThrowsCodeEE()
	{
		SetUp(null);

		// ADDCHARA 必须有实参;实参解析失败的行不执行(此前会带着空 BuiltArgument 进 DoInstruction)
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("ADDCHARA"));
	}

	[Fact]
	public void ExecuteLine_DebugInstructionInNonDebugMode_ThrowsCodeEE()
	{
		SetUp(null);

		// 非 Debug 模式下 SetArgumentTo 不解析 Debug 系指令的实参、Argument 留空,
		// 直接执行会读空 BuiltArgument,故按不可执行拒绝
		Assert.False(Program.DebugMode);
		Assert.Throws<CodeEE>(() => PluginManager.GetInstance().ExecuteLine("DEBUGPRINT x"));
	}
}
