# Nullable:配对被破坏时未标记错误行，空跳转目标进入执行期

id: nullable-migration-16
Status: in-progress

## 任务

`LogicalLine.JumpTo`（`InstructionLine` 继承）在解析期由 `ErbLoader.nestCheck` 按结构配对写入（`IF`/`ELSEIF`↔`ENDIF`、`SELECTCASE`/`CASE`↔`ENDSELECT`、`REPEAT`/`FOR`↔`REND`/`NEXT`、`WHILE`↔`WEND`、`DO`↔`LOOP`、`PRINTDATA`/`STRDATA`↔`ENDDATA`、`CATCH`↔`ENDCATCH`、`TRYCALLLIST` 系↔`ENDFUNC` 等）。执行期对 `JumpTo` 的读取绝大多数以 `!` 断言非空（`GOTO_Instruction`/`SIF_Instruction` 走判空分支，`GOTO_Instruction.SetJumpTo` 另有 `func.JumpTo = null` 的复位路径，故非「一律」），而该断言依赖的唯一门禁是「配对失败的行会被 `Warn(..., isError: true)` 标记，`Process.ScriptProc` 在执行前对 `IsError` 行抛 `CodeEE`」。

实测存在两处绕过该门禁的路径：

1. **`ErbLoader.nestCheck` 的两处恢复分支在弹出未闭合构造时不标记被弹出的行**。分支在 `nestStack.Pop()` 循环里只对当前的 `CASE`/`ENDSELECT` 行调用 `Warn`，被弹出的构造既不配对也不标错。
2. **`ErbLoader.nestCheck` 的 `pairLine` 声明在扫描循环之外且从不复位**。`BREAK`/`CONTINUE` 分支靠 `if (pairLine == null)` 判定「不在循环内」并告警，而该值可能残留自上一条 `REND`/`NEXT`/`WEND`/`LOOP`、`CATCH`、`ENDCATCH`（`ENDDATA`/`ENDFUNC`/`ENDNOSKIP` 只是不复位、并不写入）。残留值为 `CATCH` 行时可致崩（`JumpTo` 从不赋值）；残留值为循环行时不崩，而是**静默错跳**：`BREAK` 取到已结束循环行的 `JumpTo`（= 其 `REND`），把循环重新进入。

完成标准：上述两处在产生处修复（配对完成或被弹出行标错），`Emuera.Tests` 补回归用例；`Emuera` 与 `Emuera.Tests` 的 CS86xx 为 0、测试通过。同源残余缺口记录在「方案」第 2 节，不在本票修。

## 方案

### 复核更正（`CASE`/`CASEELSE` 分支同样可达）

原判「`CASE`/`CASEELSE` 分支弹出后 `SELECTCASE` 仍留在 `nestStack` 上、会被收尾循环标错，被弹出行排在其后故不可达」只在 SELECTCASE 最终**未**被 `ENDSELECT` 关闭时成立。若后续存在 `ENDSELECT`，SELECTCASE 会被正常配对且不带错误标记（即经恢复分支后门禁并不生效），被弹出的行随即在入口行之后被顺次执行：

```
@TESTF
#FUNCTION
SELECTCASE 1
CASE 1
PRINTFORM "a"
IF 0
CASE 2
ENDSELECT
```

`CASE 2` 触发恢复分支弹出未闭合的 IF（既不配对也不标错），`CASE 2` 自身标错，`ENDSELECT` 随后正常配对。运行时 `SELECTCASE 1` 命中 `CASE 1` → 执行 `PRINTFORM` → 落到 IF 行：`IF_Instruction.DoInstruction` 的 `ifJumpto = func.JumpTo`（null，若该分支从未配对）使 `state.JumpTo(null)`，下一次 `ShiftNextLine` 解引用 null。**两个恢复分支都要给被弹出行补 `IsError`。**

### 1. 解析期修复（本票范围）

全部位于 `ErbLoader.nestCheck`：

1. `ENDSELECT` 分支：弹出未闭合构造时对每个被弹出线写 `IsError`/`ErrMes`，随后**落回正常配对路径**（删除该分支原有的 `SelectcaseStack.Pop(); nestStack.Pop(); break;`），让 `SELECTCASE` 与各 `CASE` 行拿到 `ENDSELECT` 作为跳转目标。只标记被弹出行不足以修该路径——复现脚本的第一个崩溃点在 `CASE` 行（`ELSEIF_Instruction` 读 `func.JumpTo`），它在被弹出行之前执行。
2. `CASE`/`CASEELSE` 分支：同样对被弹出行写 `IsError`/`ErrMes`，保留 `break`（`SELECTCASE` 留在栈上，后续 `ENDSELECT` 仍走正常配对）。
3. `pairLine` 改为各 `case` 块内的局部变量（`BREAK`/`CONTINUE` 的 `loopLine`；`REND`/`NEXT`/`WEND`/`LOOP`、`CATCH`、`ENDCATCH` 各自的局部变量），从结构上消除跨迭代共享状态，而不是依赖「在循环顶部复位」。

