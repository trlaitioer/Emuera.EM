using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// FunctionMethod.CheckArgumentTypeEx 的实参契约校验(arg-contract issues/01、02)回归网。缝是解析期契约入口:
/// IdentifierDictionary.GetFunctionMethod 校验不过即抛 CodeEE(用法同 UnguardedAsCastTests)。
/// 覆盖两处已核实的判据缺陷:
/// 1) ArgType.Array3D 读成 Array2D ⇒ RefInt3D 槽维度不校验,标量/一维变量被放行
/// 2) OmitStart 未写(= -1)时省略检查恒假 ⇒ 该槽的 null 一律放行
/// 另有 ReduceArguments 的形状用例:null 元素只由「显式空槽」产生,数量上的省略只让列表更短——
/// 这是两处修复共同依赖的前提。维度用例覆盖 3D/2D 通过、1D/标量被拒四个方向,避免只钉住单向。
/// </summary>
public class CheckArgumentTypeEx
{
	public CheckArgumentTypeEx() => TestBootstrap.Initialize();

	// ── 前提:显式空槽与数量省略的区别(两处修复共同依赖) ─────────────────

	[Fact]
	public void Shape_WithoutTrailingComma_HasNoNull()
	{
		List<AExpression?> args = Shape("1,2");
		Assert.Equal(2, args.Count);
		Assert.All(args, a => Assert.NotNull(a));
	}

	/// <summary>单个尾逗号在循环底部被吃掉,故不产生 null(只有数量语义,无空槽语义)。</summary>
	[Fact]
	public void Shape_WithSingleTrailingComma_IsEaten()
	{
		List<AExpression?> args = Shape("1,2,");
		Assert.Equal(2, args.Count);
		Assert.All(args, a => Assert.NotNull(a));
	}

	/// <summary>第二个逗号起就是真正的空槽:长度不变、该槽为 null。</summary>
	[Fact]
	public void Shape_WithDoubleTrailingComma_HasTrailingNull()
	{
		List<AExpression?> args = Shape("1,2,,");
		Assert.Equal(3, args.Count);
		Assert.Null(args[2]);
	}

	[Fact]
	public void Shape_WithMiddleEmptySlot_HasMiddleNull()
	{
		List<AExpression?> args = Shape("1,,2");
		Assert.Equal(3, args.Count);
		Assert.Null(args[1]);
	}

	[Fact]
	public void Shape_WithLeadingEmptySlot_HasLeadingNull()
	{
		List<AExpression?> args = Shape(",2");
		Assert.Equal(2, args.Count);
		Assert.Null(args[0]);
	}

	// ── issues/02:OmitStart 未写时省略检查恒假 ──────────────────────────────

	/// <summary>SETVAR 规则 {String, Any} 未写 OmitStart,故第 2 槽不可空(SetVarMethod.GetIntValue 直接 Required(1))。</summary>
	[Fact]
	public void SetVar_WithOmittedValue_IsRejected()
		=> AssertRejectedWith("SETVAR", trerror.ArgCanNotBeNull, 2,
			new SingleStrTerm("TARGET"), null);

	/// <summary>DT_ROW_REMOVE 规则 {String, RefInt1D, Int} 未写 OmitStart(DataTableRowRemoveMethod.GetIntValue 直接转型)。</summary>
	[Fact]
	public void DataTableRowRemove_WithOmittedArray_IsRejected()
		=> AssertRejectedWith("DT_ROW_REMOVE", trerror.ArgCanNotBeNull, 2,
			new SingleStrTerm("t"), null, new SingleLongTerm(1));

	/// <summary>REPLACE 规则 {String, String, RefString1D|AllowConstRef, Int} 未写 OmitStart(ReplaceMethod.GetIntValue 直接 Required(2))。</summary>
	[Fact]
	public void Replace_WithOmittedArray_IsRejected()
		=> AssertRejectedWith("REPLACE", trerror.ArgCanNotBeNull, 3,
			new SingleStrTerm("a"), new SingleStrTerm("b"), null, new SingleLongTerm(0));

	/// <summary>正控制:完整实参仍通过(收紧不得误伤)。</summary>
	[Fact]
	public void SetVar_WithValue_IsAccepted()
		=> AssertAccepted("SETVAR", new SingleStrTerm("TARGET"), new SingleLongTerm(1));

	/// <summary>正控制:数量上的省略(少传尾实参)是既有语义,列表更短而非含 null,必须继续接受。</summary>
	[Fact]
	public void Rand_WithMaximumOnly_IsAccepted()
		=> AssertAccepted("RAND", new SingleLongTerm(5));

	/// <summary>正控制:同上,HTML_STRINGLEN 的 OmitStart = 1。</summary>
	[Fact]
	public void HtmlStringLen_WithLengthOnly_IsAccepted()
		=> AssertAccepted("HTML_STRINGLEN", new SingleStrTerm("x"));

	// ── issues/01:ArgType.Array3D 读错标志位 ───────────────────────────────

	/// <summary>
	/// 二维在三段结构里是**有意支持**的:GraphicsDrawGMethod 构造器之后那段被注释掉的旧手工检查要求
	/// (!IsArray2D &amp;&amp; !IsArray3D) 即拒绝,故 RefInt2D 与 RefInt3D 两段合起来正是「二维或三维」,
	/// 规则本身无需改动——这是 issue 01 要求的裁定,此处以用例钉住。
	/// </summary>
	[Fact]
	public void GraphicsDrawG_WithTwoDimensionalIntVariableInColormatrixSlot_IsAccepted()
		=> AssertAccepted("GDRAWG", GraphicsDrawGArgs(Variable("DA:0:0")));

