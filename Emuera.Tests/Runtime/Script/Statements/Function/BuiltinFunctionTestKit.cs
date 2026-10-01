using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// 内建函数用例的共用夹具:解析实参文本(<see cref="Args"/>)、按名取已注册的 <see cref="FunctionMethod"/>
/// (<see cref="Method"/>)、按返回类型分派取值入口(<see cref="Invoke(string, string)"/>)。
/// 前两项由检查器用例 <see cref="CheckArgumentTypeEx"/> 与执行期用例
/// <see cref="IntentionalEmptySlotExecutionTests"/> 共用;分派只有执行期用例需要——检查器用例走
/// <c>IdentifierDictionary.GetFunctionMethod</c>,不求值。
/// </summary>
internal static class BuiltinFunctionTestKit
{
	/// <summary>
	/// 解析一段调用实参(传括号内的文本)。词法器要求括号配平,故补上外层括号,再按生产路径
	/// (ExpressionParser.reduceIdentifier)跳过 '(' 后调 ReduceArguments;显式空槽在其中成为 null 元素。
	/// </summary>
	internal static List<AExpression?> Args(string inner)
	{
		WordCollection wc = LexicalAnalyzer.Analyse(
			new CharStream("(" + inner + ")"), LexEndWith.EoL, LexAnalyzeFlag.None);
		Assert.Equal('(', Assert.IsType<SymbolWord>(wc.Current).Type);
		wc.ShiftNext();
		return ExpressionParser.ReduceArguments(wc, ArgsEndWith.RightParenthesis, false);
	}

	/// <summary>按名取注册的内建函数。名字写错会让 GetFunctionMethod 返回 null 而非抛错,故先断言确实注册。</summary>
	internal static FunctionMethod Method(string name)
	{
		Assert.True(FunctionMethodCreator.GetMethodList().TryGetValue(name, out FunctionMethod? method),
			$"内建函数 {name} 未注册");
		return method;
	}

	/// <summary>按返回类型分派取值入口:字符串型函数走 `GetStrValue`,其余走 `GetIntValue`。
	/// 与生产码 `FunctionMethod.GetReturnValue` 是**反向**写法的同一判据(那边先判 `typeof(long)`),
	/// 对现役内建的 long/string 两态等价;这里直接返回装箱值,省去用例侧的 `SingleTerm` 拆包。</summary>
	internal static object Invoke(FunctionMethod method, List<AExpression?> args)
		=> method.ReturnType == typeof(string)
			? method.GetStrValue(GlobalStatic.EMediator, args)
			: method.GetIntValue(GlobalStatic.EMediator, args);

	/// <summary>按名取方法并求值(执行期用例的最短形式)。</summary>
	internal static object Invoke(string name, string inner)
		=> Invoke(Method(name), Args(inner));
}
