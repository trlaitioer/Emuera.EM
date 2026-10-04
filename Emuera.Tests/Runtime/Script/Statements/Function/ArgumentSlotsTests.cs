using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// arg-contract-05 阶段 B 的机制用例:检查期的槽位角色投影(单次
/// <see cref="FunctionMethod.CheckAndResolveSlots"/>)、槽位访问器契约(<see cref="FunctionMethod.ArgumentSlots"/>),
/// 以及「检查期快照不跟随检查之后的原地折叠」这条定案。缝与 <see cref="CheckArgumentTypeEx"/> 同一处
/// (解析期契约入口),但只问投影与访问器,不重复该校验判据。
/// </summary>
public class ArgumentSlotsTests
{
	public ArgumentSlotsTests() => TestBootstrap.Initialize();

	/// <summary>按生产路径解析一个书式(与 CheckArgumentTypeEx 的夹具同法,不求值也不折叠)。</summary>
	private static AExpression Parse(string text)
		=> ExpressionParser.ReduceExpressionTerm(
			LexicalAnalyzer.Analyse(new CharStream(text), LexEndWith.EoL, LexAnalyzeFlag.None),
			TermEndWith.EoL)!;

	private static AExpression Variable(string text) => Assert.IsType<VariableTerm>(Parse(text));

	/// <summary>按生产路径解析角色:检查器一遍同时给出结果与槽位视图。</summary>
	private static FunctionMethod.ArgumentSlots Roles(string name, List<AExpression?> args)
	{
		FunctionMethod method = Method(name);
		Assert.Null(method.CheckAndResolveSlots(name, args, out FunctionMethod.ArgumentSlots slots));
		Assert.True(slots.HasDeclaredRoles, $"{name} 应产出角色");
		return slots;
	}

	// ── 角色投影 ──────────────────────────────────────────────────────────────

