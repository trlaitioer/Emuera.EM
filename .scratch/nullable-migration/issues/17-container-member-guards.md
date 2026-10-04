# Nullable:容器与成员断言的零散守卫缺失

id: nullable-migration-17
Status: in-progress

## 任务

issues/13 把接收者为 `AExpression?` 的元素断言收敛到 `ArgumentListExtensions.Required`，其余容器的元素断言逐点保留。逐类核实后，绝大多数有界（`XmlNodeList` 的索引均落在 `Count` 内、`List<AExpression?>` 的空元素在构造期已被拒绝、`VariableToken?[]` 的赋值点后紧跟判空），但有一组位置缺守卫或守卫写错对象。本票逐个修正，并连带处理两处同型的「warn 后缺 `return` / 缺判空」相邻缺陷。

完成标准：四处站点的守卫归位（两处改为判空或抛错、一处为等值替换、一处只补理由注释），`Emuera` 与 `Emuera.Tests` 的 CS86xx 为 0、`Emuera.Tests` 通过；两处行为变更（`DT_COLUMN_OPTIONS`、`ReduceArguments`）须与可空迁移的元数据改动分开提交。

### 范围与处置（2026-09-26 复核）

| 位置 | 现状 | 复核判据与处置 |
| --- | --- | --- |
| `Process.ScriptProc.cs` 的 `doNormalFunction`（ARRAYCOPY 分支） | `vars` 声明为 `VariableToken?[]`，动态分支在每次 `GetVariableToken` 赋值后判空并抛错，字面量分支不判空，随后直接用 `vars[0]!`/`vars[1]!` | **不可达（判定成立）**，依据见下「站点 1」；处置＝补理由注释，不加判空 |
| `Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction` | 表不存在时置 `RESULT = -1` 后继续，紧接 `dict[key]`；列不存在时置 `RESULT = 0` 后继续，紧接 `dt.Columns[cName]!` 的解引用 | **可达，两处都是崩溃**；处置＝缺表 `RESULT = -1` + `return`（贴合 DT 函数族「缺表返回 -1」的约定），缺列抛 `CodeEE(DTLackOfNamedColumn)` |
| `Expression/ExpressionParser.cs` 的 `ReduceArguments`（`isDefine` 分支） | 判空写成 `if (terms.Last == null)`，而紧接 `AddLast` 之后该节点必非空；真正要判的是 `terms.Last.Value == null` | **守卫对象写错（判定成立）**，但后果不是死代码：null 元素会被 `terms.Last!.Value!.IsInteger`/`GetOperandType()` 解引用成 NRE；处置＝收窄为局部变量后再 `AddLast`，顺带去掉两处 `terms.Last!.Value!` |
| `Runtime/Script/Statements/Function/BuiltinMethod/PrivateAdditions.cs` 的 `HtmlSubStringMethod.GetStrValue`（`HTML_SUBSTRING`；`HtmlManager.cs` 内无该取值） | 取 `output[0]!`，`output` 来自 `VEvaluator.RESULTS_ARRAY` | **判据更正**：该 `!` 抑制的是 CS8603，删掉必报错，不属「冗余 `!`」；真正的不变量是拷贝长度 `outputlength >= 1`；处置＝`return strs[0]`（与 `output[0]` 恒等且静态非空），`Array.Copy` 保留 |

> 同型相邻缺陷（本票一并修）：`ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments` 中第 2 实参的 `IsArray1D/IsArray2D/IsArray3D` 检查缺 `return null`（第 1 实参有），与 `DT_COLUMN_OPTIONS` 缺 `return` 同型。

### 复核更正的票面记载

1. 站点 2 原记「同族其余同类读取都在前面 `throw new CodeEE`」不准确：DT 函数族对**表不存在**返回 `-1`；对**列不存在**分两类——只读查询（`DataTableColumnManagementMethod` 的 `Operation.Check`）返回 0，写入型路径经 `CheckName` 抛 `CodeEE(DTLackOfNamedColumn)`。`DT_COLUMN_OPTIONS` 是写操作，取后者。这两条约定正是本票两处处置不同的理由。
2. 站点 3 原记「`isDefine` 路径（`ErbLoader` 的 `#DIM` 等）」定位有误：`isDefine=true` 的两处调用是 `ErbLoader.parseLabel` 的 `@label` 实参表；`#DIM` 元素数走 `UserDefinedVariableData.Create` 的 `isDefine=false`。**故与 issues/20 不共用复现用例**（原方案的「与 issues/20 的复现共用同一组用例」作废）。
3. 原方案「这是行为变更，需在 changelog 记录」作废：本仓库无实际维护的 changelog（`Readme/` 下为上游旧档，issues/15 已确认），行为变更记入本文。

