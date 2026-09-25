# Nullable:BREAK 在 WHILE/DO 循环里解引用 null 的 `LoopCounter`

Status: ready-for-agent

## 任务

`BREAK_Instruction.DoInstruction` 直接断言循环计数器非空：

```csharp
jumpTo.LoopCounter!.ChangeValue(...)
```

而 `jumpTo` 来自 `ErbLoader.nestCheck` 为 `BREAK` 配对的**最内层循环行**，其匹配集合是 `REPEAT | FOR | WHILE | DO`；`LoopCounter` 全库只由 `REPEAT_Instruction.DoInstruction` 赋值，`WHILE`/`DO` 行上没有计数器。于是 eraBasic 脚本里出现 `WHILE ... BREAK ... WEND`（或 `DO ... BREAK ... LOOP`）时，断言掩盖了一次 `NullReferenceException`；这是合法脚本，`nestCheck` 的循环指令检查不会拦截。

旁证：同文件 `CONTINUE_Instruction.DoInstruction` 在完全平行的位置上写了 `if (jumpTo.LoopCounter == null) { state.JumpTo(jumpTo.JumpTo!); return; }`，说明作者已知该路径存在；两处行为不一致本身就是缺陷证据。

本票为断言审计（issues/21）判定的 **C 类可达缺陷**，对应站点：

- `Emuera/Runtime/Script/Statements/Instraction.Child.cs#BREAK_Instruction.DoInstruction#1`

## 方案

先复现确认语义，再按 `CONTINUE_Instruction` 的做法处理：

1. 若「无计数器即无需旋转」成立：`jumpTo.LoopCounter == null` 时短路（直接 `state.JumpTo(jumpTo.JumpTo!)` 或等价的跳过），删除 `!`；
2. 若期望语义是报错：把 `LoopCounter` 收窄为「仅 `REPEAT`/`FOR` 拥有」并在解析期给出 `CodeEE`，而不是在运行期空引用；
3. 两种都要顺带核对 `CONTINUE_Instruction` 与 `BREAK_Instruction` 的行为一致性。

完成标准：`WHILE`/`DO` 中的 `BREAK` 不再产生 `NullReferenceException`，且与 `CONTINUE` 的语义一致；`Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0，测试通过。

## 影响

- 行为变化限于 `WHILE`/`DO` 循环中的 `BREAK`：现状是空引用异常，改为短路或明确报错
- 触及 `BREAK_Instruction.DoInstruction`；`REPEAT`/`FOR` 中的 `BREAK`（有计数器）不受影响
- 与 issues/16（`nestCheck` 配对被破坏）不同：本票的配对是正确的，问题在于「配对到的循环行没有计数器」
