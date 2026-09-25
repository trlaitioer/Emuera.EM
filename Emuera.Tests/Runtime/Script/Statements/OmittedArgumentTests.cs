using MinorShift.Emuera;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// 省略实参(issues/15)测试。seam 是解析期边界:
/// LogicalLineParser.ParseLine → ArgumentParser.SetArgumentTo,断言 InstructionLine.IsError(解析报错)
/// 或构造出的 Argument 形态。指令的 DoInstruction 需要 EmueraConsole,不在本用例集覆盖。
/// </summary>
public class OmittedArgumentTests : IDisposable
{
	private readonly bool _prevDeferParseErrorSound;

	public OmittedArgumentTests()
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

	/// <summary>按 ErbLoader 的路由解析一行命令文并构造参数(等价装载期的 setArgument)。</summary>
	private static InstructionLine ParseInstruction(string text, FunctionLabelLine? parent = null)
	{
		var parsed = LogicalLineParser.ParseLine(new CharStream(text), Pos, null, parent);
		if (parsed is InvalidLine invalid)
		{
			Assert.Fail($"行解析失败: {text} → {invalid.ErrMes}");
		}

		var instruction = Assert.IsType<InstructionLine>(parsed);
		ArgumentParser.SetArgumentTo(instruction);
		return instruction;
	}

	private static FunctionLabelLine ParseFunctionLabel(string text)
		=> Assert.IsType<FunctionLabelLine>(LogicalLineParser.ParseLabelLine(new CharStream(text), Pos, null!));

	[Fact]
	public void Vars_WithoutInitializer_TakesEmptyString()
	{
		var line = ParseInstruction("VARS FOO", ParseFunctionLabel("@FUNC_VARS"));

		Assert.False(line.IsError, line.ErrMes);
		Assert.Equal("", Assert.IsType<StrAsignArgument>(line.Argument).Value);
	}

	[Fact]
	public void Vars_WithEqualsButNoInitializer_TakesEmptyString()
	{
		var line = ParseInstruction("VARS FOO = ", ParseFunctionLabel("@FUNC_VARS2"));

		Assert.False(line.IsError, line.ErrMes);
		Assert.Equal("", Assert.IsType<StrAsignArgument>(line.Argument).Value);
	}

	[Fact]
	public void PrintSpace_WithOmittedSize_IsRejectedAtParseTime()
	{
		var line = ParseInstruction("PRINT_SPACE ,");

		Assert.True(line.IsError, "省略的尺寸实参应在解析期报错,不得以 null 进入运行期");
	}

	[Fact]
	public void PrintRect_WithOmittedSizes_IsRejectedAtParseTime()
	{
		// 1 个与 4 个空元素都通过构造器的参数个数检查,修正前会构造出 num 为 null 的项
		Assert.True(ParseInstruction("PRINT_RECT ,").IsError);
		Assert.True(ParseInstruction("PRINT_RECT ,,,,").IsError);
	}

	[Fact]
	public void PrintShape_WithFullArguments_StillParses()
	{
		var space = ParseInstruction("PRINT_SPACE 10");
		Assert.False(space.IsError, space.ErrMes);
		Assert.Single(Assert.IsType<SpPrintShapeArgument>(space.Argument).Param);

		var rect = ParseInstruction("PRINT_RECT 1, 2, 3, 4");
		Assert.False(rect.IsError, rect.ErrMes);
		Assert.All(Assert.IsType<SpPrintShapeArgument>(rect.Argument).Param, p => Assert.NotNull(p.num));
	}

	[Fact]
	public void PrintImg_WithOmittedOptionalName_TreatsSlotAsAbsent()
	{
		var line = ParseInstruction("PRINT_IMG \"a\", , 100");

		Assert.False(line.IsError, line.ErrMes);
		var arg = Assert.IsType<SpPrintImgArgument>(line.Argument);
		Assert.Null(arg.Nameb);
		Assert.Null(arg.Namem);
		Assert.NotNull(arg.Param);
		Assert.Equal(100L, arg.Param![0].num.GetIntValue(null!));
	}

