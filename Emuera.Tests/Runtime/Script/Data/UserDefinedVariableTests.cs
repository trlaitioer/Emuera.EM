using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Data;

/// <summary>
/// #DIM/#DIMS 声明解析(ERB 私有变量与 ERH 全局变量共用 UserDefinedVariableData.Create)。
/// ERH 入口 ErhLoader.analyzeSharpDimLines 的 catch 只接 CodeEE,元素数折叠期的 NRE
/// 会一路逃逸到 Process.Initialize,故这里直接从 Create 钉住折叠所用上下文;
/// 该 catch 差异本身不在本用例覆盖范围(夹具不经 ErhLoader)。
/// </summary>
public class UserDefinedVariableTests
{
	public UserDefinedVariableTests()
	{
		TestBootstrap.Initialize();
	}

	private static ScriptPosition Pos => new("test.erh", 1);

	/// <summary>解析 "#DIM &lt;声明尾部&gt;" 中变量名之后的部分(等价 ParseSharpLine 交给 Create 的 WordCollection)。</summary>
	private static UserDefinedVariableData Create(string declarationTail)
	{
		var wc = LexicalAnalyzer.Analyse(new CharStream(declarationTail), LexEndWith.EoL, LexAnalyzeFlag.AllowAssignment);
		return UserDefinedVariableData.Create(wc, false, false, Pos);
	}

	/// <summary>
	/// 元素数里的 EXISTCSV 折叠读 exm.VEvaluator:无求值上下文时此处 NRE,
	/// 有上下文时按 csv 数据折出值(夹具为空 csv,EXISTCSV 得 0,MAX 兜到 1)。
	/// </summary>
	[Fact]
	public void Create_ElementCountNeedingMediator_UsesParseContext()
	{
		var data = Create("ARR, MAX(EXISTCSV(4), 1)");
		Assert.Equal([1], data.Lengths);
	}
}
