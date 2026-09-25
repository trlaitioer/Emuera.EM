# Nullable:插件与错误报告路径的空引用

Status: ready-for-agent

## 任务

非空断言中有一组只在插件调用或错误处理路径上可达。这些路径的调用方不受解析期不变量保护（内存行、跨线程调用、初始化期失败），断言掩盖的是真实的 null。本票逐个补守卫或改为可空读取。

### 范围

| 位置 | 现状 | 可达路径 |
| --- | --- | --- |
| `PluginManager` 的 `ExecuteLine` | 用 `LogicalLineParser.ParseLine` 构造的内存行中 `ParentLabelLine` 为 null，而 `Position` **非 null**（是 `ScriptPosition` 默认值：`LineNo = -1`、`Filename = ""`，无参构造写死），故插件路径上真正会命中 `!` 的是 `ParentLabelLine`；且分发指令时不施加 `IsFlowContorol`/`IsPartial`/`IsMethodSafe` 过滤（`EmueraConsole.DebugCommand` 有过滤） | 插件执行 `RESTART` 之类流控指令，命中 `RESTART_Instruction.DoInstruction` 的 `state.JumpTo(func.ParentLabelLine!)` |
| `Process.State.cs` 的 `ProcessState.IntoFunction`（调试回溯） | 只判 `retAddress != null`，未判 `Position`/`ParentLabelLine` | 内存行作为返回地址且处于 `-Debug` |
| `PluginManager` 的 `GetStackTrace` | 首处依次解引用当前行、位置与父标签行且不判空（循环内仅以 `parent.Position != null` 守卫，`ParentLabelLine` 仍为断言） | 插件在 `CALLSHARP` 执行窗口之外（自身线程、脚本已结束）调用 |
| `PluginManager` 的 `ExecuteLine`（入参未校验，issues/21 批次 C 判出的两处 C） | `(InstructionLine)logicalLine!`：`LogicalLineParser.ParseLine` 对空/纯空白行返回 null、对 `@`/`$` 行返回非指令行，引用转换不抛，下一句 `func.Function` 即空引用；`func.Function.Instruction!`：`Instruction` 为 null 的正是 PRINT 族之外由 `doNormalFunction`/`doFlowControlFunction` 分派的命令（`runScriptProc` 的三路分派即证据） | 插件 `ExecuteLine("")`、`ExecuteLine("QUIT")`、`ExecuteLine("PRINTPLAIN x")`、`ExecuteLine("GOTO @FOO")`；示例插件用的 PRINTL 不属于该组，故此前未暴露 |
| `Process.cs` 的 `Process.handleException` | 外层只判 `position != null`，未检查 `current`；而 `handleException(e, null, true)` 是合法调用（`Process.Initialize` 就这么调） | 初始化期逃逸的带位置 `CodeEE`——触发条件是 `exc is EmueraException { Position: not null }` 而 `current == null`（如 `ParserMediator.LoadEraExRenameFile` 的逐行处理阶段失败；`pos` 在首个 `new ScriptPosition(filepath, lineNo)` 之前的 `File.ReadAllLines`/编码探测失败时仍为 null，那种失败走 `position == null` 分支、不触发本缺陷）——报错路径自身抛异常，掩盖原始错误 |
| `IdentifierDictionary` 的 `GetVariableToken`（显式 subKey 分支，`LOCAL@name`/`ARG@name`） | 只以 `isError: false` 告警后即解引用 `line` 与 `ParentLabelLine`；紧邻的无 subKey 分支有正确的 `CodeEE` 处理 | 解析期 `GetScaningLine()` 返回 null 或无父标签的行 |
| `ConstantData` 的 `GetKeywordDictionary`（潜在缺陷，B 类） | 只在走到方法末段、`index >= 0` 时才把 null 转为 `CodeEE`；ERD 分支有 4 处早退 `return ret;` 会在 `index >= 0` 时把 null 原样返回——`string.IsNullOrEmpty(varname)` 一处，以及 `erdNameToIntDics` 未命中的 3 处（`VAR`/`VARS`/`CVAR`/`CVARS`、`VAR2D` 族、`VAR3D`/`VARS3D`），调用方（`VariableDataWrappers`、`PluginManager` 的包装访问器）随即索引字典 | 插件以空 key 读取一个非关键字支撑的变量码（空 key 只是最容易命中的一条）。当前无此类调用方，记录为潜在缺陷 |

issues/21 的断言审计（批次 1/2/3）另记入以下 B 类位置——断言内容不成立，但当前没有可达路径；破坏者已写明，供本票统一收口：

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

### 修法方向

- 内存行 API 的读取改为可空读取（`ParentLabelLine?.LabelName` 一类），或在 `ExecuteLine` 施加与 `DebugCommand` 相同的指令过滤——后者更贴近「插件不应执行流控指令」的原意，需先确认插件文档是否承诺可执行任意命令。
- `GetStackTrace` 与 `handleException` 的空值按「无位置信息」降级输出，而不是抛异常掩盖原始错误。
- `GetVariableToken` 的显式 subKey 分支与无 subKey 分支对齐（拿不到父标签即抛 `CodeEE`）。
- `ExecuteLine` 复用 `runScriptProc` 的三路分派（`Instruction != null` / `IsFlowContorol()` / 其他），并在入参不是指令行时抛带说明的异常或改为返回 bool——后者更贴近插件 API 的用法。
- `GetKeywordDictionary` 的 ERD 早退在 `index >= 0` 时改为抛 `CodeEE`，或在包装访问器入口判空。
- 上表 B 类位置只登记、不修改（当前无可达路径）；待破坏者进入可达范围时再按本节修法处理。

## 方案

1. 先用 `EmueraPluginExample` 写最小插件复现前四项（`ExecuteLine("RESTART")`、执行窗口外调用 `GetStackTrace`、内存行作为返回地址、初始化期抛错），确认异常与栈。
2. `GetVariableToken` 与 `GetKeywordDictionary` 两处不需要插件即可复现（前者用 `LOCAL@name` 写法，后者用空 key 的包装访问器），优先处理。
3. 逐项记录修法与理由；「插件可执行哪些指令」若涉及对外契约变更，需在票内标注并同步 `EmueraPluginExample`。

## 影响

- 涉及插件 API 的可观测行为（`ExecuteLine` 的指令过滤会改变插件能做什么），属对外行为变更，须单独标注
- `handleException` 与 `GetStackTrace` 的改动只影响报错输出，不改变脚本语义
- 本票不处理解析期配对不变量（issues/16）与省略参数（issues/15）
