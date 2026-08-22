using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Expression;

/// <summary>
/// 表达式解析(ExpressionParser.ReduceExpressionTerm)测试。
/// 变量引用替换为常量(变量求值依赖运行时,归集成层)。
/// </summary>
public class ExpressionParserTests : IDisposable
{
	private readonly bool _prevUseMacro;

	public ExpressionParserTests()
	{
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
			TermEndWith.EoL);

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

	// 注:含函数标识符的表达式(如 GETBIT(5,0)、INRANGE(3,1,5)、TOSTR(1+1))的解析
	// 需要 GlobalStatic.IdentifierDictionary(其构造依赖 VariableData = GameBase+ConstantData,
	// 属运行时初始化范畴,与 StrForm 一起推迟)。函数本身的纯计算覆盖见 FunctionMethodTests。
}
