using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// ARRAYCOPY 的解析期实参校验回归网(nullable-migration-22 §5-1/§5-2)。被测对象是
/// <c>ArgumentBuilder.SP_COPY_ARRAY_Arguments</c> 抽出的 <c>TryResolveArrayArg</c>/<c>CheckSameShape</c>,
/// 以及 <c>SpCopyArrayArgument</c> 新增的 <c>ResolvedVar1</c>/<c>ResolvedVar2</c>。缝与
/// <see cref="OmittedArgumentTests"/> 相同:装载期入口
/// (LogicalLineParser.ParseLine → ArgumentParser.SetArgumentTo → ArgumentBuilder.CreateArgument)。
///
/// 三处夹具事实(决定了下面各条用例的写法):
/// 1. ARRAYCOPY 的两个实参在规则表里都是「文字列式」,操作数类型必须是字符串:裸的**整型**变量名
///    (`DAY`)会在 checkArgumentType 处按操作数类型被拒(既有语义,不是本票的改动),
///    故 <see cref="BareVariableName_IsRejectedByOperandType"/> 只钉住这一读法边界;裸的**字符串**
///    变量(`SAVEDATA_TEXT`)放行,但保持 VariableTerm、由执行期定名(见
///    <see cref="RuntimeNamedArgument_LeavesResolvedVarNull"/>)。只有常量串(字面量或已折叠的拼接)
///    才在解析期解析出令牌。
/// 2. 常量串**拼接**在解析期就折叠成 SingleStrTerm,因而同样进入解析分支——
///    「字面量」的判据是「解析期已得常量串」而非源码里有没有引号(见 <see cref="FoldedConstantString_IsResolvedAsLiteralName"/>);
///    反之,普通变量(如 <c>RESULTS:0</c>)保持 VariableTerm,解析期无法定名 ⇒ ResolvedVar 为 null。
/// 3. TestBootstrap 以 null console 初始化 ParserMediator,告警列表不记录(见 ParserMediator.Warn 的
///    console 判据),故「告警键」经 <c>line.ErrMes</c> 观察、「level 2」经 <c>line.IsError</c> 观察
///    (ArgumentBuilder.warn:isError = level >= 2 且同一分支写 ErrMes);level 1 的对照见
///    <see cref="LevelOneWarning_DoesNotMarkTheLineAndStillBuilds"/>。
///
/// 取名一律取空 csv 夹具下可解析的保留变量:DAY/MONEY(一维整型)、SAVESTR/GLOBALS(一维字符串)、
/// DA/DB(二维整型)、TA/TB(三维整型)、RAND/ITEMPRICE(一维整型**常量**数组)、CFLAG/TALENT(角色变量)、
/// SAVEDATA_TEXT(字符串标量)。空目录不会让这些 token 变 IsForbid,故都能取到。
/// </summary>
public class ArraycopyArgumentTests : IDisposable
{
	private readonly bool previousDeferParseErrorSound;

	public ArraycopyArgumentTests()
	{
		TestBootstrap.Initialize();
		// 解析失败路径会经 NotifyParseError 播提示音,用例中抑制(同 OmittedArgumentTests)
		previousDeferParseErrorSound = ParserMediator.DeferParseErrorSound;
		ParserMediator.DeferParseErrorSound = true;
	}

	public void Dispose()
	{
		ParserMediator.DeferParseErrorSound = previousDeferParseErrorSound;
	}

	// ── 实参名不存在 ────────────────────────────────────────────────────────

	/// <summary>未定义的变量名:告警 ArraycopyArgIsNotDefined,行被标错,CreateArgument 返回 null(SetArgumentTo=false)。</summary>
	[Theory]
	[InlineData("\"NOPE\", \"DAY\"", "1")]
	[InlineData("\"DAY\", \"NOPE\"", "2")]
	public void UndefinedName_IsRejected(string argText, string slot)
		=> AssertRejectedWith(argText, trerror.ArraycopyArgIsNotDefined, slot, "NOPE");

	// ── 实参名不是数组 ──────────────────────────────────────────────────────

