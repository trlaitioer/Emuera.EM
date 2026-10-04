# 输入协议没有 module：WinForms 事件处理器直接写 eraBasic 输入结果

id: backlog-10
Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（表现层）。

「输入事件 → eraBasic 输入契约」这件事没有 module，规则分散在事件处理器、`EmueraConsole` 与 `Process` 三处，且同一处理器内两条通道并存：

- `MainWindow.cs:906-1099`（`:925-928`、`:984-1007`、`:1030-1045`、`:1048-1068`）、`:1112-1134`、`:1744-1886`（140 行键协议）
- locality 破口：同一处理器内 `:989-1007` 走 `GlobalStatic.Process.InputInteger(1, 3)` / `InputString(1, str)`，30 行后的 `:1030-1045` 直接写 `GlobalStatic.VEvaluator.RESULTS_ARRAY[1]` / `RESULT_ARRAY[1]`；`:1064` 直接写 `RESULT_ARRAY[2]` 绕过访问器
- `EmueraConsole.cs:872, 876, 1498-1785`；`Process.cs:321-330`（`InputResult5`）、`:334-356`（`InputInteger(long)` 与 `InputInteger(long idx, long)` 语义完全不同）
- hotkey 第三条路径：`UI/Game/HotkeyState.cs:345`，被 `MainWindow.cs:1763` 调用

鼠标与键盘两条路径**没有共享代码**，规则因此在三处重现。

完成标准：

- 「输入事件 → eraBasic 输入契约」成为 module：internal 决定写哪些槽位、是否推进 ctrlZ 与按钮世代、是否清定时器
- 键盘 / 鼠标 / hotkey 只是报告事件的 adapter；两条路径的等价性可断言
- 用例：每种 `InputType` × 按钮/修饰键 → 槽位与值，无需构造 `Form`

## 方案

待评估。

## 影响

- **删除测试判决**：无 module 可删；若把规则实现到最近的两个 adapter（鼠标/键盘），规则会在这两处 + `HotkeyState` 三处重现 → 提取是 earning its keep
- **测试面**：今天 0 覆盖，且必须先造 `Form`
- 改动量估计：中
- 相关票：issues/05（console seam）
