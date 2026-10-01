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
/// FunctionMethod.CheckArgumentTypeEx 的实参契约校验(arg-contract/issues/01、arg-contract/issues/02、arg-contract/issues/03)回归网。缝是解析期契约入口:
/// IdentifierDictionary.GetFunctionMethod 校验不过即抛 CodeEE(用法同 UnguardedAsCastTests)。
/// 覆盖四处已核实的判据缺陷:
/// 1) ArgType.Array3D 读成 Array2D ⇒ RefInt3D 槽维度不校验,标量/一维变量被放行
/// 2) OmitStart 未写(= -1)时省略检查恒假 ⇒ 该槽的 null 一律放行
/// 3) 设计上可省略(OmitStart 之后)的槽收到显式空槽时检查器放行,函数体却直接取值 ⇒ NRE
/// 4) 规则表把函数体无条件读取的槽划进可省略区间 ⇒ 少写尾实参也让列表短于该下标,函数体越界
/// 另有 ReduceArguments 的形状用例:null 元素只由「显式空槽」产生,数量上的省略只让列表更短——
/// 这是四处修复共同依赖的前提。维度用例覆盖 3D/2D 通过、1D/标量被拒四个方向,避免只钉住单向。
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

	// ── arg-contract/issues/02:OmitStart 未写时省略检查恒假 ──────────────────────────────

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

	// ── arg-contract/issues/03:设计上可省略 ≠ 该槽可以为 null(片 1 的三处可达 NRE) ────────

	/// <summary>
	/// OmitStart 只表达「最少传几个」;显式空槽(第二个逗号起)是另一回事,原先在可省略区间内被放行,
	/// 而函数体用 arguments.Required(i) 直接取值 ⇒ NRE。这三处已举证的站点改由检查器拒绝,
	/// 语义是「规则表里没有默认值声明处,空槽按错误处理」。
	/// 文案里的槽位是 1 基,等于列表索引 + 1。
	/// 同一判据后来也铺到「补车道」的槽(见表尾):那些槽不由字面量 Required(i) 读取,而由
	/// ReadPoint/ReadColormatrix 之类的 helper 或变长下标读取,空槽一样会 NRE。
	/// </summary>
	[Theory]
	[InlineData("RAND", "1,,", 2)]
	[InlineData("HTML_STRINGLEN", "\"x\",,", 2)]
	[InlineData("REGEXPMATCH", "\"a\",\"b\",,", 3)]
	// —— 片 2-A：A 表逐槽铺 ArgType.NotNull 后,下列各槽的显式空槽同样被拒 ——
	// 多段类每个使该槽可省略的注册名各一行;多段共享同一数组时只列一个名(如 DT_CELL_* 共用 DataTableCellGetMethod)。
	[InlineData("XML_GET", "1,\"a\",,", 3)]
	[InlineData("XML_GET_BYNAME", "1,\"a\",,", 3)]
	[InlineData("XML_GET", "1,\"a\",1,,", 4)]
	[InlineData("XML_GET_BYNAME", "1,\"a\",1,,", 4)]
	[InlineData("ENUMFILES", "\"a\",,", 2)]
	[InlineData("ENUMFILES", "\"a\",\"b\",,", 3)]
	// 批次 2:VARSETEX / XML_SET(_BYNAME) / XML_ADDNODE(_BYNAME) / XML_REMOVENODE(_BYNAME) / XML_REPLACE(_BYNAME)
	[InlineData("VARSETEX", "\"X\",1,,", 3)]
	[InlineData("VARSETEX", "\"X\",1,1,,", 4)]
	[InlineData("VARSETEX", "\"X\",1,1,1,,", 5)]
	[InlineData("XML_SET", "1,\"a\",\"b\",,", 4)]
	[InlineData("XML_SET", "1,\"a\",\"b\",1,,", 5)]
	[InlineData("XML_SET_BYNAME", "\"a\",\"b\",\"c\",,", 4)]
	[InlineData("XML_SET_BYNAME", "\"a\",\"b\",\"c\",1,,", 5)]
	[InlineData("XML_ADDNODE", "1,\"a\",\"b\",,", 4)]
	[InlineData("XML_ADDNODE_BYNAME", "\"a\",\"b\",\"c\",,", 4)]
	[InlineData("XML_REMOVENODE", "1,\"a\",,", 3)]
	[InlineData("XML_REMOVENODE_BYNAME", "\"a\",\"b\",,", 3)]
	[InlineData("XML_REPLACE", "1,\"a\",\"b\",,", 4)]
	// 批次 2b:属性段——XML_ADDATTRIBUTE/XML_REMOVEATTRIBUTE 走 XmlAddNodeMethod/XmlRemoveNodeMethod 的 Attribute 分支
	[InlineData("XML_ADDATTRIBUTE", "1,\"a\",\"b\",,", 4)]
	[InlineData("XML_ADDATTRIBUTE_BYNAME", "\"a\",\"b\",\"c\",,", 4)]
	[InlineData("XML_REMOVEATTRIBUTE", "1,\"a\",,", 3)]
	[InlineData("XML_REMOVEATTRIBUTE_BYNAME", "\"a\",\"b\",,", 3)]
	[InlineData("XML_REPLACE_BYNAME", "\"a\",\"b\",\"c\",,", 4)]
	// 批次 3:DataTable 族与 MAP_GETKEYS。DT_ROW_ADD 的空槽落在中间(name/value 对的第一项)。
	[InlineData("DT_COLUMN_ADD", "\"t\",\"c\",,", 3)]
	[InlineData("DT_COLUMN_ADD", "\"t\",\"c\",1,,", 4)]
	[InlineData("DT_CELL_GET", "\"t\",1,\"c\",,", 4)]
	[InlineData("DT_CELL_SET", "\"t\",1,\"c\",1,,", 5)]
	[InlineData("MAP_GETKEYS", "\"m\",,", 2)]
	[InlineData("DT_ROW_ADD", "\"t\",,\"c\"", 2)]
	// 批次 4a:Getnum / HtmlGetPrintedStr / Graphics 族。ARRAYMSORTEX[3] 的规则段自带 NotNull,此行是回归钉子。
	[InlineData("GETNUM", "DA:0:0,\"a\",,", 3)]
	[InlineData("HTML_GETPRINTEDSTR", ",", 1)]
	[InlineData("GSETFONT", "0,\"f\",12,,", 4)]
	[InlineData("GGETTEXTSIZE", "\"a\",\"b\",1,,", 4)]
	[InlineData("GCREATEFROMFILE", "0,\"f\",,", 3)]
	[InlineData("CBGSETBUTTONSPRITE", "0,\"a\",\"b\",1,1,1,,", 7)]
	[InlineData("ARRAYMSORTEX", "CFLAG:0,STR:0,1,,", 4)]
	// 批次 4b:文件读写 / 函数查询 / 输入流 / 输出日志
	[InlineData("SAVETEXT", "\"f\",1,,", 3)]
	[InlineData("SAVETEXT", "\"f\",1,1,,", 4)]
	[InlineData("LOADTEXT", "1,,", 2)]
	[InlineData("LOADTEXT", "1,1,,", 3)]
	[InlineData("EXISTFUNCTION", "\"a\",,", 2)]
	[InlineData("ERDNAME", "DA:0:0,1,,", 3)]
	[InlineData("FLOWINPUT", "1,,", 2)]
	[InlineData("FLOWINPUT", "1,1,,", 3)]
	[InlineData("FLOWINPUT", "1,1,1,,", 4)]
	[InlineData("FLOWINPUTS", "1,,", 2)]
	[InlineData("OUTPUTLOG", ",\"a\"", 1)]
	[InlineData("OUTPUTLOG", "\"a\",,", 2)]
	// 批次 4c:REPLACE 槽 3(预筛漏在批次外,补上)
	[InlineData("REPLACE", "\"a\",\"b\",\"c\",,", 4)]
	// —— A10 形态补全:上面各行覆盖「空槽在末位」(f(a,,))与「空槽在首位」(f(,a)),下面补「空槽在中间」(f(a,,c))。
	// 中位可达的条件是该槽所在的可省略段还有后继槽(argc > i + 1);一槽可省且它就是末槽的类(RAND、HTML_STRINGLEN、
	// XmlRemoveNodeMethod 族等)无中位形态。各行只断言检查器拒绝,故后继槽的字面量不必符合该段类型——判据循环在空槽处已 break。
	[InlineData("XML_GET", "1,\"a\",,0", 3)]
	[InlineData("XML_GET_BYNAME", "1,\"a\",,0", 3)]
	[InlineData("ENUMFILES", "\"a\",,1", 2)]
	[InlineData("ENUMFILES", "\"a\",\"b\",,STR:0", 3)]
	[InlineData("VARSETEX", "\"X\",1,,1", 3)]
	[InlineData("VARSETEX", "\"X\",1,1,,1", 4)]
	[InlineData("XML_SET", "1,\"a\",\"b\",,0", 4)]
	[InlineData("XML_SET_BYNAME", "\"a\",\"b\",\"c\",,0", 4)]
	[InlineData("XML_ADDNODE", "1,\"a\",\"b\",,1", 4)]
	[InlineData("XML_ADDNODE_BYNAME", "\"a\",\"b\",\"c\",,1", 4)]
	[InlineData("XML_ADDATTRIBUTE", "1,\"a\",\"b\",,1", 4)]
	[InlineData("XML_ADDATTRIBUTE_BYNAME", "\"a\",\"b\",\"c\",,1", 4)]
	[InlineData("DT_COLUMN_ADD", "\"t\",\"c\",,1", 3)]
	[InlineData("SAVETEXT", "\"f\",1,,1", 3)]
	[InlineData("LOADTEXT", "1,,1", 2)]
	[InlineData("FLOWINPUT", "1,,1", 2)]
	[InlineData("FLOWINPUT", "1,1,,1", 3)]
	[InlineData("OUTPUTLOG", ",1", 1)]
	// —— A10 同槽的「更长实参表」形态:上两处(REGEXPMATCH、MAP_GETKEYS)的可省略段里该槽**就是末槽**,
	// 不存在中位形态;它们的 4/3 实参写法只落在同一规则表的另一段(无 OmitStart)上,由 arg-contract/issues/02 的判据
	// (而非本票新铺的 NotNull)拒 null。此处单列,不与中位形态混计。
	[InlineData("REGEXPMATCH", "\"a\",\"b\",,STR:0", 3)]
	[InlineData("MAP_GETKEYS", "\"m\",,1", 2)]
	// —— 补车道(未被 Required(字面量) 读取的可省略槽):这些槽由 helper(ReadPoint/ReadColormatrix)或
	// 变长下标 Required(i) 读取,空槽同样直接解引用,故一并标 ArgType.NotNull。数组形态的槽位是 1 基。
	[InlineData("GDRAWTEXT", "0,\"t\",,1", 3)] // ReadPoint(argNo=2) 读槽 2/3
	[InlineData("GDRAWTEXT", "0,\"t\",1,,", 4)]
	[InlineData("GDRAWGWITHROTATE", "0,1,90,,1", 4)] // ReadPoint(argNo=3) 读槽 3/4
	[InlineData("GDRAWGWITHROTATE", "0,1,90,1,,", 5)]
	[InlineData("GDRAWSPRITE", "0,\"i\",1,2,3,4,,", 7)] // ReadColormatrix(argNo=6)
	[InlineData("XML_ADDNODE", "1,\"a\",\"b\",1,,", 5)] // setAllPos 读槽 4(Node 段)
	[InlineData("XML_ADDNODE_BYNAME", "\"a\",\"b\",\"c\",1,,", 5)]
	[InlineData("XML_ADDATTRIBUTE", "1,\"a\",\"b\",\"c\",,1", 5)] // methodPos 读槽 4(Attribute 段)
	[InlineData("XML_ADDATTRIBUTE_BYNAME", "\"a\",\"b\",\"c\",\"d\",,1", 5)]
	[InlineData("XML_ADDATTRIBUTE", "1,\"a\",\"b\",\"c\",1,,", 6)] // setAllPos 读槽 5(Attribute 段)
	[InlineData("XML_ADDATTRIBUTE_BYNAME", "\"a\",\"b\",\"c\",\"d\",1,,", 6)]
	public void OmittableSlot_WithExplicitEmptySlot_IsRejected(string name, string inner, int slot)
		=> AssertRejectedWith(name, trerror.ArgCanNotBeNull, slot, [.. Shape(inner)]);

	/// <summary>
	/// DT_ROW_ADD 的槽 1 是 <see cref="ArgTypeList.MatchVariadicGroup"/> 的变长对(name/value)首项,故它的
	/// 末位形态 `DT_ROW_ADD("t",,)`(列表长 2)先被「变长组不整除」判据拒掉,够不到 null 判据——
	/// 该槽因此没有末位形态可断言,只有中位形态(上面那行)。
	/// </summary>
	[Fact]
	public void DataTableRowAdd_WithTailEmptySlot_IsRejectedByVariadicGroup()
	{
		CodeEE ex = Assert.Throws<CodeEE>(() => Get("DT_ROW_ADD", [.. Shape("\"t\",,")]));
		Assert.Contains(string.Format(trerror.ArgsNotFitExpr.Text, "DT_ROW_ADD", 2, 1, 2), ex.Message);
	}

	/// <summary>
	/// 反例(不标 NotNull):预筛把「类内全部可省略段」与站点做笛卡尔匹配,`DataTableColumnManagementMethod[1]`
	/// 因此进过 A 表,但该站点在 `op == Names` 的早退之后;而 NAMES 的可省略段把 null 与「不传该实参」
	/// 同样解释为「写进 RESULTS 数组」(`arguments[1] is VariableTerm` 对 null 为假),今天能正常求值,
	/// 故保持可空——标了会把可用写法变成 CodeEE。
	/// </summary>
	[Fact]
	public void DataTableColumnNames_WithExplicitEmptyOutputSlot_IsAccepted()
		=> AssertAccepted("DT_COLUMN_NAMES", new SingleStrTerm("t"), null);

	/// <summary>正控制:批次 3 各槽填满或按数量省略后仍通过。</summary>
	[Fact]
	public void DataTableAndMapGetKeys_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("DT_COLUMN_ADD", new SingleStrTerm("t"), new SingleStrTerm("c"), new SingleLongTerm(1));
		AssertAccepted("DT_CELL_GET", new SingleStrTerm("t"), new SingleLongTerm(1), new SingleStrTerm("c"));
		AssertAccepted("DT_CELL_SET", new SingleStrTerm("t"), new SingleLongTerm(1), new SingleStrTerm("c"),
			new SingleLongTerm(1));
		AssertAccepted("MAP_GETKEYS", new SingleStrTerm("m"));
		AssertAccepted("DT_ROW_ADD", new SingleStrTerm("t"), new SingleStrTerm("c"), new SingleLongTerm(1));
	}

	/// <summary>正控制:批次 4a 各槽填满或按数量省略后仍通过。</summary>
	[Fact]
	public void GetnumAndGraphics_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("GETNUM", Variable("DA:0:0"), new SingleStrTerm("a"));
		AssertAccepted("HTML_GETPRINTEDSTR");
		AssertAccepted("HTML_GETPRINTEDSTR", new SingleLongTerm(1));
		AssertAccepted("ARRAYMSORTEX", Variable("CFLAG:0"), Variable("STR:0"), new SingleLongTerm(1));
		AssertAccepted("GGETTEXTSIZE", new SingleStrTerm("a"), new SingleStrTerm("b"), new SingleLongTerm(1));
		AssertAccepted("GCREATEFROMFILE", new SingleLongTerm(0), new SingleStrTerm("f"));
		AssertAccepted("CBGSETBUTTONSPRITE", new SingleLongTerm(0), new SingleStrTerm("a"), new SingleStrTerm("b"),
			new SingleLongTerm(1), new SingleLongTerm(1), new SingleLongTerm(1));
	}

	/// <summary>正控制:批次 4b 各槽填满或按数量省略后仍通过(OUTPUTLOG 的 OmitStart = 0 即零实参形态)。</summary>
	[Fact]
	public void FileAndFlowAndOutputlog_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("SAVETEXT", new SingleStrTerm("f"), new SingleLongTerm(1));
		AssertAccepted("LOADTEXT", new SingleLongTerm(1));
		AssertAccepted("EXISTFUNCTION", new SingleStrTerm("a"));
		AssertAccepted("ERDNAME", Variable("DA:0:0"), new SingleLongTerm(1));
		AssertAccepted("FLOWINPUT", new SingleLongTerm(1));
		AssertAccepted("FLOWINPUTS", new SingleLongTerm(1));
		AssertAccepted("OUTPUTLOG");
		AssertAccepted("OUTPUTLOG", new SingleStrTerm("a"));
	}

	/// <summary>正控制:REPLACE 的 3 实参(不写 type)形态继续通过。</summary>
	[Fact]
	public void Replace_WithThreeArgs_IsAccepted()
		=> AssertAccepted("REPLACE", new SingleStrTerm("a"), new SingleStrTerm("b"), new SingleStrTerm("c"));

	// ── arg-contract/issues/03 片 2-B:B 表 34 个槽裁定为「有意可空」,逐槽钉子 ──────────────
	// 空槽与「不传该实参」同义:函数体把它读成默认值(或走明确分支),故规则表不加 ArgType.NotNull,
	// 这些形态今天就能求值,标了反而会把可用写法变成 CodeEE。各槽的默认语义写在规则段注释里。

	/// <summary>
	/// 逐槽断言检查器**放行**显式空槽(槽位即上表;实参文本里 "," 的两个逗号给出该槽的 null)。
	/// 与 <see cref="OmittableSlot_WithExplicitEmptySlot_IsRejected"/> 成对:A 表拒绝、B 表放行,
	/// 两个表覆盖了「被 Required(字面量) 读取的可省略槽」全集 76 项;两表末尾另有「补车道」
	/// (未被 Required(字面量) 读取、改由 helper 或变长下标读取的可省略槽)的追加行。
	/// </summary>
	[Theory]
	[InlineData("ARRAYMSORTEX", "CFLAG:0,STR:0,,1")] // [2] 升序
	[InlineData("CSVBASE", "0,0,,")] // CsvDataMethod[2] z = 0
	[InlineData("CSVNAME", "0,,")] // CsvStrDataMethod[1] y = 0
	[InlineData("CSVCSTR", "0,0,,")] // CsvcstrMethod[2] z = 0
	[InlineData("ENCODETOUNI", "\"a\",,")] // [1] position = 0
	[InlineData("EXISTCSV", "0,,")] // [1] isSp = false
	[InlineData("FINDELEMENT", "CFLAG:0,1,,3")] // [2] start = 0
	[InlineData("FINDELEMENT", "CFLAG:0,1,2,,")] // [3] end = 数组长度
	[InlineData("FINDELEMENT", "CFLAG:0,1,2,3,,")] // [4] isExact = false
	[InlineData("FIND_CHARADATA", ",")] // FindFilesMethod[0] pattern = "*"
	[InlineData("FINDCHARA", "CFLAG:0,0,,5")] // [2] startindex = 0
	[InlineData("FINDCHARA", "CFLAG:0,0,1,,")] // [3] lastindex = CHARANUM
	[InlineData("GETMETH", "\"NOPE\",,")] // GetMethMethod[1] 无兜底值(目标未定义时抛 CodeEE)
	[InlineData("GETMETHS", "\"NOPE\",,")] // GetMethsMethod[1] 同上
	[InlineData("GETCHARA", "0,,")] // [1] CheckSp = false
	[InlineData("INRANGEARRAY", "CFLAG:0,1,2,,4")] // [3] start = 0
	[InlineData("INRANGEARRAY", "CFLAG:0,1,2,3,,")] // [4] end = 数组长度
	[InlineData("STRJOIN", "DA:0:0,,")] // JoinMethod[1] 分隔符 = ","
	[InlineData("STRJOIN", "DA:0:0,\"-\",,")] // JoinMethod[2] index1 = 0
	[InlineData("STRJOIN", "DA:0:0,\"-\",1,,")] // JoinMethod[3] index2 = 余长
	[InlineData("MATCH", "CFLAG:0,1,,3")] // [2] start = 0
	[InlineData("MATCH", "CFLAG:0,1,2,,")] // [3] end = 数组长度
	[InlineData("MAXARRAY", "CFLAG:0,,2")] // [1] start = 0
	[InlineData("MAXARRAY", "CFLAG:0,1,,")] // [2] end = 数组长度
	[InlineData("MONEYSTR", "1,,")] // [1] 用 Config 的默认货币格式
	[InlineData("STRFIND", "\"a\",\"b\",,")] // [2] start = 0
	[InlineData("SUBSTRING", "\"abc\",,2")] // [1] start = 0
	[InlineData("SUBSTRING", "\"abc\",1,,")] // [2] length = -1(到末尾)
	[InlineData("SUBSTRINGU", "\"abc\",,2")] // [1] start = 0
	[InlineData("SUBSTRINGU", "\"abc\",1,,")] // [2] length = -1
	[InlineData("SUMARRAY", "DA:0:0,,2")] // [1] index1 = 0
	[InlineData("SUMARRAY", "DA:0:0,1,,")] // [2] index2 = 数组长度
	[InlineData("TOSTR", "1,,")] // [1] 默认整数格式
	[InlineData("VARSIZE", "\"X\",,")] // [1] dim = 0
	// —— A10 形态补全:同 A 表,上面各行缺的「末位空槽」形态补在这里。
	[InlineData("ARRAYMSORTEX", "CFLAG:0,STR:0,,")] // [2] 升序(已有行是中位形态)
	[InlineData("FINDELEMENT", "CFLAG:0,1,,")] // [2] start = 0(已有行是中位形态)
	[InlineData("FINDCHARA", "CFLAG:0,1,,")] // [2] startindex = 0(已有行是中位形态)
	[InlineData("INRANGEARRAY", "CFLAG:0,1,1,,")] // [3] start = 0(已有行是中位形态)
	[InlineData("MATCH", "CFLAG:0,1,,")] // [2] start = 0(已有行是中位形态)
	[InlineData("MAXARRAY", "CFLAG:0,,")] // [1] start = 0(已有行是中位形态)
	[InlineData("SUBSTRING", "\"abc\",,")] // [1] start = 0(已有行是中位形态)
	[InlineData("SUBSTRINGU", "\"abc\",,")] // [1] start = 0(已有行是中位形态)
	[InlineData("SUMARRAY", "DA:0:0,,")] // [1] index1 = 0(已有行是中位形态)
	[InlineData("DT_COLUMN_NAMES", "\"t\",,")] // [1] 空槽 = 写进 RESULTS 数组(已有钉子用的是实参表构造)
	// —— A10 形态补全:空槽在中间(空槽后仍有实参)的形态。
	[InlineData("FINDELEMENT", "CFLAG:0,1,1,,1")] // [3] end = 数组长度
	[InlineData("GETMETH", "\"NOPE\",,1")] // GetMethMethod[1] 后接转发给下游的 variadic 槽
	[InlineData("GETMETHS", "\"NOPE\",,1")] // GetMethsMethod[1] 同上
	[InlineData("STRJOIN", "DA:0:0,,1")] // JoinMethod[1] 分隔符 = ","
	[InlineData("STRJOIN", "DA:0:0,\"-\",,1")] // JoinMethod[2] index1 = 0
	// —— 补车道里裁定为「有意可空」的两槽:空槽在函数体走 `v == null` 分支写成 DBNull。
	[InlineData("DT_CELL_SET", "\"t\",1,\"c\",,")] // [3] 空槽 = 写 DBNull(DataTableCellSetMethod)
	[InlineData("DT_ROW_ADD", "\"t\",\"c\",,\"c2\",1")] // [2] 变长对的 value 位,空槽 = DBNull
	public void IntentionalEmptySlot_IsAccepted(string name, string inner)
		=> AssertAccepted(name, [.. Shape(inner)]);

	/// <summary>正控制:本批被收紧的槽填满后仍通过,且数量省略(列表更短)不受影响。</summary>
	[Fact]
	public void XmlGet_And_EnumFiles_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("XML_GET", new SingleLongTerm(1), new SingleStrTerm("a"));
		AssertAccepted("XML_GET", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleLongTerm(1),
			new SingleLongTerm(0));
		AssertAccepted("ENUMFILES", new SingleStrTerm("a"));
		AssertAccepted("ENUMFILES", new SingleStrTerm("a"), new SingleStrTerm("b"));
	}

	/// <summary>正控制:同上,批次 2 / 2b 的各注册名。</summary>
	[Fact]
	public void VarSetEx_And_XmlArgStyles_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("VARSETEX", new SingleStrTerm("X"), new SingleLongTerm(1));
		AssertAccepted("XML_SET", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleStrTerm("b"));
		AssertAccepted("XML_ADDNODE", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleStrTerm("b"));
		AssertAccepted("XML_REMOVENODE", new SingleLongTerm(1), new SingleStrTerm("a"));
		AssertAccepted("XML_REPLACE", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleStrTerm("b"));
		AssertAccepted("XML_ADDATTRIBUTE", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleStrTerm("b"),
			new SingleStrTerm("c"));
		AssertAccepted("XML_ADDATTRIBUTE_BYNAME", new SingleStrTerm("a"), new SingleStrTerm("b"), new SingleStrTerm("c"));
		AssertAccepted("XML_REMOVEATTRIBUTE", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleLongTerm(1));
		AssertAccepted("XML_REMOVEATTRIBUTE_BYNAME", new SingleStrTerm("a"), new SingleStrTerm("b"));
	}

	/// <summary>正控制:填满可省略槽、以及数量上的省略(列表更短、不含 null)都必须继续通过。</summary>
	[Fact]
	public void RegexpMatch_WithOptionAndWithOmittedOption_IsAccepted()
	{
		AssertAccepted("REGEXPMATCH", new SingleStrTerm("a"), new SingleStrTerm("b"), new SingleLongTerm(1));
		AssertAccepted("REGEXPMATCH", new SingleStrTerm("a"), new SingleStrTerm("b"));
	}

	// ── arg-contract/issues/03 片 4(A10):数量省略 ≠ 显式空槽 ──────────────────────────────

	/// <summary>
	/// 第三种写法 `f(a,)`(写足最小实参数后跟一个尾逗号):它**不产生空槽**——ReduceArguments 只吞单个尾逗号,
	/// 列表比写全时短一个、槽位全部非 null(见 <see cref="Shape_WithSingleTrailingComma_IsEaten"/>)。
	/// 故逐个注册名钉住「最小实参数形态必须被接受」(最小实参数 = 该注册名可用段 OmitStart 的最小值;
	/// 多段类按 ctor 分支取该注册名实际使用的那一段)。
	/// 这同时是 OmitStart 的护栏:把它抬成「连这个形态也拒」会立刻变红——`GSETFONT`/`HOTKEY_STATE`
	/// 的附修正是这类改动(<see cref="GSetFont_WithFontNameOnly_IsRejected"/>、<see cref="HotkeyState_WithStateOnly_IsRejected"/>)。
	/// 第三参是期望的实参个数,即该注册名的最小实参数。
	/// </summary>
	[Theory]
	[InlineData("RAND", "1,", 1)]
	[InlineData("HTML_STRINGLEN", "\"x\",", 1)]
	[InlineData("REGEXPMATCH", "\"a\",\"b\",", 2)]
	[InlineData("XML_GET", "1,\"a\",", 2)]
	[InlineData("XML_GET_BYNAME", "1,\"a\",", 2)]
	[InlineData("ENUMFILES", "\"a\",", 1)]
	[InlineData("VARSETEX", "\"X\",1,", 2)]
	[InlineData("XML_SET", "1,\"a\",\"b\",", 3)]
	[InlineData("XML_SET_BYNAME", "\"a\",\"b\",\"c\",", 3)]
	[InlineData("XML_ADDNODE", "1,\"a\",\"b\",", 3)]
	[InlineData("XML_ADDNODE_BYNAME", "\"a\",\"b\",\"c\",", 3)]
	[InlineData("XML_ADDATTRIBUTE", "1,\"a\",\"b\",", 3)]
	[InlineData("XML_ADDATTRIBUTE_BYNAME", "\"a\",\"b\",\"c\",", 3)]
	[InlineData("XML_REMOVENODE", "1,\"a\",", 2)]
	[InlineData("XML_REMOVENODE_BYNAME", "\"a\",\"b\",", 2)]
	[InlineData("XML_REMOVEATTRIBUTE", "1,\"a\",", 2)]
	[InlineData("XML_REMOVEATTRIBUTE_BYNAME", "\"a\",\"b\",", 2)]
	[InlineData("XML_REPLACE", "1,\"a\",\"b\",", 3)]
	[InlineData("XML_REPLACE_BYNAME", "\"a\",\"b\",\"c\",", 3)]
	[InlineData("DT_COLUMN_ADD", "\"t\",\"c\",", 2)]
	[InlineData("DT_COLUMN_NAMES", "\"t\",", 1)]
	[InlineData("DT_ROW_ADD", "\"t\",", 1)]
	[InlineData("MAP_GETKEYS", "\"m\",", 1)]
	[InlineData("DT_CELL_GET", "\"t\",1,\"c\",", 3)]
	[InlineData("DT_CELL_SET", "\"t\",1,\"c\",", 3)]
	[InlineData("GETNUM", "DA:0:0,\"a\",", 2)]
	[InlineData("HTML_GETPRINTEDSTR", "", 0)]
	[InlineData("GSETFONT", "0,\"f\",12,", 3)]
	[InlineData("GGETTEXTSIZE", "\"a\",\"b\",1,", 3)]
	[InlineData("GCREATEFROMFILE", "0,\"f\",", 2)]
	[InlineData("CBGSETBUTTONSPRITE", "0,\"a\",\"b\",1,1,1,", 6)]
	[InlineData("ARRAYMSORTEX", "CFLAG:0,STR:0,", 2)]
	[InlineData("SAVETEXT", "\"f\",1,", 2)]
	[InlineData("LOADTEXT", "1,", 1)]
	[InlineData("EXISTFUNCTION", "\"a\",", 1)]
	[InlineData("ERDNAME", "DA:0:0,1,", 2)]
	[InlineData("FLOWINPUT", "1,", 1)]
	[InlineData("FLOWINPUTS", "1,", 1)]
	[InlineData("OUTPUTLOG", "", 0)]
	[InlineData("REPLACE", "\"a\",\"b\",\"c\",", 3)]
	[InlineData("CSVBASE", "0,0,", 2)]
	[InlineData("CSVNAME", "0,", 1)]
	[InlineData("CSVCSTR", "0,0,", 2)]
	[InlineData("ENCODETOUNI", "\"a\",", 1)]
	[InlineData("EXISTCSV", "0,", 1)]
	[InlineData("FINDELEMENT", "CFLAG:0,1,", 2)]
	[InlineData("FIND_CHARADATA", "", 0)]
	[InlineData("FINDCHARA", "CFLAG:0,1,", 2)]
	[InlineData("GETMETH", "\"NOPE\",", 1)]
	[InlineData("GETMETHS", "\"NOPE\",", 1)]
	[InlineData("GETCHARA", "0,", 1)]
	[InlineData("INRANGEARRAY", "CFLAG:0,1,1,", 3)]
	[InlineData("STRJOIN", "DA:0:0,", 1)]
	[InlineData("MATCH", "CFLAG:0,1,", 2)]
	[InlineData("MAXARRAY", "CFLAG:0,", 1)]
	[InlineData("MONEYSTR", "1,", 1)]
	[InlineData("STRFIND", "\"a\",\"b\",", 2)]
	[InlineData("SUBSTRING", "\"abc\",", 1)]
	[InlineData("SUBSTRINGU", "\"abc\",", 1)]
	[InlineData("SUMARRAY", "DA:0:0,", 1)]
	[InlineData("TOSTR", "1,", 1)]
	[InlineData("VARSIZE", "\"X\",", 1)]
	// —— 补车道新收紧的三个注册名:数量省略(少写尾实参)必须继续合法,`NotNull` 只拒显式空槽。
	[InlineData("GDRAWTEXT", "0,\"t\",", 2)]
	[InlineData("GDRAWGWITHROTATE", "0,1,90,", 3)]
	[InlineData("GDRAWSPRITE", "0,\"i\",", 2)]
	public void CountOmittedArgument_IsAccepted_AndYieldsNoEmptySlot(string name, string inner, int count)
	{
		List<AExpression?> args = Shape(inner);
		Assert.Equal(count, args.Count);
		Assert.All(args, a => Assert.NotNull(a));
		AssertAccepted(name, [.. args]);
	}

	/// <summary>正控制:补车道被收紧的槽填满后仍通过(数量省略由上面的用例表逐名钉住)。</summary>
	[Fact]
	public void SupplementalSlots_WithFilledArgs_AreAccepted()
	{
		AssertAccepted("GDRAWTEXT", new SingleLongTerm(0), new SingleStrTerm("t"), new SingleLongTerm(1),
			new SingleLongTerm(2));
		AssertAccepted("GDRAWGWITHROTATE", new SingleLongTerm(0), new SingleLongTerm(1), new SingleLongTerm(90),
			new SingleLongTerm(1), new SingleLongTerm(2));
		AssertAccepted("GDRAWSPRITE", new SingleLongTerm(0), new SingleStrTerm("i"), new SingleLongTerm(1),
			new SingleLongTerm(2), new SingleLongTerm(3), new SingleLongTerm(4), Variable("DA:0:0"));
		AssertAccepted("XML_ADDNODE", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleStrTerm("b"),
			new SingleLongTerm(1), new SingleLongTerm(0));
		AssertAccepted("XML_ADDNODE_BYNAME", new SingleStrTerm("a"), new SingleStrTerm("b"), new SingleStrTerm("c"),
			new SingleLongTerm(1), new SingleLongTerm(0));
		AssertAccepted("XML_ADDATTRIBUTE", new SingleLongTerm(1), new SingleStrTerm("a"), new SingleStrTerm("b"),
			new SingleStrTerm("c"), new SingleLongTerm(1), new SingleLongTerm(0));
		AssertAccepted("XML_ADDATTRIBUTE_BYNAME", new SingleStrTerm("a"), new SingleStrTerm("b"), new SingleStrTerm("c"),
			new SingleStrTerm("d"), new SingleLongTerm(1), new SingleLongTerm(0));
		AssertAccepted("DT_CELL_SET", new SingleStrTerm("t"), new SingleLongTerm(1), new SingleStrTerm("c"),
			new SingleLongTerm(1));
		AssertAccepted("DT_ROW_ADD", new SingleStrTerm("t"), new SingleStrTerm("c"), new SingleLongTerm(1),
			new SingleStrTerm("c2"), new SingleLongTerm(2));
	}

	// ── arg-contract/issues/03 附:OmitStart 写得比函数体小的两处(数量省略即可越界) ──────

	/// <summary>
	/// 另一类缺陷:函数体无条件读取某槽,而规则表把它划进了可省略区间 ⇒ 少写尾实参也让列表短于该下标,
	/// 执行到函数体时越界。GSETFONT 的 fontsize 由 GetIntValue 无条件读取,故最小个数是 3;
	/// 这里 "3" 是 NotEnoughArgs 文案里的最小个数,不是槽位。
	/// </summary>
	[Fact]
	public void GSetFont_WithFontNameOnly_IsRejected()
		=> AssertRejectedWith("GSETFONT", trerror.NotEnoughArgs, 3,
			new SingleLongTerm(0), new SingleStrTerm("f"));

	/// <summary>显式空槽同理:槽 2 落回必填区间(i &lt; OmitStart),由同一处 OmitStart 一并拒绝。</summary>
	[Fact]
	public void GSetFont_WithOmittedFontSize_IsRejected()
		=> AssertRejectedWith("GSETFONT", trerror.ArgCanNotBeNull, 3,
			new SingleLongTerm(0), new SingleStrTerm("f"), null);

	/// <summary>正控制:3 实参(不写 fontStyle)与 4 实参都必须继续通过。</summary>
	[Fact]
	public void GSetFont_WithFontSize_IsAccepted()
	{
		AssertAccepted("GSETFONT", new SingleLongTerm(0), new SingleStrTerm("f"), new SingleLongTerm(12));
		AssertAccepted("GSETFONT", new SingleLongTerm(0), new SingleStrTerm("f"), new SingleLongTerm(12), new SingleLongTerm(1));
	}

	/// <summary>
	/// HOTKEY_STATE 的两个实参都要写入热键状态,没有可代的默认值 ⇒ 规则表要求恰好 2 个
	/// (OmitStart = 2 与槽数相等,即不可省略),原先的 OmitStart = 1 使一实参形态在函数体越界。
	/// 这里 "2" 是 NotEnoughArgs 文案里的最小个数,不是槽位。
	/// </summary>
	[Fact]
	public void HotkeyState_WithStateOnly_IsRejected()
		=> AssertRejectedWith("HOTKEY_STATE", trerror.NotEnoughArgs, 2, new SingleLongTerm(0));

	/// <summary>显式空槽:两槽都落回必填区间(i &lt; OmitStart = 2),由 arg-contract/issues/02 的判据拒绝。</summary>
	[Fact]
	public void HotkeyState_WithOmittedState_IsRejected()
		=> AssertRejectedWith("HOTKEY_STATE", trerror.ArgCanNotBeNull, 2,
			new SingleLongTerm(0), null);

	/// <summary>正控制:完整两实参继续通过。</summary>
	[Fact]
	public void HotkeyState_WithBothArgs_IsAccepted()
		=> AssertAccepted("HOTKEY_STATE", new SingleLongTerm(0), new SingleLongTerm(1));

	// ── arg-contract/issues/03 片 2-C:变长尾部按「转发型 / 消费型」分型定案(风险 3) ──────────
	// 转发型(尾部整段交给下游,见 GETMETH/GETMETHS)保持可空;消费型(函数体把尾部整条读完)没有默认值
	// 可代,整条标 ArgType.NotNull。两类的数量省略(少写尾实参)都继续合法,故 OmitStart 不动。

	/// <summary>
	/// MAX/MIN 的 {Int, VariadicInt} 是消费型变长尾:GetIntValue 以
	/// `for (int i = 1; i &lt; arguments.Count; i++) arguments.Required(i)` 逐个取值。原先尾部未标
	/// NotNull,`MAX(1,,2)`(中位显式空槽)能过检查器,再在循环里解引用 null ⇒ 与片 1 同型的 NRE。
	/// </summary>
	[Theory]
	[InlineData("MAX", "1,,2", 2)]
	[InlineData("MIN", "1,,2", 2)]
	[InlineData("MAX", "1,2,,", 3)]
	[InlineData("MIN", "1,2,,", 3)]
	public void MaxMin_WithExplicitEmptySlotInVariadicTail_IsRejected(string name, string inner, int slot)
		=> AssertRejectedWith(name, trerror.ArgCanNotBeNull, slot, [.. Shape(inner)]);

	/// <summary>正控制:填满尾部与数量省略(只写一个实参)都必须继续通过。</summary>
	[Fact]
	public void MaxMin_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("MAX", new SingleLongTerm(5));
		AssertAccepted("MIN", new SingleLongTerm(5));
		AssertAccepted("MAX", new SingleLongTerm(1), new SingleLongTerm(2));
		AssertAccepted("MIN", new SingleLongTerm(1), new SingleLongTerm(2), new SingleLongTerm(3));
	}

	/// <summary>
	/// ARRAYMSORT 的变长尾同样是消费型:GetIntValue 起手 `arguments[0] as VariableTerm`,末尾
	/// `foreach (VariableTerm term in arguments.Cast&lt;VariableTerm&gt;())` 遍历全部实参,空槽没有默认值
	/// 可代(该类被注释掉的旧手工检查器也要求每个实参非 null)⇒ 尾部标 NotNull。
	/// 文档只把首个实参写成必填(`ARRAYMSORT variableName1(, variableName2,...)`),与 OmitStart = 1 一致。
	/// </summary>
	[Theory]
	[InlineData("ARRAYMSORT", "CFLAG:0,,STR:0", 2)]
	[InlineData("ARRAYMSORT", "CFLAG:0,STR:0,,", 3)]
	public void ArrayMsort_WithExplicitEmptySlotInVariadicTail_IsRejected(string name, string inner, int slot)
		=> AssertRejectedWith(name, trerror.ArgCanNotBeNull, slot, [.. Shape(inner)]);

	/// <summary>
	/// 首槽的空槽由 `i &lt; OmitStart`(而非 NotNull)拒掉——这是修前即成立的形态,作为回归钉子,
	/// 不与上面的尾部钉子混计(尾部才是本次新铺的标志)。
	/// </summary>
	[Theory]
	[InlineData("ARRAYMSORT", ",CFLAG:0", 1)]
	[InlineData("MAX", ",2", 1)]
	public void ConsumedVariadic_WithEmptyLeadingSlot_IsRejected(string name, string inner, int slot)
		=> AssertRejectedWith(name, trerror.ArgCanNotBeNull, slot, [.. Shape(inner)]);

	/// <summary>正控制:ARRAYMSORT 的填满形态与数量省略(只排序一个数组)都继续通过。</summary>
	[Fact]
	public void ArrayMsort_WithFilledOrCountOmittedArgs_AreAccepted()
	{
		AssertAccepted("ARRAYMSORT", Variable("CFLAG:0"));
		AssertAccepted("ARRAYMSORT", Variable("CFLAG:0"), Variable("STR:0"));
		AssertAccepted("ARRAYMSORT", Variable("CFLAG:0"), Variable("STR:0"), Variable("DA:0:0"));
	}

	// ── arg-contract/issues/01:ArgType.Array3D 读错标志位 ───────────────────────────────

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