	/// <summary>标量(非数组)变量名:告警 ArraycopyArgIsNotArray。字符串标量走不到角色/常量判据。</summary>
	[Theory]
	[InlineData("\"SAVEDATA_TEXT\", \"DAY\"", "1")]
	[InlineData("\"DAY\", \"SAVEDATA_TEXT\"", "2")]
	public void ScalarVariable_IsRejectedAsNotArray(string argText, string slot)
		=> AssertRejectedWith(argText, trerror.ArraycopyArgIsNotArray, slot, "SAVEDATA_TEXT");

	// ── 实参名是角色数据变量 ────────────────────────────────────────────────

	/// <summary>角色数据变量(含 CFLAG 这类数组):告警 ArraycopyArgIsCharaVar。判据在数组判据之后,故先过数组检查。</summary>
	[Theory]
	[InlineData("\"CFLAG\", \"DAY\"", "1", "CFLAG")]
	[InlineData("\"DAY\", \"TALENT\"", "2", "TALENT")]
	public void CharacterVariable_IsRejected(string argText, string slot, string name)
		=> AssertRejectedWith(argText, trerror.ArraycopyArgIsCharaVar, slot, name);

	// ── 常量数组:两实参的差异(本票重点) ──────────────────────────────────

	/// <summary>
	/// 第 2 实参是常量数组:告警 ArraycopyArgIsConst。这条与下一条成对,共同钉住被保留下来的差异——
	/// 两个实参块的唯一区别就是 arg1 不做 IsConst 检查(arg2 做)。
	/// </summary>
	[Theory]
	[InlineData("\"DAY\", \"RAND\"", "RAND")]
	[InlineData("\"DAY\", \"ITEMPRICE\"", "ITEMPRICE")]
	public void ConstArrayInSecondArgument_IsRejected(string argText, string name)
		=> AssertRejectedWith(argText, trerror.ArraycopyArgIsConst, "2", name);

	/// <summary>
	/// 第 1 实参是常量数组必须放行:同一个常量一维整型数组放到第 1 实参不报 ArraycopyArgIsConst,
	/// 构造照常成功。抽 TryResolveArrayArg 时若把 rejectConst 一并套到两个实参上,本用例立刻变红。
	/// </summary>
	[Fact]
	public void ConstArrayInFirstArgument_IsAccepted()
	{
		// RAND 与 ITEMPRICE 都是 __UNCHANGEABLE__ 的一维整型数组;对侧 DAY 是一维整型可写数组
		SpCopyArrayArgument rand = AssertAccepted("\"RAND\", \"DAY\"");
		Assert.Same(Token("RAND"), rand.ResolvedVar1);
		Assert.Same(Token("DAY"), rand.ResolvedVar2);

		SpCopyArrayArgument itemprice = AssertAccepted("\"ITEMPRICE\", \"DAY\"");
		Assert.Same(Token("ITEMPRICE"), itemprice.ResolvedVar1);
		Assert.Same(Token("DAY"), itemprice.ResolvedVar2);

		// 对照:同一个 ITEMPRICE 换到第 2 实参立刻被拒(见 ConstArrayInSecondArgument_IsRejected),
		// 故本用例通过不是因为它不是常量数组。
		Assert.True(Token("ITEMPRICE").IsConst);
	}

	// ── 两个已解析实参的形状比对 ────────────────────────────────────────────

	/// <summary>
	/// 维数不一致:告警 DifferentArraycopyArgsDim。判据是三个 <c>var1.IsArrayND &amp;&amp; !var2.IsArrayND</c> 的析取,
	/// 故每项各给一行(一维项由 1D←2D/1D←3D 触发、二维项由 2D←1D/2D←3D、三维项由 3D←2D),免得只覆盖到一个合取项。
	/// </summary>
	[Theory]
	[InlineData("\"DAY\", \"DA\"")] // 一维 ← 二维
	[InlineData("\"DAY\", \"TA\"")] // 一维 ← 三维
	[InlineData("\"DA\", \"DAY\"")] // 二维 ← 一维
	[InlineData("\"DA\", \"TA\"")] // 二维 ← 三维
	[InlineData("\"TA\", \"DA\"")] // 三维 ← 二维
	public void DifferentDimensions_AreRejected(string argText)
		=> AssertRejectedWith(argText, trerror.DifferentArraycopyArgsDim);

