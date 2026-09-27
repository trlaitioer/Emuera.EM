# Nullable:插件与错误报告路径的空引用

Status: in-progress

## 任务

非空断言中有一组只在插件调用或错误处理路径上可达。这些路径的调用方不受解析期不变量保护（内存行、跨线程调用、初始化期失败），断言掩盖的是真实的 null。本票逐个补守卫或改为可空读取。

范围收口：本票吸收 issues/25（显式 `@子键` 无父标签，已按 `superseded` 关闭）；issues/23、26、27、28 各自承接同一批次的其他 C 类站点；issues/24 是本票行 7 的相邻变体（同为「无父标签」，根因是行位置而非显式子键），仍分立。

### 共同前提:内存行的三个缺口

`LogicalLineParser.ParseLine(string, EmueraConsole?)` 构造的行（`EmueraConsole.DebugCommand` 与 `PluginManager.ExecuteLine` 都用它）缺三样东西，本票插件侧各行都由此展开：

- `ParentLabelLine` 为 null——生产代码里只有 `ErbLoader.loadErb` 会经 `ParseLine` 的 `parentLine` 参数赋 `nextLine.ParentLabelLine`；
- `NextLine` 为 null——`LogicalLine.NextLine` 声明处即 `= null!`，故任何把内存行留在执行流里的指令，都会让下一步的 `ProcessState.ShiftNextLine`（`currentLine!.NextLine`）或 `runScriptProc`（`line!.IsError`）空引用；
- `Position` **非 null**——是 `ScriptPosition` 的无参默认值（`LineNo = -1`、`Filename = ""`）。因此「有 `Position` 即来自文件、必有父标签」这类判据对内存行一律失效。

### 范围

