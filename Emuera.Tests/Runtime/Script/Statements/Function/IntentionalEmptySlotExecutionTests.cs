using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// arg-contract/issues/03 片 5:B 表「有意可空」槽的**执行期**语义回归网。
/// 片 4 只钉住检查器放行显式空槽(解析期,见 <see cref="CheckArgumentTypeEx.IntentionalEmptySlot_IsAccepted"/>),
/// 本文件钉住函数体把该 null 读成规则段注释声明的默认值——空槽形态与「显式写默认值」或「数量省略」
/// 两种写法必须给出同一结果,另用 DAY/SAVESTR 的确定值断言默认值本身(start = 0、end = 数组长度、
/// 分隔符 = ","、position = 0 等)。
/// 缝是 FunctionMethod 的公开取值入口 + GlobalStatic.EMediator(真实求值上下文)。
/// 未纳入的槽(图形/角色/CSV/文件系统依赖)与原因记在票内片 5,不在此处留白。
/// </summary>
public class IntentionalEmptySlotExecutionTests : IDisposable
{
	// 共享全局状态(见 Emuera.Tests/README.md「共享全局状态」):DAY/SAVESTR 写入前快照,Dispose 还原。
	private readonly long[] day;
	private readonly long[] dayBackup;
	private readonly string?[] savestr;
	private readonly string?[] savestrBackup;

	public IntentionalEmptySlotExecutionTests()
	{
		TestBootstrap.Initialize();
		day = (long[])GlobalStatic.VariableData.GetSystemVariableToken("DAY").GetArray();
		dayBackup = [.. day];
		savestr = (string?[])GlobalStatic.VariableData.GetSystemVariableToken("SAVESTR").GetArray();
		savestrBackup = [.. savestr];
		WriteFixtures();
	}

	public void Dispose()
	{
		dayBackup.CopyTo(day, 0);
		savestrBackup.CopyTo(savestr, 0);
	}

	/// <summary>
	/// 取值依赖数组内容,故写入确定值:DAY = [9, 5, 3, 5, 0, …]、SAVESTR = ["a", "b", "c", "d", "", …]。
	/// 5 出现两次(索引 1 与 3),使 start/end 的默认值可用绝对值区分;9 只在索引 0,用于区分 start 是否为 0。
	/// </summary>
	private void WriteFixtures()
	{
		Array.Clear(day);
		Array.Clear(savestr);
		day[0] = 9;
		day[1] = 5;
		day[2] = 3;
		day[3] = 5;
		savestr[0] = "a";
		savestr[1] = "b";
		savestr[2] = "c";
		savestr[3] = "d";
	}

	// ── 空槽 ≡ 默认值/数量省略:两种写法不可区分(逐槽) ──────────────────────────────

