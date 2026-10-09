---
id: nullable-migration-22
status: implemented
---

# Nullable:保留断言的理由注释与契约诚实化

## 任务

issues/14 判定为「不变量成立」的断言将保留 `!`。本票为其补上说明「编译器看不到哪条不变量」的注释，同时清理经判定确属冗余的断言，并把若干「实际可空却声明非空」的位置改为诚实的可空契约。方案第 1~4 节的改动为元数据级（注解、注释、只读访问器、局部类型），不改变运行行为；第 5~6 节是 issues/17 实施后 review 移交的相邻项，属结构性改动，与本票的元数据级前提不同——开工时须先定这两节是并入本票还是另开票。

## 方案

### 1. 保留断言的理由注释（以 `as` 转换为主，约 49 处）

issues/14 判定 `(x as T)!` 中 49 处有前置类型检查，需逐点注明保证来自何处，例如：`FunctionMethod` 的实参类型表（含 `RefAny1D`/`AllowConstRef` 与 `UniqueRestructure` 的组合）、`Word.Type` 的分派常量、`FunctionCode`→`ArgumentBuilder` 的分派、`VariableCode` 的数组形状判定、委托注册的框架契约。注释写「由哪个检查或分派保证」，不复述代码。

无保护的 3 处不在此列（issues/18）。

另：issues/21 的清单判定出的 A 类组不止 `as` 转换——至少包括 `CaseExpression.LeftTerm`/`RightTerm`（构造点 `ExpressionParser.reduceCaseExpression` 已保证非空，读取处按 `CaseType` 门禁）、`VariableStrArgTerm` 的 `dic`（读取前已判空转 `CodeEE`）等成员与局部断言。这些组同样需要「编译器看不到哪条不变量」的理由注释，归本票，具体分组以 issues/21 的判定表为准。

### 2. 冗余 `!` 清理

| 位置 | 依据 |
| --- | --- |
| `ExpressionMediator.cs`（`ConvertStringType` 3 处）与 `BuiltinMethod/StringOperations.cs`（`StrChangeStyleMethod.GetStrValue` 2 处）的 `Strings.StrConv` | **判定更正：不属冗余，本票不删。** `StrConv` 在 .NET 上的签名是 `string? StrConv(string? str, VbStrConv, int LocaleID = 0)`，该 `!` 抑制的是 CS8603；不支持的转换抛 `ArgumentException`。两处已有同义 TODO（issues/08 迁移时补入，早于本票），本票只复核 TODO |
| `BuiltinMethod/PrivateAdditions.cs` 的 `DataColumnCollection` 整型索引 | **已完成**：`e70ec779 clean: 移除冗余的 ! 标识` 把 `DataTableColumnManagementMethod.GetIntValue` 的 `Operation.Names` 分支由 `dt.Columns[i]!.ColumnName` 改为 `dt.Columns[i].ColumnName`；全仓已无整型索引器断言 |
| `BuiltinMethod/PrivateAdditions.cs` 的 `ArrayMultiSortExMethod` 中 `is VariableTerm` 分支后再 `as` | 两分支均非空 |
| `Instraction.Child.cs` 的 `VARI_Instruction` 对 `IntAsignArgument.Exp` | 两条构造路径都提供表达式，无非空构造器在用 |
| `Instraction.Child.cs` 的 `CALLSHARP_Instruction` 对 `arg.CallFunc` | 该构造器以常量字符串（`SingleStrTerm`）构造函数名，`IsConst` 必为真，`SetJumpTo` 中的非常量分支不可达；`CallFunc` 保持 null 的唯一路径是 `!manager.HasMethod` 分支，该处以 `Warn(..., isError: true)` 阻断执行——`!` 的实际保证来自这一错误标记，而非 `IsConst` |
| `Instraction.Child.cs` 的 `RETURNF_Instruction` 对 `((ExpressionArgument)…).Term` | `SetJumpTo` 与 `DoInstruction` 两处读取点都紧接 `if (term != null)`，`!` 纯属冗余（issues/14 复核表第 1 项移交） |

`ConstantData` 的 `GetCharacterTemplate*` 无可更正项：该文件（写票时与当前）唯一 TODO 就在 `GetKeywordDictionary`（潜在缺陷，见 issues/19）。该组方法返回 `CharacterTemplate?` 且 6 个调用方全部判空（`VariableEvaluator.cs` 的 1537/1549/1561/1985/1999/2066），确不是问题。

### 3. 契约诚实化（把 null 写进类型）

