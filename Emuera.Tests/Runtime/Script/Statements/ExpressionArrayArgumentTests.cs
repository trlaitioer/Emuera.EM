using System.Reflection;
using MinorShift.Emuera;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// ExpressionArrayArgument.TermList 的契约回归网(nullable-migration-22 §6)。issues/17 的审计移交给本票
/// 的处置是「构造器改收 <c>List&lt;AExpression&gt;</c>」,元素非空性因此写在类型上、由
/// <c>ArgumentBuilder.ToNonNullTerms</c> 单点断言;本文件把这个契约钉在两层:
/// 1. 类型层:构造器形参与 <c>TermList</c> 的声明可空性(反射的 NullabilityInfoContext 读编译器写下的注解)——
///    退回 <c>List&lt;AExpression?&gt;</c> 会让这条立刻变红,而编译不会失败。
/// 2. 行为层:三条构造路径(INT_ANY / FORM_STR_ANY / SP_SAVECHARA)的实参表逐项非空,且显式空槽在
///    构造之前就被拒(故不存在「填进去一个 null」的写法)。
/// 缝与 <see cref="OmittedArgumentTests"/> 相同:LogicalLineParser.ParseLine → ArgumentParser.SetArgumentTo。
/// </summary>
public class ExpressionArrayArgumentTests
{
	public ExpressionArrayArgumentTests()
	{
		TestBootstrap.Initialize();
	}

	// ── 类型层契约:元素可空性写在类型上 ────────────────────────────────────

	/// <summary>
	/// 构造器收 <c>List&lt;AExpression&gt;</c>、<c>TermList</c> 是 <c>AExpression[]</c>——两者的元素都声明为
	/// 非空。判据取自编译器写入的 NullableAttribute(用 NullabilityInfoContext 读出),
	/// 因为把形参改回 <c>List&lt;AExpression?&gt;</c> 只是注解变化,生产码与测试码都不会编译失败。
	/// </summary>
	[Fact]
	public void ConstructorParameterAndField_DeclareNonNullElements()
	{
		var nullability = new NullabilityInfoContext();

		ConstructorInfo ctor = Assert.Single(typeof(ExpressionArrayArgument).GetConstructors());
		NullabilityInfo termListParameter = nullability.Create(Assert.Single(ctor.GetParameters()));
		Assert.Equal(NullabilityState.NotNull, termListParameter.ReadState);
		Assert.Equal(NullabilityState.NotNull, Assert.Single(termListParameter.GenericTypeArguments).ReadState);

		FieldInfo termListField = typeof(ExpressionArrayArgument)
			.GetField(nameof(ExpressionArrayArgument.TermList))!;
		NullabilityInfo termListInfo = nullability.Create(termListField);
		Assert.NotNull(termListInfo.ElementType);
		Assert.Equal(NullabilityState.NotNull, termListInfo.ElementType!.ReadState);
	}

	/// <summary>
	/// 反空跑:上面那条断言「读出来是 NotNull」,若 NullabilityInfoContext 对任何形参都返回同一个值,
	/// 它就什么也没钉住。这里用同程序集里的 <see cref="SpCopyArrayArgument"/> 作对照——它的前两个形参是
	/// 非空的名字表达式、后两个是 <c>VariableToken?</c>,同一读者必须报出两种状态。
	/// </summary>
	[Fact]
	public void NullabilityReader_DistinguishesNullableFromNonNull()
	{
		var nullability = new NullabilityInfoContext();
		ParameterInfo[] parameters = Assert.Single(typeof(SpCopyArrayArgument).GetConstructors()).GetParameters();
		Assert.Equal(4, parameters.Length);

		Assert.Equal(NullabilityState.NotNull, nullability.Create(parameters[0]).ReadState); // AExpression str1
		Assert.Equal(NullabilityState.NotNull, nullability.Create(parameters[1]).ReadState); // AExpression str2
		Assert.Equal(NullabilityState.Nullable, nullability.Create(parameters[2]).ReadState); // VariableToken? var1
		Assert.Equal(NullabilityState.Nullable, nullability.Create(parameters[3]).ReadState); // VariableToken? var2
	}

	// ── INT_ANY:RETURN / ADDCHARA / ADDCOPYCHARA / PICKUPCHARA ──────────────

	/// <summary>
	/// INT_ANY 的构造路径(由 checkArgumentType 拒绝省略元素):列表长度等于实参个数、逐项非空。
	/// RETURN 的无实参形态是唯一允许空表的分支(补 0 只写进 builder 自己的列表,TermList 是构造时的快照),
	/// 另由 <see cref="ReturnWithoutArgument_KeepsEmptyTermListAndStaysConstant"/> 单独钉住。
	/// </summary>
	[Theory]
	[InlineData("RETURN 1, 2", 2)]
	[InlineData("RETURN 1, 2,", 2)] // 单个尾逗号被吃掉,不产生空槽
	[InlineData("PICKUPCHARA 1, 2", 2)]
	[InlineData("ADDCHARA 1, 2", 2)]
	[InlineData("ADDCOPYCHARA 3", 1)]
	public void IntAnyInstruction_BuildsListOfNonNullTerms(string text, int count)
		=> AssertNonNullTerms(text, count);