| # | 位置 | 现状 | 可达路径 |
| --- | --- | --- | --- |
| 1 | `PluginManager` 的 `ExecuteLine`（入参未校验） | `LogicalLineParser.ParseLine` 对空/纯空白行返回 null，而 `ArgumentParser.SetArgumentTo` 的 false 返回值被忽略，下一句 `func.Function` 空引用；对 `@`/`$`/未定义行返回 `InvalidLine`，`(InstructionLine)logicalLine!` 抛 `InvalidCastException`（只有 null 的转换不抛）；`SetArgumentTo` 因实参不合法返回 false 并置 `IsError` 时既不查返回值也不查 `IsError`，下一句读 `BuiltArgument` 空引用 | 插件 `ExecuteLine("")`、`ExecuteLine("@FOO")`、`ExecuteLine("ADDCHARA")` |
| 2 | `PluginManager` 的 `ExecuteLine`（指令面未过滤,issues/21 批次 C 的两处 C） | `func.Function.Instruction!`：`Instruction` 为 null 的正是 PRINT 族之外由 `doNormalFunction`/`doFlowControlFunction` 分派的命令（`QUIT`、`PRINTPLAIN` 等以 `ArgumentBuilder` 重载注册）；且分发时不施加 `IsFlowContorol`/`IsWaitInput`/`IsMethodSafe`/`IsPartial` 过滤（`EmueraConsole.DebugCommand` 有过滤），流控指令对插件开放 | 插件 `ExecuteLine("QUIT")`、`ExecuteLine("PRINTPLAIN x")`、`ExecuteLine("RESTART")`、`ExecuteLine("CALL @FOO")`、`ExecuteLine("CALLEVENT X")`；示例插件用的 `PRINTL` 是 `METHOD_SAFE`、`Instruction` 非 null，不属该组，故此前未暴露 |
| 3 | 同上,被留在执行流里的内存行 | `RESTART` 的 `state.JumpTo(func.ParentLabelLine!)` **本行不抛**：null 只是被写进 `currentLine`，NRE 落在脚本下一步的 `ShiftNextLine`；`CALL @FOO` 正常进入被调函数，返回时 `currentLine = called.ReturnAddress`（即内存行），下一步 `runScriptProc` 的 `line!.IsError` 空引用；`GOTO` 反而安全——`GOTO_Instruction.DoInstruction` 在 `BuiltArgument.IsConst` 且 `JumpTo == null` 时 `return`，而 `SetJumpTo` 只由 `ErbLoader` 调用，内存行永远没有 `JumpTo` | 插件 `ExecuteLine("RESTART")`、`ExecuteLine("CALL @FOO")`（`GOTO @FOO` 是静默无操作） |
| 4 | `Process.State.cs` 的 `ProcessState.IntoFunction`（调试回溯） | 只判 `retAddress != null`，未判 `Position`/`ParentLabelLine`；该解引用只存在于 `exm == null` 分支，而传 `exm = null!` 且 `retAddress` 非 null 的唯一入口是 `CALLEVENT_Instruction.DoInstruction`（传 `retAddress = func`；系统过程传 null） | 插件 `ExecuteLine("CALLEVENT X")` 且处于 `-Debug`。`CALLEVENT` 带 `FLOW_CONTROL`，`DebugCommand` 会拒绝，故行 2 的指令过滤落地后本行失去可达路径，守卫只作防御 |
| 5 | `PluginManager` 的 `GetStackTrace` | 首处依次解引用当前行、位置与父标签行且不判空（循环内仅以 `parent.Position != null` 守卫，`ParentLabelLine` 仍为断言）；内存行的 `Position` 是默认结构（非 null）故 `Position!.Value` 不抛，真正的命中点是 `CurrentLine` 为 null 与 `ParentLabelLine` 为 null | 插件在 `CALLSHARP` 执行窗口之外（自身线程、脚本已结束）调用 |
| 6 | `Process.cs` 的 `Process.handleException` | `position != null` 分支的位置取的是 `exc is EmueraException { Position: not null }` 或 `current`，而 `handleException(e, null, true)` 是合法调用（`Process.Initialize` 的 catch 就这么调），块内无条件解引用 `current!` 与 `current.ParentLabelLine!`；调用栈循环里另有 `parent.ParentLabelLine!`，而 `parent.Position != null` 对内存行恒真，任何以内存行为返回地址的条目都会让后续报错停在打印栈处 | **无需插件、无需 `-Debug`**：`VariableSize.CSV` 的 CDFLAG 不变量失败时 `ConstantData.decideActualArraySize` 抛带位置的 `CodeEE`（CDFLAGNAME1/2 与 CDFLAG 长度不一致，或两维乘积 > 1000000），该 throw 在 `loadVariableSizeData` 的 try 之外，而 `LoadData` 又在 `Process.Initialize` 的 try 内，于是报错路径自身抛异常、`Initialize` 不再 `return false`。原票举的 `ParserMediator.LoadEraExRenameFile` 是同型路径之一，不是唯一入口 |
| 7 | `IdentifierDictionary` 的 `GetVariableToken`（显式 subKey 分支，`LOCAL@name`/`ARG@name`） | 只以 `isError: false` 告警后即解引用 `line` 与 `ParentLabelLine`；紧邻的无 subKey 分支有正确的 `CodeEE` 处理。收窄必须放在 `ParserMediator.Warn` **之前**：`Warn` 在真正记录告警时会读 `line.Position`，否则 NRE 先发生在 `Warn` 内 | 解析期 `GetScaningLine()` 返回 null 或无父标签的行（issues/25 记了 `ErbLoader.setLabelsArg` 的入口；issues/24 是同族的行位置变体） |
| 8 | `ConstantData` 的 `GetKeywordDictionary`（潜在缺陷,B 类） | **已由 issues/29 关闭（2026-09-27）**：方法拆为 `TryGetKeywordDictionary`（`index < 0` 的探测与 ERD 表缺失如实返回 false）与抛 `CodeEE` 的非空入口 `GetKeywordDictionary`。原文登记的「ERD 分支 5 条早退在 `index >= 0` 时把 null 原样返回」不再可能到达消费点——求值点在缺字典时得到 `CodeEE`，探测点得到 false | — |

### 只登记、不修改(B 类)

issues/21 的断言审计（批次 1/2/3）与 issues/14 复核表第 4 项另记入以下位置——断言内容不成立，但当前没有可达路径；破坏者已写明，本票只登记，供其进入可达范围时按上文修法收口：