以下位置经判定「实际可空或读取前必判空」，`= null!`/`!` 掩盖了真实状态，应改为可空类型：

- `ErbLoader.setJumpTo` 的局部 `FunctionNotFoundName` 与 `Instruction.cs` 中 `AInstruction.SetJumpTo` 的 `ref string FunctionoNotFoundName`（另有 8 处同型 override）——null 是「未上报函数名」的正常信号，读取处本就包在判空内，类型应为 `string?`（含 `ref string?`）。
- `UI/Game/Image/GraphicsImage.cs` 的 `brush`/`pen`/`font`——`Dispose` 路径置 null，读取处均判空，公开访问器却在传播非空签名。
- `Process.State.cs` 的 `IntoFunction` 的 `exm` 参数、`Process.cs` 的 `SetScaningLineForThread` 的 `line` 参数——调用方传 null 是既定语义，参数应可空。
- `VariableToken` 构造函数中未使用的 `VariableData` 参数。
- `Process.CalledFunction.cs` 的 `ConvertArg` 的 `out string errMes`——仅在返回 null 时被读取，应为 `out string?`。**已完成**（`c82aa022`）：改为 `TryConvertArg(…, [NotNullWhen(false)] out string? errMes, [NotNullWhen(true)] out UserDefinedFunctionArgument? argument)`，6 个调用点由「判 null」改为「判 `bool`」。
- `ConstantData.GetKeywordDictionary` 的可空返回 + `out string? errPos`——同型：21 个求值点只能写 `!`，而「该变量码必命中主 switch」的不变量不写在类型里。**已完成**（issues/29）：拆出 `TryGetKeywordDictionary` 与非空入口 `GetKeywordDictionary([NotNull] out string? errPos, …)`，22 处 `!` 删除。
- `ConfigData.GetConfigValueInERB` 的 `ref string? errMes`——同型：失败原因从 `ref` 侧漏，唯一调用点必须写 `if (errMes != null) throw` 再 `term!`。**已完成**（`59cd307e`）：拆出 `TryGetConfigValueInERB(string, [NotNullWhen(true)] out SingleTerm? value, [NotNullWhen(false)] out string? errMes)` 与抛异常版 `GetConfigValueInERB(string)`——后者返回非空 `SingleTerm`，调用点两段样板一并删除。
- `InstructionLine.PopArgumentPrimitive()` 的 `CharStream?` 返回——`argprimitive` 由构造器写入一次、每行至多被取一次（issues/21 判 A），可空签名只换来 18 处 `!`。**已完成**（`59cd307e`）：改为非可空返回，字段已被取走时抛 `ExeEE(MissingArg)`。
- `Process.CalledFunction.ConvertArg`/`UserDefinedMethodTerm.Create`/`MemoryLineEntry.Build`——`TryXxx` 已就位，但「调用方只关心失败就抛」的那一半仍由每个调用点手写（`? : throw new CodeEE(errMes)`、`if (!Try…) throw`）。**已完成**（`59cd307e`）：补上抛异常兄弟并替换 3 处调用点；`ErbLoader` 与 `SetJumpTo` 的 Warn 路径继续用 Try 版。
- `UI/Game/HtmlManager.cs` 中作为换行哨兵的 null 元素（`buttonList.Add(null!)`）——容器元素类型 `List<ConsoleButtonString>` 应改为 `List<ConsoleButtonString?>`；消费方 `PrintStringBuffer.ButtonsToDisplayLines` 的形参与 `m_buttonsToDisplayLine` 字段须同步改，否则该处既有的 `if (buttonList[i] == null)` 判空与新元素类型对不上。
- `BuiltinMethod/PrivateAdditions.cs` 的 `XmlGetMethod`/`XmlSetMethod` 中 `nodes` 局部——赋值发生在 `try` 内、唯一 `catch` 抛出，编译器无法据此证明「读取前必已赋值」；提取方法或调整结构让赋值可被证明，而不是用 `= null!`。
- `HtmlManager.tagAnalyze` 的 `ref height!`/`ref width!`/`ref x!`/`ref y!` 等 15 处与 `ParseParam4IntNum(ref CreateIfNull(ref box!).color!, …)`（issues/21 的 D 类）——`Utils.ParseMixedNum(ref MixedNum num, …)` 的第一步就是 `if (num == null) num = new MixedNum();`，`CreateIfNull<T>(ref T obj)` 同样以 null 为正常入参；`!` 断言的内容为假。把形参改为 `ref MixedNum?`/`ref int[]?`，调用点的 `!` 一并删除（元数据级，零行为变化）。

