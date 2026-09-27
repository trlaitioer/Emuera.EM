using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// UserDefinedMethodTerm 测试。seam 取该类本身:TryCreate 是式中 #FUNCTION 方法调用解析的收窄点,
/// 新增的 Create 是它抛异常的那一半——原先唯一调用点 IdentifierDictionary.GetFunctionMethod
/// 自己写 `? term : throw new CodeEE(errMes)`。夹具用零参标签:Arg 置空数组即可,无需构造 VariableToken。
/// </summary>
public class UserDefinedMethodTermTests
{
	public UserDefinedMethodTermTests()
	{
		TestBootstrap.Initialize();
	}

	private static FunctionLabelLine Label(string name)
		=> new(new ScriptPosition("test.erb", 1), name, new WordCollection()) { Arg = [] };

	[Fact]
	public void Create_TooManyArguments_ThrowsCodeEE()
	{
		var e = Assert.Throws<CodeEE>(() => UserDefinedMethodTerm.Create(Label("FOO"), [new SingleLongTerm(1)]));

		Assert.Equal(string.Format(trerror.TooManyFuncArgs.Text, "FOO"), e.Message);
	}

	[Fact]
	public void Create_NoArguments_ReturnsTermWithEmptyArgument()
	{
		UserDefinedMethodTerm term = UserDefinedMethodTerm.Create(Label("FOO"), []);

		Assert.Empty(term.Argument.Arguments);
	}
}