### 非目标

以下经核实不构成缺陷，不在本票内改动：

- `XmlNodeList` 的 15 处元素断言（`BuiltinMethod/PrivateAdditions.cs` 11 处 `nodes[...]!` + `Lang.cs` 2 处 + `sMap[key[0]!...] = val[0]!...` 2 处）：BCL 索引器仅在越界时返回 null，各站点均在 `Count`/`Math.Min` 界内。
- `DataColumnCollection` 的整型索引器：对应断言**已由 `e70ec779 clean: 移除冗余的 ! 标识` 移除**（现为 `dt.Columns[i].ColumnName`，全仓已无整型索引器断言，剩余的 `!` 都在字符串索引器上）。反射实测其注解为 `this[int]` = NotNull、`this[string]` = Nullable，与该删除一致。
- `List<AExpression?>`（`SpSetArrayArgument.TermList`）2 处：两条构造路径都对 null 元素 `assignwarn` 并返回 null。
- `ExpressionArrayArgument.TermList` 是 `AExpression[]` 而由 `List<AExpression?>` 经 `CopyTo` 填充（`Argument.cs`）：两条构造路径都已保证非空（`INT_ANY_ArgumentBuilder` 经 `checkArgumentType` 拒绝 null 元素，`FORM_STR_ANY_ArgumentBuilder` 自建非空 term），非缺陷；其「契约诚实化」属 issues/22 的可选条目。

## 方案

### 站点 1：ARRAYCOPY 字面量分支（补理由注释）

不可达的四条依据：

1. 该分支要求两实参都是 `SingleTerm`；`SingleTerm` 的具体子类只有 `SingleStrTerm`/`SingleLongTerm`（`Term.cs`），后者被 `SP_COPY_ARRAY_Arguments` 的 `argumentTypeArray = [typeof(string), typeof(string)]` 经 `checkArgumentType` 的 `GetOperandType()` 比较拒绝。
2. 解析期对 `terms[i] is SingleStrTerm` 必然用同一个 `GetVariableToken(term.Str, null, true)` 解析；失败即 `warn(..., level 2)` → `line.IsError`、`CreateArgument` 返回 null，`runScriptProc` 在派发前对 `IsError` 行抛 `CodeEE`。
3. 执行期重解析的上下文与解析期一致：`GetVariableToken` 的上下文相关分支只有 private 变量与 `LOCAL/ARG/LOCALS/ARGS`；解析期 `scaningLine` 为正在解析的 label，执行期 `GetScaningLine()` 回落 `state.ErrorLine`（＝当前执行行），两者的 `ParentLabelLine` 同为该函数；private 变量在 `FunctionLabelLine` 上只增不删。
4. 名字字符串同源：`checkArgumentType` 已把 term `Restructure` 后写回列表，`SpCopyArrayArgument` 持有同一批 term。

注释写英文（`Emuera` 约定），并点名 `SP_COPY_ARRAY_Arguments`——本处看不到该约定，任何让 builder 不再解析字面量名的改动都会静默退回 NRE。

### 站点 2：`DT_COLUMN_OPTIONS`（两处都改）

```csharp
if (!dict.TryGetValue(key, out System.Data.DataTable? dt))
{
    // Same convention as the DT_* functions: a missing table reports -1 instead of throwing.
    exm.VEvaluator.RESULT = -1;
    return;
}

if (dt.Columns[cName] is not DataColumn column)
{
    // Nothing can be set without the column, and the DT_* family throws here as well.
    throw new CodeEE(string.Format(trerror.DTLackOfNamedColumn.Text, "DT_COLUMN_OPTIONS", key, cName));
}
```

保持 `cName` 先于 `key` 求值的既有顺序；`is not DataColumn column` 与索引器的 Nullable 注解匹配，省掉一次 `Contains` 查找与一处 `!`。`DTLackOfNamedColumn` 在 `Lang.cs`（日文原文）与 `emuera-eng.xml`/`emuera-zhs.xml` 中均已存在，无需新增条目。

