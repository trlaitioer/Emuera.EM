# Nullable:保留断言的理由注释与契约诚实化

Status: ready-for-agent

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
| `ExpressionMediator.cs`（`ConvertStringType` 3 处）与 `Creator.Method.cs`（`StrChangeStyleMethod.GetStrValue` 2 处）的 `Strings.StrConv` | **判定更正：不属冗余，本票不删。** `StrConv` 在 .NET 上的签名是 `string? StrConv(string? str, VbStrConv, int LocaleID = 0)`，该 `!` 抑制的是 CS8603；不支持的转换抛 `ArgumentException`。两处已有同义 TODO（issues/08 迁移时补入，早于本票），本票只复核 TODO |
| `Creator.Method.cs` 的 `DataColumnCollection` 整型索引 | **已完成**：`e70ec779 clean: 移除冗余的 ! 标识` 把 `DataTableColumnManagementMethod.GetIntValue` 的 `Operation.Names` 分支由 `dt.Columns[i]!.ColumnName` 改为 `dt.Columns[i].ColumnName`；全仓已无整型索引器断言 |
| `Creator.Method.cs` 的 `ArrayMultiSortExMethod` 中 `is VariableTerm` 分支后再 `as` | 两分支均非空 |
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
- `Creator.Method.cs` 的 `XmlGetMethod`/`XmlSetMethod` 中 `nodes` 局部——赋值发生在 `try` 内、唯一 `catch` 抛出，编译器无法据此证明「读取前必已赋值」；提取方法或调整结构让赋值可被证明，而不是用 `= null!`。
- `HtmlManager.tagAnalyze` 的 `ref height!`/`ref width!`/`ref x!`/`ref y!` 等 15 处与 `ParseParam4IntNum(ref CreateIfNull(ref box!).color!, …)`（issues/21 的 D 类）——`Utils.ParseMixedNum(ref MixedNum num, …)` 的第一步就是 `if (num == null) num = new MixedNum();`，`CreateIfNull<T>(ref T obj)` 同样以 null 为正常入参；`!` 断言的内容为假。把形参改为 `ref MixedNum?`/`ref int[]?`，调用点的 `!` 一并删除（元数据级，零行为变化）。

### 4. 常量折叠实参的具名入口

`GetIntValue(null!)`/`GetStrValue(null!)` 共 18 处（`ArgumentBuilder.cs` 14、`VariableEvaluator.cs` 的 `FindChara` 两重载 4），另有 `ExpressionParser.ToStrFormTerm` 的 1 处 `strf.GetString(null!)`（票面原先记的「约 65 处」是 issues/14 对全部「调用实参位 `null!`」的计数，口径不同）。这些调用当前安全，但判据分三类：`ArgumentBuilder` 各处由 `is SingleTerm`/`allConst` 支配，`ToStrFormTerm` 由 `strf.IsConst` 支配，`FindChara` 两重载则没有此类判据——它靠 `FindcharaMethod` 的 `ArgType.CharacterData` 保证实参是 character-data 变量，而该类 token 的取值不读 `exm`。按 issues/13 对 `Required`/`BuiltArgument` 的做法收敛为具名入口，让「此处无求值上下文」只表达一次（例如单值 term 上的无上下文取值成员）。

### 5. issues/17 两轴 review 的相邻重复（本票记录，来自 issues/17 实施后的 review）

三处均为判定级气味，非缺陷；与 issues/17 的改动同址，但那三处已按最小改动落地，收敛留给本票：

- `ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments`：`terms[0]`/`terms[1]` 两块约 15 行仅差下标、参数序号与局部名（`vars[0]`/`vars[1]`、`term`/`term1`），第一个实参块有 `return null` 而第二个原本没有（issues/17 已补）。抽出一个「按序号检查一个数组实参」的辅助后，两块的差异只剩实参序号，`return null` 也从「要记得写」变成「不可漏写」。
- `ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments` × `Process.ScriptProc.cs` 的 `doNormalFunction`（ARRAYCOPY 分支）：字面量实参名 `GetVariableToken(term.Str, null, true)` 的解析存在两处，解析期那处的结果未传给执行期那处。issues/17 已在执行期补注释说明「两处必须同步」，属把耦合写明；若要消除，可让 `SpCopyArrayArgument` 携带解析期已得到的 `VariableToken?`，执行期只在动态分支重解析。
- `Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction`：表/列解析内联复刻了 DT 函数族（`Creator.Method.cs` 的 `DataTableColumnManagementMethod` 与 `DataTableRowSetMethod.CheckName`）的形状——`TryGetValue` 取表、缺表 `-1`、缺列抛 `DTLackOfNamedColumn`。共享一处解析器可让族内约定只有一份；顺带说明：族内 `id` 列不可改的守卫（`DTCanNotEditIdColumn`）只加在写行的路径上，`DT_COLUMN_OPTIONS` 改的是列默认值，是否需要该守卫属产品判断，需与共享解析一起定。

### 6. `ExpressionArrayArgument.TermList` 的契约诚实化（issues/17 审计移交）

`Argument.cs` 的 `ExpressionArrayArgument` 声明 `AExpression[] TermList`，却由 `List<AExpression?>` 经 `CopyTo` 填充，把元素可空性在类型上洗掉。两条构造路径当前都已拒绝 null 元素（`ArgumentBuilder.cs` 的 `INT_ANY_ArgumentBuilder` 经 `checkArgumentType` 拒绝、`FORM_STR_ANY_ArgumentBuilder` 自建非空 term），故非缺陷；但读取侧（`Process.ScriptProc.cs` 的 `INT_ANY` 消费、`Instraction.Child.cs` 的 `ExpressionArrayArgument` 取值）依赖的是这条不写在类型上的不变量。处置二选一：构造函数改收 `List<AExpression>`（调用点各自收窄），或字段改 `AExpression?[]` 并由消费点处理。

## 影响

- 方案第 1~4 节的全部改动为注解与注释级，IL 除元数据外不变；无运行行为变化
- 第 5~6 节（issues/17 review 移交）是结构性改动，超出「注解与注释级」：抽实参检查辅助会改 `SP_COPY_ARRAY_Arguments` 的发出物、共享 DT 解析器会改 `DT_COLUMN_OPTIONS` 的错误处置与 `id` 列守卫的有无、`TermList` 的元素类型变更会波及全部消费点；这三项须各自给判据与测试，不与第 1~4 节同批
- 改动后仍须满足两种声音后端 `-p:Nullable=enable` 的 CS86xx 为 0（`WarningsAsErrors>nullable</WarningsAsErrors>` 已开启）与 `Emuera.Tests` 通过
- 删除冗余 `!` 时须逐处构建确认：`!` 除断言外还参与流分析收窄，删除可能让后续位置的判据失效（issues/13 已实测该现象），必要时以局部回退处理
