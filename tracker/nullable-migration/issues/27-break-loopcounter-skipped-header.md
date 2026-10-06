# Nullable:GOTO 跳入 REPEAT/FOR 体时 BREAK 解引用 null 的 `LoopCounter`

id: nullable-migration-27
Status: implemented

## 任务

`BREAK_Instruction.DoInstruction` 在旋转计数器前直接断言非空：

```csharp
InstructionLine jumpTo = (InstructionLine)func.JumpTo!;
InstructionLine iLine = (InstructionLine)jumpTo.JumpTo!;
//WHILEとDOはカウンタがないので、即ジャンプ
if (jumpTo.FunctionCode != FunctionCode.WHILE && jumpTo.FunctionCode != FunctionCode.DO)
{
    unchecked
    {
        jumpTo.LoopCounter!.ChangeValue(jumpTo.LoopStep, exm);
    }
}

state.JumpTo(iLine);
```

`jumpTo` 来自 `ErbLoader.nestCheck` 为 `BREAK` 配对的**词法上最内层循环行**，匹配集合是 `REPEAT | FOR | WHILE | DO`；`LoopCounter` 只在循环头行执行时由 `REPEAT_Instruction.DoInstruction` 写入（`FunctionIdentifier.cs` 把 `FunctionCode.FOR` 映射到 `REPEAT_Instruction(true)`，即 `REPEAT` 与 `FOR` 共用该指令类），全库没有把它置 null 的路径。

`WHILE`/`DO` 由上面那行守卫排除（它们没有计数器，直接跳出）：脚本里 `WHILE ... BREAK ... WEND`、`DO ... BREAK ... LOOP` 不会空引用。缺口在 `REPEAT`/`FOR`，两种形态都在同一站点崩溃：

```
@TESTF
#FUNCTION
GOTO L1
REPEAT 3
$L1
BREAK
REND
```

- 循环块**配对完整**时：`GOTO` 跳到循环体内的 `$标签`（`GOTO_Instruction.SetJumpTo` 只校验标签存在、不禁止跳入循环体），循环头从未执行，`LoopCounter` 仍为 null（`REPEAT.JumpTo` 正常指向 `REND`，不影响本站点）。
- 循环块**未闭合**时（`REPEAT` 缺 `REND`）：配对残余由 issues/16 处理，但它明确把「内部行经 `GOTO` 越入」列为残余缺口（issues/16「方案」第 2 节与「影响」末条），该形态同样在这里抛。

同文件 `CONTINUE_Instruction`（`jumpTo.LoopCounter == null` 即 `state.JumpTo(jumpTo.JumpTo!)`）与 `REND_Instruction` 都对同一情形做了短路，`BREAK` 没有；两处行为不一致本身就是缺陷证据。

**判据更正（2026-09-26）**：本票原先写「`WHILE`/`DO` 中的 `BREAK` 空引用」，与 `BREAK_Instruction` 自带的 `FunctionCode` 守卫矛盾，且 `LoopCounter` 并非「只由 `REPEAT` 赋值」。issues/21 的对应行已同步更正，本票的复现路径以上文为准。

本票为断言审计（issues/21）判定的 **C 类可达缺陷**，对应站点：

- `Emuera/Runtime/Script/Statements/Instraction.Child.cs#BREAK_Instruction.DoInstruction#1`

## 方案

先按上文路径复现（`REPEAT`/`FOR` 体内的 `$标签`，从循环外 `GOTO` 跳入后执行 `BREAK`），再按 `CONTINUE_Instruction` 的做法处理：

1. 若「无计数器即无需旋转」成立：`jumpTo.LoopCounter == null` 时短路，走到与 `CONTINUE`/`REND` 一致的「跳出该循环」（`state.JumpTo(iLine)`），删除 `!`；
2. 若期望语义是报错：把 `LoopCounter` 收窄为「仅 `REPEAT`/`FOR` 拥有」并在解析期给出 `CodeEE`，而不是在运行期空引用；
3. 两种都要顺带核对 `CONTINUE_Instruction`、`REND_Instruction` 与 `BREAK_Instruction` 的行为一致性。

完成标准：上述脚本不再产生 `NullReferenceException`，且与 `CONTINUE` 的语义一致；`Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0，测试通过。

### 实施结果（2026-10-05）

按方案第 1 条落地：`BREAK_Instruction.DoInstruction` 先把 `jumpTo.LoopCounter` 取到局部变量，`loopCounter != null` 才旋转计数器；为 null 时跳过旋转、直接 `state.JumpTo(iLine)` 跳出该循环——与 `CONTINUE_Instruction`/`REND_Instruction` 的「ループ変数が不明なら無視してループを抜ける」一致，`!` 随之删除，`WHILE`/`DO` 的既有 `FunctionCode` 短路保持不变。计数器非空时（循环头已执行的正常路径）执行语句逐字未变，只是多了一个 null 前置条件。

`FOR` 与 `REPEAT` 共用 `REPEAT_Instruction`（`FunctionIdentifier` 映射），两形态同一站点、同一修法，故未另设 `FOR` 夹具。

新增用例 2 例（`Emuera.Tests/Runtime/Script/Statements/BreakLoopCounterTests.cs`，缝为装载入口 + `BREAK_Instruction.DoInstruction` + 观察 `ProcessState.CurrentLine`）：GOTO 跳入 `REPEAT` 体后 `BREAK` 不抛异常且跳到 `REND`（并先断言 `LoopCounter == null` 的缺陷前提）；同形态的 `WHILE` 跳入不回归。红→绿实测：还原旧行为后 REPEAT 用例报 `NullReferenceException`。

「方案」第 3 条的一致性核对：`CONTINUE`/`REND` 的短路在本次改动前就已存在，`BREAK` 现与二者同为「无计数器即跳出」。

验证：`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` CS86xx = 0；`Emuera.Tests` 由 700 条增至 702 条，全绿。

## 影响

- 行为变化限于「循环头未执行就进入 `REPEAT`/`FOR` 体」这一路径的 `BREAK`：现状是空引用异常，改为短路或明确报错
- 触及 `BREAK_Instruction.DoInstruction`；`WHILE`/`DO` 中的 `BREAK` 已被 `FunctionCode` 守卫排除，`REPEAT`/`FOR` 正常进入（循环头已执行）时不受影响
- 与 issues/16 的关系：本票修的是运行期读取侧的缺口，两种触发形态（配对完整、未闭合块残余）都在同一站点崩溃；issues/16 的解析期修法不覆盖「循环头被 `GOTO` 跳过」的配对完整形态，故两票互补而非重复
