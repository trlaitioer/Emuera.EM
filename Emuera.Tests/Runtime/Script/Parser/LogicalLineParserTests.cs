using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Parser;

/// <summary>
/// LogicalLineParser(逻辑行解析)测试与脚本级冒烟。
/// ParseLine/ParseLabelLine 的 console 参数在非 AnalysisMode 下不参与解析,传 null
/// (与 TestBootstrap 的 ParserMediator.Initialize(null) 同理,无需 WinForms/STA)。
/// </summary>
public class LogicalLineParserTests
{
	public LogicalLineParserTests()
	{
		TestBootstrap.Initialize();
	}

	private static ScriptPosition Pos => new("test.erb", 1);

	/// <summary>解析函数体行(不含 @/$/# 路由,console 传 null)。</summary>
	private static LogicalLine? ParseBodyLine(string line)
		=> LogicalLineParser.ParseLine(new CharStream(line), Pos, null);

	/// <summary>解析标签行(@函数 / $跳转标签)。</summary>
	private static LogicalLine ParseLabelLine(string line)
		=> LogicalLineParser.ParseLabelLine(new CharStream(line), Pos, null);

	[Fact]
	public void EmptyLine_ReturnsNull()
	{
		Assert.Null(LogicalLineParser.ParseLine("", null!));
		Assert.Null(LogicalLineParser.ParseLine("   \t ", null!));
	}

	[Fact]
	public void InstructionLine_WithAndWithoutArgument()
	{
		var line1 = Assert.IsType<InstructionLine>(ParseBodyLine("PRINTL こんにちは"));
		Assert.Equal(FunctionCode.PRINTL, line1.Function.Code);

		var line2 = Assert.IsType<InstructionLine>(ParseBodyLine("WAIT"));
		Assert.Equal(FunctionCode.WAIT, line2.Function.Code);
	}

	[Fact]
	public void AssignmentLine_BecomesSetInstruction()
	{
		var line = Assert.IsType<InstructionLine>(ParseBodyLine("FLAG:0 = 5"));
		Assert.Same(FunctionIdentifier.SETFunction, line.Function);
	}

	[Fact]
	public void PreIncrementLine_BecomesSetInstruction()
	{
		var line = Assert.IsType<InstructionLine>(ParseBodyLine("++COUNT"));
		Assert.Same(FunctionIdentifier.SETFunction, line.Function);
	}

	[Fact]
	public void UnparsableLine_BecomesInvalid()
	{
		var line = Assert.IsType<InvalidLine>(ParseBodyLine("__THIS_IS_NOT_DEFINED__"));
		Assert.False(string.IsNullOrEmpty(line.ErrMes));
	}

	[Fact]
	public void LabelLine_Goto()
	{
		var line = Assert.IsType<GotoLabelLine>(ParseLabelLine("$RETRY"));
		Assert.Equal("RETRY", line.LabelName);
	}

	[Fact]
	public void LabelLine_FunctionEvent()
	{
		var line = Assert.IsType<FunctionLabelLine>(ParseLabelLine("@EVENTFIRST"));
		Assert.Equal("EVENTFIRST", line.LabelName);
		Assert.True(line.IsEvent);
		Assert.False(line.IsMethod);
	}

	[Fact]
	public void LabelLine_EmptyName_BecomesInvalid()
	{
		Assert.IsType<InvalidLabelLine>(ParseLabelLine("@"));
	}

	[Fact]
	public void SharpLine_FunctionDeclaresMethod()
	{
		var label = Assert.IsType<FunctionLabelLine>(ParseLabelLine("@FUNC_SHARP"));
		Assert.True(LogicalLineParser.ParseSharpLine(label, new CharStream("#FUNCTION"), Pos, []));
		Assert.True(label.IsMethod);
		Assert.Equal(typeof(long), label.MethodType);
	}

	[Fact]
	public void SharpLine_DimAddsPrivateVariable()
	{
		var label = Assert.IsType<FunctionLabelLine>(ParseLabelLine("@FUNC_DIM"));
		Assert.True(LogicalLineParser.ParseSharpLine(label, new CharStream("#DIM COUNTER, 1"), Pos, []));
		Assert.NotNull(label.GetPrivateVariable("COUNTER"));
	}

	/// <summary>
	/// 脚本级冒烟:一段覆盖标签/属性/赋值/分支/FORM/调用/跳转的脚本片段,
	/// 按 ErbLoader 的路由规则(@/$→ParseLabelLine、#→ParseSharpLine、其余→ParseLine)逐行解析,
	/// 断言无解析错误行。
	/// </summary>
	[Fact]
	public void Smoke_EventFirstFragment_ParsesWithoutError()
	{
		var lines = new[]
		{
			"@EVENTFIRST",
			"#DIM COUNTER, 1",
			"PRINTL ゲーム開始",
			"FLAG:0 = 5",
			"IF 1 > 0",
			"\tPRINTL TRUE",
			"ELSE",
			"\tPRINTL FALSE",
			"ENDIF",
			"PRINTFORML 値は{1+1}です",
			"CALL SHOW_TITLE",
			"$RETRY",
			"WAIT",
			"@SHOW_TITLE",
			"PRINTL タイトル",
		};
		FunctionLabelLine? current = null;
		var functionCount = 0;
		foreach (var raw in lines)
		{
			// EraStreamReader 已在此前滤除空行与注释行,冒烟循环同约定
			var line = raw.Trim();
			if (line.Length == 0)
				continue;
			LogicalLine parsed;
			var st = new CharStream(line);
			if (st.Current == '@' || st.Current == '$')
			{
				parsed = LogicalLineParser.ParseLabelLine(st, Pos, null);
				if (parsed is FunctionLabelLine func)
				{
					current = func;
					functionCount++;
				}
			}
			else if (st.Current == '#')
			{
				Assert.NotNull(current);
				Assert.True(LogicalLineParser.ParseSharpLine(current, st, Pos, []), $"属性行解析失败: {raw}");
				continue;
			}
			else
			{
				parsed = LogicalLineParser.ParseLine(st, Pos, null, current);
			}
			Assert.False(parsed is InvalidLine || parsed is InvalidLabelLine,
				$"行解析失败: {raw} → {parsed?.ErrMes}");
		}
		Assert.Equal(2, functionCount);
	}
}
