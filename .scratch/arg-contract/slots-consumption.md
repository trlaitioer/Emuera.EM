# arg-contract/issues/04 阶段 A 片 0：实参消费面重新基线

本文件由 [arg-slots.py](arg-slots.py) 生成，口径见脚本头注释；票见 [arg-contract/issues/04](issues/04-argument-slots.md)。

扫描范围是**规则表族**（`Creator.Method.cs` 的 183 个子类，经 `FunctionMethod.CheckArgumentTypeEx` 校验）。
运算符族（`OperatorMethod.cs`）、StrForm 族（`StrForm.cs`）、TOSTR 包装（`Process.CalledFunction.cs`）
都绕过规则表、由调用点担保，不在本表。

## 计数（live = 剥注释；含注释 = 原始文本）

| 量 | live | 含注释 |
| --- | --- | --- |
| `arguments.Required(` | 404 | 405 |
| ├ 字面量下标 `Required(<n>)` | 371 | 372 |
| └ 非字面量 `Required(i)` 等 | 33 | — |
| `arguments[<字面量>]` | 54 | 229 |
| `arguments[<变量>]` | 18 | 51 |
| `arguments.Count` | 119 | 265 |
| `as VariableTerm` | 24 | 24 |
| `(VariableTerm)` | 10 | 16 |
| `is VariableTerm` | 10 | 21 |
| `.Restructure(exm)` | 20 | 20 |
| 列表模式 `arguments is […]` | 16 | 16 |
| `arguments.Skip(` / `arguments.Cast<` | 2 / 1 | — |
| `HasUniqueRestructure = true;` | 17 条语句 / 13 个类 | — |
| `UniqueRestructure` override | 14 个 | — |
| `[.. Arguments]`（全仓） | 1 | — |
| 全仓 `arguments.Required(` | 491 | — |
| 可省略 (类,槽) 全集 | 96 | — |
| 其中已带 `ArgType.NotNull` | 54 | — |

可空的槽共 **42** 项 = A 段（无判空 + 硬解引用，需本片处置）**2** + B 段（类内有判空写法）**37** + A2 段（无判空但机械未见硬解引用）**3**；其余 54 项已在全部可省略段带 `ArgType.NotNull`。

A 段 6 项里，本片（阶段 A 片 1）标了 **4** 个 `ArgType.NotNull`，另 **2** 个经逐点核查为「有意可空」（读取路径把 null 当默认值用，标了会拒掉可用写法）。

## A. 可达 null 候选与裁定

判据：**可空**（存在规则段 `OmitStart >= 0` 且 `OmitStart <= i < ArgTypes.Count` 且该槽未标 `ArgType.NotNull`）
∧ 读取形态含硬解引用 ∧ 类内该槽无判空写法。`状态` 列的「已标」= 本片已在其全部可省略段加 `ArgType.NotNull`，
槽因此不再可空（机械判定）；「裁定有意可空」= 不标，理由见 `处置` 列。

`数量省略尺子` 是机械预筛给不出的那一列——须人工核「读该槽时是否保证 `Count >= i + 1`」；
`可能缺失` 列是该尺子的机械候选（存在一个规则段的最小实参数 <= i）。

| 状态 | 类[槽] | 读取形态 | 读取点（方法） | 可省略段 | 可能缺失 | 已带标志 | 类别 | 处置 | 数量省略尺子 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 裁定有意可空 | `DataTableCellSetMethod[3]` | index | GetIntValue | 3/5 | 是 | — | 有意可空 | 假阳性：`AExpression? v = arguments.Count > 3 ? arguments[3] : null` 把 null 原样交给 DBNull 分支（issues/03 片 2-D 已补注释 + 用例） | 过（`Count > 3` 才读） |
| 裁定有意可空 | `DataTableColumnManagementMethod[1]` | Str/is Var | GetIntValue×2 | 1/2 | 是 | — | 有意可空 | 假阳性（跨构造器）：`Required(1).GetStrValue` 在 `op == Names` 早退之后，属 Check/Create/Remove 的定长规则；Names 段只读 `arguments[1] is VariableTerm`，null = 写 RESULTS（issues/03 片 2-A 已裁定不标 + 放行用例） | 过（Names 段 `Count > 1`；其余段无 `OmitStart`） |
| 已标（片 1） | `DataTableSelectMethod[3]` | as Var | GetIntValue | 1/4 | 是 | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（`Count == 4` 才读） |
| 已标（片 1） | `DataTableToXmlMethod[1]` | as Var | GetStrValue | 1/2 | 是 | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（`Count > 1` 才读） |
| 已标（片 1） | `EnumFilesMethod[3]` | as Var | GetIntValue | 1/4 | 是 | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（`Count == 4` 才读） |
| 已标（片 1） | `EnumNameMethod[1]` | as Var | GetIntValue | 1/2 | 是 | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（`Count == 2` 才读） |

