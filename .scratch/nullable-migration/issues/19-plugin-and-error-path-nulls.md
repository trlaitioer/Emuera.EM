# Nullable:插件与错误报告路径的空引用

Status: ready-for-agent

## 任务

非空断言中有一组只在插件调用或错误处理路径上可达。这些路径的调用方不受解析期不变量保护（内存行、跨线程调用、初始化期失败），断言掩盖的是真实的 null。本票逐个补守卫或改为可空读取。

### 范围

| 位置 | 现状 | 可达路径 |
| --- | --- | --- |
| `PluginManager` 的 `PluginAPI.ExecuteLine` | 用 `LogicalLineParser.ParseLine` 构造的内存行中 `Position` 与 `ParentLabelLine` 均为 null，且分发指令时不施加 `IsFlowContorol`/`IsPartial`/`IsMethodSafe` 过滤（`EmueraConsole.DebugCommand` 有过滤） | 插件执行 `RESTART` 之类流控指令，命中 `RESTART_Instruction.DoInstruction` 的 `state.JumpTo(func.ParentLabelLine!)` |
| `Process.State.cs` 的 `ProcessState.IntoFunction`（调试回溯） | 只判 `retAddress != null`，未判 `Position`/`ParentLabelLine` | 内存行作为返回地址且处于 `-Debug` |
| `PluginManager` 的 `PluginAPI.GetStackTrace` | 完全不判空，依次解引用当前行、位置与父标签行 | 插件在 `CALLSHARP` 执行窗口之外（自身线程、脚本已结束）调用 |
| `Process.cs` 的 `Process.handleException` | 外层只判 `position != null`，未检查 `current`；而 `handleException(e, null, true)` 是合法调用（`Process.Initialize` 就这么调） | 初始化期逃逸的带位置 `CodeEE`（如 `ParserMediator.LoadEraExRenameFile` 包装的 I/O 失败）——报错路径自身抛异常，掩盖原始错误 |
| `IdentifierDictionary` 的 `GetVariableToken`（显式 subKey 分支，`LOCAL@name`/`ARG@name`） | 只以 `isError: false` 告警后即解引用 `line` 与 `ParentLabelLine`；紧邻的无 subKey 分支有正确的 `CodeEE` 处理 | 解析期 `GetScaningLine()` 返回 null 或无父标签的行 |
| `ConstantData` 的 `GetKeywordDictionary`（潜在缺陷，B 类） | 在 `index >= 0` 时会把 null 转为 `CodeEE`，但配置为 ERD 且传入空 key 时直接返回 null，调用方（`VariableDataWrappers`、`PluginManager` 的包装访问器）随即索引字典 | 插件以空 key 读取一个非关键字支撑的变量码。当前无此类调用方，记录为潜在缺陷 |

### 修法方向

- 内存行 API 的读取改为可空读取（`ParentLabelLine?.LabelName` 一类），或在 `ExecuteLine` 施加与 `DebugCommand` 相同的指令过滤——后者更贴近「插件不应执行流控指令」的原意，需先确认插件文档是否承诺可执行任意命令。
- `GetStackTrace` 与 `handleException` 的空值按「无位置信息」降级输出，而不是抛异常掩盖原始错误。
- `GetVariableToken` 的显式 subKey 分支与无 subKey 分支对齐（拿不到父标签即抛 `CodeEE`）。
- `GetKeywordDictionary` 的 ERD 早退在 `index >= 0` 时改为抛 `CodeEE`，或在包装访问器入口判空。

## 方案

1. 先用 `EmueraPluginExample` 写最小插件复现前四项（`ExecuteLine("RESTART")`、执行窗口外调用 `GetStackTrace`、内存行作为返回地址、初始化期抛错），确认异常与栈。
2. `GetVariableToken` 与 `GetKeywordDictionary` 两处不需要插件即可复现（前者用 `LOCAL@name` 写法，后者用空 key 的包装访问器），优先处理。
3. 逐项记录修法与理由；「插件可执行哪些指令」若涉及对外契约变更，需在票内标注并同步 `EmueraPluginExample`。

## 影响

- 涉及插件 API 的可观测行为（`ExecuteLine` 的指令过滤会改变插件能做什么），属对外行为变更，须单独标注
- `handleException` 与 `GetStackTrace` 的改动只影响报错输出，不改变脚本语义
- 本票不处理解析期配对不变量（issues/16）与省略参数（issues/15）