### 4. 常量折叠实参的具名入口

`GetIntValue(null!)`/`GetStrValue(null!)` 共 18 处（`ArgumentBuilder.cs` 14、`VariableEvaluator.cs` 的 `FindChara` 两重载 4），另有 `ExpressionParser.ToStrFormTerm` 的 1 处 `strf.GetString(null!)`（票面原先记的「约 65 处」是 issues/14 对全部「调用实参位 `null!`」的计数，口径不同）。这些调用当前安全，但判据分三类：`ArgumentBuilder` 各处由 `is SingleTerm`/`allConst` 支配，`ToStrFormTerm` 由 `strf.IsConst` 支配，`FindChara` 两重载则没有此类判据——它靠 `FindcharaMethod` 的 `ArgType.CharacterData` 保证实参是 character-data 变量，而该类 token 的取值不读 `exm`。按 issues/13 对 `Required`/`BuiltArgument` 的做法收敛为具名入口，让「此处无求值上下文」只表达一次（例如单值 term 上的无上下文取值成员）。

### 5. issues/17 两轴 review 的相邻重复（本票记录，来自 issues/17 实施后的 review）

三处均为判定级气味，非缺陷；与 issues/17 的改动同址，但那三处已按最小改动落地，收敛留给本票：

- `ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments`：`terms[0]`/`terms[1]` 两块约 15 行仅差下标、参数序号与局部名（`vars[0]`/`vars[1]`、`term`/`term1`），第一个实参块有 `return null` 而第二个原本没有（issues/17 已补）。抽出一个「按序号检查一个数组实参」的辅助后，两块的差异只剩实参序号，`return null` 也从「要记得写」变成「不可漏写」。
- `ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments` × `Process.ScriptProc.cs` 的 `doNormalFunction`（ARRAYCOPY 分支）：字面量实参名 `GetVariableToken(term.Str, null, true)` 的解析存在两处，解析期那处的结果未传给执行期那处。issues/17 已在执行期补注释说明「两处必须同步」，属把耦合写明；若要消除，可让 `SpCopyArrayArgument` 携带解析期已得到的 `VariableToken?`，执行期只在动态分支重解析。
- `Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction`：表/列解析内联复刻了 DT 函数族（`BuiltinMethod/PrivateAdditions.cs` 的 `DataTableColumnManagementMethod` 与 `DataTableRowSetMethod.CheckName`）的形状——`TryGetValue` 取表、缺表 `-1`、缺列抛 `DTLackOfNamedColumn`。共享一处解析器可让族内约定只有一份；顺带说明：族内 `id` 列不可改的守卫（`DTCanNotEditIdColumn`）只加在写行的路径上，`DT_COLUMN_OPTIONS` 改的是列默认值，是否需要该守卫属产品判断，需与共享解析一起定。

### 6. `ExpressionArrayArgument.TermList` 的契约诚实化（issues/17 审计移交）

`Argument.cs` 的 `ExpressionArrayArgument` 声明 `AExpression[] TermList`，却由 `List<AExpression?>` 经 `CopyTo` 填充，把元素可空性在类型上洗掉。两条构造路径当前都已拒绝 null 元素（`ArgumentBuilder.cs` 的 `INT_ANY_ArgumentBuilder` 经 `checkArgumentType` 拒绝、`FORM_STR_ANY_ArgumentBuilder` 自建非空 term），故非缺陷；但读取侧（`Process.ScriptProc.cs` 的 `INT_ANY` 消费、`Instraction.Child.cs` 的 `ExpressionArrayArgument` 取值）依赖的是这条不写在类型上的不变量。处置二选一：构造函数改收 `List<AExpression>`（调用点各自收窄），或字段改 `AExpression?[]` 并由消费点处理。

## 实施结果

### 2026-10-06,分支 `nullable/assertion-rationale`

§5/§6 按用户决定并入本票(不再另开票);§1~§6 已落地(§5-3 的 `id` 列守卫待产品判断)。

**§2 冗余 `!` 清理(完成)**