### 站点 3：`ReduceArguments`（守卫改为判值）

```csharp
AExpression? argTerm = ReduceExpressionTerm(wc, termEndWith_Assignment);
if (argTerm == null)
{
    throw new CodeEE(trerror.CannotOmitFuncArg.Text);
}

terms.AddLast(argTerm);
```

后续 `=` 分支与「无 `=` 时补 `NullTerm`」分支改读该局部变量，等价于原文对 `terms.Last!.Value!` 的两次读取。`AddLast` 由「先入列再判空」改为「判空后再入列」——对被判出的 null 元素而言入列本就无意义（`ErbLoader.parseLabel` 在失败路径丢弃整个列表，`setLabelsArg` 的 catch 只记警告），异常的抛出点与顺序不变。

### 站点 4：`HtmlSubStringMethod.GetStrValue`（返回源元素）

```csharp
Array.Copy(strs, output, outputlength);
return strs[0];
```

`outputlength >= 1` 的依据：`HtmlManager.HtmlSubString` 两条返回路径都构造 2 元素数组；`RESULTS` 的 1D 字符串槽位长度不可能小于 100（`ConstantData` 对非 local 的 1D 变量默认 100，`changeVariableSizeData` 拒绝 `length <= 0` 与 `length < 100`，`VariableCode.RESULTS` 无 `__CAN_FORBID__`；`VariableData` 只在构造期分配该数组，全仓无对该槽位的再赋值）。`output[0]!` 本身不防「空数组」——那只会在 `output.Length == 0` 时抛 `IndexOutOfRangeException`，与可空性无关。

### 相邻缺陷：`SP_COPY_ARRAY_Arguments` 第 2 实参补 `return null`

复核为**无可观测差异**：`warn` 已置 `IsError`，且后续「维度不一致」检查对「非数组的第 2 实参」必然返回 null，故该分支的产物无论如何都到不了执行期。改动仅为与第 1 实参对称的防护，**不写护栏测试**（断言只能钉住内部状态，属实现耦合）。

### 测试（seam 与用例）

seam：`ExpressionParser.ReduceArguments`（解析器函数入口）、`LogicalLineParser.ParseLine` + `ArgumentParser.SetArgumentTo`（`OmittedArgumentTests` 已用的解析期入口）、`AInstruction.DoInstruction`（`DT_COLUMN_OPTIONS` 的执行期入口，`exm` 取 `GlobalStatic.EMediator`）。站点 1、4 与相邻缺陷无红用例可写（注释、等值替换、无可观测差异），不加测试。

| 用例 | 断言 |
| --- | --- |
| `ReduceArguments` 首位实参省略（`isDefine`） | 抛 `CodeEE`（原为 NRE） |
| `ReduceArguments` 中间位实参省略（`isDefine`） | 抛 `CodeEE` |
| 正控制：`FLAG:0=1` | 返回 2 元素（值项为默认值项），修复前后都通过 |
| `DT_COLUMN_OPTIONS` 表不存在 | 不抛异常且 `RESULT == -1` |
| `DT_COLUMN_OPTIONS` 列不存在 | 抛 `CodeEE` |
| 正控制：表/列齐备 | 默认值写入该列 |

### 提交拆分

1. `fix(script): DT_COLUMN_OPTIONS 缺表返回 -1、缺列报错，补齐函数定义实参守卫`（站点 2、3 + 测试）
2. `refactor(script): ARRAYCOPY 字面量分支补不变量注释与对称 return，HTML_SUBSTRING 返回非空源元素`（站点 1、4 + 相邻缺陷）
3. `docs(nullable): 回填 17 的判定更正与实施结论`

### 执行结果（2026-09-26）

四处站点与相邻缺陷全部落地，方案未作改动；`Emuera` 侧 5 个文件、`Emuera.Tests` 2 个文件。

红→绿实测（每处先写用例再改实现）：

| 站点 | 红（改前） | 绿（改后） |
| --- | --- | --- |
| `ReduceArguments` 首位/中间位省略实参 | `NullReferenceException`，落在 `terms.Last.Value.IsInteger` | `CodeEE(CannotOmitFuncArg)` |
| `DT_COLUMN_OPTIONS` 缺表 | `KeyNotFoundException`，落在 `dict[key]` | `RESULT == -1`，无异常 |
| `DT_COLUMN_OPTIONS` 缺列 | `NullReferenceException`，落在 `column.DataType` | `CodeEE(DTLackOfNamedColumn)` |

