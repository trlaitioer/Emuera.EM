using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// CallformCache 测试:per-callsite 动态调用名解析缓存的键模式(long/str)与正/负结果语义。
/// FuncnameTerm 走生产链 LexicalAnalyzer.AnalyseFormattedString → ExpressionParser.ToStrFormTerm → Restructure。
/// 变量选用 TFLAG/SAVESTR 等其他测试不触碰的变量,避免并行执行下的共享状态竞争。
/// </summary>
public class CallformCacheTests
{
	public CallformCacheTests()
	{
		TestBootstrap.Initialize();
	}

	private static ExpressionMediator Exm => GlobalStatic.EMediator;

	/// <summary>与 SP_CALLFORM 参数构造同链路:FORM 文本 → FuncnameTerm(含 Restructure 解包)。</summary>
	private static AExpression ParseFuncname(string form)
	{
		var sfw = LexicalAnalyzer.AnalyseFormattedString(new CharStream(form), FormStrEndWith.LeftParenthesis_Bracket_Comma_Semicolon, true);
		return ExpressionParser.ToStrFormTerm(sfw).Restructure(Exm);
	}

	private static void SetTflag(long value)
		=> GlobalStatic.VariableData.GetSystemVariableToken("TFLAG").SetValue(value, new long[] { 0 });

	private static void SetSavestr(string value)
		=> GlobalStatic.VariableData.GetSystemVariableToken("SAVESTR").SetValue(value, new long[] { 0 });

	[Fact]
	public void SingleIntForm_KeyIsEvaluatedTerm_NoNameBuild()
	{
		SetTflag(1);
		var cache = new CallformCache(ParseFuncname("PRE_{TFLAG}"));

		// miss:long 键模式下 strKey 为 null(未拼接任何名字字符串)
		Assert.Null(cache.Lookup(Exm, out long key, out string strKey));
		Assert.Null(strKey);
		Assert.Equal(1L, key);

		var entry = new CallformEntry { LabelName = "PRE_1" };
		cache.Store(key, strKey, entry);
		Assert.Same(entry, cache.Lookup(Exm, out _, out _));

		// 键随变量值变化:证明键是求值后的整数,不是名字字符串
		SetTflag(2);
		Assert.Null(cache.Lookup(Exm, out _, out _));
	}

	[Fact]
	public void BareSingleInterpolation_UsesLongKey()
	{
		// "{TFLAG}" 经 Restructure 解包为 FunctionMethodTerm(CurlyBrace)(生产形态),
		// 无字面前缀的单插值名(语料中 {NO:ARG} 类高频形态)同样命中零拼接键
		SetTflag(7);
		var cache = new CallformCache(ParseFuncname("{TFLAG}"));
		Assert.Null(cache.Lookup(Exm, out long key, out string strKey));
		Assert.Null(strKey);
		Assert.Equal(7L, key);

		var entry = new CallformEntry { LabelName = "7" };
		cache.Store(key, strKey, entry);
		Assert.Same(entry, cache.Lookup(Exm, out _, out _));

		SetTflag(8);
		Assert.Null(cache.Lookup(Exm, out _, out _));
	}

	[Fact]
	public void NegativeResult_Roundtrips()
	{
		SetTflag(9);
		var cache = new CallformCache(ParseFuncname("TRY_{TFLAG}"));
		Assert.Null(cache.Lookup(Exm, out long key, out _));

		// 负结果(Call 为 null)原样命中,调用方按未找到处理且不再重复查表
		var negative = new CallformEntry { LabelName = "TRY_9", Call = null };
		cache.Store(key, null, negative);

		var hit = cache.Lookup(Exm, out _, out _);
		Assert.Same(negative, hit);
		Assert.Null(hit.Call);
		Assert.Equal("TRY_9", hit.LabelName);
	}

	[Fact]
	public void MultiInterpolationForm_UsesStrKey()
	{
		SetTflag(0);
		var cache = new CallformCache(ParseFuncname("A_{TFLAG}_{RESULT}"));

		// str 键模式:lookup 即拼出名字,按名字内容缓存
		Assert.Null(cache.Lookup(Exm, out long longKey, out string strKey));
		Assert.Equal(0L, longKey);
		Assert.Equal("A_0_0", strKey);

		var entry = new CallformEntry { LabelName = strKey };
		cache.Store(longKey, strKey, entry);
		Assert.Same(entry, cache.Lookup(Exm, out _, out _));

		SetTflag(3);
		Assert.Null(cache.Lookup(Exm, out _, out _));
	}

	[Fact]
	public void StrKey_UsesStrComper()
	{
		Assert.Equal(StringComparer.OrdinalIgnoreCase, Config.StrComper);

		SetSavestr("abc");
		var cache = new CallformCache(ParseFuncname("%SAVESTR%"));
		Assert.Null(cache.Lookup(Exm, out long longKey, out string strKey));
		Assert.Equal("abc", strKey);
		var entry = new CallformEntry { LabelName = strKey };
		cache.Store(longKey, strKey, entry);

		// 内容同、大小写不同时命中,与 LabelDictionary 的查表语义一致
		SetSavestr("ABC");
		Assert.Same(entry, cache.Lookup(Exm, out _, out _));

		SetSavestr("XYZ");
		Assert.Null(cache.Lookup(Exm, out _, out _));
	}
}
