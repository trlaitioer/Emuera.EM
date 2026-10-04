# Nullable:显式 `@子键` 的局部变量引用在无父标签时解引用 null

id: nullable-migration-25
Status: superseded

## 任务

`IdentifierDictionary.GetVariableToken` 对带显式子键的局部变量引用没有做与「空子键」路径相同的收窄：

```csharp
if (string.IsNullOrEmpty(subKey))
{
    if (line?.ParentLabelLine == null)          // ← 只有这条路径收窄
    {
        throw new CodeEE(string.Format(treer.CannotGetKeyNotExistRunningFunction.Text, key));
    }
    subKey = line.ParentLabelLine.LabelName;
}
else
{
    ParserMediator.Warn(treer.CannotRecommendCallLocalVar.Text, line!, 1, false, false);
}

// Only the subKey-less path above proves a parent label exists; with an explicit subKey the
// dereference inside keeps its previous behavior
LocalVariableToken retLocal = value.GetNewVariableTokenIfAbsent(subKey, line!.ParentLabelLine!);
```

`line` 是 `Process.GetScaningLine()`（`scaningLine`）。当它指向一个 `FunctionLabelLine` 且该标签的 `ParentLabelLine` 为 null（`ErbLoader.loadErb` 把 `nextLine.ParentLabelLine = lastLabelLine`，首个标签处 `lastLabelLine` 仍为 null）时，第二个实参是 null。`GetNewVariableTokenIfAbsent` → `GetNewLocalVariableToken` 立即读 `func.LocalLength`/`LocalsLength`/`ArgLength`/`ArgsLength`/`IsSystem`/`LabelName`，于是 `NullReferenceException`。

触发路径：解析某个函数定义的参数（`ErbLoader.setLabelsArg` 先 `parentProcess.scaningLine = label`，再 `parseLabel` → `ExpressionParser.ReduceArguments` → `reduceIdentifier` → `ReduceVariableIdentifier` → `GetVariableToken(key, subId, true)`），且该表达式里出现带 `@子键` 的局部变量引用（`LOCAL@x` 一类），而该标签正是文件里第一个标签。

本票为断言审计（issues/21）判定的 **C 类可达缺陷**，对应站点：

- `Emuera/Runtime/Script/Data/IdentifierDictionary.cs#IdentifierDictionary.GetVariableToken#2`

代码内的注释已经承认这一缺口（「with an explicit subKey the dereference inside keeps its previous behavior」），本票把它变成显式处理。

## 方案

1. 在 `else`（显式子键）分支补上与空子键分支同型的收窄：`line?.ParentLabelLine == null` 时抛 `CodeEE`（可复用 `CannotGetKeyNotExistRunningFunction`，或新增一条更贴切的错误文本）；
2. 或把 `GetNewVariableTokenIfAbsent`/`GetNewLocalVariableToken` 的 `func` 参数改为可空，并在内部对 null 走「无默认尺寸」分支——但这会把「无父标签」的语义埋进变量层，不如（1）直接；
3. 两处 `!`（`line!`、`line!.ParentLabelLine!`）随之删除。

完成标准：显式 `@子键` 的局部变量引用在无父标签时不再产生 `NullReferenceException`；`Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0，测试通过。

## 影响

- **本票由 issues/19 承接**：该票范围表已列 `IdentifierDictionary` 的 `GetVariableToken`（显式 subKey 分支），指向同一处，且其修法方向与本票方案一致（拿不到父标签即抛 `CodeEE`）。本票只保留为 issues/21 判定表里该站点的锚点，不再单独实施
- 行为变化限于「无父标签 + 显式 `@子键`」：现状是空引用异常，改为脚本可见的错误
- 触及 `IdentifierDictionary.GetVariableToken`；合法用法（`@函数` 之内的 `LOCAL@x`）不受影响
- 与 issues/24 同属「首个函数之前 / 无父标签」这一族，根因不同（本票是显式子键缺收窄），故分列