无红用例的三处已在上文交代理由：ARRAYCOPY 注释、`HtmlSubStringMethod` 的等值替换、`SP_COPY_ARRAY_Arguments` 的对称 `return`（其产物必然被下游维度检查作废）。

验证：`Emuera.Tests` 由 309 条增至 315 条（`ExpressionParserTests` +3、新增 `DtColumnOptionsInstructionTests` 3 条），全绿；`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental` 0 错误 / 83 警告（全部为既有非 CS86xx：CS8981、CS4014、CA*、WFO0003，与基线一致）、CS86xx = 0。

验收发现（已修）：`DtColumnOptionsInstructionTests` 首版直接把 `RESULT` 写成 -1 后未还原，污染了同程序集里插值 `{RESULT}` 的既有用例 `CallformCacheTests.MultiInterpolationForm_UsesStrKey`；该用例自己不改写 `RESULT`，故表现为「按用例发现顺序时红时绿」。改为用例上 `IDisposable` 还原 `RESULT` 与 `DataDataTables` 的临时表后，全量 315 条与「两个类同批跑」的隔离用例均稳定通过。（该既有用例依赖未自设的全局 `RESULT`，属其自身脆弱性，不在本票范围。）

未覆盖：WMP（`Debug`）配置与实机 era 游戏验证未跑——本票改动不触及声音后端，且两处行为变更只改变错误路径的处置与诊断文本，正常路径的执行结果不变。

两轴 review（Standards / Spec）无实现错误；气味类发现三处不在本票处理，连同「非目标」记下的 `ExpressionArrayArgument.TermList`，一并记入 issues/22：`SP_COPY_ARRAY_Arguments` 的两个实参检查重复（可抽 `CheckArrayArg`，抽出后「缺 `return null`」不可再表达）、该处与 `doNormalFunction` 的字面量名重复查询、DT 表/列解析未与 DT 函数族共享一处解析器。

## 曾考虑的替代方案

1. ARRAYCOPY 把字面量分支并入已判空的动态分支（`GetStrValue(exm)` 对 `SingleStrTerm` 不分配，可让全部检查走 `CodeEE`）：能从结构上消除两次解析与跨文件约定，但改动指令的正常执行路径，且与迁移期「少加运行时判断、保持现有运行行为」相悖；注释已足以表达不变量，不采用。
2. `DT_COLUMN_OPTIONS` 缺表也抛 `CodeEE`：与 DT 函数族「缺表返回 -1」的约定分叉，同一族里「表不存在」的处置会不一致，不采用。
3. `HtmlSubStringMethod` 保留 `!` 并补理由注释：该断言的对象是「目标槽位已被写入非空值」，直接返回源元素可让不变量在类型上成立，断言与注释都不再需要，不采用。
4. 直接删除 `output[0]!`：返回 `string?` 给 `string`，CS8603，不可行。
5. 为「被拒绝的行不产出 `Argument`」写测试：现状下被拒产物必然被下游维度检查作废，测试只能钉住内部状态，属实现耦合，不采用。

## 影响

- 行为变更两处，均为「崩溃/劣化诊断改为既有约定的错误处置」，无正常路径行为变化：
  - `DT_COLUMN_OPTIONS`：缺表由 `KeyNotFoundException`（`Process.handleException` 报「予期しないエラー」+ 栈）改为 `RESULT = -1`；缺列由 NRE 改为 `CodeEE(DTLackOfNamedColumn)`。
  - `ReduceArguments`：`@label` 实参表的省略项由 NRE 改为 `CodeEE(CannotOmitFuncArg)`；两者同样被 `setLabelsArg` 的 `catch (Exception)` 包成 `FuncArgError` 警告，仅诊断文本变化（原文本含 `System.NullReferenceException`）。
- 无行为变更两处：ARRAYCOPY 的注释；`HtmlSubStringMethod` 返回 `strs[0]`（与 `output[0]` 恒等）。
- 相邻缺陷一处：`SP_COPY_ARRAY_Arguments` 第 2 实参补 `return null`，无可观测差异。
- 本票不处理 `JumpTo` 族（issues/16）、省略参数（issues/15）、`as` 转换（issues/18）与调试期求值上下文（issues/20）。
