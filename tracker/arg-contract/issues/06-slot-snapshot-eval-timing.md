# 槽位快照不跟随原地折叠：不可折叠外层会重新求值已折叠的内层实参

id: arg-contract-06
Status: wontfix

## 任务

承接 `arg-contract-05` 的「快照语义定案」与「未决」节——该票把这条语义写进代码注释与票面，但明确不动它：要不要保留须单独立项裁定。

现象：`FunctionMethod.ArgumentSlots` 的访问器读**检查期快照**（ctor 里的 `terms = [.. arguments]`），而原地折叠只改活体表 `FoldTarget`（`FunctionMethodTerm.Restructure`）。外层不可折叠时（`CanRestructure = false` 的 `XML_GET`/`DT_*`/`GDRAW*` 等），访问器读到的是内层调用的原项 ⇒ **每次调用都重新求值那个已被折叠的内层调用**。对纯表达式只是白算一次；对**状态相关的可折叠调用**（如 `VARSIZE`：长度随 `DIM` 变）读到的是「调用时的值」，而旧的活体读法会一直用「首次执行时的常量」。

完成标准（2026-10-05 裁定后落地）：

- 裁定**保留**（见「方案」）
- 保留 ⇒ 补 `GETMETH`/`GETMETHS` 转交路径的用例——已落 `ArgumentSlotsTests.ForwardingCall_HandsTheSnapshotTailToTheDownstreamLookup` 与 `ForwardingStringCall_HandsTheSnapshotTailToTheDownstreamLookup`：真走解析 → `Restructure`（尾部实参就地折叠）→ 函数体 `Tail(2)`，断言**转交源**是检查期那一项而非活体表里的常量
  - **未覆盖（据实记）**：下游对转交段的实际消费（已注册 UDF 收到该段后重新求值）没有被观察到——那需要一个能执行的 UDF 目标，本仓测试夹具（`TestBootstrap`）没有可运行的脚本状态。故本票钉住的是「转交给下游的是什么」，而不是「下游拿到后算出什么」
- 要谈每执行成本，先按 issues/05 决策 4 补基准（仓库现无基准设施）——未做，故本票不提出性能主张
- 改回 ⇒ 未采纳

## 方案

定为**保留现状**：访问器读检查期快照，不跟随原地折叠。改回要让访问器与折叠共用一处存储，并重估 13 个 `HasUniqueRestructure` 类（17 处赋值）与 `arg-contract-09` 的写回路径；换来的只是省掉一次纯表达式白算。快照的代价已知且已写进类注释：只保证「折叠前后读值等价」，不保证「求值时机不变」。

## 影响

- 行为面：状态相关的可折叠调用取值随之为「调用时的值」，这是**有意的语义变更**而非回归（issues/05 第四轮 review 已把这一维写进票面与类注释）
- 现有用例钉住的是当前语义：`ArgumentSlotsTests` 的 `FoldedCall_StillReadsTheRightValueThroughTheAccessors`、`UnfoldableCall_ReEvaluatesItsFoldedArgumentsAtCallTime`、`Slots_DoNotFollowTheInPlaceFold`
- 与 `arg-contract-09` 同属折叠面，但改的是**读**的一侧，两者可各自独立裁定
