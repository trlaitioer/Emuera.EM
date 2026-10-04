using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using System.Reflection;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// arg-contract-05 阶段 B 切片④ 的门禁用例:旧签名
/// (<c>(ExpressionMediator, List&lt;AExpression?&gt;)</c>) 的取值入口不再是虚方法、也不允许任何子类声明它——
/// 编译期由 CS0115 拦,这里再机械枚举一遍,免得哪天把 virtual 加回去而没人发现;槽位入口必须仍是虚方法,
/// 否则函数体无从覆写。检查入口同理:只允许声明带命中段的那个重载,否则
/// <see cref="FunctionMethod.CheckAndResolveSlots"/> 的虚分派会绕过子类的自检。第三项补上编译期拦不住的
/// 那一半:函数体被删掉(而签名未被改写)不会报错,只会静默回落到基类抛 ExeEE,故逐个子类核对实现是否存在。
/// </summary>
public class SlotMigrationGateTests
{
	private static readonly Type[] OldSignature = [typeof(ExpressionMediator), typeof(List<AExpression?>)];
	private static readonly Type[] SlotSignature = [typeof(ExpressionMediator), typeof(FunctionMethod.ArgumentSlots)];
	private static readonly Type[] OldCheckSignature = [typeof(string), typeof(List<AExpression?>)];
	private static readonly Type[] CheckSignature = [typeof(string), typeof(List<AExpression?>), typeof(int).MakeByRefType()];
	private static readonly string[] Entries = ["GetIntValue", "GetStrValue", "GetReturnValue"];
	private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

	/// <summary>程序集里的全部 <see cref="FunctionMethod"/> 子类(基类自己不算)。</summary>
	private static List<Type> Subclasses()
	{
		List<Type> subclasses = [];
		foreach (Type type in typeof(FunctionMethod).Assembly.GetTypes())
		{
			if (type != typeof(FunctionMethod) && typeof(FunctionMethod).IsAssignableFrom(type))
			{
				subclasses.Add(type);
			}
		}

		return subclasses;
	}

	/// <summary>枚举程序集里全部 <see cref="FunctionMethod"/> 子类,断言没有一处声明旧签名。</summary>
	[Fact]
	public void NoSubclassDeclaresTheLegacyListSignature()
	{
		List<string> offenders = [];
		List<Type> subclasses = Subclasses();
		foreach (Type type in subclasses)
		{
			foreach (string entry in Entries)
			{
				if (type.GetMethod(entry, Declared, null, OldSignature, null) != null)
				{
					offenders.Add($"{type.Name}.{entry}");
				}
			}
		}

		Assert.Empty(offenders);
		// 反空跑:按族核对扫到的数量,而不是一个与族数无关的魔数——整族消失时这里必须失败。
		int ruleTableFamily = subclasses.Count(type => type.BaseType == typeof(FunctionMethod));
		int operatorFamily = subclasses.Count(type => typeof(OperatorMethod).IsAssignableFrom(type));
		Assert.True(ruleTableFamily >= 183, $"规则表族只扫到 {ruleTableFamily} 个 FunctionMethod 子类");
		Assert.True(operatorFamily >= 39, $"运算符族只扫到 {operatorFamily} 个");
	}

	/// <summary>旧入口是门面(非虚),槽位入口是本体(虚)。</summary>
	[Fact]
	public void LegacyEntriesAreFacadesAndSlotEntriesAreOverridable()
	{
		foreach (string entry in Entries)
		{
			MethodInfo legacy = typeof(FunctionMethod).GetMethod(entry,
				BindingFlags.Instance | BindingFlags.Public, null, OldSignature, null)!;
			MethodInfo slots = typeof(FunctionMethod).GetMethod(entry,
				BindingFlags.Instance | BindingFlags.Public, null, SlotSignature, null)!;

			Assert.False(legacy.IsVirtual, $"{entry}(List) 必须是不可覆写的门面");
			Assert.True(slots.IsVirtual, $"{entry}(ArgumentSlots) 必须可覆写");
		}
	}

	/// <summary>检查入口只允许声明带命中段的重载:去 override 旧的两参门面会让
	/// <see cref="FunctionMethod.CheckAndResolveSlots"/> 的虚分派绕过它,自检与角色解析一起失效。</summary>
	[Fact]
	public void NoSubclassOverridesTheLegacyCheckFacade()
	{
		List<string> offenders = [];
		foreach (Type type in Subclasses())
		{
			if (type.GetMethod("CheckArgumentType", Declared, null, OldCheckSignature, null) != null)
			{
				offenders.Add($"{type.Name}.CheckArgumentType");
			}
		}

		Assert.Empty(offenders);
		// 基类的两参门面必须存在且**不可覆写**:留着 virtual,覆写它同样会被 CheckAndResolveSlots 的虚分派绕过,
		// 而 CS0115 是唯一能替反射用例兜住这条的编译期门禁。带命中段的重载是唯一的检查入口。
		MethodInfo legacy = typeof(FunctionMethod).GetMethod("CheckArgumentType",
			BindingFlags.Instance | BindingFlags.Public, null, OldCheckSignature, null)!;
		Assert.False(legacy.IsVirtual, "CheckArgumentType(两参) 必须是不可覆写的门面");
		Assert.NotNull(typeof(FunctionMethod).GetMethod("CheckArgumentType",
			BindingFlags.Instance | BindingFlags.NonPublic, null, CheckSignature, null));
	}

	/// <summary>每个具体子类都必须<b>自己</b>声明至少一个槽位入口:删掉函数体(而不是改写签名)不触发 CS0115,
	/// 只会静默回落到基类那三个抛 ExeEE 的实现,运行期才炸。判据必须收在「自己声明」上——继承来的实现同样可能
	/// 只是抛 ExeEE 的中转(FormattedStringMethod.GetIntValue 就是一例),按「含继承」判会把它当成实现。</summary>
	[Fact]
	public void EveryConcreteSubclassDeclaresASlotEntry()
	{
		List<string> offenders = [];
		int concrete = 0;
		foreach (Type type in Subclasses())
		{
			if (type.IsAbstract)
			{
				continue;
			}

			concrete++;
			if (!Entries.Any(entry => type.GetMethod(entry, Declared, null, SlotSignature, null) != null))
			{
				offenders.Add(type.Name);
			}
		}

		Assert.Empty(offenders);
		// 前提对照:基类那三个槽位入口是抛 ExeEE 的默认实现(子类删掉自己的实现就静默回落到它们),
		// 断言它们都在,否则本用例拦的那个失败模式根本不存在。
		foreach (string entry in Entries)
		{
			Assert.NotNull(typeof(FunctionMethod).GetMethod(entry, Declared, null, SlotSignature, null));
		}

		// 负对照:继承来的槽位入口可能是抛 ExeEE 的中转,故不能计入。FormatPercent 的 GetIntValue 继承自
		// FormattedStringMethod(只抛 ExeEE),它的真实现是自己声明的 GetStrValue——删掉后者必须被本用例抓到。
		Type formatPercent = Subclasses().Single(type => type.Name == "FormatPercent");
		Assert.NotNull(formatPercent.GetMethod("GetStrValue", Declared, null, SlotSignature, null));
		MethodInfo inheritedInt = formatPercent.GetMethod("GetIntValue", SlotSignature)!;
		Assert.NotEqual(typeof(FunctionMethod), inheritedInt.DeclaringType);

		// 反空跑:规则表族一项就有 183 个具体子类,扫不到这个量级说明枚举本身失效了。
		Assert.True(concrete >= 183, $"只扫到 {concrete} 个具体 FunctionMethod 子类");
	}
}
