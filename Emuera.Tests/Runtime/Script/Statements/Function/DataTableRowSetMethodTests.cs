using System.Data;
using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// arg-contract/issues/05 阶段 B 切片② 的 pilot 用例:DataTableRowSetMethod(DT_ROW_ADD/DT_ROW_SET)。
/// 该类是规则表里唯一同时踩到「双规则段(定长数组形态 / MatchVariadicGroup 变长对形态)+ 动态基址 b +
/// 三态(显式空槽 = 写 DBNull)+ 裸句柄转发」的类,故用它钉住迁移到 <c>ArgumentSlots</c> 访问器后的执行期语义:
/// 数组形态走 <c>Var</c>,变长对形态走 <c>Str</c>/<c>TermOrNull</c>。<c>DT_ROW_ADD</c> 原先只有检查器用例,
/// 没有执行期覆盖。
/// 缝是 FunctionMethod 的取值入口 + <c>GlobalStatic.EMediator</c>;夹具表与共享数组在本类内建、Dispose 还原。
/// </summary>
public class DataTableRowSetMethodTests : IDisposable
{
	private const string Table = "dt_row_pilot";
	private const string SetTable = "dt_row_pilot_set";

	// 数组形态的 Ref 槽要真的数组,故借共享系统变量并快照还原(见 Emuera.Tests/README.md「共享全局状态」)。
	private readonly string?[] savestr;
	private readonly string?[] savestrBackup;
	private readonly long[] day;
	private readonly long[] dayBackup;

	public DataTableRowSetMethodTests()
	{
		TestBootstrap.Initialize();
		savestr = (string?[])GlobalStatic.VariableData.GetSystemVariableToken("SAVESTR").GetArray();
		savestrBackup = [.. savestr];
		day = (long[])GlobalStatic.VariableData.GetSystemVariableToken("DAY").GetArray();
		dayBackup = [.. day];

		DataTable table = new(Table);
		table.Columns.Add("num", typeof(long));
		table.Columns.Add("text", typeof(string));
		table.Columns.Add("a", typeof(long));
		table.Columns.Add("b", typeof(long));
		GlobalStatic.VEvaluator.VariableData.DataDataTables[Table] = table;

		// DT_ROW_SET 按主键定位行(dt.Rows.Find),故第二张表要有主键与一行数据。
		DataTable set = new(SetTable);
		DataColumn id = set.Columns.Add("num", typeof(long));
		set.Columns.Add("text", typeof(string));
		set.Columns.Add("a", typeof(long));
		set.Columns.Add("b", typeof(long));
		set.PrimaryKey = [id];
		DataRow seed = set.NewRow();
		seed["num"] = 0L;
		seed["text"] = "";
		seed["a"] = 0L;
		seed["b"] = 0L;
		set.Rows.Add(seed);
		GlobalStatic.VEvaluator.VariableData.DataDataTables[SetTable] = set;
	}

	public void Dispose()
	{
		savestrBackup.CopyTo(savestr, 0);
		dayBackup.CopyTo(day, 0);
		GlobalStatic.VEvaluator.VariableData.DataDataTables.Remove(Table);
		GlobalStatic.VEvaluator.VariableData.DataDataTables.Remove(SetTable);
	}

	private static DataTable TableData(string name) => GlobalStatic.VEvaluator.VariableData.DataDataTables[name];

	private static long Int(string name, string inner) => (long)Invoke(name, inner);

	/// <summary>为数组形态准备确定的名/值数组:a = 11、b = 22。</summary>
	private void WriteArrayFixtures()
	{
		Array.Clear(savestr);
		Array.Clear(day);
		savestr[0] = "a";
		savestr[1] = "b";
		day[0] = 11;
		day[1] = 22;
	}

	// ── 变长对形态(MatchVariadicGroup):name/value 交替,值按列类型写入 ────────────────

	/// <summary>两对(name/value 交替)分别写进数值列与字符串列;Add 的返回值是行的 0 号列,
	/// 本例 0 号列就是被写过的 num,故可断言。</summary>
	[Fact]
	public void PairForm_WritesEveryPair()
	{
		long result = Int("DT_ROW_ADD", $"\"{Table}\",\"num\",1,\"text\",\"a\"");

		DataTable table = TableData(Table);
		Assert.Equal(1, table.Rows.Count);
		Assert.Equal(1L, table.Rows[0]["num"]);
		Assert.Equal("a", table.Rows[0]["text"]);
		Assert.Equal(1L, result);
	}