| 位置 | 现状 | 破坏者（当前不可达的原因） |
| --- | --- | --- |
| `ConstantData` 的 `output`（`= null!` 字段） | 唯一赋值点在 `LoadData`，但同方法内自带 `output != null` 守卫，`Emuera.Tests` 的 `TestBootstrap`/`ConstantDataTests` 以 `LoadData(dir, null!, false)` 传 null | 非空 output 的生产调用者传 null（当前仅测试传 null） |
| `LexicalAnalyzer.expandFunctionlikeMacro` 的 `while (macroEnd == wc.Pointer)` | `macroStart` 与 `macroEnd` 不相等，循环体（含两处 `wc.Pointer!`）当前不执行 | 把条件改成显然意图的 `!=`，且 `)` 是行尾最后一个 token 时，`wc.Pointer!.Next` 会置 null、紧随的 `wc.Pointer!` 立即空引用 |
| `YenAtSubWord.Right!`（`SetIsMacro`） | 缺失 `#` 的警告路径上 `Right` 确为 null，注释称「该路径不会进入宏展开」 | 更强的理由：`WordCollection.SetIsMacro`/`StrFormWord.SetIsMacro` 当前**无任何调用者**，整段不可达 |
| `EnumNameMethod` 的局部 `array` | `switch (type)` 三分支赋值且无 `default`，`EType` 只有三个值 | 给 `ENUMNAME` 类方法新增 `EType` 值却漏改该 `switch` |
| `FunctionLabelLine.CompareTo` 的 `other!` | `IComparable<T>` 强制可空形参，方法体首行即解引用 | 以 null 显式调用 `CompareTo`；当前唯一调用方 `List<T>.Sort` 不会传 null 元素 |
| `EmueraConsole.Print.cs` 的 `deleteLine` 对 `BufferToSingleLine(true, false)` | `force=true` 时唯一 null 路径是 `!Enabled`（`window.Created`），而达到该分支需已有 `MaxLog-2` 行、窗口早已 Created | 窗口销毁/禁用后仍调 `deleteLine` |
| `PrintStringBuffer.getDivideIndex` 的 `css.Font!` | `Font == null` 只在 `Error`，本方法不查 `Error`，但 `Error` 部件宽度恒为 0 | `Error` 部件参与宽度累加 |
| `ErbLoader.nestCheck` 的 `func!`（`Stack<InstructionLine>.Pop()`） | 循环条件 `nestStack.Count != 0` 保证 `Pop()` 非空，故 `if (func != null)` 恒真、含该 `!` 的分支是死代码 | 附带缺陷：null 测试疑似写反（应为 `funcMatch == null`），属潜在缺陷 |
| `EmueraConsole.GetDebugTraceLog` 的 `line.ParentLabelLine!` | 该行的理由注释「有 Position 就是从文件解析来的行」对内存行失效（默认 `ScriptPosition` 非 null），`Position == null` 的判空挡不住 `ParentLabelLine == null` | 调试窗口打开时 `Process.GetScaningLine()` 指向内存行（插件/调试命令解析过 `VARI` 一类会写 `scaningLine` 的行） |
| `Creator.Method.cs` 的 `UnicodeMethod.GetStrValue` 对 `getCurrentLine.Position!` | 只判 `getCurrentLine != null`，未判 `Position`；由 issues/14 复核表第 4 项移交（B） | 出现 `Position` 为 null 的当前行（现无该形态的构造点） |
| `UserDefinedMethodTerm.cs` 的 `UserDefinedRefMethodTerm.Restructure` 对 `srcArgs.Required(i)` | `srcArgs` 的元素可空（省略实参），此处按非空读取；由 issues/14 复核表第 4 项移交（该 `Restructure` 现无调用点，B） | 该 `Restructure` 重新接回调用链 |

### 相关发现(不在本票)

- `PluginManager.processState` 是 `Process.Initialize` 时捕获的 state 对象，而 `saveCurrentState` 会把 `Process.state` 换成克隆对象、`loadPrevState` 才换回原对象：调试窗口内（含窗口内经 `CALLSHARP` 调用的插件）`ExecuteLine`/`GetStackTrace` 操作的是克隆前的那份 state。与行 2~4 相邻，需另立票或并入插件 API 的其他工作。
- `CharUserdefinedInt1dWrapper.variableCode` 无任何读取——与行 8 同时发现，属 issues/22 一类的契约/气味项。

## 方案

### 决策(2026-09-26)