	// ── 省略元素在构造之前被拒 ──────────────────────────────────────────────

	/// <summary>
	/// 显式空槽(第二个逗号起)在 checkArgumentType 处以 CanNotRecognizeArg 被拒 ⇒ 构造器永远拿不到
	/// 含 null 的列表,这正是 <c>ToNonNullTerms</c> 那处单点断言成立的理由。
	/// </summary>
	[Theory]
	[InlineData("RETURN ,", 1)]
	[InlineData("PICKUPCHARA ,", 1)]
	[InlineData("SAVECHARA \"f\", \"m\", 0, ,", 4)]
	public void ExplicitEmptySlot_IsRejectedBeforeConstruction(string text, int slot)
	{
		(bool built, InstructionLine line) = Parse(text);
		Assert.False(built, $"{text} 应被拒绝");
		Assert.True(line.IsError, $"{text} 的行应被标错(level >= 2)");
		Assert.Contains(string.Format(trerror.CanNotRecognizeArg.Text, slot), line.ErrMes);
		Assert.Null(line.Argument);
	}

	// ── FORM_STR_ANY:SETBGIMAGE / REMOVEBGIMAGE ────────────────────────────

	/// <summary>
	/// FORM_STR_ANY 自建 term(不经 checkArgumentType),故它对空槽的处理方式与上面两条路径不同:
	/// 空槽也产出**非空**的项(空 StrForm),列表因此永不出现 null。
	/// </summary>
	[Theory]
	[InlineData("SETBGIMAGE \"a\", \"b\"", 2)]
	[InlineData("SETBGIMAGE \"a\",", 1)] // 尾逗号被吃掉
	[InlineData("SETBGIMAGE \"a\",,", 2)] // 空槽也产出非空项(与 INT_ANY 的拒绝不同)
	[InlineData("REMOVEBGIMAGE \"a\"", 1)]
	public void FormStrAnyInstruction_BuildsListOfNonNullTerms(string text, int count)
		=> AssertNonNullTerms(text, count);

	// ── SP_SAVECHARA ────────────────────────────────────────────────────────

	/// <summary>SP_SAVECHARA 同样经 checkArgumentType:长度等于实参个数、逐项非空(含数量省略形态)。</summary>
	[Theory]
	[InlineData("SAVECHARA \"f\", \"m\", 0, 1", 4)]
	[InlineData("SAVECHARA \"f\", \"m\", 0", 3)]
	[InlineData("SAVECHARA \"f\", \"m\", 0,", 3)]
	public void SaveCharaInstruction_BuildsListOfNonNullTerms(string text, int count)
		=> AssertNonNullTerms(text, count);

	/// <summary>
	/// 空表快照:RETURN 无实参时 INT_ANY 先构造(空表)再往**它自己的**列表里补 0,故 TermList 仍为空,
	/// 由 RETURN_Instruction 按「长度为 0」置 RESULT = 0。这同时钉住构造器取的是副本而不是别名。
	/// </summary>
	[Fact]
	public void ReturnWithoutArgument_KeepsEmptyTermListAndStaysConstant()
	{
		(bool built, InstructionLine line) = Parse("RETURN");

		Assert.True(built, line.ErrMes);
		var arg = Assert.IsType<ExpressionArrayArgument>(line.Argument);
		Assert.Empty(arg.TermList);
		Assert.True(arg.IsConst);
		Assert.Equal(0L, arg.ConstInt);
	}

	// ── 夹具 ────────────────────────────────────────────────────────────────

	private static ScriptPosition Pos => new("test.erb", 1);

	private static (bool Built, InstructionLine Line) Parse(string text)
	{
		LogicalLine? parsed = LogicalLineParser.ParseLine(new CharStream(text), Pos, null);
		InstructionLine line = Assert.IsType<InstructionLine>(parsed);
		bool built = ArgumentParser.SetArgumentTo(line);
		return (built, line);
	}

	/// <summary>断言构造结果是元素个数正确的 ExpressionArrayArgument,且逐项非空(§6 的契约)。</summary>
	private static void AssertNonNullTerms(string text, int count)
	{
		(bool built, InstructionLine line) = Parse(text);
		Assert.True(built, $"{text} 应被接受,实际报错:{line.ErrMes}");
		Assert.False(line.IsError, line.ErrMes);
		var arg = Assert.IsType<ExpressionArrayArgument>(line.Argument);

		Assert.Equal(count, arg.TermList.Length);
		// 迭代变量显式写作可空:要断言的正是「类型上非空的表里没有 null」
		foreach (AExpression? term in arg.TermList)
		{
			Assert.NotNull(term);
		}
	}
}