类别分布：硬转型解引用 4、有意可空 2。

## A2. 可空、无判空、机械未见硬解引用

这些槽的读取路径不在「字面量下标」车道（走非字面量下标 / 整表转发），或函数体根本不读；
裁定来自 [arg-contract/issues/03](03-required-on-omittable-slot.md) 的片 2-C/2-D，本片不改。

| 类[槽] | 可省略段 | 读取形态 | 类级读取路径（D 段） | 类别 | 裁定依据 |
| --- | --- | --- | --- | --- | --- |
| `DataTableRowSetMethod[2]` | 1/3 | — | 变长下标 `Required(i)`×4、`arguments.Count`×2 | 有意可空 | 变长对 value 位 = 写 DBNull（规则段注释 + 用例） |
| `GetMethMethod[2]` | 1/3 | — | 整段转发 `Skip(k)`、`arguments.Count` | 转发槽 | `arguments.Skip(2)` 交给下游，null 由被调方解释 |
| `GetMethsMethod[2]` | 1/3 | — | 整段转发 `Skip(k)`、`arguments.Count` | 转发槽 | 同上 |

## B. 可空但有判空写法（人工核「判空是否守住解引用」）

本段与 [arg-contract/issues/03](03-required-on-omittable-slot.md) 的 B 表逐项对应：34 项「有意可空」
（裁定见该票片 2-B） + 片 2-D 补判的 `DT_SELECT[1]`/`[2]`、`GDRAWG[10]`（有意可空），共 37 项。
另两条「有意可空」按本工件 A 段的判据（无判空写法 ∧ 读取形态在解引用车道）落在 A 段，不在此列：
`DataTableCellSetMethod[3]`、`DataTableColumnManagementMethod[1]`（跨构造器，Names 段空槽 = 写 RESULTS）。
本片只复核判空是否守住解引用，不重复裁定。

| 类[槽] | 读取形态 | 判空点（方法） | 可省略段 | 已带标志 |
| --- | --- | --- | --- | --- |
| `ArrayMultiSortExMethod[2]` | Int/判空 | GetIntValue | 2/4、2/4 | — |
| `CsvDataMethod[2]` | Int | GetIntValue | 2/3、2/3 | — |
| `CsvStrDataMethod[1]` | Int/判空 | GetStrValue | 1/2、1/2 | — |
| `CsvcstrMethod[2]` | Int | GetStrValue | 2/3 | — |
| `DataTableSelectMethod[1]` | 判空 | GetIntValue | 1/4 | — |
| `DataTableSelectMethod[2]` | 判空 | GetIntValue | 1/4 | — |
| `EncodeToUniMethod[1]` | Int/判空 | GetIntValue | 1/2 | — |
| `ExistCsvMethod[1]` | Int | GetIntValue | 1/2 | — |
| `FindElementMethod[2]` | Int/判空 | GetIntValue | 2/5 | — |
| `FindElementMethod[3]` | Int/判空 | GetIntValue | 2/5 | — |
| `FindElementMethod[4]` | Int/判空 | GetIntValue | 2/5 | — |
| `FindFilesMethod[0]` | Str/判空 | GetIntValue | 0/1 | — |
| `FindcharaMethod[2]` | Int | GetIntValue | 2/4 | — |
| `FindcharaMethod[3]` | Int | GetIntValue | 2/4 | — |
| `GetMethMethod[1]` | Int/判空 | GetIntValue | 1/3 | — |
| `GetMethsMethod[1]` | Str/判空 | GetStrValue | 1/3 | — |
| `GetcharaMethod[1]` | Int/判空 | GetIntValue | 1/2 | — |
| `GraphicsDrawGMethod[10]` | 判空/helper | GetIntValue | 10/11、10/11 | — |
| `InRangeArrayMethod[3]` | Int/判空 | GetIntValue | 3/5、3/5 | — |
| `InRangeArrayMethod[4]` | Int/判空 | GetIntValue | 3/5、3/5 | — |
| `JoinMethod[1]` | Str | GetStrValue | 1/4 | — |
| `JoinMethod[2]` | Int | GetStrValue | 1/4 | — |
| `JoinMethod[3]` | Int | GetStrValue | 1/4 | — |
| `MatchMethod[2]` | Int/判空 | GetIntValue | 2/4、2/4 | — |
| `MatchMethod[3]` | Int/判空 | GetIntValue | 2/4、2/4 | — |
| `MaxArrayMethod[1]` | Int/判空 | GetIntValue | 1/3、1/3、1/3、1/3 | — |
| `MaxArrayMethod[2]` | Int/判空 | GetIntValue | 1/3、1/3、1/3、1/3 | — |
| `MoneyStrMethod[1]` | Str/判空 | GetStrValue | 1/2 | — |
| `StrfindMethod[2]` | Int | GetIntValue | 2/3 | — |
| `SubstringMethod[1]` | Int | GetStrValue | 1/3 | — |
| `SubstringMethod[2]` | Int | GetStrValue | 1/3 | — |
| `SubstringuMethod[1]` | Int | GetStrValue | 1/3 | — |
| `SubstringuMethod[2]` | Int | GetStrValue | 1/3 | — |
| `SumArrayMethod[1]` | Int | GetIntValue | 1/3、1/3 | — |
| `SumArrayMethod[2]` | Int | GetIntValue | 1/3、1/3 | — |
| `ToStrMethod[1]` | Str/判空 | GetStrValue | 1/2 | — |
| `VarsizeMethod[1]` | Int/is Single/折叠 | GetIntValue | 1/2 | — |