	/// <summary>元素类型不一致(整型 vs 字符串):告警 DifferentArraycopyArgsType,两个方向都钉。</summary>
	[Theory]
	[InlineData("\"DAY\", \"SAVESTR\"")] // 一维整型 ← 一维字符串
	[InlineData("\"SAVESTR\", \"DAY\"")] // 一维字符串 ← 一维整型
	public void DifferentElementTypes_AreRejected(string argText)
		=> AssertRejectedWith(argText, trerror.DifferentArraycopyArgsType);

	/// <summary>同形状的正控制:一维/二维/三维与字符串一维都必须继续放行(收紧不得误伤)。</summary>
	[Theory]
	[InlineData("\"DAY\", \"MONEY\"")] // 一维整型
	[InlineData("\"DA\", \"DB\"")] // 二维整型
	[InlineData("\"TA\", \"TB\"")] // 三维整型
	[InlineData("\"SAVESTR\", \"GLOBALS\"")] // 一维字符串
	public void SameShape_IsAccepted(string argText)
		=> Assert.NotNull(AssertAccepted(argText));

	// ── SpCopyArrayArgument 携带的解析期结果 ────────────────────────────────

	/// <summary>
	/// 合法输入:返回 SpCopyArrayArgument,且两个字面量名的 ResolvedVar1/ResolvedVar2 非空并**就是**
	/// IdentifierDictionary 里那个变量令牌(执行期不再二次 GetVariableToken 的全部前提)。
	/// 同时钉住实参仍是 SingleTerm,即执行期会走「两字面量」分支。
	/// </summary>
	[Fact]
	public void LiteralNames_AreResolvedToTheirTokens()
	{
		SpCopyArrayArgument arg = AssertAccepted("\"DAY\", \"MONEY\"");

		Assert.Same(Token("DAY"), arg.ResolvedVar1);
		Assert.Same(Token("MONEY"), arg.ResolvedVar2);
		Assert.IsType<SingleStrTerm>(arg.VarName1);
		Assert.IsType<SingleStrTerm>(arg.VarName2);
	}

	/// <summary>
	/// 运行期才定名的形态(变量书式而非常量串):解析期不比对、不解析,对应的 ResolvedVarN 为 null,
	/// 实参保持 VariableTerm,由 ARRAYCOPY 执行期的动态分支取字符串再查表。
	/// 三种书式都可达:字符串一维变量取下标、字符串标量变量、第 1 实参位同样如此。
	/// </summary>
	[Theory]
	[InlineData("\"DAY\", RESULTS:0", true)] // arg1 已解析、arg2 未解析
	[InlineData("RESULTS:0, \"DAY\"", false)] // arg1 未解析、arg2 已解析
	[InlineData("\"DAY\", SAVEDATA_TEXT", true)]
	public void RuntimeNamedArgument_LeavesResolvedVarNull(string argText, bool arg1IsLiteral)
	{
		SpCopyArrayArgument arg = AssertAccepted(argText);

		VariableToken? resolved = arg1IsLiteral ? arg.ResolvedVar1 : arg.ResolvedVar2;
		VariableToken? unresolved = arg1IsLiteral ? arg.ResolvedVar2 : arg.ResolvedVar1;
		AExpression unresolvedName = arg1IsLiteral ? arg.VarName2 : arg.VarName1;

		Assert.Same(Token("DAY"), resolved);
		Assert.Null(unresolved);
		// 未解析的名字不是 SingleTerm ⇒ 执行期走「非两字面量」分支(故 ResolvedVar 为 null 不等于悬空)
		Assert.False(unresolvedName is SingleTerm);
	}

	/// <summary>
	/// 解析期已折叠为常量串的实参同样进入解析分支:名字不必在源码里写成引号字面量,
	/// 只要 checkArgumentType 的 Restructure 把它折成 SingleStrTerm。这条钉住 ResolvedVar 的
	/// 真实判据(常量串),免得后来者以为判据是「源码字面量」而把折叠后的实参漏掉。
	/// </summary>
	[Fact]
	public void FoldedConstantString_IsResolvedAsLiteralName()
	{
		SpCopyArrayArgument arg = AssertAccepted("\"DAY\", \"MO\" + \"NEY\"");

		Assert.Same(Token("MONEY"), arg.ResolvedVar2);
		Assert.IsType<SingleStrTerm>(arg.VarName2);
	}