	/// <summary>显式空槽的 value 位写 DBNull(而不是跳过或代入默认值)——这正是该槽不能铺 ArgType.NotNull 的原因:
	/// 它由 <c>TermOrNull</c> 转发,被 SetValue 的 null 分支读成 DBNull。此处空槽落在非 0 号列,
	/// 0 号列由后一对写入,故返回值仍可读(见下一条用例的 0 号列边界)。</summary>
	[Fact]
	public void PairForm_ExplicitEmptyValue_WritesDBNull()
	{
		long result = Int("DT_ROW_ADD", $"\"{Table}\",\"text\",,\"num\",1");

		DataRow row = TableData(Table).Rows[0];
		Assert.Equal(DBNull.Value, row["text"]);
		Assert.Equal(1L, row["num"]);
		Assert.Equal(1L, result);
	}

	/// <summary>
	/// 0 号列被写成 DBNull 时,Add 的返回语句 <c>(long)row[0]</c> 抛 InvalidCastException——
	/// 这是迁移前就有的行为(DT_ROW_ADD 的返回值取自 0 号列),不在本票范围;这里钉住它,
	/// 以免迁移顺手把「显式空槽 = DBNull」这条语义改掉。
	/// </summary>
	[Fact]
	public void PairForm_EmptyValueOnColumnZero_ThrowsInvalidCast()
	{
		Assert.Throws<InvalidCastException>(() => Int("DT_ROW_ADD", $"\"{Table}\",\"num\",,"));
		Assert.Equal(DBNull.Value, TableData(Table).Rows[0]["num"]);
	}

	/// <summary>DT_ROW_SET 的对形态基址 b = 1:槽 1 是按主键定位的行号,名/值对从槽 2 起。</summary>
	[Fact]
	public void RowSetPairForm_UsesBaseOffsetOne()
	{
		long count = Int("DT_ROW_SET", $"\"{SetTable}\",0,\"a\",11");

		Assert.Equal(1L, count);
		Assert.Equal(11L, TableData(SetTable).Rows[0]["a"]);
	}

	// ── 定长数组形态:Ref 槽走 Var,槽 b+3 是写入上限 ──────────────────────────────

	/// <summary>数组形态(4 参,Add):SAVESTR:0 是 string 1D、DAY:0 是 int 1D,两者都走 <c>Var</c> 拿实体再取数组。</summary>
	[Fact]
	public void ArrayForm_WritesNamesAgainstValues()
	{
		WriteArrayFixtures();

		_ = Int("DT_ROW_ADD", $"\"{Table}\",SAVESTR:0,DAY:0,2");

		DataRow row = TableData(Table).Rows[0];
		Assert.Equal(11L, row["a"]);
		Assert.Equal(22L, row["b"]);
	}

	/// <summary>槽 b+3 是写入上限:写 1 时只落第一对,第二列保持未写。</summary>
	[Fact]
	public void ArrayForm_ValueLimit_StopsAtGivenCount()
	{
		WriteArrayFixtures();

		_ = Int("DT_ROW_ADD", $"\"{Table}\",SAVESTR:0,DAY:0,1");

		DataRow row = TableData(Table).Rows[0];
		Assert.Equal(11L, row["a"]);
		Assert.Equal(DBNull.Value, row["b"]);
	}

	/// <summary>DT_ROW_SET 的数组形态是 5 槽(b = 1):槽 1 行号、槽 2/3 是 Ref、槽 4 上限;返回值是写入对数。</summary>
	[Fact]
	public void RowSetArrayForm_UsesBaseOffsetOne()
	{
		Array.Clear(savestr);
		Array.Clear(day);
		savestr[0] = "a";
		day[0] = 11;

		long count = Int("DT_ROW_SET", $"\"{SetTable}\",0,SAVESTR:0,DAY:0,1");

		Assert.Equal(1L, count);
		Assert.Equal(11L, TableData(SetTable).Rows[0]["a"]);
	}
}