	/// <summary>变长尾:MAX 的规则是 {Int, VariadicInt|NotNull}(OmitStart=1),0 号槽是定长槽,
	/// 尾部每槽按 LastVariadics 取模,都带 Required(NotNull)与 Variadic。</summary>
	[Fact]
	public void VariadicTail_ProjectsPerSlotRoles()
	{
		FunctionMethod.ArgumentSlots slots = Roles("MAX", Args("1,2,3"));
		Assert.Equal(3, slots.Count);
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int, slots.RoleAt(0));
		Assert.False(slots.RoleAt(0).HasFlag(FunctionMethod.SlotRole.Variadic));
		for (int i = 1; i < slots.Count; i++)
		{
			FunctionMethod.SlotRole role = slots.RoleAt(i);
			Assert.True(role.HasFlag(FunctionMethod.SlotRole.Required));
			Assert.True(role.HasFlag(FunctionMethod.SlotRole.Int));
			Assert.True(role.HasFlag(FunctionMethod.SlotRole.Variadic));
		}
	}

	/// <summary>「Required」是规则段的性质(检查器会不会放行显式空槽),不是「本次调用填了这个槽」:
	/// VARSIZE 的 {String, Int} OmitStart=1 有意让空槽表示 dim=0,故 1 号槽填了值也只带类型声明。</summary>
	[Fact]
	public void OmittableSlotWithoutNotNull_NeverClaimsRequired()
	{
		FunctionMethod.ArgumentSlots empty = Roles("VARSIZE", Args("\"v\",,"));
		Assert.Equal(2, empty.Count);
		Assert.False(empty.RoleAt(1).HasFlag(FunctionMethod.SlotRole.Required));
		Assert.Equal(FunctionMethod.SlotRole.Int, empty.RoleAt(1));

		FunctionMethod.ArgumentSlots filled = Roles("VARSIZE", Args("\"v\",0"));
		Assert.False(filled.RoleAt(1).HasFlag(FunctionMethod.SlotRole.Required));
		Assert.Equal(FunctionMethod.SlotRole.Int, filled.RoleAt(1));
	}

	/// <summary>标了 NotNull 的可省略段反过来:省略数量仍合法,但该槽一旦出现就不许为空 ⇒ Required。</summary>
	[Fact]
	public void NotNullSlot_ClaimsRequired()
	{
		FunctionMethod.ArgumentSlots slots = Roles("XML_GET", Args("\"x\",\"p\",0,0"));
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int, slots.RoleAt(3));
	}

	/// <summary>规则表的多段按实参形状命中不同段,投影跟随命中段:XML_GET 的 3 参形状落在
	/// {Any, String, Int|NotNull, Int|NotNull}(OmitStart=2);输出数组形状落在
	/// {Any, String, RefString1D, Int|NotNull}(OmitStart=3),第 3 槽因此是 Ref|String 而非 Int。</summary>
	[Fact]
	public void MatchedRuleBlock_DrivesTheProjection()
	{
		FunctionMethod.ArgumentSlots intStyle = Roles("XML_GET", Args("\"x\",\"p\",0"));
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int, intStyle.RoleAt(2));

		FunctionMethod.ArgumentSlots refStyle = Roles("XML_GET",
			[new SingleStrTerm("x"), new SingleStrTerm("p"), Variable("RESULTS:0"), new SingleLongTerm(0)]);
		Assert.True(refStyle.RoleAt(2).HasFlag(FunctionMethod.SlotRole.Ref));
		Assert.True(refStyle.RoleAt(2).HasFlag(FunctionMethod.SlotRole.String));
		Assert.False(refStyle.RoleAt(2).HasFlag(FunctionMethod.SlotRole.AllowConstRef));
	}

	/// <summary>定数表(argumentTypeArray,无 OmitStart 可省段)的槽一律 Required,类型按声明。</summary>
	[Fact]
	public void FixedCountTable_ProjectsRequired()
	{
		FunctionMethod.ArgumentSlots slots = Roles("HTML_ESCAPE", Args("\"<a>\""));
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String, slots.RoleAt(0));
	}

	/// <summary>没有规则表的类(MESSKIP 自 override 检查、0 参)不产出角色:调用点按「无声明」处理,
	/// 不臆造 Required。</summary>
	[Fact]
	public void ClassWithoutRuleTable_ProducesNoRoles()
	{
		FunctionMethod method = Method("MESSKIP");
		Assert.Null(method.CheckAndResolveSlots("MESSKIP", [], out FunctionMethod.ArgumentSlots slots));
		Assert.False(slots.HasDeclaredRoles);
	}

	/// <summary>规则表族的角色来自检查器命中的那一遍(不是另跑一次):同一组实参经
	/// <see cref="FunctionMethod.CheckAndResolveSlots"/> 拿到的角色与 <see cref="FunctionMethod.ArgumentSlots.RoleAt"/> 一致。</summary>
	[Fact]
	public void CheckerResolvedSlots_CarryTheMatchedRoles()
	{
		List<AExpression?> args = Args("1,2");
		FunctionMethod.ArgumentSlots slots = Roles("MAX", args);
		Assert.Equal(2, slots.Count);
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int, slots.RoleAt(0));
		Assert.True(slots.RoleAt(1).HasFlag(FunctionMethod.SlotRole.Variadic));
	}

	// ── 访问器契约 ────────────────────────────────────────────────────────────

	[Fact]
	public void Accessors_ExposeTheTriState()
	{
		FunctionMethod.SlotRole[] roles =
		[
			FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int,
			FunctionMethod.SlotRole.Loose
		];
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted([new SingleLongTerm(5), null], roles);
		Assert.Equal(2, slots.Count);
		Assert.True(slots.HasDeclaredRoles);
		Assert.True(slots.Has(0));
		Assert.False(slots.Has(1));
		Assert.True(slots.IsEmpty(1));
		Assert.False(slots.IsMissing(1));
		Assert.True(slots.IsMissing(2));
		Assert.Equal(5L, slots.Int(GlobalStatic.EMediator, 0));
		Assert.True(slots.IsInteger(0));
		Assert.False(slots.IsString(0));
		Assert.Null(slots.TermOrNull(1));
		Assert.Single(slots.Tail(1));
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int, slots.RoleAt(0));
		Assert.Equal(FunctionMethod.SlotRole.Loose, slots.RoleAt(9));
	}

	/// <summary>读一个被显式留空的槽不是 NRE:访问器把该槽声明的角色写进失败信息,指出破坏的是哪条契约。</summary>
	[Fact]
	public void Accessors_ReportAnEmptySlotWithItsRole()
	{
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted([new SingleLongTerm(5), null],
		[
			FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int,
			FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String
		]);
		ExeEE ex = Assert.Throws<ExeEE>(() => slots.Str(GlobalStatic.EMediator, 1));
		Assert.Contains("slot 1", ex.Message);
		Assert.Contains("Required", ex.Message);
		Assert.Contains("String", ex.Message);
	}

	/// <summary>索引越过实参表与旧 <c>Required(i)</c> 是同一个契约:抛 IndexOutOfRangeException,不吞成 null。</summary>
	[Fact]
	public void Accessors_PastTheEndThrowIndexOutOfRange()
	{
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted([new SingleLongTerm(5)],
			[FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int]);
		Assert.Throws<IndexOutOfRangeException>(() => slots.Int(GlobalStatic.EMediator, 1));
	}

	/// <summary>取变量实体时槽里不是变量实体:报契约失败,而不是把 InvalidCastException 留给调用方。</summary>
	[Fact]
	public void VarAccessor_ReportsANonReferenceSlot()
	{
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted([new SingleLongTerm(5)],
			[FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int]);
		ExeEE ex = Assert.Throws<ExeEE>(() => slots.Var(0));
		Assert.Contains("not a variable entity", ex.Message);
	}

	/// <summary>三态的越界口径统一:负下标与越过尾巴同属「缺失」,不是一个抛异常、一个返回 false。</summary>
	[Fact]
	public void TriState_TreatsNegativeIndicesAsMissing()
	{
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted([new SingleLongTerm(5)], null);
		Assert.False(slots.Has(-1));
		Assert.False(slots.IsEmpty(-1));
		Assert.True(slots.IsMissing(-1));
		Assert.Null(slots.TermOrNull(-1));
		Assert.Single(slots.Tail(-1));
	}

	/// <summary>Var 只在 Ref 槽上有意义:返回变量实体本身(读写都经它),而不是取值。</summary>
	[Fact]
	public void VarAccessor_ReturnsTheVariableEntity()
	{
		AExpression variable = Variable("CFLAG:0");
		FunctionMethod.SlotRole[] roles =
		[
			FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Ref | FunctionMethod.SlotRole.Int
		];
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted([variable], roles);
		Assert.Same(variable, slots.Var(0));
	}

	/// <summary>快照定案:检查之后对原列表的原地折叠不写回槽位视图(阶段 B 的等价性依赖「折叠只换等值项」)。
	/// 这里换成一个不同值的项把差异显式化——访问器与 <c>Tail</c> 仍读检查期的那个项,而 <c>FoldTarget</c> 是活体列表。</summary>
	[Fact]
	public void Slots_DoNotFollowTheInPlaceFold()
	{
		List<AExpression?> arguments = [new SingleLongTerm(5)];
		FunctionMethod.ArgumentSlots slots = FunctionMethod.ArgumentSlots.Trusted(arguments,
			[FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int]);

		arguments[0] = new SingleLongTerm(9);

		Assert.Equal(5L, slots.Int(GlobalStatic.EMediator, 0));
		Assert.Same(arguments[0], slots.FoldTarget[0]);
		Assert.Equal(9L, slots.FoldTarget[0]!.GetIntValue(GlobalStatic.EMediator));
		// 整段转交(GETMETH/GETMETHS)也从快照取:转交的是检查期的项,不是折叠后的活体项。
		Assert.NotSame(arguments[0], Assert.Single(slots.Tail(0)));
		Assert.Equal(5L, Assert.Single(slots.Tail(0))!.GetIntValue(GlobalStatic.EMediator));
	}

	/// <summary>快照定案的另一半(票面要求的执行期证据):真走一遍解析 →
	/// <see cref="FunctionMethodTerm.Restructure"/>(把活体列表的实参就地折叠,MAX 全常量故整体折叠成 SingleTerm)
	/// → 函数体经访问器取值。断言折叠之后仍取到正确值。</summary>
	[Fact]
	public void FoldedCall_StillReadsTheRightValueThroughTheAccessors()
	{
		FunctionMethodTerm term = Assert.IsType<FunctionMethodTerm>(Parse("MAX(1,2)"));
		List<AExpression?> live = term.Slots.FoldTarget;

		AExpression restructured = term.Restructure(GlobalStatic.EMediator);

		Assert.IsType<SingleLongTerm>(restructured);
		Assert.Equal(2L, restructured.GetIntValue(GlobalStatic.EMediator));
		// 折叠只改活体列表;函数体读的快照不受影响,取值仍正确。
		Assert.All(live, argument => Assert.IsType<SingleLongTerm>(argument));
		Assert.Equal(2L, term.GetIntValue(GlobalStatic.EMediator));
	}

	/// <summary>快照定案在<b>不可折叠外层</b>上的执行期后果:XML_GET 不整体折叠,故它此后一直用检查期的实参视图,
	/// 而活体表里的内层调用早已被折叠成常量。两者读值相等,但<b>求值时机不同</b>——访问器在每次调用时重新求值那个
	/// 内层调用。对纯表达式只是白算一次;对状态依赖的可折叠调用(如 VARSIZE,长度会随 DIM 变)读到的就是调用时的值,
	/// 而不是首次折叠时的常量。这是本票有意的语义:快照只保证「折叠前后读值等价」,不保证「求值时机不变」。</summary>
	[Fact]
	public void UnfoldableCall_ReEvaluatesItsFoldedArgumentsAtCallTime()
	{
		FunctionMethodTerm term = Assert.IsType<FunctionMethodTerm>(Parse("XML_GET(\"<a/>\", \"/a\", MAX(1, 2))"));
		List<AExpression?> live = term.Slots.FoldTarget;

		AExpression restructured = term.Restructure(GlobalStatic.EMediator);

		Assert.Same(term, restructured); // XML_GET 不整体折叠,故这条缝会留在执行期
		Assert.IsType<SingleLongTerm>(live[2]); // 活体表里的实参已被就地折叠成常量
		Assert.IsType<FunctionMethodTerm>(term.Slots.Term(2)); // 快照仍是折叠前那个内层调用
		Assert.Equal(live[2]!.GetIntValue(GlobalStatic.EMediator), term.Slots.Int(GlobalStatic.EMediator, 2));
	}

	/// <summary>切片④ 的旧入口门面:它解析检查期角色后转发到槽位入口,故「直接传 List 调用函数体」的既有写法
	/// 不必改;同一组实参经门面与经槽位入口必须给出同一结果。</summary>
	[Fact]
	public void LegacyFacade_ResolvesRolesAndMatchesTheSlotEntry()
	{
		FunctionMethod max = Method("MAX");
		List<AExpression?> args = [new SingleLongTerm(3), new SingleLongTerm(7)];

		long viaFacade = max.GetIntValue(null!, args);
		long viaSlots = max.GetIntValue(null!, Slots(max, args));

		Assert.Equal(7L, viaFacade);
		Assert.Equal(viaSlots, viaFacade);
	}

	/// <summary>门面在无规则表的类上不问检查器(「调用点担保族」的检查重载故意抛 ExeEE,问了就等于禁用门面),
	/// 但走规则表族时照样把检查失败报成检查器的文案——不能带着 Loose 角色进函数体。</summary>
	[Fact]
	public void LegacyFacade_SplitsOnWhetherACheckExists()
	{
		Assert.Equal(5L, new CallerCheckedMethod().GetIntValue(null!, [new SingleLongTerm(5)]));

		FunctionMethod max = Method("MAX");
		CodeEE ex = Assert.Throws<CodeEE>(() => max.GetIntValue(null!, []));
		Assert.Contains("MAX", ex.Message);
	}

	/// <summary>运算符族(真实生产路径)同样不能因为门面而被禁用:先经工厂造出一个运算符 term,
	/// 再用它自己的实参走旧签名入口,结果必须与走槽位入口一致——迁移前那正是函数体本身。</summary>
	[Fact]
	public void LegacyFacade_WorksForTheOperatorFamily()
	{
		FunctionMethodTerm term = Assert.IsType<FunctionMethodTerm>(
			OperatorMethodManager.ReduceBinaryTerm(OperatorCode.Plus, Variable("TFLAG:0"), new SingleLongTerm(1)));
		List<AExpression?> args = [Variable("TFLAG:0"), new SingleLongTerm(1)];

		long viaSlots = term.Method.GetIntValue(GlobalStatic.EMediator, term.Slots);
		long viaFacade = term.Method.GetIntValue(GlobalStatic.EMediator, args);

		Assert.Equal(viaSlots, viaFacade);
	}

	/// <summary>运算符族的另一半:带规则表(<c>argumentTypeArray</c>)而检查重载照样拒绝的类——生产实例是 PLUS 的
	/// 字符串重载 <c>PlusStrStr</c>,全运算符族唯一一个带表的。门面若按「有没有规则表」分流,这里会撞上那句故意的
	/// ExeEE;判据必须是「检查器由调用点担保」。</summary>
	[Fact]
	public void LegacyFacade_WorksForACallerCheckedClassThatHasARuleTable()
	{
		// 先钉住生产实例的形状:PLUS 的字符串分支确实带表,否则下面的合成类就没有原型。
		FunctionMethodTerm plus = Assert.IsType<FunctionMethodTerm>(
			OperatorMethodManager.ReduceBinaryTerm(OperatorCode.Plus, Variable("RESULTS:0"), new SingleStrTerm("x")));
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String, plus.Slots.RoleAt(0));
		Assert.Equal(FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String, plus.Slots.RoleAt(1));

		Assert.Equal(6L, new CallerCheckedWithTableMethod().GetIntValue(null!, [new SingleLongTerm(5)]));
	}

	/// <summary>「调用点担保族」的最小复刻:无规则表(两处都是 null)且带命中段的检查重载照运算符族那样拒绝调用。</summary>
	private sealed class CallerCheckedMethod : FunctionMethod
	{
		public CallerCheckedMethod() => ReturnType = typeof(long);

		internal override string? CheckArgumentType(string name, List<AExpression?> arguments, out int matchedIdx)
		{
			matchedIdx = -1;
			throw new ExeEE("caller owns the check");
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots) => slots.Int(exm, 0);
	}

	/// <summary>「调用点担保 + 带规则表」的最小复刻(生产实例:PLUS 的字符串重载):表照旧声明,检查重载拒绝,
	/// 故 <see cref="FunctionMethod.CallerOwnsCheck"/> 必须为真,否则门面会去问那个只抛 ExeEE 的检查器。</summary>
	private sealed class CallerCheckedWithTableMethod : FunctionMethod
	{
		public CallerCheckedWithTableMethod()
		{
			ReturnType = typeof(long);
			argumentTypeArray = [typeof(long)];
		}

		internal override bool CallerOwnsCheck => true;

		internal override string? CheckArgumentType(string name, List<AExpression?> arguments, out int matchedIdx)
		{
			matchedIdx = -1;
			throw new ExeEE("caller owns the check");
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots) => slots.Int(exm, 0) + 1;
	}
}
