using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
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

	/// <summary>检查期路径的槽位构造:与生产码的构造点同法(单次 <c>CheckAndResolveSlots</c> → 规则表族
	/// <c>Checked</c>、无规则表族 <c>Trusted</c>)。名字只进错误文案,夹具断言调用合法故传 null。</summary>
	internal static FunctionMethod.ArgumentSlots Slots(FunctionMethod method, List<AExpression?> args)
	{
		string? errmes = method.CheckAndResolveSlots(null!, args, out FunctionMethod.ArgumentSlots slots);
		Assert.Null(errmes);
		return slots;
	}

	/// <summary>按返回类型分派取值入口:字符串型函数走 `GetStrValue`,其余走 `GetIntValue`。
	/// 与生产码 <see cref="FunctionMethodTerm"/> 是**反向**写法的同一判据(那边先判 `typeof(long)`),
	/// 对现役内建的 long/string 两态等价;这里直接返回装箱值,省去用例侧的 `SingleTerm` 拆包。
	/// 取值一律走 <c>GlobalStatic.EMediator</c>;要断言「函数体不碰 exm」的用例用
	/// <see cref="InvokeWith"/> 显式传 null。</summary>
	internal static object Invoke(FunctionMethod method, List<AExpression?> args)
		=> InvokeWith(method, args, GlobalStatic.EMediator);

	/// <summary><see cref="Invoke"/> 的原样传参版:<paramref name="exm"/> 为 null 就真的传 null
	/// (纯常量实参的用例借此钉住函数体没读 exm)。</summary>
	internal static object InvokeWith(FunctionMethod method, List<AExpression?> args, ExpressionMediator? exm)
	{
		FunctionMethod.ArgumentSlots slots = Slots(method, args);
		return method.ReturnType == typeof(string)
			? method.GetStrValue(exm!, slots)
			: method.GetIntValue(exm!, slots);
	}

	/// <summary>按名取方法并求值(执行期用例的最短形式)。</summary>
	internal static object Invoke(string name, string inner)
		=> Invoke(Method(name), Args(inner));
}