	/// <summary>
	/// 一侧未解析时不做形状比对(CheckSameShape 的前提是两个令牌都非空):字符串一维 + 运行期定名
	/// 在解析期不报 DifferentArraycopyArgsType,该比对留给执行期的动态分支。
	/// </summary>
	[Fact]
	public void ShapeCheck_IsSkippedWhenOneSideIsRuntimeNamed()
	{
		SpCopyArrayArgument arg = AssertAccepted("\"SAVESTR\", RESULTS:0");

		Assert.Same(Token("SAVESTR"), arg.ResolvedVar1);
		Assert.Null(arg.ResolvedVar2);
	}

	// ── 告警级别与实参读法 ──────────────────────────────────────────────────

	/// <summary>
	/// level 1 的告警不标错行,实参照常构造:多写一个尾实参走 checkArgumentType 的 TooManyArg(level 1),
	/// 超出规则表长度的部分被弃用后仍按前两个实参构造。这既是既有语义的正控制,也是下面各条用例
	/// 「IsError 即 level >= 2」这一观察口径的对照。
	/// </summary>
	[Fact]
	public void LevelOneWarning_DoesNotMarkTheLineAndStillBuilds()
	{
		(bool built, InstructionLine line) = Parse("\"DAY\", \"MONEY\", 999");

		Assert.True(built);
		Assert.False(line.IsError);
		SpCopyArrayArgument arg = Assert.IsType<SpCopyArrayArgument>(line.Argument);
		Assert.Same(Token("DAY"), arg.ResolvedVar1);
		Assert.Same(Token("MONEY"), arg.ResolvedVar2);
	}

	/// <summary>
	/// ARRAYCOPY 的两个实参在规则表里是文字列式,裸变量名按操作数类型被拒(第 N 引数の型が正しくありません)。
	/// 这条只钉住既有读法边界:名字写成常量串才可能被解析成变量令牌。
	/// </summary>
	[Theory]
	[InlineData("DAY, \"MONEY\"", "1")]
	[InlineData("\"DAY\", MONEY", "2")]
	public void BareVariableName_IsRejectedByOperandType(string argText, string slot)
		=> AssertRejectedWith(argText, trerror.IncorrectArg, slot);

	// ── 夹具 ────────────────────────────────────────────────────────────────

	private static ScriptPosition Pos => new("test.erb", 1);

	/// <summary>按装载期的路由解析一行 ARRAYCOPY(参数文本原样接在指令名之后)并构造实参。</summary>
	private static (bool Built, InstructionLine Line) Parse(string argText)
	{
		LogicalLine? parsed = LogicalLineParser.ParseLine(new CharStream("ARRAYCOPY " + argText), Pos, null);
		InstructionLine line = Assert.IsType<InstructionLine>(parsed);
		bool built = ArgumentParser.SetArgumentTo(line);
		return (built, line);
	}

	/// <summary>断言被拒:构造失败、行被标错(level >= 2)、ErrMes 含指定告警键的文案、Argument 保持 null。</summary>
	private static void AssertRejectedWith(string argText, Lang.TranslatableString expected, params object?[] formatArgs)
	{
		(bool built, InstructionLine line) = Parse(argText);
		Assert.False(built, $"ARRAYCOPY {argText} 应被拒绝");
		Assert.True(line.IsError, $"ARRAYCOPY {argText} 的行应被标错(level >= 2)");
		Assert.Contains(string.Format(expected.Text, formatArgs), line.ErrMes);
		Assert.Null(line.Argument);
	}

	/// <summary>断言合法输入构造出 SpCopyArrayArgument。</summary>
	private static SpCopyArrayArgument AssertAccepted(string argText)
	{
		(bool built, InstructionLine line) = Parse(argText);
		Assert.True(built, $"ARRAYCOPY {argText} 应被接受,实际报错:{line.ErrMes}");
		Assert.False(line.IsError, line.ErrMes);
		return Assert.IsType<SpCopyArrayArgument>(line.Argument);
	}

	/// <summary>取保留变量令牌——与生产码 TryResolveArrayArg 同一张表、同一入口。</summary>
	private static VariableToken Token(string name)
	{
		VariableToken? token = GlobalStatic.IdentifierDictionary.GetVariableToken(name, null, true);
		Assert.NotNull(token);
		return token!;
	}
}