写 `IsError`/`ErrMes` 而不额外调用 `Warn` 的理由：同一句 `InstructionNotClosed` 已在同一次迭代里报到 `func` 位置，诊断输出完全不变；nestCheck 运行在 Depth 层并行区 join 之后、每行只有一个写入者，与同文件 `printFunctionNotFoundWarning` 的直接写同一理由（那里已写明该论证）。

### 2. 残余缺口（本票记录，不修）

`nestCheck` 收尾循环对未闭合构造只对**构造行本身**调用 `Warn(..., isError: true)`（`MissingCorresponding`），块内部读 `JumpTo` 的行不被标记：

- `IF` 的 `ELSEIF`/`ELSE` 行、`SELECTCASE` 的 `CASE`/`CASEELSE` 行——两种构造行都持有 `IfCaseList`，收尾时未遍历其成员；
- 循环内 `BREAK`/`CONTINUE` 行——它们的 `JumpTo` 指向循环行，而循环行不反向持有跳转者，`REND`/`NEXT`/`WEND`/`LOOP` 缺失时循环行的 `JumpTo` 保持 null。

块的入口行被标错挡住了顺序进入，但 `$label` 在 IF/SELECTCASE/循环内是合法的（本函数只对 PRINTDATA/STRDATA/DATALIST/TRYCALL\* 内的标签报 `CanNotLabelDefineInSyntax`，SELECTCASE 那一行的检查被注释），`GOTO` 也不检查目标标签的宿主块，于是 `GOTO $label` 可从块外越入并绕过门禁。触发需「未闭合块内定义 `$label` + 越入 `GOTO`」，与本节第 1、2 项（普通顺序脚本即可命中）不同，故本票不修，仅记录最小复现（均待运行期确认）：

```
@TESTF
#FUNCTION
GOTO L1
IF 1
$L1
PRINTFORM "a"
ELSE
PRINTFORM "b"
```

`ELSE` 行未标记、`JumpTo` 为 null：从 `$L1` 顺次落到 `ELSE` 行时 `ELSEIF_Instruction.DoInstruction` 执行 `state.JumpTo(func.JumpTo!)`。

```
@TESTF
#FUNCTION
GOTO L1
SELECTCASE 1
$L1
CASE 1
PRINTFORM "a"
```

`CASE 1` 行同样未标记、`JumpTo` 为 null（与上一形态同一读点）。

```
@TESTF
#FUNCTION
GOTO L1
REPEAT 3
$L1
BREAK
```

`BREAK` 的配对本身成功、`JumpTo` 指向 REPEAT 行，但 REPEAT 行的 `JumpTo` 与 `LoopCounter` 均为 null（REPEAT 从未执行），`BREAK_Instruction.DoInstruction` 在取 `LoopCounter` 时抛。

若将来处理，两条路线：(a) 解析期——收尾循环同时标记构造行 `IfCaseList` 的其余行，循环的 `BREAK`/`CONTINUE` 需在配对时把跳转者记到循环行（新增状态）或在收尾时重扫行链；(b) 运行期——`JumpTo` 的读取侧入口（见「曾考虑的替代方案」第 4 项），一次覆盖全部残余而不依赖枚举触发形态。

### 3. `JumpTo` 族入口的边界（沿用结论）

- `JumpToEndCatch` 不并入同一入口：`CALL`/`GOTO` 的正常控制流就以 `JumpToEndCatch != null` 判断是否处于 TRY 中，该 null 是设计而非缺陷。
- `ParentLabelLine` 不获得断言访问器：插件的内存行 API 使其在执行期确实可空（见 issues/19）。
- `GOTO_Instruction.SetJumpTo` 的 `func.JumpTo = null` 复位路径不改：issues/14 已复核它喂不到任何断言点。

### 复现（已逐点静态核对，运行期确认由新增测试承担）

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

`ENDSELECT` 分支：`SELECTCASE` 落入 `CASE 1` 后顺序落入 `CASE 2` 行，该行的 `ELSEIF_Instruction.DoInstruction` 执行 `state.JumpTo(func.JumpTo!)` 时 `JumpTo` 为 null（恢复分支跳过了配对），`currentLine` 被置为 null。同类写法可按 `SELECTCASE 2`（无匹配 CASE）、`PRINTDATA`/`STRDATA`、`FOR`、`WHILE 0`、`REPEAT 1` + `CONTINUE`、`DO` + `CONTINUE`、`TRYCCALL`/`CATCH`、`TRYCALLLIST` + `FUNC` 分别命中其余断言点。

