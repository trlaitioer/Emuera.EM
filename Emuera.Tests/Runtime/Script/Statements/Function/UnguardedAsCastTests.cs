using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Loader;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System.Text;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// 无保护的 as 转换(nullable-migration-18)回归网。缝是函数方法的公开入口:
/// FunctionMethod.GetIntValue(运行期)与 IdentifierDictionary.GetFunctionMethod(解析期契约);
/// 常量数组那一类走 ErbLoader 装载真实脚本 + InstructionLine.DoInstruction。
/// 旧行为:转换失败时 as 返回 null,! 只压住编译期警告,运行期在此解引用
/// (NullReferenceException;MAXARRAY 因用直接转换而抛 InvalidCastException)。
/// </summary>
public class UnguardedAsCastTests : IDisposable
{
	/// <summary>XML_GET 走 RESULTS 书式时会改写全局 RESULTS,用例结束必须还原。</summary>
	private readonly string?[] previousResults;

	/// <summary>InRangeArray/MaxArray 用例按脚本语义写全局 RESULT,同样必须还原。</summary>
	private readonly long previousResult;

	public UnguardedAsCastTests()
	{
		TestBootstrap.Initialize();
		previousResults = [.. GlobalStatic.VEvaluator.RESULTS_ARRAY];
		previousResult = GlobalStatic.VEvaluator.RESULT;
	}

	public void Dispose()
	{
		previousResults.CopyTo(GlobalStatic.VEvaluator.RESULTS_ARRAY, 0);
		GlobalStatic.VEvaluator.RESULT = previousResult;
	}

	private static FunctionMethod Method(string name) => FunctionMethodCreator.GetMethodList()[name];

	/// <summary>XML doc 固定为两个 file 节点,用于同时覆盖两种书式。</summary>
	private const string Xml = "<test><file name=\"a\"/><file name=\"b\"/></test>";

	/// <summary>
	/// 书式1:doOutput 为 0 时只返回合致结果数(文档:「0 または省略した場合、合致結果数のみ返し」)。
	/// 旧实现把「整型」与「变量」写进同一个 if 的 &amp;&amp;,0 会掉进变量分支解引用 null。
	/// 首实参传字符串,该路径不经 DataXmlDocument,故不需要 exm。
	/// </summary>
	[Fact]
	public void XmlGet_WithZeroDoOutput_ReturnsCount()
	{
		long count = (long)InvokeWith(Method("XML_GET"),
		[
			new SingleStrTerm(Xml),
			new SingleStrTerm("/test/file"),
			new SingleLongTerm(0)
		], null);

		Assert.Equal(2L, count);
	}

	/// <summary>正控制:非 0 的 doOutput 仍写入 RESULTS(修复不得误伤这条书式)。</summary>
	[Fact]
	public void XmlGet_WithNonZeroDoOutput_StillWritesResults()
	{
		long count = (long)Invoke(Method("XML_GET"),
		[
			new SingleStrTerm(Xml),
			new SingleStrTerm("/test/file/@name"),
			new SingleLongTerm(1)
		]);

		Assert.Equal(2L, count);
		Assert.Equal("a", GlobalStatic.VEvaluator.RESULTS_ARRAY[0]);
		Assert.Equal("b", GlobalStatic.VEvaluator.RESULTS_ARRAY[1]);
	}

	/// <summary>正控制:第三实参为字符串一维数组变量时仍写入该数组(规则 2 的书式)。</summary>
	[Fact]
	public void XmlGet_WithOutputArrayVariable_StillWritesThatArray()
	{
		var outputArray = GlobalStatic.VariableData.GetSystemVariableToken("SAVESTR");
		string?[] previous = [.. (string?[])outputArray.GetArray()];
		try
		{
			long count = (long)Invoke(Method("XML_GET"),
			[
				new SingleStrTerm(Xml),
				new SingleStrTerm("/test/file/@name"),
				new VariableTerm(outputArray, [new SingleLongTerm(0)])
			]);

			Assert.Equal(2L, count);
			Assert.Equal("a", ((string?[])outputArray.GetArray())[0]);
			Assert.Equal("b", ((string?[])outputArray.GetArray())[1]);
		}
		finally
		{
			previous.CopyTo((string?[])outputArray.GetArray(), 0);
		}
	}

	/// <summary>
	/// CMATCH 的第 1 实参必须是角色数组变量(文档:「charaArray でなければなりません」)。
	/// 旧规则首项是 ArgType.Any,字面量 0 能通过解析,运行期在 (arguments[0] as VariableTerm)! 处解引用 null。
	/// 缝取解析期契约入口:IdentifierDictionary.GetFunctionMethod 校验不过即抛 CodeEE。
	/// </summary>
	[Fact]
	public void CMatch_WithNonVariableFirstArgument_IsRejectedAtParseTime()
	{
		Assert.Throws<CodeEE>(() => GlobalStatic.IdentifierDictionary.GetFunctionMethod(null,
			"CMATCH", [new SingleLongTerm(0), new SingleLongTerm(1)], false));
	}