## C. 可省略槽全集的消费形态（类 × 槽）

`判据` 列：`可空` = 检查器会放行该槽的显式空槽；`缺失` = 数量省略即可让列表短于该下标。
`已带标志` = 该槽出现的**全部**可省略段都在该槽位带 `ArgType.NotNull`。

| 类[槽] | 可省略段 | 已带标志 | 读取形态 | 可空 | 缺失 |
| --- | --- | --- | --- | --- | --- |
| `ArrayMultiSortExMethod[2]` | 2/4、2/4 | — | Int/判空 | 是 | 是 |
| `ArrayMultiSortExMethod[3]` | 2/4、2/4 | 是 | Int | — | 是 |
| `ArrayMultiSortMethod[1]` | 1/2 | 是 | — | — | 是 |
| `CBGSETButtonSpriteMethod[6]` | 6/7 | 是 | Str | — | 是 |
| `CsvDataMethod[2]` | 2/3、2/3 | — | Int | 是 | 是 |
| `CsvStrDataMethod[1]` | 1/2、1/2 | — | Int/判空 | 是 | 是 |
| `CsvcstrMethod[2]` | 2/3 | — | Int | 是 | 是 |
| `DataTableCellGetMethod[3]` | 3/4 | 是 | Int | — | 是 |
| `DataTableCellSetMethod[3]` | 3/5 | — | index | 是 | 是 |
| `DataTableCellSetMethod[4]` | 3/5 | 是 | Int | — | 是 |
| `DataTableColumnManagementMethod[1]` | 1/2 | — | Str/is Var | 是 | 是 |
| `DataTableColumnManagementMethod[2]` | 2/4 | 是 | Int/Str/IsStr | — | 是 |
| `DataTableColumnManagementMethod[3]` | 2/4 | 是 | Int | — | 是 |
| `DataTableRowSetMethod[1]` | 1/3 | 是 | Int | — | 是 |
| `DataTableRowSetMethod[2]` | 1/3 | — | — | 是 | 是 |
| `DataTableSelectMethod[1]` | 1/4 | — | 判空 | 是 | 是 |
| `DataTableSelectMethod[2]` | 1/4 | — | 判空 | 是 | 是 |
| `DataTableSelectMethod[3]` | 1/4 | 是 | as Var | — | 是 |
| `DataTableToXmlMethod[1]` | 1/2 | 是 | as Var | — | 是 |
| `EncodeToUniMethod[1]` | 1/2 | — | Int/判空 | 是 | 是 |
| `EnumFilesMethod[1]` | 1/4 | 是 | Str | — | 是 |
| `EnumFilesMethod[2]` | 1/4 | 是 | Int | — | 是 |
| `EnumFilesMethod[3]` | 1/4 | 是 | as Var | — | 是 |
| `EnumNameMethod[1]` | 1/2 | 是 | as Var | — | 是 |
| `ErdNameMethod[2]` | 2/3 | 是 | Int | — | 是 |
| `ExistCsvMethod[1]` | 1/2 | — | Int | 是 | 是 |
| `ExistFunctionMethod[1]` | 1/2 | 是 | Int | — | 是 |
| `FindElementMethod[2]` | 2/5 | — | Int/判空 | 是 | 是 |
| `FindElementMethod[3]` | 2/5 | — | Int/判空 | 是 | 是 |
| `FindElementMethod[4]` | 2/5 | — | Int/判空 | 是 | 是 |
| `FindFilesMethod[0]` | 0/1 | — | Str/判空 | 是 | 是 |
| `FindcharaMethod[2]` | 2/4 | — | Int | 是 | 是 |
| `FindcharaMethod[3]` | 2/4 | — | Int | 是 | 是 |
| `FlowInputMethod[1]` | 1/4 | 是 | Int | — | 是 |
| `FlowInputMethod[2]` | 1/4 | 是 | Int | — | 是 |
| `FlowInputMethod[3]` | 1/4 | 是 | Int | — | 是 |
| `FlowInputsMethod[1]` | 1/2 | 是 | Str | — | 是 |
| `GetMethMethod[1]` | 1/3 | — | Int/判空 | 是 | 是 |
| `GetMethMethod[2]` | 1/3 | — | — | 是 | 是 |
| `GetMethsMethod[1]` | 1/3 | — | Str/判空 | 是 | 是 |
| `GetMethsMethod[2]` | 1/3 | — | — | 是 | 是 |
| `GetcharaMethod[1]` | 1/2 | — | Int/判空 | 是 | 是 |
| `GetnumMethod[2]` | 2/3 | 是 | Int | — | 是 |
| `GraphicsCreateFromFileMethod[2]` | 2/3 | 是 | Int | — | 是 |
| `GraphicsDrawGMethod[10]` | 10/11、10/11 | — | 判空/helper | 是 | 是 |
| `GraphicsDrawGWithRotateMethod[3]` | 3/5 | 是 | helper | — | 是 |
| `GraphicsDrawGWithRotateMethod[4]` | 3/5 | 是 | helper | — | 是 |
| `GraphicsDrawSpriteMethod[6]` | 6/7、6/7 | 是 | helper | — | 是 |
| `GraphicsDrawStringMethod[2]` | 2/4 | 是 | helper | — | 是 |
| `GraphicsDrawStringMethod[3]` | 2/4 | 是 | helper | — | 是 |
| `GraphicsGetTextSizeMethod[3]` | 3/4 | 是 | Int | — | 是 |
| `GraphicsSetFontMethod[3]` | 3/4 | 是 | Int | — | 是 |
| `HtmlGetPrintedStrMethod[0]` | 0/1 | 是 | Int | — | 是 |
| `HtmlStringLenMethod[1]` | 1/2 | 是 | Int | — | 是 |
| `InRangeArrayMethod[3]` | 3/5、3/5 | — | Int/判空 | 是 | 是 |
| `InRangeArrayMethod[4]` | 3/5、3/5 | — | Int/判空 | 是 | 是 |
| `JoinMethod[1]` | 1/4 | — | Str | 是 | 是 |
| `JoinMethod[2]` | 1/4 | — | Int | 是 | 是 |
| `JoinMethod[3]` | 1/4 | — | Int | 是 | 是 |
| `LoadTextMethod[1]` | 1/3 | 是 | Int | — | 是 |
| `LoadTextMethod[2]` | 1/3 | 是 | Int | — | 是 |
| `MapGetStrMethod[1]` | 1/2 | 是 | Int/Str/as Var | — | 是 |
| `MatchMethod[2]` | 2/4、2/4 | — | Int/判空 | 是 | 是 |
| `MatchMethod[3]` | 2/4、2/4 | — | Int/判空 | 是 | 是 |
| `MaxArrayMethod[1]` | 1/3、1/3、1/3、1/3 | — | Int/判空 | 是 | 是 |
| `MaxArrayMethod[2]` | 1/3、1/3、1/3、1/3 | — | Int/判空 | 是 | 是 |
| `MaxMethod[1]` | 1/2、1/2 | 是 | — | — | 是 |
| `MoneyStrMethod[1]` | 1/2 | — | Str/判空 | 是 | 是 |
| `OutputlogMethod[0]` | 0/2 | 是 | Str | — | 是 |
| `OutputlogMethod[1]` | 0/2 | 是 | Int | — | 是 |
| `RandMethod[1]` | 1/2 | 是 | Int | — | 是 |
| `RegexpMatchMethod[2]` | 2/3 | 是 | Int/as Var | — | 是 |
| `ReplaceMethod[3]` | 3/4 | 是 | Int | — | 是 |
| `SaveTextMethod[2]` | 2/4 | 是 | Int | — | 是 |
| `SaveTextMethod[3]` | 2/4 | 是 | Int | — | 是 |
| `StrfindMethod[2]` | 2/3 | — | Int | 是 | 是 |
| `SubstringMethod[1]` | 1/3 | — | Int | 是 | 是 |
| `SubstringMethod[2]` | 1/3 | — | Int | 是 | 是 |
| `SubstringuMethod[1]` | 1/3 | — | Int | 是 | 是 |
| `SubstringuMethod[2]` | 1/3 | — | Int | 是 | 是 |
| `SumArrayMethod[1]` | 1/3、1/3 | — | Int | 是 | 是 |
| `SumArrayMethod[2]` | 1/3、1/3 | — | Int | 是 | 是 |
| `ToStrMethod[1]` | 1/2 | — | Str/判空 | 是 | 是 |
| `VarSetExMethod[2]` | 2/5 | 是 | Int | — | 是 |
| `VarSetExMethod[3]` | 2/5 | 是 | Int | — | 是 |
| `VarSetExMethod[4]` | 2/5 | 是 | Int | — | 是 |
| `VarsizeMethod[1]` | 1/2 | — | Int/is Single/折叠 | 是 | 是 |
| `XmlAddNodeMethod[3]` | 3/5、3/5、3/6、3/6、3/5、3/6 | 是 | Str | — | 是 |
| `XmlAddNodeMethod[4]` | 3/5、3/5、3/6、3/6、3/5、3/6 | 是 | — | — | 是 |
| `XmlAddNodeMethod[5]` | 3/6、3/6、3/6 | 是 | — | — | 是 |
| `XmlGetMethod[2]` | 2/4、2/4 | 是 | term | — | 是 |
| `XmlGetMethod[3]` | 2/4、3/4、2/4、3/4 | 是 | Int | — | 是 |
| `XmlRemoveNodeMethod[2]` | 2/3、2/3、2/3 | 是 | Int | — | 是 |
| `XmlReplaceMethod[3]` | 3/4、3/4、3/4 | 是 | Int | — | 是 |
| `XmlSetMethod[3]` | 3/5、3/5、3/5 | 是 | Int | — | 是 |
| `XmlSetMethod[4]` | 3/5、3/5、3/5 | 是 | Int | — | 是 |