| 位置 | 结果 |
| --- | --- |
| `Strings.StrConv` 5 处 | 保留(判定成立:只承担 CS8603);两处 TODO 改写为理由注释 |
| `DataColumnCollection` 整型索引 | 已由 `e70ec779` 完成,复核确认全仓无整型索引断言 |
| `ArrayMultiSortExMethod` 的 `(term as VariableTerm)!` | 保留(已有理由注释);同方法的 `CheckVariableTerm(term!)` 已消除——形参改 `AExpression?`(实现以 `is` 模式对 null 安全) |
| `Instraction.Child.cs` 的 `VARI_Instruction` 对 `IntAsignArgument.Exp` | **判定更正**:该字段本可非空(两条构造路径都传表达式)→ 字段改非可空、删除 `!` |
| `Instraction.Child.cs` 的 `CALLSHARP_Instruction` 对 `arg.CallFunc` | 保留并注明:真实保证来自 `SetJumpTo` 的 `isError` 标记,不是 `IsConst` |
| `RETURNF_Instruction` 对 `((ExpressionArgument)…).Term` | 两处读取点本就判空 → 局部改 `AExpression?`、删除 `!` |

**§3 契约诚实化(已实施)**

- `AInstruction.SetJumpTo` 的 `ref string` → `ref string?`(8 处 override 同步;`ErbLoader` 局部随之去 `= null!`)
- `ProcessState.IntoFunction` 的 `exm` 参数、`Process.SetScaningLineForThread` 的 `line` 参数、`Process.scaningLine` 字段改可空(`IntoFunction(call, null, null!)` 两处随之改 `null`)
- `XmlGetMethod`/`XmlSetMethod` 的 `nodes` 局部去掉 `= null!`:**判定更正**——实测 `try { nodes = …; } catch { throw …; }` 之后的读取由「catch 必抛」证明,编译器本就无 CS0165/CS86xx,故无需提取方法
- `CircularBuffer<T>` 元素类型改 `T?[]`:槽位可空是事实;`Enqueue` 的读取在缓冲未满时读的是死槽(票面「槽位在下次 Enqueue 前不被读」只对活槽成立,已在写点与字段处注明),故该处 `!` 是断言而非冗余
- `Utils.CreateIfNull(ref int[]?)` 与 `ParseMixedNum`/`TryParseStyledBoxModel`/`CreateIfNull<T>` 形参改可空并补 `[NotNull]` 真实后置条件 → `HtmlManager` 删 13 处 `ref x!`、`box!`/`color!` 3 处
- `HtmlAnalzeStateFontTag.FontName` 与 `StringStyle` 构造器改 `string?`;`RefArgument` 两个构造器的 `name` 形参改 `AExpression?`
- `DataTableRowSetMethod.CheckName` 补「列存在」的后置条件注释(覆盖 5 处 `dt.Columns[name]!`)

**§3 判定更正 / 未实施(移出本票)**

| 项 | 结论 |
| --- | --- |
| `MainWindow.console` 改 `readonly` | **不成立**:`MainWindow_FormClosing` 有 `console.Dispose(); console = null;`,`readonly` 不可能;26 处 `if (console == null)` 也不是死代码(构造器先 `InitializeComponent()` 接线、后赋值) |
| `VariableToken` 构造函数「未使用的 `VariableData` 参数」 | **不成立**:该字段在 Chara/CHARANUM/LASTLOAD 等分支被读 60+ 处;真正的假断言只在 `: base(varCode, null!)`(`UserDefinedVariableToken` 分支),两种诚实化代价都是净亏 |
| `GraphicsImage` 的 `brush`/`pen`/`font`、`ConstImage` 的 `RealBitmap`、`HtmlManager` 的 `ButtonValueStr`、换行哨兵 `buttonList.Add(null!)` | 断言内容确为假,但可空化会沿消费链牵动迁移清单外的非空消费方(`BuiltinMethod/Graphics.cs`、`ConsoleDisplayLine.buttons` 的消费方),且需先定报错语义或哨兵设计 → 移交 [issues/31](31-assertion-honesty-nonnull-consumers.md) |
| `ConsoleButtonString` 构造器的 `parent = console!` | 保留:`parent` 类内 9 处读取,改可空净亏;原注释与事实不符,已改写为真实不变量 |
| `Process.scaningLine` 的唯一读取点(`UnicodeMethod.GetStrValue`) | 字段已诚实化,该处保留断言:三种改法中有两种改行为(改告警指向的行 / NRE 变静默);并行解析走线程本地值,断言并不可证 → 记入 issues/19 的 B 类清单 |

**§4 常量折叠实参的具名入口(完成)**