	/// <summary>正控制:角色数组变量作第 1 实参仍通过解析(收紧不得误伤合法写法)。</summary>
	[Fact]
	public void CMatch_WithCharacterArrayVariable_StillParses()
	{
		AExpression charaArray = ExpressionParser.ReduceExpressionTerm(
			LexicalAnalyzer.Analyse(new CharStream("CFLAG:2"), LexEndWith.EoL, LexAnalyzeFlag.None),
			TermEndWith.EoL)!;

		AExpression? term = GlobalStatic.IdentifierDictionary.GetFunctionMethod(null, "CMATCH",
			[charaArray, new SingleLongTerm(1)], false);

		Assert.NotNull(term);
	}

	/// <summary>常量一维数组的初值,以及由它独立推出的期望值见各用例。</summary>
	private const string ConstArray = "#DIM CONST CONSTARR = 5, 1, 9";

	/// <summary>
	/// 常量一维数组作第 1 实参:解析期规则含 AllowConstRef,而 InRangeArrayMethod 未设
	/// HasUniqueRestructure,FunctionMethodTerm.Restructure 走非 unique 分支就地改写入参列表,
	/// 变量被折叠成 SingleTerm,运行期 (arguments[0] as VariableTerm)! 得 null。
	/// 缝是真实装载路径(临时游戏根 + #DIM CONST)+ InstructionLine.DoInstruction:
	/// 0 &lt;= v &lt; 9 的元素是 5 和 1。
	/// </summary>
	[Fact]
	public async Task InRangeArray_WithConstantArray_CountsElementsInRange()
	{
		InstructionLine line = await LoadAssignment("RESULT = INRANGEARRAY(CONSTARR, 0, 9)");

		line.Function.Instruction!.DoInstruction(GlobalStatic.EMediator, line, null!);

		Assert.Equal(2L, GlobalStatic.VEvaluator.RESULT);
	}

	/// <summary>
	/// 同源:MAXARRAY/MINARRAY 的非角色规则同样含 AllowConstRef 且未设 HasUniqueRestructure,
	/// 常量数组被折叠后取变量处是直接转换 (VariableTerm),故抛 InvalidCastException 而非空引用。
	/// CONSTARR 全体的最大值是 9。
	/// </summary>
	[Fact]
	public async Task MaxArray_WithConstantArray_ReturnsMaximum()
	{
		InstructionLine line = await LoadAssignment("RESULT = MAXARRAY(CONSTARR, 0, 3)");

		line.Function.Instruction!.DoInstruction(GlobalStatic.EMediator, line, null!);

		Assert.Equal(9L, GlobalStatic.VEvaluator.RESULT);
	}

	/// <summary>装载「#DIM CONST 数组 + 一条赋值文」的脚本,返回该赋值文的解析产物。</summary>
	private static async Task<InstructionLine> LoadAssignment(string assignment)
	{
		string root = GameRootScope.UseTempRoot("issues18");
		string erbDir = Path.Combine(root, "erb");
		Directory.CreateDirectory(erbDir);
		string path = Path.Combine(erbDir, "test.erb");
		File.WriteAllLines(path, ["@SYSTEM_TITLE", ConstArray, assignment], new UTF8Encoding(false));
		// Preload 是装载期的缓存入口,未预读的文件在 OpenOnCache 里直接 KeyNotFound
		await Preload.Load(path);

		// scaningLine 的占位值由 TestBootstrap 设置,装载结束时会被清掉;用例必须还原
		LogicalLine? placeholder = GlobalStatic.Process.scaningLine;
		GlobalStatic.LabelDictionary.RemoveAll();
		InstructionLine assignmentLine;
		try
		{
			var loader = new ErbLoader(null!, GlobalStatic.EMediator, GlobalStatic.Process);
			await loader.LoadErbList([path], GlobalStatic.LabelDictionary);

			FunctionLabelLine label = Assert.Single(GlobalStatic.LabelDictionary.GetAllLabels(false));
			List<InstructionLine> body = [];
			for (LogicalLine? line = label.NextLine; line is InstructionLine instruction; line = line.NextLine)
			{
				body.Add(instruction);
			}

			assignmentLine = Assert.Single(body);
			// 实参的构造(含实参的 Restructure)发生在 SetArgumentTo;私有变量经
			// Process.GetScaningLine().ParentLabelLine 解析,故须让 scaningLine 指向该行
			GlobalStatic.Process.scaningLine = assignmentLine;
			ArgumentParser.SetArgumentTo(assignmentLine);
		}
		finally
		{
			GlobalStatic.Process.scaningLine = placeholder;
		}

		Assert.False(assignmentLine.IsError, assignmentLine.ErrMes);
		Assert.IsType<SpSetArgument>(assignmentLine.Argument);
		return assignmentLine;
	}
}
