using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Expression;

/// <summary>
/// 表达式解析(ExpressionParser.ReduceExpressionTerm)测试。
/// 常量求值传 null mediator;变量/函数标识符用例依赖 TestBootstrap 的运行时最小初始化。
/// </summary>
public class ExpressionParserTests : IDisposable
{
	private readonly bool _prevUseMacro;

	public ExpressionParserTests()
	{
		TestBootstrap.Initialize();
		_prevUseMacro = LexicalAnalyzer.UseMacro;
		LexicalAnalyzer.UseMacro = false;
	}

	public void Dispose()
	{
		LexicalAnalyzer.UseMacro = _prevUseMacro;
	}

	private static AExpression Reduce(string expr)
		=> ExpressionParser.ReduceExpressionTerm(
			LexicalAnalyzer.Analyse(new CharStream(expr), LexEndWith.EoL, LexAnalyzeFlag.None),
			TermEndWith.EoL)!;

	[Fact]
	public void Constant_Arithmetic_With_Parentheses()
	{
		// 含括号的四则运算:1 + (90 / 9 * 2) = 21
		Assert.Equal(21L, Reduce("1 + (90 / 9 * 2)").GetIntValue(null!));
	}

	[Fact]
	public void Modulo_Operator()
	{
		// 取模运算:7 % 3 = 1
		Assert.Equal(1L, Reduce("7 % 3").GetIntValue(null!));
	}

	[Fact]
	public void Comparison_GreaterThan()
	{
		// 比较运算:60 > 0 = 真(1)
		Assert.Equal(1L, Reduce("60 > 0").GetIntValue(null!));
	}

	[Fact]
	public void Logical_And_Comparison()
	{
		// 逻辑与组合比较:3 >= 1 && 3 <= 5
		Assert.Equal(1L, Reduce("3 >= 1 && 3 <= 5").GetIntValue(null!));
		Assert.Equal(0L, Reduce("3 >= 1 && 3 > 5").GetIntValue(null!));
	}

	#region 需要运行时初始化(IdentifierDictionary/VEvaluator)

	[Fact]
	public void VariableReference_Integer()
	{
		// FLAG:0 默认 0;解析走 IdentifierDictionary,求值走 VEvaluator
		Assert.Equal(0L, Reduce("FLAG:0").GetIntValue(GlobalStatic.EMediator));
	}

	[Fact]
	public void VariableReference_String()
	{
		if (GlobalStatic.VariableData.CharacterList.Count == 0)
			GlobalStatic.VEvaluator.AddPseudoCharacter();
		GlobalStatic.VariableData.GetSystemVariableToken("CALLNAME")
			.SetValue("CALLNAME测试", new long[] { 0 });
		Assert.Equal("CALLNAME测试", Reduce("CALLNAME:MASTER").GetStrValue(GlobalStatic.EMediator));
	}

	[Fact]
	public void MethodCall_InExpression()
	{
		// 函数方法经 IdentifierDictionary.methodDic 解析
		Assert.Equal(1L, Reduce("GETBIT(5,0)").GetIntValue(GlobalStatic.EMediator));
		Assert.Equal(3L, Reduce("MAX(2,3)").GetIntValue(GlobalStatic.EMediator));
		Assert.Equal("123", Reduce("TOSTR(123)").GetStrValue(GlobalStatic.EMediator));
	}

	[Fact]
	public void Ternary_InExpression()
	{
		// eraBasic 表达式三元分隔符为 ? 与 #(: 保留给变量下标)
		Assert.Equal(10L, Reduce("1 > 0 ? 10 # 20").GetIntValue(GlobalStatic.EMediator));
		Assert.Equal(20L, Reduce("0 > 1 ? 10 # 20").GetIntValue(GlobalStatic.EMediator));
		Assert.Equal("甲", Reduce("1 > 0 ? \"甲\" # \"乙\"").GetStrValue(GlobalStatic.EMediator));
	}

	[Fact]
	public void UnknownIdentifier_Throws()
	{
		Assert.Throws<IdentifierNotFoundCodeEE>(() => Reduce("__THIS_IS_NOT_DEFINED__"));
	}

	#endregion

	#region ReduceArguments(isDefine):函数定义的实参表不允许省略

	private static List<AExpression?> ReduceDefineArgs(string text)
		=> ExpressionParser.ReduceArguments(
			LexicalAnalyzer.Analyse(new CharStream(text), LexEndWith.EoL, LexAnalyzeFlag.AllowAssignment),
			ArgsEndWith.EoL,
			true);

	/// <summary>
	/// 实参表首位的省略项必须在解析期报 CodeEE。旧实现的守卫写成 terms.Last(节点)而非其值,
	/// 该判断恒假,于是 null 元素直接落到 terms.Last.Value.IsInteger 上抛 NRE。
	/// </summary>
	[Fact]
	public void DefineArgs_LeadingOmittedArg_ThrowsCodeEE()
	{
		Assert.Throws<CodeEE>(() => ReduceDefineArgs(",1"));
	}

	/// <summary>省略项出现在已有实参之后时同样必须报 CodeEE(不得落到后继的默认值处理上)。</summary>
	[Fact]
	public void DefineArgs_OmittedArgAfterAValue_ThrowsCodeEE()
	{
		Assert.Throws<CodeEE>(() => ReduceDefineArgs("FLAG:0,,1"));
	}

	/// <summary>正控制:=给出默认值的实参表必须照常解析为「实参, 默认值」两项。</summary>
	[Fact]
	public void DefineArgs_ArgWithDefault_ParsesArgAndDefault()
	{
		List<AExpression?> terms = ReduceDefineArgs("FLAG:0=1");

		Assert.Equal(2, terms.Count);
		Assert.IsType<VariableTerm>(terms[0]);
		Assert.Equal(1L, terms[1]!.GetIntValue(GlobalStatic.EMediator));
	}

	#endregion
}