```
@F
TRYCCALL X
CATCH
ENDCATCH
BREAK
```

`pairLine` 残留：`BREAK` 取到残留的 `pairLine`（`CATCH` 行），其 `JumpTo` 为 null，`BREAK_Instruction.DoInstruction` 在取 `LoopCounter` 时抛。同一原理的循环行残留形态：

```
@F
REPEAT 3
BREAK
REND
BREAK
```

第二个 `BREAK`（循环外）复用 `REND` 留下的循环行，`iLine = 该行.JumpTo = REND` 非空、`LoopCounter` 也已由 REPEAT 赋值，于是不抛异常而把已结束的循环重新进入。

### 实施结果（2026-09-26）

- `ErbLoader.nestCheck` 的两个恢复分支（`ENDSELECT` 与 `CASE`/`CASEELSE`）在弹出未闭合构造时对被弹出行调用 `markUnpaired` 写 `IsError`/`ErrMes`；`ENDSELECT` 分支随后落回正常配对路径，`SELECTCASE` 与各 `CASE` 行拿到 `ENDSELECT` 作为跳转目标。
- 扫描循环外共享的 `pairLine` 已删除：`REND`/`NEXT`/`WEND`/`LOOP` 各取本次迭代的局部循环行，`BREAK`/`CONTINUE` 每次从 `nestStack` 现取所属循环构造，跨迭代残留不再存在。
- 新增 `Emuera.Tests` 的 `ErbLoaderNestCheckTests`（4 例）覆盖两处恢复分支与 `pairLine` 复用。

## 曾考虑的替代方案

1. 只给被弹出行补 `IsError`、不完成 `ENDSELECT` 的配对：不足以修 `ENDSELECT` 路径，崩溃点在 `CASE` 行而非被弹出行（见「复核更正」）。
2. `CASE`/`CASEELSE` 分支改为把当前 `CASE` 行加入 `IfCaseList`（跨过恢复后继续正常路径）：语义上把该行从「被丢弃且标错」变成「有效 CASE」，原本必然报错的脚本改为静默按配对语义跳转，改动面与诊断行为变化大于本票所需。
3. `pairLine` 在扫描循环顶部复位：满足不变量但保留跨迭代共享状态，取「各 `case` 局部变量」替代。
4. `JumpTo` 收敛为抛具名诊断的单点访问器（原「方案 3」）：需新增 lang 条目（3 份 xml + `Lang.cs`）并改造 23 处读取点，把 23 处 `NullReferenceException` 换成 `CodeEE` 的影响面大于解析期修复；且 `GOTO_Instruction.SetJumpTo`、`NOSKIP`/`ENDNOSKIP` 必须保留 `JumpTo == null` 的判空读取，「读取侧单点」与「判空侧原属性」并存。该路线保留给「方案」第 2 节的残余缺口。

## 影响

- 涉及 46 处 `JumpTo` 族断言中的 23 处（分布在 `Instraction.Child.cs` 的 `PRINT_DATA_Instruction`、`REPEAT_Instruction`、`WHILE_Instruction`、`ELSEIF_Instruction`、`IF_Instruction`、`SELECTCASE_Instruction`、`CATCH_Instruction`、`BREAK_Instruction`、`CONTINUE_Instruction`、`REND_Instruction`、`WEND_Instruction`、`LOOP_Instruction`，以及 `Process.ScriptProc.cs` 的 `doNormalFunction`/`doFlowControlFunction`）。46 的构成：`JumpTo!` 28 处（`Instraction.Child.cs` 23、`Process.ScriptProc.cs` 5，后者正落在上述两个方法内）+ `JumpToEndCatch!` 1 + `ParentLabelLine!` 17；「23 处可达」的口径来自 issues/14 的复核(10 成立、11 仅靠 `IsError` 门禁、23 可达)，与 `Instraction.Child.cs` 的 `JumpTo!` 数相同属巧合
- 修复属解析期行为变更：原本静默产生坏跳转目标的脚本会改为在解析期报错或按配对语义正确跳转。诊断文本与位置不变；能正常运行的脚本走到恢复分支时，新写入的 `JumpTo` 不会被读取（或读到 `ENDSELECT`，与原语义一致）
- 残余缺口（「方案」第 2 节，本票不修）：未闭合块内部行经 `GOTO` 越入时仍为空跳转；该形态若落在 `BREAK` 上，同一次执行会先撞上 `jumpTo.LoopCounter!`（issues/21 判定 C 类，由 issues/27 承接）
- 本票新增 `Emuera.Tests` 的 `ErbLoaderNestCheckTests`（4 例）覆盖两处恢复分支与 `pairLine` 复用，作为不变量回归网（改动前 `Emuera.Tests` 无任何 `ErbLoader`/`nestCheck` 覆盖，已核对）
