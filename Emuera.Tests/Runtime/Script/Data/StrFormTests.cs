using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Data;

/// <summary>
/// StrForm 测试:走生产链 LexicalAnalyzer.AnalyseFormattedString → StrFormWord → FromWordToken → 求值。
/// 依赖 TestBootstrap 的运行时最小初始化(static StrForm() 的三连符号绑定、系统变量、LangManager)。
/// </summary>
public class StrFormTests
{
	public StrFormTests()
	{
		TestBootstrap.Initialize();
	}

	private static bool charaReady;

	/// <summary>
	/// 两个伪角色:0 号 NAME=アリス/CALLNAME=爱丽丝,1 号 NAME=ボブ/CALLNAME=鲍勃;
	/// TARGET=1、MASTER=0、PLAYER=1、ASSI=0。
	/// </summary>
	private static void SetupCharacters()
	{
		if (charaReady)
			return;
		var ve = GlobalStatic.VEvaluator;
		ve.AddPseudoCharacter();
		ve.AddPseudoCharacter();
		var varData = GlobalStatic.VariableData;
		varData.GetSystemVariableToken("NAME").SetValue("アリス", new long[] { 0 });
		varData.GetSystemVariableToken("CALLNAME").SetValue("爱丽丝", new long[] { 0 });
		varData.GetSystemVariableToken("NAME").SetValue("ボブ", new long[] { 1 });
		varData.GetSystemVariableToken("CALLNAME").SetValue("鲍勃", new long[] { 1 });
		varData.GetSystemVariableToken("TARGET").SetValue(1, new long[] { 0 });
		varData.GetSystemVariableToken("MASTER").SetValue(0, new long[] { 0 });
		varData.GetSystemVariableToken("PLAYER").SetValue(1, new long[] { 0 });
		varData.GetSystemVariableToken("ASSI").SetValue(0, new long[] { 0 });
		charaReady = true;
	}

	private static void SetStr(string name, string value)
		=> GlobalStatic.VariableData.GetSystemVariableToken(name).SetValue(value, new long[] { 0 });

	/// <summary>FORM 文本 → StrForm(与 PRINT 系 FORM 参数同链路)。</summary>
	private static StrForm ParseForm(string form)
	{
		var sfw = LexicalAnalyzer.AnalyseFormattedString(new CharStream(form), FormStrEndWith.EoL, false);
		return StrForm.FromWordToken(sfw);
	}

	private static string Eval(string form)
		=> ParseForm(form).GetString(GlobalStatic.EMediator);

	[Fact]
	public void LiteralOnly_IsConst()
	{
		var form = ParseForm("这是纯文本");
		Assert.True(form.IsConst);
		Assert.Equal("这是纯文本", form.GetString(GlobalStatic.EMediator));
	}

	[Fact]
	public void CurlyBrace_IntegerExpression()
	{
		Assert.Equal("数值=2", Eval("数值={1+1}"));
	}

	[Fact]
	public void Percent_StringVariable()
	{
		SetStr("STR", "测试");
		Assert.Equal("值=测试", Eval("值=%STR:0%"));
	}

	[Fact]
	public void Mixed_LiteralAndTerms()
	{
		Assert.Equal("甲1乙", Eval("甲{1}乙"));
	}

	[Fact]
	public void CurlyBrace_Alignment()
	{
		// 无第三参 = RIGHT(PadLeft);LEFT = PadRight
		Assert.Equal("    1END", Eval("{1,5}END"));
		Assert.Equal("1    END", Eval("{1,5,LEFT}END"));
	}

	[Fact]
	public void Percent_Alignment_UsesLangWidth()
	{
		// 全角按语言字节宽计算:"テスト" 占 6 字节(CP932 每假名 2 字节),
		// 总宽 10 → 10-(6-3)=7 → 补 4 个空格;汉字"测试"不在 CP932 内,宽度按 1 计,勿用
		SetStr("STR", "テスト");
		Assert.Equal("テスト    ", Eval("%STR:0,10,LEFT%"));
	}

	[Fact]
	public void Ternary_YenAt()
	{
		Assert.Equal("甲END", Eval("\\@ 1 ? 甲 # 乙 \\@END"));
		Assert.Equal("乙END", Eval("\\@ 0 ? 甲 # 乙 \\@END"));
	}

	[Fact]
	public void Ternary_BranchIsNestedForm()
	{
		Assert.Equal("10END", Eval("\\@ 1 ? {5+5} # 乙 \\@END"));
	}

	[Fact]
	public void TripleSymbols_BoundByInitialize()
	{
		// *** = NAME:TARGET、+++ = CALLNAME:MASTER、=== = CALLNAME:PLAYER、
		// /// = NAME:ASSI、$$$ = CALLNAME:TARGET(static StrForm() 绑定)
		SetupCharacters();
		Assert.Equal("ボブ", Eval("***"));
		Assert.Equal("爱丽丝", Eval("+++"));
		Assert.Equal("鲍勃", Eval("==="));
		Assert.Equal("アリス", Eval("///"));
		Assert.Equal("鲍勃", Eval("$$$"));
	}

	[Fact]
	public void GetAExpression_SingleTerm()
	{
		// {5} 是字符串格式化项(formatCurlyBrace),GetAExpression 原样返回该项
		var form = ParseForm("{5}");
		var term = form.GetAExpression();
		Assert.NotNull(term);
		Assert.Equal("5", term!.GetStrValue(GlobalStatic.EMediator));
		// 非单一项的 FORM 返回 null
		Assert.Null(ParseForm("a{5}").GetAExpression());
	}

	[Fact]
	public void Restructure_FoldsConstantTerms()
	{
		var form = ParseForm("A{2+3}B");
		form.Restructure(GlobalStatic.EMediator);
		Assert.True(form.IsConst);
		Assert.Equal("A5B", form.GetString(GlobalStatic.EMediator));
	}

	[Fact]
	public void Error_EmptyCurlyBrace()
	{
		Assert.Throws<CodeEE>(() => Eval("{}"));
	}

	[Fact]
	public void Error_EmptyPercent()
	{
		Assert.Throws<CodeEE>(() => Eval("%%"));
	}

	[Fact]
	public void Error_AlignmentThirdArgMustBeLeftOrRight()
	{
		Assert.Throws<CodeEE>(() => Eval("{1,2,MIDDLE}"));
	}
}
