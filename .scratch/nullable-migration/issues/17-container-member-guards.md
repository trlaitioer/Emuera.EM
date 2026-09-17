# Nullable:容器与成员断言的零散守卫缺失

Status: ready-for-agent

## 任务

issues/13 把接收者为 `AExpression?` 的元素断言收敛到 `ArgumentListExtensions.Required`，其余容器的元素断言逐点保留。逐类核实后，绝大多数有界（`XmlNodeList` 的索引均落在 `Count` 内、`List<AExpression?>` 的空元素在构造期已被拒绝、`VariableToken?[]` 的赋值点后紧跟判空），但有一组位置缺守卫或守卫写错对象。本票逐个修正。

### 范围

| 位置 | 现状 | 判据 |
| --- | --- | --- |
| `Process.ScriptProc.cs` 的 `doFlowControlFunction`（ARRAYCOPY 分支） | `vars` 声明为 `VariableToken?[]`，其一号分支在每次 `GetVariableToken` 赋值后判空并抛错，另一分支不判空，随后直接 `vars[0]!`/`vars[1]!` | 可达性尚未证实：需追 `SpCopyArrayArgument` 的构造是否保证实参名可解析为变量。若可达则补判空，若不可达则补理由注释 |
| `Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction` | `DataColumnCollection` 的字符串索引器在列不存在时返回 null；同族其余同类读取都在前面 `throw new CodeEE`，此处只把 `RESULT` 置 0 后继续，随后解引用 | 可达。需要产品决策：抛 `CodeEE` 还是返回 0 |
| `Expression/ExpressionParser.cs` 的 `ReduceArguments`（`isDefine` 分支） | 判空写成 `if (terms.Last == null)`，而紧接 `AddLast` 之后该节点必非空；真正要判的是 `terms.Last.Value == null` | 守卫对象写错。改为判值后，null 值在 `isDefine` 路径（`ErbLoader` 的 `#DIM` 等）是否可达需与 issues/20 一并确认 |
| `UI/Game/HtmlManager.cs`（`HTMLSPLIT` 系列结果取值） | 取 `output[0]!`，当被调用方法返回空数组时该槽位从未写入 | 弱断言。需确认空数组是否可达，可达则补判空 |

### 非目标

以下经核实不构成缺陷，不在本票内改动：

- `XmlNodeList` 的 15 处元素断言：BCL 索引器仅在越界时返回 null，各站点均在 `Count`/`Math.Min` 界内。
- `DataColumnCollection` 的整型索引器：BCL 声明为非空，越界抛 `IndexOutOfRangeException`；对应断言冗余，归 issues/22 清理。
- `List<AExpression?>`（`SpSetArrayArgument.TermList`）2 处：两条构造路径都对 null 元素 `assignwarn` 并返回 null。

## 方案

1. 先定 `ARRAYCOPY` 那一分支的可达性：读 `SpCopyArrayArgument` 的构造与 `checkArgumentType`，若解析期已保证变量名可解析则记为「不变量成立」并补注释，否则归入本票修复。
2. `DT_COLUMN_OPTIONS` 与同族对齐（列不存在即 `CodeEE`），选择理由写入票内；这是行为变更，需在 changelog 记录。
3. `ReduceArguments` 的守卫改为判值；改动会影响 `isDefine` 路径的错误信息（原本可能悄悄产生 null 元素），需与 issues/20 的复现共用同一组用例。
4. `HtmlManager` 的空数组判断先复现再决定。

## 影响

- 改动分布在解析器、指令实现与 UI 三处，其中 `DT_COLUMN_OPTIONS` 与 `ReduceArguments` 属行为变更，须与可空迁移的元数据改动分开提交
- 本票不处理 `JumpTo` 族（issues/16）、省略参数（issues/15）、`as` 转换（issues/18）与调试期求值上下文（issues/20）
