using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests;

/// <summary>
/// TestBootstrap 最小初始化序列的 sanity 验证。
/// </summary>
public class TestBootstrapTests
{
	public TestBootstrapTests()
	{
		TestBootstrap.Initialize();
	}

	[Fact]
	public void SystemVariableTokens_AreRegistered()
	{
		// static StrForm() 依赖的六个系统变量均已在 VariableData 构造时注册
		foreach (var name in new[] { "TARGET", "MASTER", "PLAYER", "ASSI", "NAME", "CALLNAME" })
		{
			var token = GlobalStatic.VariableData.GetSystemVariableToken(name);
			Assert.NotNull(token);
		}
	}

	[Fact]
	public void GlobalStatics_AreWired()
	{
		Assert.NotNull(GlobalStatic.GameBaseData);
		Assert.NotNull(GlobalStatic.ConstantData);
		Assert.NotNull(GlobalStatic.VariableData);
		Assert.NotNull(GlobalStatic.VEvaluator);
		Assert.NotNull(GlobalStatic.IdentifierDictionary);
		Assert.NotNull(GlobalStatic.EMediator);
		Assert.NotNull(GlobalStatic.Process);
		Assert.NotNull(GlobalStatic.LabelDictionary);
	}

	[Fact]
	public void Expression_Reduce_ConstantEvaluation()
	{
		var wc = LexicalAnalyzer.Analyse(new CharStream("1 + (90 / 9 * 2)"), LexEndWith.EoL, LexAnalyzeFlag.None);
		var term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
		Assert.Equal(21L, term.GetIntValue(null!));
	}

	[Fact]
	public void Expression_Reduce_VariableReference()
	{
		// 变量引用解析走 IdentifierDictionary,求值走 VEvaluator(FLAG:0 默认 0)
		var wc = LexicalAnalyzer.Analyse(new CharStream("FLAG:0"), LexEndWith.EoL, LexAnalyzeFlag.None);
		var term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
		Assert.Equal(0L, term.GetIntValue(GlobalStatic.EMediator));
	}
}
