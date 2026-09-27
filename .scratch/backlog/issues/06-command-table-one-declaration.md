# 67 条命令的实参形状与行为分居两个 module，只靠 FunctionCode 名字相连

Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（声明/指令执行层）。

`FunctionIdentifier` 有两条注册路径：`addFunction(code, AInstruction, additionalFlag)`（`Emuera/Runtime/Script/Statements/FunctionIdentifier.cs:52-61`）与 `addFunction(code, ArgumentBuilder, flag)`（`:63-72`）。**后者有 67 个 FunctionCode 没有指令对象**（`:186-199, 223-232, 245-246, 286-309, 315-353, 385-397, 420-425, 440-442`），其行为落在 `Process.ScriptProc.cs` 的两个 switch 里：`doNormalFunction`（`:145-1126`，981 行）与 `doFlowControlFunction`（`:1135-1284`）。

两侧只有 FunctionCode 名字这一条线相连，没有任何检查：

- `PRINTBUTTON` 的实参形状在 `ArgumentBuilder.cs:2522`（`new SpButtonArgument`）定义，行为在 `Process.ScriptProc.cs:161` 以 `(SpButtonArgument)func.BuiltArgument` 消费
- 同一知识在两个 module 重复：颜色名校验写在通用 `STR_ArgumentBuilder` 里按 FunctionCode 特判（`ArgumentBuilder.cs:831-843`），读取点又重做一遍 `Color.FromName`（`Process.ScriptProc.cs:574-590, 631-647`）
- 形状无法命名，于是被「结构恰好相同」借用：`PLAYSOUND_Instruction`（`Instraction.Child.cs:3192`）与 `HTML_PRINT_ISLAND_Instruction`（`:436`）都借 `FunctionArgType.SP_HTML_PRINT`

物证：`Instraction.Child.cs:1495-1513` 的 `CALLSHARP_Instruction.CreateArgument` 自带 `//Unreachable` 注释（ArgBuilder 已占先）。

完成标准：

- 「实参形状 + 行为 + flag」收敛到一处声明（每条命令一族一个 module）；主循环只保留确实需要 Process 级状态的少数几条（flow control、callList、STRDATA）
- seam 落在「命令表构造」与「主循环」之间，而非主循环内的 switch
- 物证那处 `//Unreachable` 死方法删除
- 表驱动用例：断言全部 FunctionCode 的实参产物类型 == 行为入口声明的期望类型（今天做不到，因为没有那处声明）

## 方案

待评估。

## 影响

- 删除测试判决：删掉两个 switch，复杂度在 67 个注册点重新出现（要么给每条命令写 `AInstruction`，要么把 switch 复制到别处）→ 这份行为在挣饭钱，**问题在 seam 位置而不在要不要保留**
- 与本票相邻的两条在 `docs/architecture.md:50` 有记载（`MemoryLineEntry` 已是「单行执行入口的共用校验」的正确样板；`ParserMediator` 的窄 interface 被数百个解析点使用）
- 改动量估计：大（67 条命令 + 981 行 switch）。但不含 `ArgumentBuilder` 的 50 个子类合并——只做「一处声明」
- 相关票：issues/02（实参契约表兑现）、issues/05（执行入口的会话依赖）
