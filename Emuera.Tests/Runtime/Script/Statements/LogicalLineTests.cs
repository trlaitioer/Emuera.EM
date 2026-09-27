using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// LogicalLine.PopArgumentPrimitive 测试。该字段由 InstructionLine 构造器写入一次、恰好被一个消费方
/// 取走(ArgumentParser.SetArgumentTo 每行至多一次,或 LogicalLineParser 的 VARI/VARS 分支),
/// 故「取第二次」只可能是内部不变量被破坏:调用点不再各自写 `!`,由方法本身抛 ExeEE。
/// 夹具要 FunctionIdentifier 的静态构造(读 Config/JSONConfig),故仍需 TestBootstrap。
/// </summary>
public class LogicalLineTests
{
	public LogicalLineTests()
	{
		TestBootstrap.Initialize();
	}

	private static ScriptPosition Pos => new("test.erb", 1);

	private static InstructionLine Line(string primitive)
		=> new(Pos, FunctionIdentifier.SETFunction, new CharStream(primitive));

	[Fact]
	public void PopArgumentPrimitive_TakesTheStreamOnce()
	{
		Assert.Equal("PAYLOAD", Line("PAYLOAD").PopArgumentPrimitive().SubstringROS().ToString());
	}

	[Fact]
	public void PopArgumentPrimitive_SecondCall_ThrowsExeEE()
	{
		InstructionLine line = Line("PAYLOAD");
		line.PopArgumentPrimitive();

		Assert.Throws<ExeEE>(() => line.PopArgumentPrimitive());
	}
}