按「让『此处无求值上下文』只表达一次」落地:新增 `AExpression.GetIntValue()`/`GetStrValue()` 与 `StrForm.GetString()` 三个无参入口(每个内部就是 `GetXxx(null!)`,`!` 从 19 处收敛到 3 处),19 个调用点改为无参形态——`ArgumentBuilder.cs`(14:其中 `GetIntValue` 8、`GetStrValue` 6)、`ExpressionParser.cs`(1)、`VariableEvaluator.cs`(4)。语义完全等价(无参入口只是转发 `null`),故运行行为不变;同口径计数 `Emuera` 后缀断言 838 → 822。

**§1 保留断言的理由注释(完成)**

按「一个不变量一条注释、写在不变量的建立点」分五个区域落地,覆盖 `(x as T)!` 的前置检查组、issues/21 判定表的 A 类组,以及 issues/14 复核对象 1 的 `Argument` 可选字段桶(该桶不在 `bang-sites.md` 的站点表内,此前无人承接)。**实测**(`git diff 6bb31248^ 6bb31248 -- Emuera/`,注释块 = 连续的新增注释行):新增 **135** 个注释块 / 248 行,改写既有注释 **12** 块 / 16 行。票面原记的「合计 141 条(新增约 128 / 改写 13)」不能复现:下表前四行按文件路径计已合到 135,末行的 `Argument` 可选字段桶是跨文件的**子集**(其注释就落在 `Instraction.Child.cs` 与 `Process.ScriptProc.cs` 内),再计一次才得 141——141 是重复计数的结果,不是站点差异:

| 区域 | 注释 | 覆盖 |
| --- | --- | --- |
| `Statements/Function/**` | 21 | `(…Identifier.GetArray() as T)!` 全组的组判据写在 `ArgumentSlots.Var` 的 doc 上;`SelectNodes(path)!` 6 条、`DocumentElement`/`Attributes`、`VarSetEx` 的数组形状各按构造点 |
| `Loader`+`Parser`+`Data`+`Statements/Expression`+`Statements/Variable` | 34 | 7 处 `(x as T)!` 归到各自 builder(如 `SP_FOR_NEXT`/`INT_EXPRESSION`/`CASE_`/`SP_CALL`)、`Word.Type` 分派一条覆盖 5 处、`this.sizes!` 6 处与 `.Position` 族各一条 |
| `UI/**` | 12 | 覆盖 40 处:`AConsoleDisplayNode.Text` 13 处一条、`MainWindow.console` 13 处一条(含 `FormClosing` 置 null 的真不变量);约 28 处迁移期已有达标注释,复核未动 |
| `Config`+`Utils`+`Statements` 顶层+`Process*` | 37 | 覆盖约 110 处:`.Position!` 族 23 处在 `LogicalLine.Position` 声明处一条、`CaseExpression` 14 处、`Clipboard` 10 处、`CurrentLabel` 8 处、`WebPWrapper` 的 `bmp!` 4 处各一条 |
| `Argument` 可选字段桶(issues/14 复核对象 1) | 37 | 覆盖 91 处(`Instraction.Child.cs` 30 / `Process.ScriptProc.cs` 7),依据逐条回到各指令的 `CreateArgument` 分支 |

上表前四行的路径实测分别为 21 / 27 / 12 / 75(合计 135),与表中 34、37 两格不符;以实测的 135 为准,分区构成只用于说明覆盖意图。

判定更正与上报(均只报告、未在 §1 内改动):

- 两处先前疑为「可删冗余」的断言经取证**并非冗余**:`WordCollection.Next` 的第二个 `!`(`LinkedListNode<T>.Next` 本身可空,守卫只保证节点存在)、`ReferenceToken.IsArrayRangeValid` 的「第二处读取」——该处本就没有 `!`,首处 `array!` 的收窄已覆盖后续
- 三处疑似缺陷与一处产品不一致移交 [issues/32](32-assertion-audit-findings.md):`XML_ADDATTRIBUTE` method 0 的 `node.Attributes!` 解引用非元素节点、`DATATABLE_TO_XML` 的 `(… as string[])!` 与规则声明的形状不一致、`PrintStringBuffer.getDivideIndex` 的 `if (part == null)` 疑为 `css == null` 笔误、`SP_SPLIT_ArgumentBuilder` 的 `term!` 与姊妹 `SP_HTMLSPLIT` 的回退行为不一致
- `bang-sites.md` 中已被实施提交删除的站点行(issues/29、`59cd307e`、§3 所删)与该表「已失效站点」节的口径需在收尾时统一

**§5/§6 结构性项(完成)**

