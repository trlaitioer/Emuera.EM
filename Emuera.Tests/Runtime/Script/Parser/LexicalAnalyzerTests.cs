using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Parser;

/// <summary>
/// 词法分析器(LexicalAnalyzer.Analyse / AnalyseFormattedString)测试。
/// 测试中将 UseMacro 置 false,避免依赖 GlobalStatic.IdentifierDictionary 的宏展开路径。
/// </summary>
public class LexicalAnalyzerTests : IDisposable
{
	private readonly bool _prevUseMacro;

	public LexicalAnalyzerTests()
	{
		_prevUseMacro = LexicalAnalyzer.UseMacro;
		LexicalAnalyzer.UseMacro = false;
	}

	public void Dispose()
	{
		LexicalAnalyzer.UseMacro = _prevUseMacro;
	}

	private static WordCollection Analyse(string text, LexEndWith endWith = LexEndWith.EoL, LexAnalyzeFlag flag = LexAnalyzeFlag.None)
		=> LexicalAnalyzer.Analyse(new CharStream(text), endWith, flag);

	#region 基础

	[Fact]
	public void Identifier_Integer_Operator()
	{
		var wc = Analyse("A + 1");
		Assert.Equal(3, wc.Collection.Count);

		var id = Assert.IsType<IdentifierWord>(wc.Collection.First!.Value);
		Assert.Equal("A", id.Code);

		wc.ShiftNext();
		Assert.IsType<OperatorWord>(wc.Current);

		wc.ShiftNext();
		var num = Assert.IsType<LiteralIntegerWord>(wc.Current);
		Assert.Equal(1L, num.Int);
	}

	[Fact]
	public void Integer_Literal()
	{
		var wc = Analyse("123");
		var num = Assert.IsType<LiteralIntegerWord>(wc.Collection.First!.Value);
		Assert.Equal(123L, num.Int);
	}

	[Fact]
	public void DoubleQuoted_String()
	{
		var wc = Analyse("\"hello\"");
		var s = Assert.IsType<LiteralStringWord>(wc.Collection.First!.Value);
		Assert.Equal("hello", s.Str);
	}

	[Fact]
	public void SingleQuoted_String_With_SingleQuotation_Flag()
	{
		// AllowSingleQuotationStr(HTML_PRINT 系):'...' 为成对引号字符串
		var wc = Analyse("'hi'", LexEndWith.EoL, LexAnalyzeFlag.AllowSingleQuotationStr);
		var s = Assert.IsType<LiteralStringWord>(wc.Collection.First!.Value);
		Assert.Equal("hi", s.Str);
	}

	[Fact]
	public void PrintV_Flag_String_Runs_To_Eol()
	{
		// AnalyzePrintV(PRINTV 系):' 开头字符串直至逗号或行尾,无闭合引号概念,结尾 ' 属于内容
		var wc = Analyse("'hi'", LexEndWith.EoL, LexAnalyzeFlag.AnalyzePrintV);
		var s = Assert.IsType<LiteralStringWord>(wc.Collection.First!.Value);
		Assert.Equal("hi'", s.Str);
	}

	[Fact]
	public void SingleQuoted_String_Without_Flag_Throws()
	{
		Assert.Throws<CodeEE>(() => Analyse("'hi'"));
	}

	[Fact]
	public void Semicolon_Starts_Line_Comment()
	{
		var wc = Analyse("A ; comment");
		Assert.Single(wc.Collection);
		Assert.IsType<IdentifierWord>(wc.Collection.First!.Value);
	}

	[Fact]
	public void Parentheses_Produce_Symbols()
	{
		var wc = Analyse("(A)");
		Assert.Equal(3, wc.Collection.Count);
		Assert.Equal('(', Assert.IsType<SymbolWord>(wc.Collection.First!.Value).Type);
	}

