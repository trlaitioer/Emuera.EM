using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// LineLocationText(错误报告/调试输出里的行位置文本)测试。
/// 内存行(PluginManager.ExecuteLine / EmueraConsole.DebugCommand 构造的行)的 ParentLabelLine 为 null、
/// Position 恒为 ScriptPosition 的默认值,故文本必须按可用部分降级,而不是断言非空。
/// </summary>
public class LineLocationTextTests
{
	public LineLocationTextTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>@函数的定义位置。ScriptPosition(file, n) 的 LineNo 是 n + 1。</summary>
	private static FunctionLabelLine Label(string file, int lineNo, string name)
		=> new(new ScriptPosition(file, lineNo), name, new WordCollection());

	/// <summary>属于某个函数的行。</summary>
	private static GotoLabelLine LineIn(FunctionLabelLine? parent, string file, int lineNo)
		=> new(new ScriptPosition(file, lineNo), "SITE") { ParentLabelLine = parent };

	/// <summary>默认结构的 ScriptPosition:内存行的位置。</summary>
	private static ScriptPosition NoPosition => new();

	/// <summary>插件/调试命令构造的内存行。</summary>
	private static LogicalLine MemoryLine()
		=> LogicalLineParser.ParseLine("PRINTL x", null)!;

	[Fact]
	public void CurrentFunction_UsesFunctionDefinitionPosition()
	{
		var text = LineLocationText.CurrentFunction(LineIn(Label("def.erb", 2, "FOO"), "call.erb", 6));

		Assert.NotNull(text);
		Assert.Contains("FOO", text);
		// 函数定义处,不是调用点
		Assert.Contains("def.erb", text);
		Assert.Contains("3", text);
		Assert.DoesNotContain("call.erb", text);
	}

	[Fact]
	public void CurrentFunction_WithoutFunction_IsNull()
	{
		Assert.Null(LineLocationText.CurrentFunction(null));
		Assert.Null(LineLocationText.CurrentFunction(MemoryLine()));
		Assert.Null(LineLocationText.CurrentFunction(LineIn(null, "call.erb", 6)));
		// 标签定义位置缺失时同样无法构成
		Assert.Null(LineLocationText.CurrentFunction(LineIn(new FunctionLabelLine(null, "FOO", new WordCollection()), "call.erb", 6)));
	}

	[Fact]
	public void CallStackEntry_UsesCallSitePosition()
	{
		var text = LineLocationText.CallStackEntry(LineIn(Label("def.erb", 2, "FOO"), "call.erb", 6));

		Assert.NotNull(text);
		Assert.Contains("call.erb", text);
		Assert.Contains("7", text);
		Assert.Contains("FOO", text);
		Assert.DoesNotContain("def.erb", text);
	}

	[Fact]
	public void CallStackEntry_WithoutPositionOrFunction_IsNull()
	{
		var label = Label("def.erb", 2, "FOO");
		Assert.Null(LineLocationText.CallStackEntry(null));
		Assert.Null(LineLocationText.CallStackEntry(MemoryLine()));
		Assert.Null(LineLocationText.CallStackEntry(LineIn(null, "call.erb", 6)));
		// 默认结构的位置视为「无位置」,即便它有父标签
		Assert.Null(LineLocationText.CallStackEntry(new GotoLabelLine(NoPosition, "SITE") { ParentLabelLine = label }));
	}

	[Fact]
	public void StackFrame_JoinsPositionAndFunction()
	{
		Assert.Equal("call.erb:7@FOO", LineLocationText.StackFrame(LineIn(Label("def.erb", 2, "FOO"), "call.erb", 6)));
	}

	[Fact]
	public void StackFrame_KeepsThePartsItHas()
	{
		var label = Label("def.erb", 2, "FOO");
		Assert.Equal("call.erb:7", LineLocationText.StackFrame(LineIn(null, "call.erb", 6)));
		Assert.Equal("@FOO", LineLocationText.StackFrame(new GotoLabelLine(NoPosition, "SITE") { ParentLabelLine = label }));
		Assert.Null(LineLocationText.StackFrame(MemoryLine()));
		Assert.Null(LineLocationText.StackFrame(null));
	}

	[Fact]
	public void JumpTrace_KeepsLabelFileAndCallSiteLine()
	{
		// 既有格式是 标签名:标签所在文件:返回地址的行号
		Assert.Equal(" at @FOO:def.erb:7", LineLocationText.JumpTrace(LineIn(Label("def.erb", 2, "FOO"), "call.erb", 6)));
	}

	[Fact]
	public void JumpTrace_WithoutFunction_IsNull()
	{
		Assert.Null(LineLocationText.JumpTrace(null));
		Assert.Null(LineLocationText.JumpTrace(MemoryLine()));
		Assert.Null(LineLocationText.JumpTrace(LineIn(null, "call.erb", 6)));
	}
}
