# Nullable:保留断言的理由注释与契约诚实化

Status: ready-for-agent

## 任务

issues/14 判定为「不变量成立」的断言将保留 `!`。本票为其补上说明「编译器看不到哪条不变量」的注释，同时清理经判定确属冗余的断言，并把若干「实际可空却声明非空」的位置改为诚实的可空契约。全部改动为元数据级（注解、注释、只读访问器、局部类型），不改变运行行为。

## 方案

### 1. `as` 转换的理由注释（约 49 处）

issues/14 判定 `(x as T)!` 中 49 处有前置类型检查，需逐点注明保证来自何处，例如：`FunctionMethod` 的实参类型表（含 `RefAny1D`/`AllowConstRef` 与 `UniqueRestructure` 的组合）、`Word.Type` 的分派常量、`FunctionCode`→`ArgumentBuilder` 的分派、`VariableCode` 的数组形状判定、委托注册的框架契约。注释写「由哪个检查或分派保证」，不复述代码。

无保护的 3 处不在此列（issues/18）。

### 2. 冗余 `!` 清理

| 位置 | 依据 |
| --- | --- |
| `ExpressionMediator.cs`（`ConvertStringType` 3 处）与 `Creator.Method.cs`（`StrChangeStyleMethod.GetStrValue` 2 处）的 `Strings.StrConv` | **判定更正：不属冗余，本票不删。** `StrConv` 在 .NET 上的签名是 `string? StrConv(string? str, VbStrConv, int LocaleID = 0)`，该 `!` 抑制的是 CS8603；不支持的转换抛 `ArgumentException`。两处已有同义 TODO（issues/08 迁移时补入，早于本票），本票只复核 TODO |
| `Creator.Method.cs` 的 `DataColumnCollection` 整型索引 | **已完成**：`e70ec779 clean: 移除冗余的 ! 标识` 把 `DataTableColumnManagementMethod.GetIntValue` 的 `Operation.Names` 分支由 `dt.Columns[i]!.ColumnName` 改为 `dt.Columns[i].ColumnName`；全仓已无整型索引器断言 |
| `Creator.Method.cs` 的 `ArrayMultiSortExMethod` 中 `is VariableTerm` 分支后再 `as` | 两分支均非空 |
| `Instraction.Child.cs` 的 `VARI_Instruction` 对 `IntAsignArgument.Exp` | 两条构造路径都提供表达式，无非空构造器在用 |
| `Instraction.Child.cs` 的 `CALLSHARP_Instruction` 对 `arg.CallFunc` | 该构造器以常量字符串（`SingleStrTerm`）构造函数名，`IsConst` 必为真，`SetJumpTo` 中的非常量分支不可达；`CallFunc` 保持 null 的唯一路径是 `!manager.HasMethod` 分支，该处以 `Warn(..., isError: true)` 阻断执行——`!` 的实际保证来自这一错误标记，而非 `IsConst` |

`ConstantData` 的 `GetCharacterTemplate*` 无可更正项：该文件（写票时与当前）唯一 TODO 就在 `GetKeywordDictionary`（潜在缺陷，见 issues/19）。该组方法返回 `CharacterTemplate?` 且 6 个调用方全部判空（`VariableEvaluator.cs` 的 1537/1549/1561/1985/1999/2066），确不是问题。

### 3. 契约诚实化（把 null 写进类型）

以下位置经判定「实际可空或读取前必判空」，`= null!`/`!` 掩盖了真实状态，应改为可空类型：

- `ErbLoader.setJumpTo` 的局部 `FunctionNotFoundName` 与 `Instruction.cs` 中 `AInstruction.SetJumpTo` 的 `ref string FunctionoNotFoundName`（另有 8 处同型 override）——null 是「未上报函数名」的正常信号，读取处本就包在判空内，类型应为 `string?`（含 `ref string?`）。
- `UI/Game/Image/GraphicsImage.cs` 的 `brush`/`pen`/`font`——`Dispose` 路径置 null，读取处均判空，公开访问器却在传播非空签名。
- `Process.State.cs` 的 `IntoFunction` 的 `exm` 参数、`Process.cs` 的 `SetScaningLineForThread` 的 `line` 参数——调用方传 null 是既定语义，参数应可空。
- `VariableToken` 构造函数中未使用的 `VariableData` 参数。
- `Process.CalledFunction.cs` 的 `ConvertArg` 的 `out string errMes`——仅在返回 null 时被读取，应为 `out string?`。
- `UI/Game/HtmlManager.cs` 中作为换行哨兵的 null 元素（`buttonList.Add(null!)`）——容器元素类型 `List<ConsoleButtonString>` 应改为 `List<ConsoleButtonString?>`；消费方 `PrintStringBuffer.ButtonsToDisplayLines` 的形参与 `m_buttonsToDisplayLine` 字段须同步改，否则该处既有的 `if (buttonList[i] == null)` 判空与新元素类型对不上。
- `Creator.Method.cs` 的 `XmlGetMethod`/`XmlSetMethod` 中 `nodes` 局部——赋值发生在 `try` 内、唯一 `catch` 抛出，编译器无法据此证明「读取前必已赋值」；提取方法或调整结构让赋值可被证明，而不是用 `= null!`。

### 4. 常量折叠实参的具名入口

`GetIntValue(null!)`/`GetStrValue(null!)` 共 18 处（`ArgumentBuilder.cs` 14、`VariableEvaluator.cs` 的 `FindChara` 两重载 4），另有 `ExpressionParser.ToStrFormTerm` 的 1 处 `strf.GetString(null!)`（票面原先记的「约 65 处」是 issues/14 对全部「调用实参位 `null!`」的计数，口径不同）。这些调用当前安全，但判据分三类：`ArgumentBuilder` 各处由 `is SingleTerm`/`allConst` 支配，`ToStrFormTerm` 由 `strf.IsConst` 支配，`FindChara` 两重载则没有此类判据——它靠 `FindcharaMethod` 的 `ArgType.CharacterData` 保证实参是 character-data 变量，而该类 token 的取值不读 `exm`。按 issues/13 对 `Required`/`BuiltArgument` 的做法收敛为具名入口，让「此处无求值上下文」只表达一次（例如单值 term 上的无上下文取值成员）。

## 影响

- 全部改动为注解与注释级，IL 除元数据外不变；无运行行为变化
- 改动后仍须满足两种声音后端 `-p:Nullable=enable` 的 CS86xx 为 0（`WarningsAsErrors>nullable</WarningsAsErrors>` 已开启）与 `Emuera.Tests` 通过
- 删除冗余 `!` 时须逐处构建确认：`!` 除断言外还参与流分析收窄，删除可能让后续位置的判据失效（issues/13 已实测该现象），必要时以局部回退处理
