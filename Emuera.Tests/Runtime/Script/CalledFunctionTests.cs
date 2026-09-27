using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script;

/// <summary>
/// CalledFunction 的实参转换测试。seam 取该类本身:TryConvertArg 是运行期调用(CALL/CALLF)与
/// 解析期配对(#FUNCTION 方法)共用的收窄点,新增的 ConvertArg 是它抛异常的那一半——
/// 原先两处调用点各自写 `if (!TryConvertArg(…)) throw new CodeEE(errMes);`。
/// 夹具用零参标签:Arg 置空数组即可,无需构造 VariableToken。
/// </summary>
public class CalledFunctionTests
{
	public CalledFunctionTests()
	{
		TestBootstrap.Initialize();
	}

	private static CalledFunction Call(string name)
		=> CalledFunction.CreateCalledFunctionMethod(Label(name), name);

	private static FunctionLabelLine Label(string name)
		=> new(new ScriptPosition("test.erb", 1), name, new WordCollection()) { Arg = [] };

	[Fact]
	public void ConvertArg_TooManyArguments_ThrowsCodeEE()
	{
		var e = Assert.Throws<CodeEE>(() => Call("FOO").ConvertArg([new SingleLongTerm(1)]));

		Assert.Equal(string.Format(trerror.TooManyFuncArgs.Text, "FOO"), e.Message);
	}

	[Fact]
	public void ConvertArg_NoArguments_ReturnsArgument()
	{
		UserDefinedFunctionArgument argument = Call("FOO").ConvertArg([]);

		Assert.Empty(argument.Arguments);
	}
}