- §5-1 `SP_COPY_ARRAY_Arguments` 的两个实参块合并为 `TryResolveArrayArg`(按序号复用同一检查序列)与 `CheckSameShape`,保留原差异(arg1 不查 `IsConst`、arg2 查)
- §5-2 `SpCopyArrayArgument` 携带解析期已解析的 `ResolvedVar1/2`,ARRAYCOPY 执行期不再二次查找——去掉 issues/17 留下的「两处查找必须同步」耦合约。判据口径:该实参在**解析期折成常量串**(字面量或字面量拼接),不是「源码里写了引号」——运行期定名的形态(如 `RESULTS:0`)仍走执行期解析。两种构造时机下结果一致的理由(review 复核补记):名字是按 `GetScaningLine()` 的所属函数解析私有变量的,而装载期由 `ErbLoader.setArgument` 的线程本地(`SetScaningLineForThread(nextLine)`)、执行期由 `Process.GetScaningLine()` 回退到 `state.ErrorLine`(正在执行的行)各自给出**同一条 ARRAYCOPY 行**,故解析期存下的令牌与执行期再查一次等价;该等价性依赖「执行期无路径改写全局 `scaningLine`」(赋值点只有 `ErbLoader` 的装载收尾与 `LogicalLineParser` 解析 `VARI` 初值),此前提未被测试固定,与下节「未覆盖(夹具边界)」同源
- §5-3 表/列解析抽到 `Utils.DataTable.FindTable`/`RequireColumn`:DT_COLUMN_OPTIONS 与 `DataTableRowSetMethod.CheckName` 共用,另有 5 处 DT_* 的取表改用 `FindTable`(语义不变,缺表仍 `-1`、缺列仍抛 `DTLackOfNamedColumn`)。**未实施**:「`id` 列不可改」守卫是否也应覆盖 DT_COLUMN_OPTIONS 改列默认值的路径——该守卫今天只加在写行路径上,扩展与否属产品判断,已登记为 issues/32 第 6 项
- §6 `ExpressionArrayArgument` 改收 `List<AExpression>`(元素非空由 `ToNonNullTerms` 单点断言;原 `CopyTo` 把元素可空性在类型上洗掉);`FORM_STR_ANY` 的内部列表同步改非可空。注意这是**元素契约**而非长度契约:`RETURN` 无实参时 `TermList` 合法为空表
- 回归网:`Emuera.Tests` 新增 `ArraycopyArgumentTests`(29 例:未定义名/标量/角色数据/arg2 常量/arg1 常量放行这一保留差异/维数/类型/正控制/解析期令牌同一性/运行期定名/常量串拼接/形状比对跳过/level 1 对照/读法边界)与 `ExpressionArrayArgumentTests`(18 例:构造器与 `TermList` 的类型层契约 + 反空跑对照、INT_ANY 五条形态、省略元素先被拒、FORM_STR_ANY、SP_SAVECHARA、空表快照),全量 707 → **754** 通过
- 未覆盖(夹具边界):ARRAYCOPY 的执行期分支走 `Process.ScriptProc.doNormalFunction`,现夹具没有运行中的 `Process`/`ProcessState` 入口,故「执行期复用 `ResolvedVar1/2`」只有间接证据(令牌 `Same` + 实参是常量串 ⇒ 必进该分支);`ParserMediator.Warn` 在 null console 下不写 `warningList`,用例改以 `line.ErrMes` 与 `line.IsError` 观察告警键与级别



## 影响

- 方案第 1~4 节的全部改动为注解与注释级，IL 除元数据外不变；无运行行为变化
- 第 5~6 节（issues/17 review 移交）是结构性改动，超出「注解与注释级」：抽实参检查辅助会改 `SP_COPY_ARRAY_Arguments` 的发出物、共享 DT 解析器会改 `DT_COLUMN_OPTIONS` 的错误处置与 `id` 列守卫的有无、`TermList` 的元素类型变更会波及全部消费点；这三项须各自给判据与测试，不与第 1~4 节同批（2026-10-06：用户决定并入本票；已按「判据 + 回归网」落地，见「实施结果」§5/§6，`Emuera.Tests` 707 → 754）
- 改动后仍须满足两种声音后端 `-p:Nullable=enable` 的 CS86xx 为 0（`WarningsAsErrors>nullable</WarningsAsErrors>` 已开启）与 `Emuera.Tests` 通过
- 删除冗余 `!` 时须逐处构建确认：`!` 除断言外还参与流分析收窄，删除可能让后续位置的判据失效（issues/13 已实测该现象），必要时以局部回退处理
