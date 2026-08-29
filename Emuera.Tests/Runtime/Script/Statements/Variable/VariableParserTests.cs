using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>
/// VariableParser(变量解析)测试,依赖 TestBootstrap 的运行时最小初始化。
/// </summary>
public class VariableParserTests
{
	public VariableParserTests()
	{
		TestBootstrap.Initialize();
	}

	private static VariableToken? GetToken(string name)
		=> GlobalStatic.IdentifierDictionary.GetVariableToken(name, null, false);

	[Fact]
	public void IsVariable_RegisteredAndUnknown()
	{
		Assert.True(VariableParser.IsVariable("FLAG"));
		// 只检查首个标识符,下标部分不参与
		Assert.True(VariableParser.IsVariable("FLAG:0"));
		Assert.False(VariableParser.IsVariable("__NO_SUCH_VAR__"));
		Assert.False(VariableParser.IsVariable(""));
	}

	[Fact]
	public void ZeroTerm_IsZero()
	{
		Assert.Equal(0L, VariableParser.ZeroTerm.GetIntValue(null!));
	}

	[Fact]
	public void TargetTerm_ReflectsSetValue()
	{
		GlobalStatic.VariableData.GetSystemVariableToken("TARGET").SetValue(2, new long[] { 0 });
		Assert.Equal(2L, VariableParser.TARGET.GetIntValue(GlobalStatic.EMediator));
	}

	[Fact]
	public void ReduceVariable_WithIndex()
	{
		var wc = LexicalAnalyzer.Analyse(new CharStream("FLAG:0"), LexEndWith.EoL, LexAnalyzeFlag.None);
		var id = Assert.IsType<IdentifierWord>(wc.Current);
		var token = GetToken(id.Code);
		Assert.NotNull(token);
		wc.ShiftNext();
		var term = VariableParser.ReduceVariable(token!, wc);
		Assert.Equal(0L, term.GetIntValue(GlobalStatic.EMediator));
	}

	[Fact]
	public void ReduceVariable_CharacterStringVar()
	{
		if (GlobalStatic.VariableData.CharacterList.Count == 0)
			GlobalStatic.VEvaluator.AddPseudoCharacter();
		GlobalStatic.VariableData.GetSystemVariableToken("NAME").SetValue("解析テスト", new long[] { 0 });

		var wc = LexicalAnalyzer.Analyse(new CharStream("NAME:0"), LexEndWith.EoL, LexAnalyzeFlag.None);
		wc.ShiftNext();
		var token = GetToken("NAME");
		Assert.NotNull(token);
		var term = VariableParser.ReduceVariable(token!, wc);
		Assert.Equal("解析テスト", term.GetStrValue(GlobalStatic.EMediator));
	}
}
