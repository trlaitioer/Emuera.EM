using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>
/// LOADCHARA / LOADVAR 的 RESULT:名字被拒绝与文件不存在一样,都是失败,都要让脚本看到 0。
/// </summary>
public class VariableEvaluatorLoadTests
{
	[Fact]
	public void LoadChara_RejectedName_ClearsResult()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadchara-rejected-name");

		evaluator.LoadChara(@"..\evil");

		Assert.Equal(0L, evaluator.RESULT);
	}

	[Fact]
	public void LoadVariable_RejectedName_ClearsResult()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadvar-rejected-name");

		evaluator.LoadVariable(@"..\evil");

		Assert.Equal(0L, evaluator.RESULT);
	}

	[Fact]
	public void LoadChara_MissingFile_ClearsResult()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadchara-missing-file");

		evaluator.LoadChara("no-such-chara");

		Assert.Equal(0L, evaluator.RESULT);
	}

	/// <summary>一次成功读取会留下 RESULT=1,下一个用例从这里出发才能看见失败是否归零。</summary>
	private static VariableEvaluator StartWithStaleResult(string rootName)
	{
		GameRootScope.UseTempRoot(rootName);
		TestBootstrap.Initialize();

		VariableEvaluator evaluator = GlobalStatic.VEvaluator;
		evaluator.RESULT = 1;
		return evaluator;
	}
}
