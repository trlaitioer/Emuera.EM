using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script;

/// <summary>
/// CalledFunction 的实参转换测试。seam 取该类本身:TryConvertArg 是运行期调用(CALL/CALLF)与
/// 解析期配对(#FUNCTION 方法)共用的收窄点,新增的 ConvertArg 是它抛异常的那一半——
/// 原先两处调用点各自写 `if (!TryConvertArg(…)) throw new CodeEE(errMes);`。
/// 夹具以零参标签(Arg 置空数组)服务既有用例;需要形参的用例由 FormalArg 走解析路径构造 VariableTerm。
/// </summary>
public class CalledFunctionTests
{
	public CalledFunctionTests()
	{
		TestBootstrap.Initialize();
	}

	private static CalledFunction Call(string name)
		=> CalledFunction.CreateCalledFunctionMethod(Label(name), name);

	private static CalledFunction Call(FunctionLabelLine label)
		=> CalledFunction.CreateCalledFunctionMethod(label, label.LabelName);

	private static FunctionLabelLine Label(string name)
		=> new(new ScriptPosition("test.erb", 1), name, new WordCollection()) { Arg = [] };

	/// <summary>
	/// 形参书式由 ErbLoader.parseLabel 解析,这里走同一条取值路径拿到 VariableTerm。
	/// 生产中形参是 ARG/ARGS/私有变量,但前两者是函数局部变量、离开运行中的函数就无法解析
	/// (IdentifierDictionary 需要当前扫描行的 ParentLabelLine),被试点(TryConvertArg 的
	/// `term == null → func.Def[i]`)只看 IsReference 与 operand type,故用全局变量占位。
	/// </summary>
	private static VariableTerm FormalArg(string text)
		=> Assert.IsType<VariableTerm>(ExpressionParser.ReduceExpressionTerm(
			LexicalAnalyzer.Analyse(new CharStream(text), LexEndWith.EoL, LexAnalyzeFlag.None), TermEndWith.EoL)!);

	/// <summary>
	/// arg-contract-03:UDF 的空槽取函数标签声明的默认值(默认表达式由 ErbLoader.parseLabel 建成
	/// FunctionLabelLine.Def),代入点是 TryConvertArg。这是 UDF 活路径既有的 null 语义,
	/// 与内建函数侧「可省略槽不放行 null」的裁定无关,此处只钉住它。
	/// </summary>
	[Fact]
	public void ConvertArg_WithOmittedArgument_TakesTheDeclaredDefault()
	{
		FunctionLabelLine label = Label("FOO");
		label.Arg = [FormalArg("DAY:0")];
		label.Def = [new SingleLongTerm(5)];

		UserDefinedFunctionArgument argument = Call(label).ConvertArg([]);

		Assert.Equal(5L, argument.Arguments[0]!.GetIntValue(null!));
	}

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