	[Fact]
	public void HtmlPrint_WithOmittedOption_TreatsSlotAsAbsent()
	{
		var line = ParseInstruction("HTML_PRINT \"a\", ,");

		Assert.False(line.IsError, line.ErrMes);
		var arg = Assert.IsType<SpHtmlPrint>(line.Argument);
		Assert.NotNull(arg.Str);
		Assert.Null(arg.Opt);
	}

	[Fact]
	public void Input_WithCanSkipOnly_IsRejectedAtParseTime()
	{
		// CanSkip 分支用默认值自动填 RESULT 并按鼠标标志选择写入的变量,缺任一项都无法实现该语义
		Assert.True(ParseInstruction("INPUT ,,1").IsError);
		Assert.True(ParseInstruction("INPUT 5,,1").IsError);
	}

	[Fact]
	public void Input_WithAllArguments_KeepsThem()
	{
		var line = ParseInstruction("INPUT 5, 1, 1");

		Assert.False(line.IsError, line.ErrMes);
		var arg = Assert.IsType<SpInputsArgument>(line.Argument);
		Assert.NotNull(arg.Def);
		Assert.NotNull(arg.Mouse);
		Assert.NotNull(arg.CanSkip);
	}

	[Fact]
	public void Input_WithoutCanSkip_StillsAllowsOmittedArguments()
	{
		// 不含 CanSkip 的形态继续接受省略的默认值/鼠标标志(既有语义)
		Assert.False(ParseInstruction("INPUT 5").IsError);
		Assert.False(ParseInstruction("INPUT ,1").IsError);
	}

	[Fact]
	public void CallSharp_WithOmittedArgument_IsRejectedAtParseTime()
	{
		// 插件方法只有位置参数,既无逐参数类型也无默认值,省略没有可传递的值
		Assert.True(ParseInstruction("CALLSHARP \"m\", 1, , 3").IsError);
		Assert.True(ParseInstruction("CALLSHARP \"m\", , 3").IsError);
	}

	[Fact]
	public void PrintImg_WithOmittedCoordinate_IsRejectedAtParseTime()
	{
		// 只有 Nameb/Namem 槽位可以缺席;坐标槽位若被跳过,Param 会压缩、后续数字改换角色
		// (第 1 实参在循环外读取,循环首轮即第 2 实参,故坐标槽位从 argCount 4 起)
		Assert.True(ParseInstruction("PRINT_IMG \"a\", \"b\", \"c\", , 100").IsError);
		Assert.True(ParseInstruction("PRINT_IMG \"a\", 100, , 200").IsError);
	}

	[Fact]
	public void PrintSpace_WithPxSuffix_KeepsPixelFlag()
	{
		var line = ParseInstruction("PRINT_SPACE 10 px");

		Assert.False(line.IsError, line.ErrMes);
		var param = Assert.IsType<SpPrintShapeArgument>(line.Argument).Param;
		Assert.Single(param);
		Assert.NotNull(param[0].num);
		Assert.True(param[0].isPx);
	}

	[Fact]
	public void PrintSpace_WithGluedPxSuffix_IsRejectedToday()
	{
		// 词法器把紧跟数字的 p 当作二进制指数(读到 x 处得到空串),故 "10px" 报
		// CanNotConvertToInt("");能用的写法是 "10 px"。只钉住既有状态(LexicalAnalyzer 未在本票改动)。
		Assert.True(ParseInstruction("PRINT_SPACE 10px").IsError);
	}

	[Fact]
	public void CallWithBracketSubscript_IsRejectedToday()
	{
		// 方括号下标形式整体不可用:ArgsEndWith.RightBracket 的 TermEndWith 映射被注释掉,
		// reduceTerm 遇 ']' 直接抛 UnexpectedSymbol(null 元素因此也不可达)。此处只钉住既有状态。
		Assert.True(ParseInstruction("CALLSHARP \"m\"[1, ,3]").IsError);
		Assert.True(ParseInstruction("CALLSHARP \"m\"[,3]").IsError);
		Assert.True(ParseInstruction("CALLF FOO[1, ,2]").IsError);
		Assert.True(ParseInstruction("CALLF FOO[1]").IsError);
	}
}