	/// <summary>三维是该槽原本唯一支持的形态;修复不得误伤(修前也通过——dims = 0 的 case 接受任何整型变量)。</summary>
	[Fact]
	public void GraphicsDrawG_WithThreeDimensionalIntVariableInColormatrixSlot_IsAccepted()
		=> AssertAccepted("GDRAWG", GraphicsDrawGArgs(Variable("TA:0:0:0")));

	/// <summary>一维整型变量维度不符:RefInt2D 段与 RefInt3D 段都应拒绝(修前被 RefInt3D 段放行)。</summary>
	[Fact]
	public void GraphicsDrawG_WithOneDimensionalIntVariableInColormatrixSlot_IsRejected()
		=> AssertRejectedWith("GDRAWG", trerror.ArgIsNotNDIntArray, 11, 3,
			GraphicsDrawGArgs(Variable("CFLAG:0")));

	/// <summary>
	/// 标量整型变量:两段的维度错误文案都应出现,其中 dims = 3 那条证明 RefInt3D 段确实算出了三维要求
	/// (读错标志位时它是 dims = 0,只在类型上比对,于是放行)。
	/// </summary>
	[Fact]
	public void GraphicsDrawG_WithScalarIntVariableInColormatrixSlot_IsRejected()
		=> AssertRejectedWith("GDRAWG", trerror.ArgIsNotNDIntArray, 11, 3,
			GraphicsDrawGArgs(Variable("RESULT")));

	/// <summary>
	/// 正控制:按数量省略 colormatrix(10 实参)是既有语义——GraphicsDrawGMethod.GetIntValue 正是以
	/// arguments.Count == 10 表达「无颜色矩阵」,该类被注释掉的旧检查里也有 arguments.Count == 10 → return null。
	/// </summary>
	[Fact]
	public void GraphicsDrawG_WithOmittedColormatrix_IsAccepted()
		=> AssertAccepted("GDRAWG", GraphicsDrawGArgs());

	// ── 夹具 ────────────────────────────────────────────────────────────────

	/// <summary>
	/// 解析一段调用实参(传括号内的文本)。词法器要求括号配平,故这里补上外层括号,
	/// 再按生产路径(ExpressionParser.reduceIdentifier)跳过 '(' 后调 ReduceArguments。
	/// </summary>
	private static List<AExpression?> Shape(string inner)
	{
		WordCollection wc = LexicalAnalyzer.Analyse(
			new CharStream("(" + inner + ")"), LexEndWith.EoL, LexAnalyzeFlag.None);
		Assert.Equal('(', Assert.IsType<SymbolWord>(wc.Current).Type);
		wc.ShiftNext();
		return ExpressionParser.ReduceArguments(wc, ArgsEndWith.RightParenthesis, false);
	}

	/// <summary>GDRAWG 的槽 0..9 全是 ArgType.Int,槽 10 才是颜色矩阵。</summary>
	private static AExpression?[] GraphicsDrawGArgs(AExpression? colormatrix = null)
	{
		int count = colormatrix == null ? 10 : 11;
		var args = new AExpression?[count];
		for (int i = 0; i < 10; i++)
		{
			args[i] = new SingleLongTerm(i);
		}

		if (colormatrix != null)
		{
			args[10] = colormatrix;
		}

		return args;
	}

	/// <summary>按生产路径解析变量书式,得到 Reference 槽需要的 VariableTerm。</summary>
	private static AExpression Variable(string text)
	{
		AExpression term = ExpressionParser.ReduceExpressionTerm(
			LexicalAnalyzer.Analyse(new CharStream(text), LexEndWith.EoL, LexAnalyzeFlag.None),
			TermEndWith.EoL)!;
		VariableTerm variable = Assert.IsType<VariableTerm>(term);
		// Ref 槽还要求非计算变量;这条前置不成立时本夹具会先失败,而不是伪装成「检查器放行」
		Assert.False(variable.Identifier.IsCalc, $"{text} 是计算变量,不适合做 Ref 槽夹具");
		return variable;
	}

	/// <summary>名字写错会让 GetFunctionMethod 返回 null 而非抛错,故先断言方法确实注册。</summary>
	private static void AssertMethodExists(string name)
		=> Assert.True(FunctionMethodCreator.GetMethodList().ContainsKey(name), $"内建函数 {name} 未注册");

	private static AExpression? Get(string name, AExpression?[] args)
	{
		AssertMethodExists(name);
		return GlobalStatic.IdentifierDictionary.GetFunctionMethod(null, name, [.. args], false);
	}

	private static void AssertAccepted(string name, params AExpression?[] args)
		=> Assert.NotNull(Get(name, args));

	/// <summary>断言被拒,且文案里含指定槽位的错误文案——只断言 CodeEE 的话,把槽位或维度写错也能通过。
	/// 规则块多于一段时文案由各段错误以 " | " 连接,故用 Contains。</summary>
	private static void AssertRejectedWith(string name, Lang.TranslatableString mes, int slot, params AExpression?[] args)
	{
		CodeEE ex = Assert.Throws<CodeEE>(() => Get(name, args));
		Assert.Contains(string.Format(mes.Text, name, slot), ex.Message);
	}

	/// <summary>同上,但错误文案带维度参数(ArgIsNotNDIntArray 形如 name/slot/dims)。</summary>
	private static void AssertRejectedWith(string name, Lang.TranslatableString mes, int slot, int dims, params AExpression?[] args)
	{
		CodeEE ex = Assert.Throws<CodeEE>(() => Get(name, args));
		Assert.Contains(string.Format(mes.Text, name, slot, dims), ex.Message);
	}
}