	/// <summary>
	/// 左列含显式空槽(null 元素),右列是同一语义的对照写法(显式写默认值,或按数量省略该槽)。
	/// 两者结果必须相同——这就是 B 表「空槽与不传该实参同义」的可执行形式。
	/// </summary>
	[Theory]
	[InlineData("SUBSTRING", "\"abc\",,2", "\"abc\",0,2")] // [1] start = 0
	[InlineData("SUBSTRING", "\"abc\",1,,", "\"abc\",1")] // [2] length = -1
	[InlineData("SUBSTRINGU", "\"abc\",,2", "\"abc\",0,2")]
	[InlineData("SUBSTRINGU", "\"abc\",1,,", "\"abc\",1")]
	[InlineData("STRFIND", "\"abc\",\"b\",,", "\"abc\",\"b\"")] // [2] start = 0
	[InlineData("TOSTR", "123,,", "123")] // [1] 默认整数格式
	[InlineData("MONEYSTR", "1,,", "1")] // [1] Config 的默认货币格式
	[InlineData("VARSIZE", "\"DAY\",,", "\"DAY\"")] // [1] dim = 0
	[InlineData("EXISTCSV", "0,,", "0")] // [1] isSp = false
	[InlineData("ENCODETOUNI", "\"a\",,", "\"a\"")] // [1] position = 0
	[InlineData("MAXARRAY", "DAY:0,,2", "DAY:0,0,2")] // [1] start = 0
	[InlineData("MAXARRAY", "DAY:0,2,,", "DAY:0,2")] // [2] end = 数组长度
	[InlineData("SUMARRAY", "DAY:0,,3", "DAY:0,0,3")] // [1] index1 = 0
	[InlineData("SUMARRAY", "DAY:0,1,,", "DAY:0,1")] // [2] index2 = 数组长度
	[InlineData("MATCH", "DAY:0,5,,10", "DAY:0,5,0,10")] // [2] start = 0
	[InlineData("MATCH", "DAY:0,5,0,,", "DAY:0,5,0")] // [3] end = 数组长度(注意 `x,,` 只多出一个 null 且落在末位,要让槽 3 收到 null 得先把槽 2 写满)
	[InlineData("FINDELEMENT", "DAY:0,5,,,", "DAY:0,5")] // [2]/[3] start/end(Count = 4)
	[InlineData("FINDELEMENT", "DAY:0,5,0,0,,", "DAY:0,5,0,0,0")] // [4] isExact = false(该槽只在 Count > 4 时被读取)
	[InlineData("INRANGEARRAY", "DAY:0,3,10,,3", "DAY:0,3,10,0,3")] // [3] start = 0
	[InlineData("INRANGEARRAY", "DAY:0,3,10,0,,", "DAY:0,3,10,0")] // [4] end = 数组长度(同上,尾逗号写法只到得了槽 3)
	[InlineData("STRJOIN", "SAVESTR:0,,0,3", "SAVESTR:0,\",\",0,3")] // [1] 分隔符 = ","
	[InlineData("STRJOIN", "SAVESTR:0,\"-\",,1", "SAVESTR:0,\"-\",0,1")] // [2] index1 = 0
	[InlineData("STRJOIN", "SAVESTR:0,\"-\",1,,", "SAVESTR:0,\"-\",1")] // [3] index2 = 余长
	[InlineData("GETCHARA", "0,,", "0")] // GetcharaMethod[1] CheckSp = false
	public void EmptySlot_AndItsReferenceForm_AreIndistinguishable(string name, string withEmptySlot, string reference)
		=> Assert.Equal(Invoke(name, reference), Invoke(name, withEmptySlot));

	// ── 空槽的默认值本身(取值依赖数组内容,故用绝对值钉住) ────────────────────────

	/// <summary>槽 1 与 2 都留空:start = 0、length = -1,故返回整串。</summary>
	[Fact]
	public void Substring_WithBothSlotsEmpty_ReturnsWholeString()
		=> Assert.Equal("abc", Str("SUBSTRING", "\"abc\",,,"));

	[Fact]
	public void Substringu_WithBothSlotsEmpty_ReturnsWholeString()
		=> Assert.Equal("abc", Str("SUBSTRINGU", "\"abc\",,,"));

	/// <summary>start 默认 0:用只出现在索引 0 的 "a" 才有区分力(默认成 1 时 <c>IndexOf</c> 落空、返回 -1)。</summary>
	[Fact]
	public void Strfind_WithEmptyStart_FindsFromTheBeginning()
		=> Assert.Equal(0L, Int("STRFIND", "\"abc\",\"a\",,"));

	[Fact]
	public void Tostr_WithEmptyFormat_UsesDefaultIntegerFormat()
		=> Assert.Equal("123", Str("TOSTR", "123,,"));

	[Fact]
	public void EncodeToUni_WithEmptyPosition_StartsAtZero()
		=> Assert.Equal(97L, Int("ENCODETOUNI", "\"a\",,"));

	/// <summary>start 默认 0:若默认成 1,索引 0 的 9 就被排除,结果会是 5。</summary>
	[Fact]
	public void MaxArray_WithEmptyStart_StartsAtZero()
		=> Assert.Equal(9L, Int("MAXARRAY", "DAY:0,,2"));

	/// <summary>end 默认数组长度:若默认成 0,范围 [2, 0) 为空,取不到索引 3 的 5。</summary>
	[Fact]
	public void MaxArray_WithEmptyEnd_CoversUpToArrayLength()
		=> Assert.Equal(5L, Int("MAXARRAY", "DAY:0,2,,"));

	[Fact]
	public void SumArray_WithEmptyBounds_SumsWholeArray()
		=> Assert.Equal(22L, Int("SUMARRAY", "DAY:0,,,"));

	[Fact]
	public void SumArray_WithEmptyStart_StartsAtZero()
		=> Assert.Equal(17L, Int("SUMARRAY", "DAY:0,,3"));

	/// <summary>MATCH 返回范围内等值元素的**个数**:默认 start = 0 才能数到两次 5。</summary>
	[Fact]
	public void Match_WithEmptyStart_CountsFromZero()
		=> Assert.Equal(2L, Int("MATCH", "DAY:0,5,,10"));

