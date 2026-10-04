# 槽位快照不跟随原地折叠：不可折叠外层会重新求值已折叠的内层实参

Status: needs-triage

## 任务

承接 [issues/05](05-argument-slots-type.md) 的「快照语义定案」与「未决」节——该票把这条语义写进代码注释与票面，但明确不动它：要不要保留须单独立项裁定。

现象：`FunctionMethod.ArgumentSlots` 的访问器读**检查期快照**（ctor 里的 `terms = [.. arguments]`），而原地折叠只改活体表 `FoldTarget`（`FunctionMethodTerm.Restructure`）。外层不可折叠时（`CanRestructure = false` 的 `XML_GET`/`DT_*`/`GDRAW*` 等），访问器读到的是内层调用的原项 ⇒ **每次调用都重新求值那个已被折叠的内层调用**。对纯表达式只是白算一次；对**状态相关的可折叠调用**（如 `VARSIZE`：长度随 `DIM` 变）读到的是「调用时的值」，而旧的活体读法会一直用「首次执行时的常量」。

完成标准（待评估后细化）：

- 裁定**保留**还是**改回**：改回等价只需让访问器与折叠共用一处存储
- 保留 ⇒ 补 `GETMETH`/`GETMETHS` 转交路径的用例；要谈每执行成本，先按 issues/05 决策 4 补基准（仓库现无基准设施）
- 改回 ⇒ 同步改 `ArgumentSlots` 的类注释与 issues/05 的定案段，并确认 13 个 `HasUniqueRestructure` 类（17 处赋值）不受影响

## 方案

待评估。

## 影响

- 行为面：状态相关的可折叠调用取值随之为「调用时的值」，这是**有意的语义变更**而非回归（issues/05 第四轮 review 已把这一维写进票面与类注释）
- 现有用例钉住的是当前语义：`ArgumentSlotsTests` 的 `FoldedCall_StillReadsTheRightValueThroughTheAccessors`、`UnfoldableCall_ReEvaluatesItsFoldedArgumentsAtCallTime`、`Slots_DoNotFollowTheInPlaceFold`
- 与 [issues/09](09-uniquerestructure-argument-copy.md) 同属折叠面，但改的是**读**的一侧，两者可各自独立裁定