	[Fact]
	public void Percent_EndWith_Stops_At_Percent()
	{
		var cs = new CharStream("A%B");
		var wc = LexicalAnalyzer.Analyse(cs, LexEndWith.Percent, LexAnalyzeFlag.None);
		Assert.Single(wc.Collection);
		Assert.Equal('%', cs.Current);
	}

	[Fact]
	public void Unclosed_DoubleQuote_Throws_CodeEE()
	{
		Assert.Throws<CodeEE>(() => Analyse("\"abc"));
	}

	[Fact]
	public void Unbalanced_Parenthesis_Throws_CodeEE()
	{
		Assert.Throws<CodeEE>(() => Analyse("(A"));
	}

	#endregion

	#region FORM 字符串与三元构造

	[Fact]
	public void Printforml_With_YenAt_Ternary()
	{
		// PRINTFORML 的 FORM 参数由 AnalyseFormattedString 解析(见 Instraction.Child.cs),
		// 而非主 Analyse 解析整行(主 Analyse 遇到裸 { 会报 UnexpectedCharacter)。
		// 参数文本:\@ LOCAL / 60 > 0?{LOCAL / 60}小时# \@{LOCAL % 60}分钟 经过
		var form = "\\@ LOCAL / 60 > 0?{LOCAL / 60}小时# \\@{LOCAL % 60}分钟 经过";
		var sfw = LexicalAnalyzer.AnalyseFormattedString(new CharStream(form), FormStrEndWith.EoL, false);

		// 结构:[YenAt三元] + [CurlyBrace] + "分钟 经过"
		Assert.Equal(3, sfw.Strs.Length);
		Assert.Equal("", sfw.Strs[0]);
		Assert.Equal("", sfw.Strs[1]);
		Assert.Equal("分钟 经过", sfw.Strs[2]);
		Assert.Equal(2, sfw.SubWords.Length);

		var yenat = Assert.IsType<YenAtSubWord>(sfw.SubWords[0]);
		// 条件部:LOCAL / 60 > 0
		Assert.Equal("LOCAL", Assert.IsType<IdentifierWord>(yenat.Words.Collection.First!.Value).Code);
		// 左侧 {LOCAL / 60}小时
		Assert.IsType<CurlyBraceSubWord>(yenat.Left.SubWords[0]);
		Assert.Equal("小时", yenat.Left.Strs[^1]);
		// 右侧为空('#' 与闭合 \@ 之间只有空白)
		Assert.Empty(yenat.Right.SubWords);

		// 三元之后的 {LOCAL % 60}
		Assert.IsType<CurlyBraceSubWord>(sfw.SubWords[1]);
	}

	[Fact]
	public void Percent_Form_With_SystemVariable()
	{
		// %CALLNAME:対象キャラ% 段(% 终止)
		var cs = new CharStream("CALLNAME:対象キャラ%");
		var wc = LexicalAnalyzer.Analyse(cs, LexEndWith.Percent, LexAnalyzeFlag.None);

		Assert.Equal('%', cs.Current);
		Assert.Equal(3, wc.Collection.Count);
		Assert.Equal("CALLNAME", Assert.IsType<IdentifierWord>(wc.Collection.First!.Value).Code);
		Assert.Equal(':', Assert.IsType<SymbolWord>(wc.Collection.First!.Next!.Value).Type);
	}

	[Fact]
	public void FullWidthSpace_Throws_By_Default()
	{
		// PRINTFORM 　\@ GETBIT(危険日感知フラグ, 1) ? 危险期 # 发情期 \@还剩2天结束
		// 词法器对全角空格(U+3000)的处理依赖 Config.SystemAllowFullSpace,默认 false → 拒绝。
		// 注意:未调用 Config.SetConfig 时,拒绝路径里的 GetConfigName 依赖 nameDic(为 null)
		// 会抛 NRE——潜在健壮性问题(真实运行中 SetConfig 总先执行),已记入 .scratch。
		var line = "PRINTFORM 　\\@ GETBIT(危険日感知フラグ, 1) ? 危险期 # 发情期 \\@还剩2天结束";
		Assert.ThrowsAny<Exception>(() => Analyse(line));
	}

