using System.Data;
using MinorShift.Emuera;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// DT_COLUMN_OPTIONS 指令的守卫测试(nullable-migration-17)。seam 是解析期入口
/// (LogicalLineParser.ParseLine → ArgumentParser.SetArgumentTo)构造出的 Argument,
/// 加 AInstruction.DoInstruction 的执行期入口;求值上下文用 TestBootstrap 的 GlobalStatic.EMediator。
/// 旧行为:表不存在时 RESULT=-1 后不 return,下一行 dict[key] 抛 KeyNotFoundException;
/// 列不存在时 RESULT=0 后继续,dt.Columns[cName] 为 null 时解引用抛 NRE。
/// </summary>
public class DtColumnOptionsInstructionTests : IDisposable
{
	/// <summary>指令改写全局 RESULT 与 DataDataTables,用例结束必须还原,否则污染同程序集的其他用例。</summary>
	private readonly long previousResult;
	private readonly List<string> addedTables = [];

	public DtColumnOptionsInstructionTests()
	{
		TestBootstrap.Initialize();
		previousResult = GlobalStatic.EMediator.VEvaluator.RESULT;
	}

	public void Dispose()
	{
		GlobalStatic.EMediator.VEvaluator.RESULT = previousResult;
		foreach (string name in addedTables)
		{
			GlobalStatic.VEvaluator.VariableData.DataDataTables.Remove(name);
		}
	}

	private static ScriptPosition Pos => new("test.erb", 1);

	private static InstructionLine ParseInstruction(string text)
		=> Assert.IsType<InstructionLine>(LogicalLineParser.ParseLine(new CharStream(text), Pos, null));

	/// <summary>按装载期的路由解析并构造参数。</summary>
	private static InstructionLine BuildDtColumnOptions(string table, string column)
	{
		InstructionLine line = ParseInstruction($"DT_COLUMN_OPTIONS \"{table}\", \"{column}\", DEFAULT, 1");
		ArgumentParser.SetArgumentTo(line);
		Assert.False(line.IsError, line.ErrMes);
		return line;
	}

	private static void Execute(InstructionLine line)
		=> line.Function.Instruction!.DoInstruction(GlobalStatic.EMediator, line, null!);

	private DataTable AddTable(string name, string column, Type type)
	{
		DataTable dt = new(name);
		dt.Columns.Add(column, type);
		GlobalStatic.VEvaluator.VariableData.DataDataTables[name] = dt;
		addedTables.Add(name);
		return dt;
	}

	/// <summary>正控制:表与列都存在时默认值写入该列,不能被新增的守卫误伤。</summary>
	[Fact]
	public void ExistingTableAndColumn_SetsColumnDefault()
	{
		DataTable dt = AddTable("dt_ok", "col_ok", typeof(long));
		InstructionLine line = BuildDtColumnOptions("dt_ok", "col_ok");

		Execute(line);

		Assert.Equal(1L, dt.Columns["col_ok"]!.DefaultValue);
	}

	/// <summary>表不存在:按 DT 函数族的约定以 RESULT=-1 报告,不得抛 KeyNotFoundException。</summary>
	[Fact]
	public void MissingTable_ReportsMinusOne()
	{
		InstructionLine line = BuildDtColumnOptions("dt_missing", "col_any");

		Execute(line);

		Assert.Equal(-1L, GlobalStatic.EMediator.VEvaluator.RESULT);
	}

	/// <summary>列不存在:与 DT_COLUMN_ADD 等函数一致地抛 CodeEE,不得解引用 null 列。</summary>
	[Fact]
	public void MissingColumn_ThrowsCodeEE()
	{
		AddTable("dt_nocol", "col_present", typeof(long));
		InstructionLine line = BuildDtColumnOptions("dt_nocol", "col_absent");

		Assert.Throws<CodeEE>(() => Execute(line));
	}
}