- 行 1~3：`ExecuteLine` 复用 `Process.DoDebugNormalFunction` 的两路分派，并施加 `EmueraConsole.DebugCommand` 同款的指令过滤；保持 `void ExecuteLine(string)` 签名。
- 行 4~6：抽出共用的「位置 + 父标签」文本辅助，缺信息时降级，而不是各处就地判空。
- 行 8：只改写方法上方的 TODO，不改代码。

### 实施顺序

1. **行 7**（独立、可单测）：`else` 分支入口补与空子键分支同型的收窄——`line?.ParentLabelLine == null` 时抛 `CodeEE`（复用 `CannotGetKeyNotExistRunningFunction`，或新增更贴切的文案），守卫置于 `ParserMediator.Warn` 之前，删除 `line!.ParentLabelLine!`。`line!` 保留：`Warn` 的形参非空、记录告警时会读 `line.Position`，而编译器看不到「`parentLabel` 非空即 `line` 非空」，故就地注明该推导。
2. **行 4~6**：新增「位置 + 父标签」文本辅助，形如 `file:line@label`；缺父标签退化为 `file:line`，两者都缺返回 null 由调用方跳过。替换五处：`handleException` 的 `ErrorInFunc` 与调用栈循环、`GetStackTrace` 两处、`IntoFunction` 的 `retAddress` 一处。`handleException` 在「当前函数无法命名」时只跳过 `ErrorInFunc` 一行，调用栈仍按可用项打印。
3. **行 1~3**：把 `DebugCommand` 的校验段与 `ExecuteLine` 的「解析 → `SetArgumentTo` → 分派」提成一个共用入口（落在 `Runtime/Script/Statements/MemoryLineEntry.cs`，两处调用）；`ParseLine` 仍各写一次——`DebugCommand` 另有 Rename 替换与「非命令行的表达式回退」，分派则共用既有的 `DoDebugNormalFunction`。
   - 校验：null/纯空白、非 `InstructionLine`、`IsFlowContorol()`、`IsWaitInput()`、`!IsMethodSafe()`、`IsPartial()`、`PUTFORM`/`UPCHECK`/`CUPCHECK`/`SAVEDATA`、非 `-Debug` 下的 Debug 系指令 → 返回错误文案，由调用方抛 `CodeEE`；`SetArgumentTo` 的返回值必须检查。文案复用既有键（`CanNotInterpretedLine`/`InvalidDebugCommand`/`CanNotUseInstruction`/`CanNotUseFlowInstruction`/`ArgParsingError`），不新增 `Lang` 条目。
   - 分派改为 `process.DoDebugNormalFunction(func, false)`：它已是内存行的入口（`Instruction != null` → 否则 `doNormalFunction`），`PRINTPLAIN`/`PRINTBUTTON` 等 `METHOD_SAFE` 命令继续可用，而 `QUIT`/`SAVEDATA` 等被 `IsMethodSafe`/白名单拦下。
   - `ExecuteLine` 的 XML 注释写明接受的指令子集与抛出的异常；流控过滤落地后，行 3 的 `RESTART`/`CALL` 与行 4 的 `CALLEVENT` 一并失去可达路径。
4. **行 8**：改写 `GetKeywordDictionary` 上方的 TODO，说明真实契约——`index < 0` 或非关键字支撑的变量码才可能返回 null，`index >= 0` 时 ERD 分支的 5 条早退仍会外泄 null 但当前无可达调用方（已于 issues/29 收口：早退不再可能外泄 null）。

### 验证

- 构建 `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-restore --nologo -v:minimal`；测试 `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64 --nologo -v:minimal`（沙箱内需提权到 `danger-full-access`）。
- 可进 `Emuera.Tests`：行 7 的 `CodeEE`（以首个标签为 `scaningLine` 直接调 `GetVariableToken`）；文本辅助的三种缺失组合；`ExecuteLine` 对非法入参的拒绝（在 `SetParent` 之前即可断言「先于任何状态访问抛错」，与「`DoInstruction` 需要 `EmueraConsole`，不在用例集覆盖」的既有边界相容）。
- 只能实机/人工：行 6 的 NRE——构造 `VariableSize.CSV`（`CDFLAG,3000,3000` + `CDFLAGNAME1,100`，或 `CDFLAGNAME1,2000` + `CDFLAGNAME2,2000`），确认启动显示脚本错误而不是空引用；行 1~3 的指令面——在 `-DEBUG` 调试窗口确认 `PRINTL` 仍工作、`RESTART` 被明确拒绝。
- 不覆盖 `EmueraPluginExample/`（AGENTS.md：用户未显式说明时不改动该项目）。