	[Fact]
	public void Semicolon_Comment_Line_Produces_No_Tokens()
	{
		var line = "; PRINTFORML 究極絶頂Ｃ\\@ARG == PLAYER?(調教者)#\\@";
		var wc = Analyse(line);
		Assert.Empty(wc.Collection);
	}

	[Fact]
	public void FormString_CurlyBrace_Segment()
	{
		var sfw = LexicalAnalyzer.AnalyseFormattedString(new CharStream("{LOCAL % 60}分钟 经过"), FormStrEndWith.EoL, false);

		Assert.Equal(2, sfw.Strs.Length);
		Assert.Equal("", sfw.Strs[0]);
		Assert.Equal("分钟 经过", sfw.Strs[1]);
		Assert.IsType<CurlyBraceSubWord>(sfw.SubWords[0]);
	}

	[Fact]
	public void FormString_Percent_Segment()
	{
		// 含 % 定界符的段:%CALLNAME:対象キャラ%
		var sfw = LexicalAnalyzer.AnalyseFormattedString(new CharStream("%CALLNAME:対象キャラ%"), FormStrEndWith.EoL, false);

		Assert.Equal(2, sfw.Strs.Length);
		var pw = Assert.IsType<PercentSubWord>(sfw.SubWords[0]);
		Assert.Equal("CALLNAME", Assert.IsType<IdentifierWord>(pw.Words.Collection.First!.Value).Code);
	}

	[Fact]
	public void FormString_YenAt_Segment()
	{
		// \@ GETBIT(危険日感知フラグ, 1) ? 危险期 # 发情期 \@还剩2天结束
		var sfw = LexicalAnalyzer.AnalyseFormattedString(
			new CharStream("\\@ GETBIT(危険日感知フラグ, 1) ? 危险期 # 发情期 \\@还剩2天结束"),
			FormStrEndWith.EoL, false);

		Assert.Equal(2, sfw.Strs.Length);
		Assert.Equal("还剩2天结束", sfw.Strs[1]);
		var yenat = Assert.IsType<YenAtSubWord>(sfw.SubWords[0]);
		Assert.Equal("危险期", yenat.Left.Strs[^1]);
		Assert.Equal("发情期", yenat.Right.Strs[^1]);
	}

	[Fact]
	public void Texr_Nested_FormString_Arguments()
	{
		// %TEXTR("用指腹…", @"\@ TEQUIP:上半身下着あり == -3 ? … # \@", @"\@ … # \@")% 的段内容
		var inner = "TEXTR(\"用指腹狠狠碾碎它/用指腹狠狠掐它/用指尖捏住它像绞紧一样拉扯\", @\"\\@ TEQUIP:上半身下着あり == -3 ? 把贯穿的名牌，使劲拽了下来/ # \\@\", @\"\\@ TEQUIP:上半身下着あり == -2 ? 粗暴地扯下点缀的乳环/ # \\@\")";
		var wc = Analyse(inner, LexEndWith.Percent, LexAnalyzeFlag.None);

		Assert.Equal(8, wc.Collection.Count);
		Assert.Equal("TEXTR", Assert.IsType<IdentifierWord>(wc.Collection.First!.Value).Code);

		var list = wc.Collection.ToList();
		Assert.Equal('(', Assert.IsType<SymbolWord>(list[1]).Type);
		Assert.Equal("用指腹狠狠碾碎它/用指腹狠狠掐它/用指尖捏住它像绞紧一样拉扯", Assert.IsType<LiteralStringWord>(list[2]).Str);
		// 两个 @"\@…\@" 嵌套 FORM 参数
		Assert.IsType<StrFormWord>(list[4]);
		Assert.IsType<StrFormWord>(list[6]);
		Assert.Equal(')', Assert.IsType<SymbolWord>(list[7]).Type);
	}

	#endregion
}