	/// <summary>默认 end = 数组长度:范围 [2, 长度) 覆盖索引 3 的 5,故为 1 而非 0。</summary>
	[Fact]
	public void Match_WithEmptyEnd_CountsUpToArrayLength()
		=> Assert.Equal(1L, Int("MATCH", "DAY:0,5,2,,"));

	/// <summary>
	/// 槽 3 写 0、槽 4 显式留空:判据是 min &lt;= v &lt; max(max 不含),故取 [3, 10) 命中 9/5/3/5 四个;
	/// 默认 end = 数组长度才能数到索引 3 的 5(若 end 默认成 2 则只数到索引 1,得 2)。
	/// </summary>
	[Fact]
	public void InRangeArray_WithEmptyBounds_CountsWholeArray()
		=> Assert.Equal(4L, Int("INRANGEARRAY", "DAY:0,3,10,0,,"));

	/// <summary>start 默认 0:范围 [0, 3) 内命中 9/5/3;若默认成 1,索引 0 的 9 被排除,得 2。</summary>
	[Fact]
	public void InRangeArray_WithEmptyStart_StartsAtZero()
		=> Assert.Equal(3L, Int("INRANGEARRAY", "DAY:0,3,10,,3"));

	/// <summary>start 默认 0:9 只在索引 0,默认成 1 就找不到(返回 -1)。</summary>
	[Fact]
	public void FindElement_WithEmptyStart_SearchesFromZero()
		=> Assert.Equal(0L, Int("FINDELEMENT", "DAY:0,9,,"));

	/// <summary>end 默认数组长度:范围 [2, 长度) 才够得到索引 3 的 5;默认成 2 则范围为空、找不到。</summary>
	[Fact]
	public void FindElement_WithEmptyEnd_SearchesToArrayLength()
		=> Assert.Equal(3L, Int("FINDELEMENT", "DAY:0,5,2,,"));

	/// <summary>分隔符默认 ",":索引 2 是 index2 = 3(不是 index1),故取前三个元素。</summary>
	[Fact]
	public void Strjoin_WithEmptyDelimiter_UsesComma()
		=> Assert.Equal("a,b,c", Str("STRJOIN", "SAVESTR:0,,0,3"));

	[Fact]
	public void Strjoin_WithEmptyIndex1_StartsAtZero()
		=> Assert.Equal("a", Str("STRJOIN", "SAVESTR:0,\"-\",,1"));

	/// <summary>
	/// 槽 3 显式留空:index2 默认 = 余长(<c>GetLastLength() - index1</c>),故必须等于显式写该长度的结果——
	/// 只与「不传该实参」比等价是钉不住默认值的。
	/// </summary>
	[Fact]
	public void Strjoin_WithEmptyIndex2_JoinsToTheArrayEnd()
	{
		int index2 = savestr.Length - 1; // 槽 2 写 1 ⇒ 余长 = 长度 - 1
		Assert.Equal(Str("STRJOIN", $"SAVESTR:0,\",\",1,{index2}"), Str("STRJOIN", "SAVESTR:0,\",\",1,,"));
	}

	// ── 「无兜底值」的槽:空槽与数量省略都抛同一条 CodeEE,不代入默认值 ──────────────

	/// <summary>
	/// GETMETH/GETMETHS 的槽 1 是「目标未定义时的兜底值」,B 表记的就是「无兜底:目标未定义时抛 CodeEE」。
	/// 故空槽与不传该实参都必须抛,且文案相同——若哪天改成代入 0/"" 会被这里拦住。
	/// </summary>
	[Theory]
	[InlineData("GETMETH", "\"NOPE\",,")]
	[InlineData("GETMETH", "\"NOPE\",")]
	[InlineData("GETMETHS", "\"NOPE\",,")]
	[InlineData("GETMETHS", "\"NOPE\",")]
	public void GetMeth_WithEmptyFallback_IsRejectedInsteadOfDefaulted(string name, string inner)
	{
		FunctionMethod m = Method(name);
		List<AExpression?> args = Args(inner);
		CodeEE ex = Assert.Throws<CodeEE>(() => Invoke(m, args));
		Assert.Contains(string.Format(trerror.NotDefinedUserFunc.Text, "NOPE"), ex.Message);
	}

	// ── 夹具 ────────────────────────────────────────────────────────────────

	private static long Int(string name, string inner) => (long)Invoke(name, inner);

	private static string Str(string name, string inner) => (string)Invoke(name, inner);
}