### 完成标准

- 行 1~7 的输入/路径不再产生 `NullReferenceException` 或 `InvalidCastException`，非法输入得到可读的 `CodeEE`；
- `Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0，构建与测试通过。

### 实施结果(2026-09-26)

行 1~8 全部落地，另按 review 收口了结构、行为与文档项。实现中一并关闭两处原票未列的同族缺口。

- 行 7：`GetVariableToken` 把父标签收窄提到两个分支之前（`parentLabel == null` 即抛 `CodeEE`），`line!.ParentLabelLine!` 删除；收窄先于 `ParserMediator.Warn`，告警路径因此也不会先空引用。
- 行 4~6：新增 `Runtime/Script/Statements/LineLocationText.cs`（`CurrentFunction`/`CallStackEntry`/`StackFrame`/`JumpTrace`），替换五处调用点。内部以一个小类型 `Location` 统一「可用位置 + 父标签」的取值：`Position` 为 `ScriptPosition` 默认结构（`LineNo = -1`、`Filename = ""`，内存行即此形态）时视为「无位置」，该判据由公开的 `IsUsablePosition` 提供，`handleException` 构造 `posString` 时也用它（此前同一函数里存在两套口径，默认位置会打出空文件名）。`handleException` 在「当前函数无法命名」时只跳过 `ErrorInFunc` 一行，调用栈仍按可用项打印——原先整块跳过会把仍然可用的栈一起丢掉。
- 行 1~3：新增 `Runtime/Script/Statements/MemoryLineEntry.cs`（`TryBuild`），`EmueraConsole.DebugCommand` 与 `PluginManager.ExecuteLine` 共用，`DebugCommand` 原有的同型检查段删除。校验顺序为「非指令行 → 流控 → 输入等待/非 `METHOD_SAFE`/`PARTIAL` → `PUTFORM`/`UPCHECK`/`CUPCHECK`/`SAVEDATA` → 非 `-Debug` 的 Debug 系指令 → `SetArgumentTo` 与其 `IsError`」。放在独立类型而非 `Process` 上：它不读任何 `Process` 状态，且要处理 null/`InvalidLine` 这类并非 `InstructionLine` 的输入；顺带免掉了 `EmueraConsole` 里因 `System.Diagnostics.Process` 同名而必需的 `GameProc.Process.` 限定。`ExecuteLine` 改用 `process.DoDebugNormalFunction(func, false)` 分派：`PRINTPLAIN`/`PRINTBUTTON` 等 `METHOD_SAFE` 命令仍可用，`QUIT`/`PRINTW`/`RESTART`/`CALLEVENT` 被拒；XML 注释列出可执行子集与非 `-Debug` 下 Debug 系指令同样被拒。
- 关闭的两处同族缺口（原票未列，同属「输入未校验即执行」）：`SetArgumentTo` 的 false 返回值此前被忽略，实参解析失败（如 `ADDCHARA` 缺参）会带着空 `BuiltArgument` 进 `DoInstruction`；非 `-Debug` 模式下 `DEBUGPRINT` 族由 `SetArgumentTo` 跳过实参解析、随后同样读空 `BuiltArgument`。两者现在都得到 `CodeEE`。后者落在共用入口上，故 `DebugCommand` 在非 Debug 模式下的同类输入也随之由空引用改为明确文案（方向合理，属票外行为变更，已在此记录）。
- 行为记录：`ExecuteLine` 的分派改用 `Process` 的实时 `state`/`exm`（此前是 `SetParent` 时捕获的 `processState`/`expressionMediator`），「相关发现」里记的陈旧 state 问题随之消失；`PluginManager.processState` 仍由 `GetStackTrace` 使用。签名保持 `void ExecuteLine(string)`。
- 行 8：`GetKeywordDictionary` 上方的 TODO 改写为真实契约（3 行，只写契约与 ERD 早退的现状，不再断言「无调用方可达」）；该 TODO 与整方法契约随后由 issues/29 收口为 Try + 非空入口。
- 文档：`docs/architecture.md` 补 `MemoryLineEntry`/`LineLocationText` 两个条目与 `ExecuteLine` 的可执行子集；`docs/tracking/issue-tracker.md` 的内容规则改为「实施结论与验收发现写入正文（写结论不写经过）」，与近期提交的回填实践一致。

验证结果：

- 新增 `Emuera.Tests` 三个文件共 16 条用例（`Runtime/Script/Data/IdentifierDictionaryTests.cs`、`Runtime/Script/Statements/LineLocationTextTests.cs`、`Runtime/Utils/PluginSystem/PluginManagerTests.cs`），逐条先取得失败（`NullReferenceException`/`InvalidCastException`）再转绿；「默认结构即无位置」与 `MemoryLineEntry` 的 `InvalidLine` 分支另做变异校验，确认用例能分辨。`LineLocationText` 内部类型化与 `MemoryLineEntry` 抽取后，同一批用例仍全绿（重构的行为安全网）。
- 全量构建（`-c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable`）：0 错误、CS86xx 0 条；警告 85 条＝既有 83 条 + 本票两个新增文件各 1 条 CS8981。
- `Emuera.Tests`：338 条用例，本票新增的 16 条稳定通过；**`CallformCacheTests.MultiInterpolationForm_UsesStrKey` 是既有脆弱用例**——实测全量 3 次中 2 次失败，且排除本票 3 个新测试类（322 条用例）后仍 2/3 失败，故与本票无关；机制是该用例用 `A_{TFLAG}_{RESULT}` 拼键，而 `{RESULT}` 会被其他用例改写（其注释自称只选 TFLAG/SAVESTR 等不被触碰的变量，漏了 `{RESULT}`）。该问题属测试工程，宜记入 `.scratch/test-infrastructure/`，本票不修。
- 待人工验收：真实游戏目录的启动与 `-DEBUG` 调试窗口（`PRINTL` 仍工作、`RESTART` 被明确拒绝），以及 `VariableSize.CSV` 坏输入下的报错文本。
- 验收发现（已修）：本票新增的 `LineLocationTextTests.MemoryLine()` 直接用 `LogicalLine` 承接 `LogicalLineParser.ParseLine` 的 `LogicalLine?`，测试工程因此报 CS8603，与完成标准「`Emuera.Tests` 的 CS86xx 为 0」不符；补 `!` 后归零。

## 曾考虑的替代方案

1. `ExecuteLine` 只接受 `Instruction != null`：放弃——`PRINTPLAIN`/`PRINTBUTTON` 等由 `ArgumentBuilder` 注册的 `METHOD_SAFE` 命令会一并失去插件入口，而它们本无状态耦合。
2. `ExecuteLine` 只补入参校验、不施加指令过滤：放弃——`RESTART`/`CALL`/`CALLEVENT` 的可达性保留，行 3/4 需各自再补守卫，同一不变量要写两遍。
3. `ExecuteLine` 改返回 `bool`：放弃——已编译插件 DLL 的调用点是 `callvirt void ExecuteLine(string)`，改签名破坏二进制兼容；需要非异常错误面时另立 `TryExecuteLine`。
4. 行 4~6 各处就地判空：放弃——同一段「位置 + 父标签」文本散在五处，且这些调用点需要 `EmueraConsole` 因而无法进 `Emuera.Tests`。
5. 行 8 的 ERD 早退改为抛 `CodeEE`：放弃——当前无可达调用方，属 B 类，只改写 TODO；后续 issues/29 以「Try + 非空入口」实现了同一收口（当时未考虑拆分，故未采纳就地改抛）。
6. 用 `EmueraPluginExample` 写最小插件复现（本票原方案第 1 步）：放弃——AGENTS.md 规定用户未显式说明时不改动该项目，且插件 DLL 路径也无法进 `Emuera.Tests`。

## 影响

- 涉及插件 API 的可观测行为（`ExecuteLine` 的指令过滤会改变插件能做什么），属对外行为变更，须单独标注；但保持 `void` 签名，已编译插件不受影响
- `handleException`、`GetStackTrace` 与 `IntoFunction` 回溯的改动只影响报错输出，不改变脚本语义
- 不新增 `Lang` 文本；若实施中确需新文案，须同时改 `Lang.Error` 与 `emuera-eng.xml`、`emuera-zhs.xml`
- 本票不处理解析期配对不变量（issues/16）与省略参数（issues/15）