## D. 类级形态（无法归到具体槽位）

| 类 | 变长下标 | 整段转发 | Cast/foreach | `Count` 循环 | `Count` 引用 | 列表模式 | 折叠站点 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `HtmlStringLenMethod` | — | — | —/— | — | 1 | — | — |
| `XmlGetMethod` | — | — | —/— | — | 2 | — | — |
| `EnumNameMethod` | — | — | —/— | — | 1 | — | — |
| `EnumFilesMethod` | — | — | —/— | — | 3 | — | — |
| `ArrayMultiSortExMethod` | 1 | — | —/— | 1 | 3 | — | 1 |
| `VarSetExMethod` | — | — | —/— | — | 7 | — | — |
| `RegexpMatchMethod` | — | — | —/— | — | 2 | — | — |
| `XmlSetMethod` | — | — | —/— | — | 2 | — | — |
| `XmlAddNodeMethod` | 2 | — | —/— | — | 3 | — | — |
| `XmlRemoveNodeMethod` | — | — | —/— | — | 2 | — | — |
| `XmlReplaceMethod` | — | — | —/— | — | 4 | — | — |
| `DataTableColumnManagementMethod` | — | — | —/— | — | 3 | — | — |
| `DataTableRowSetMethod` | 4 | — | —/— | — | 2 | — | — |
| `DataTableRowRemoveMethod` | — | — | —/— | — | 1 | — | — |
| `DataTableCellGetMethod` | — | — | —/— | — | 2 | — | — |
| `DataTableCellSetMethod` | — | — | —/— | — | 2 | — | — |
| `DataTableSelectMethod` | — | — | —/— | — | 3 | — | — |
| `DataTableToXmlMethod` | — | — | —/— | — | 2 | — | — |
| `MapGetStrMethod` | — | — | —/— | — | 4 | — | — |
| `GetcharaMethod` | — | — | —/— | — | 1 | — | — |
| `CsvStrDataMethod` | — | — | —/— | — | 1 | — | — |
| `CsvcstrMethod` | — | — | —/— | — | — | 1 | — |
| `CsvDataMethod` | — | — | —/— | — | — | 1 | — |
| `FindcharaMethod` | — | — | —/— | — | — | 2 | — |
| `ExistCsvMethod` | — | — | —/— | — | — | 1 | — |
| `VarsizeMethod` | — | — | —/— | — | 2 | 1 | 2 |
| `FindFilesMethod` | — | — | —/— | — | 1 | — | — |
| `MesSkipMethod` | — | — | —/— | — | 1 | — | — |
| `MoneyStrMethod` | — | — | —/— | — | 1 | — | — |
| `RandMethod` | — | — | —/— | — | 1 | — | — |
| `MaxMethod` | 1 | — | —/— | 1 | 1 | — | — |
| `SumArrayMethod` | — | — | —/— | — | — | 2 | — |
| `MatchMethod` | 1 | — | —/— | 1 | 3 | — | 2 |
| `GroupMatchMethod` | 2 | — | —/— | 2 | 2 | — | — |
| `NosamesMethod` | 2 | — | —/— | 2 | 6 | — | — |
| `AllsamesMethod` | 2 | — | —/— | 2 | 2 | — | — |
| `MaxArrayMethod` | 1 | — | —/— | 1 | 3 | — | 2 |
| `GetnumMethod` | — | — | —/— | — | 1 | — | 1 |
| `FindElementMethod` | 1 | — | —/— | 1 | 4 | — | 2 |
| `InRangeArrayMethod` | 1 | — | —/— | 1 | 3 | — | 2 |
| `ArrayMultiSortMethod` | 1 | — | 1/1 | 1 | 1 | — | 1 |
| `SubstringMethod` | — | — | —/— | — | — | 2 | — |
| `SubstringuMethod` | — | — | —/— | — | — | 2 | — |
| `StrfindMethod` | — | — | —/— | — | — | 1 | — |
| `ToStrMethod` | — | — | —/— | — | 1 | — | — |
| `ReplaceMethod` | — | — | —/— | — | 3 | — | — |
| `EncodeToUniMethod` | — | — | —/— | — | 1 | — | — |
| `StrFormMethod` | — | — | —/— | — | — | — | 1 |
| `JoinMethod` | 1 | — | —/— | 1 | 1 | 3 | 1 |
| `HtmlGetPrintedStrMethod` | — | — | —/— | — | 1 | — | — |
| `HtmlEscapeMethod` | 9 | — | —/— | — | — | — | — |
| `GraphicsSetFontMethod` | — | — | —/— | — | 1 | — | — |
| `GraphicsDrawStringMethod` | — | — | —/— | — | 2 | — | — |
| `GraphicsGetTextSizeMethod` | — | — | —/— | — | 1 | — | — |
| `GraphicsDrawGWithRotateMethod` | — | — | —/— | — | 1 | — | — |
| `GraphicsCreateFromFileMethod` | — | — | —/— | — | 1 | — | — |
| `SpriteCreateMethod` | — | — | —/— | — | 1 | — | — |
| `GraphicsClearMethod` | — | — | —/— | — | 1 | — | — |
| `GraphicsDrawGMethod` | 2 | — | —/— | 1 | 2 | — | 2 |
| `GraphicsDrawSpriteMethod` | 2 | — | —/— | 1 | 4 | — | 2 |
| `CBGSETButtonSpriteMethod` | — | — | —/— | — | 1 | — | — |
| `SaveTextMethod` | — | — | —/— | — | 2 | — | — |
| `LoadTextMethod` | — | — | —/— | — | 2 | — | — |
| `ExistFunctionMethod` | — | — | —/— | — | 1 | — | — |
| `ErdNameMethod` | — | — | —/— | — | 1 | — | 1 |
| `FlowInputMethod` | — | — | —/— | — | 3 | — | — |
| `FlowInputsMethod` | — | — | —/— | — | 1 | — | — |
| `GetMethMethod` | — | 1 | —/— | — | 1 | — | — |
| `GetMethsMethod` | — | 1 | —/— | — | 1 | — | — |
| `OutputlogMethod` | — | — | —/— | — | 2 | — | — |

