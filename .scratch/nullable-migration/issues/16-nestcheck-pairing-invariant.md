# Nullable:配对被破坏时未标记错误行，空跳转目标进入执行期

Status: ready-for-agent

## 任务

`InstructionLine.JumpTo` 在解析期由 `ErbLoader.nestCheck` 按结构配对写入（`IF`/`ELSEIF`↔`ENDIF`、`SELECTCASE`/`CASE`↔`ENDSELECT`、`REPEAT`/`FOR`↔`REND`/`NEXT`、`WHILE`↔`WEND`、`DO`↔`LOOP`、`PRINTDATA`/`STRDATA`↔`ENDDATA`、`CATCH`↔`ENDCATCH`、`TRYCALLLIST` 系↔`ENDFUNC` 等）。执行期对 `JumpTo` 的读取一律以 `!` 断言非空，而该断言依赖的唯一门禁是「配对失败的行会被 `Warn(..., isError: true)` 标记，`Process.ScriptProc` 在执行前对 `IsError` 行抛 `CodeEE`」。

实测存在两处绕过该门禁的路径：

1. **`ErbLoader.nestCheck` 的两处恢复分支在弹出未闭合构造时不标记被弹出的行**。分支在 `nestStack.Pop()` 循环里只对当前的 `CASE`/`ENDSELECT` 行调用 `Warn`，被弹出的构造既不配对也不标错；`ENDSELECT` 分支弹出 `SELECTCASE` 后直接 `break`，跳过了正常路径上的 `selectLine.JumpTo = func`。因此这些行同时满足 `JumpTo == null` 与 `IsError == false`。
2. **`ErbLoader.nestCheck` 的 `pairLine` 声明在扫描循环之外且从不复位**。`BREAK`/`CONTINUE` 分支靠 `if (pairLine == null)` 判定「不在循环内」并告警，而该值可能残留自上一条 `CATCH`/`ENDCATCH`/`REND`/`ENDDATA`/`ENDFUNC`/`ENDNOSKIP`，于是无外层循环的 `BREAK` 会通过检查并拿到一个非循环行作为跳转目标。

## 方案

1. 恢复分支对**每个**被弹出的行调用 `Warn(..., isError: true)`（或在弹出前完成配对），使不变量重新成立在产生处；`pairLine` 在扫描循环顶部复位。
2. 第 2 项与可空无关，是独立的解析期缺陷，可与第 1 项同票修但分开提交。
3. 修完后再评估 `JumpTo` 是否可收敛为单点入口（issues/13 的做法）。届时应抛出具名诊断而非直接 `JumpTo!`：单点断言只能把多处崩溃并成一处，无法表达「解析器本应保证」；抛 `CodeEE`/`ExeEE` 能把残余 null 变成带位置的脚本错误，且让该保证可核对。
4. `JumpToEndCatch` 不应并入同一入口：`CALL`/`GOTO` 的正常控制流就以 `JumpToEndCatch != null` 判断是否处于 TRY 中，该 null 是设计而非缺陷。
5. `ParentLabelLine` 不应获得断言访问器：插件的内存行 API 使其在执行期确实可空（见 issues/19）。

### 复现（静态推导，待执行确认）

```
@F
SELECTCASE 1
CASE 1
PRINTFORM "a"
CASE 2
PRINTFORM "b"
IF 0
ENDSELECT
```

`SELECTCASE` 落入 `CASE 1` 后顺序落入 `CASE 2` 行，该行的 `ELSEIF_Instruction.DoInstruction` 执行 `state.JumpTo(func.JumpTo!)` 时 `JumpTo` 为 null，`currentLine` 被置为 null。同类写法可按 `SELECTCASE 2`（无匹配 CASE）、`PRINTDATA`/`STRDATA`、`FOR`、`WHILE 0`、`REPEAT 1` + `CONTINUE`、`DO` + `CONTINUE`、`TRYCCALL`/`CATCH`、`TRYCALLLIST` + `FUNC` 分别命中其余断言点。

```
@F
TRYCCALL X
CATCH
ENDCATCH
BREAK
```

`BREAK` 取到残留的 `pairLine`（`CATCH` 行），其 `JumpTo` 为 null，`BREAK_Instruction.DoInstruction` 在取 `LoopCounter` 时抛。

## 影响

- 涉及 46 处 `JumpTo` 族断言中的 23 处（分布在 `Instraction.Child.cs` 的 `PRINT_DATA_Instruction`、`REPEAT_Instruction`、`WHILE_Instruction`、`ELSEIF_Instruction`、`IF_Instruction`、`SELECTCASE_Instruction`、`CATCH_Instruction`、`BREAK_Instruction`、`CONTINUE_Instruction`、`REND_Instruction`、`WEND_Instruction`、`LOOP_Instruction`，以及 `Process.ScriptProc.cs` 的 `doNormalFunction`/`doFlowControlFunction`）
- 修复属解析期行为变更：原本静默产生坏跳转目标的脚本会改为在解析期报错或按配对语义正确跳转
- 需补 `Emuera.Tests` 用例覆盖两处恢复分支与 `pairLine` 复用，作为不变量回归网
