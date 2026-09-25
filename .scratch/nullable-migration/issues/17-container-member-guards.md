# Nullable:容器与成员断言的零散守卫缺失

Status: ready-for-agent

## 任务

issues/13 把接收者为 `AExpression?` 的元素断言收敛到 `ArgumentListExtensions.Required`，其余容器的元素断言逐点保留。逐类核实后，绝大多数有界（`XmlNodeList` 的索引均落在 `Count` 内、`List<AExpression?>` 的空元素在构造期已被拒绝、`VariableToken?[]` 的赋值点后紧跟判空），但有一组位置缺守卫或守卫写错对象。本票逐个修正。

### 范围

| 位置 | 现状 | 判据 |
| --- | --- | --- |
| `Process.ScriptProc.cs` 的 `doNormalFunction`（ARRAYCOPY 分支） | `vars` 声明为 `VariableToken?[]`，其一号分支在每次 `GetVariableToken` 赋值后判空并抛错，另一分支不判空，随后直接 `vars[0]!`/`vars[1]!` | 已证实**不可达**：`SP_COPY_ARRAY_Arguments` 对字面量实参用同一个 `GetVariableToken(term.Str, null, true)` 解析，失败即 `warn(...)` 并 `return null`（`SetArgumentTo` 随即置 `line.IsError`，执行前抛 `CodeEE`）；`SingleTerm` 的具体子类只有 `SingleStrTerm`/`SingleLongTerm`，后者被 `checkArgumentType` 的 string 类型校验挡下。故只补理由注释，不加判空 |
| `Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction` | `DataColumnCollection` 的字符串索引器在列不存在时返回 null；同族其余同类读取都在前面 `throw new CodeEE`，此处只把 `RESULT` 置 0 后继续，随后解引用 | 可达。需要产品决策：抛 `CodeEE` 还是返回 0 |
| `Expression/ExpressionParser.cs` 的 `ReduceArguments`（`isDefine` 分支） | 判空写成 `if (terms.Last == null)`，而紧接 `AddLast` 之后该节点必非空；真正要判的是 `terms.Last.Value == null`。`terms` 是 `LinkedList<AExpression?>`，`Last` 的静态类型为 `LinkedListNode<AExpression?>?`，故该判空不产生编译告警而是死代码，真正泄漏的是 `terms.Last!.Value!` 上的 `!` | 守卫对象写错。改为判值后，null 值在 `isDefine` 路径（`ErbLoader` 的 `#DIM` 等）是否可达需与 issues/20 一并确认 |
| `Runtime/Script/Statements/Function/Creator.Method.cs` 的 `HtmlSubStringMethod.GetStrValue`（`HTML_SUBSTRING`；`HtmlManager.cs` 内无该取值） | 取 `output[0]!`，而 `output` 的来源是 `VEvaluator.RESULTS_ARRAY`，并非被调用方的返回值；被调用方 `HtmlManager.HtmlSubString` 恒返回 2 元素数组，不存在空数组路径 | 判据修正：真正依赖的不变量是 `RESULTS_ARRAY.Length >= 1`（现有指令已直接写 `RESULTS_ARRAY[1]`）。若承认该不变量，此 `!` 属冗余，可归 issues/22 清理 |

> `DT_COLUMN_OPTIONS_Instruction.DoInstruction` 同段还有一处相邻缺陷：`if (!dict.ContainsKey(key)) { exm.VEvaluator.RESULT = -1; }` 缺少 `return`，随后 `System.Data.DataTable dt = dict[key];`（`DataDataTables` 是普通 `Dictionary<string, DataTable>`）会抛 `KeyNotFoundException`。本票若动这段建议一并处理。

### 非目标

以下经核实不构成缺陷，不在本票内改动：

- `XmlNodeList` 的 15 处元素断言（`Creator.Method.cs` 11 处 `nodes[...]!` + `Lang.cs` 2 处 + `sMap[key[0]!...] = val[0]!...` 2 处）：BCL 索引器仅在越界时返回 null，各站点均在 `Count`/`Math.Min` 界内。
- `DataColumnCollection` 的整型索引器：对应断言**已由 `e70ec779 clean: 移除冗余的 ! 标识` 移除**（现为 `dt.Columns[i].ColumnName`，全仓已无整型索引器断言，剩余的 `!` 都在字符串索引器上）。
- `List<AExpression?>`（`SpSetArrayArgument.TermList`）2 处：两条构造路径都对 null 元素 `assignwarn` 并返回 null。

## 方案

1. `ARRAYCOPY` 那一分支已证实不可达（依据见范围表），按「不变量成立」补理由注释，不补判空。
2. `DT_COLUMN_OPTIONS` 与同族对齐（列不存在即 `CodeEE`），选择理由写入票内；这是行为变更，需在 changelog 记录。
3. `ReduceArguments` 的守卫改为判值；改动会影响 `isDefine` 路径的错误信息（原本可能悄悄产生 null 元素），需与 issues/20 的复现共用同一组用例。
4. `HTML_SUBSTRING` 处不存在空数组路径（见范围表），改为确认 `RESULTS_ARRAY.Length >= 1` 这一不变量；若承认该不变量，则在 issues/22 一并清理这处冗余 `!`。

## 影响

- 改动分布在解析器与指令实现两处，其中 `DT_COLUMN_OPTIONS` 与 `ReduceArguments` 属行为变更，须与可空迁移的元数据改动分开提交
- 本票不处理 `JumpTo` 族（issues/16）、省略参数（issues/15）、`as` 转换（issues/18）与调试期求值上下文（issues/20）