## F. 与邻票对齐

### issues/03（`ArgType.NotNull` 的既有铺开）

本表机械复核：可省略 (类,槽) 全集 **96**，其中**全部**可省略段都带 `ArgType.NotNull` 的 **54** 个
= issues/03 的「拒绝侧共 50 个 (类,槽)」 + 本片（阶段 A 片 1）新增的 **4** 个。
A/B/A2 三段引用的裁定均来自该票（片 2-A/2-B/2-C/2-D），本片不重复裁定。

### nullable-migration/issues/18（`AllowConstRef` 与折叠的不变量）

该票立的不变量：**规则段里出现 `AllowConstRef` 的引用位 ⇒ 该类必须设 `HasUniqueRestructure`**，
否则常量数组实参被折成 `SingleTerm`、函数体的 `(VariableTerm)` 转换失效。机械复核结果：

| 类[槽] | 可省略段 | 已设 `HasUniqueRestructure` | 结论 |
| --- | --- | --- | --- |
| `ErdNameMethod[0]` | 2/3 | 是 | 受不变量保护 |
| `FindElementMethod[0]` | 2/5 | 是 | 受不变量保护 |
| `GetnumMethod[0]` | 2/3 | 是 | 受不变量保护 |
| `GraphicsDrawGMethod[10]` | 10/11 | 是 | 受不变量保护 |
| `GraphicsDrawGMethod[10]` | 10/11 | 是 | 受不变量保护 |
| `GraphicsDrawSpriteMethod[6]` | 6/7 | 是 | 受不变量保护 |
| `GraphicsDrawSpriteMethod[6]` | 6/7 | 是 | 受不变量保护 |
| `InRangeArrayMethod[0]` | 3/5 | 是 | 受不变量保护 |
| `InRangeArrayMethod[0]` | 3/5 | 是 | 受不变量保护 |
| `JoinMethod[0]` | 1/4 | 是 | 受不变量保护 |
| `MatchMethod[0]` | 2/4 | 是 | 受不变量保护 |
| `MatchMethod[0]` | 2/4 | 是 | 受不变量保护 |
| `MaxArrayMethod[0]` | 1/3 | 是 | 受不变量保护 |
| `MaxArrayMethod[0]` | 1/3 | 是 | 受不变量保护 |
| `MaxArrayMethod[0]` | 1/3 | 是 | 受不变量保护 |
| `MaxArrayMethod[0]` | 1/3 | 是 | 受不变量保护 |
| `ReplaceMethod[2]` | -1/4 | 是 | 受不变量保护 |
| `SumArrayMethod[0]` | 1/3 | 否 | 豁免：角色数据段（`CharacterData | RefIntArray | AllowConstRef`）：常量数组不可能是角色数组变量 |

不变量全部成立或已豁免。

该不变量覆盖的是「引用型实参被折叠成 `SingleTerm`」这条 null 来源，与 A 段的「显式空槽」正交；
两类都不在 A 段里重复列出（A 段判据只看检查器是否放行 null 元素）。

## E. 自校验

| 检查 | 结果 |
| --- | --- |
| 本文件 live `arguments.Required(` == 404 | 通过 |
| 与 [slots-worklist.py](slots-worklist.py) 同函数同口径的 `ArgType.NotNull` 计数（含注释 87 处/76 行、剥注释 81 处/70 行） | 通过 |
| `HasUniqueRestructure = true;` 语句/类 == 17/13 | 通过 |
| `UniqueRestructure` override == 14 | 通过 |
| 全仓 `[.. Arguments]` == 1 | 通过 |
| 候选集裁定（未裁定 0 / 已落地 4 / 过期 0） | 通过 |
| A2 段与裁定表一致 | 通过 |
| A + A2 + B + 已标 = 可省略全集 96 | 通过 |
| issues/18 不变量（`AllowConstRef` ⇒ `HasUniqueRestructure`） | 通过 |
| helper 偏移（机械推出） | ReadGraphics=[0], ReadColor=[0], ReadPoint=[0, 1], ReadRectangle=[0, 1, 2, 3], ReadColormatrix=[0] |

