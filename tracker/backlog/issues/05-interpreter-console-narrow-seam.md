# 解释器与控制台之间的 seam 落在具体上帝类上

id: backlog-05
Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（表现层 + 声明/执行层 + 测试面测绘，三方独立命中同一处）。

`EmueraConsole` 既是 module、又是「通往 WinForms 的窗口」，解释器侧直接依赖它：

- 注入路径：`Process.cs:26, 47, 70`、`Process.State.cs:94-102`、`ExpressionMediator.cs:17-26`（`public readonly EmueraConsole Console`）、`ConstantData.cs:137-139`、`ParserMediator.cs:34-36`、`ErbLoader.cs:25, 34`、`ErhLoader.cs:18, 26`、`LogicalLineParser.cs:333, 340, 411, 454, 676`、`VariableEvaluator.cs:2250, 2327`
- 反向穿透（解释器拿到 Form）：`Instraction.Child.cs:1046, 1111, 2674, 2791, 2899, 3016` 的 `exm.Console.Window.ApplyTextBoxChanges()`、`BuiltinMethod/Graphics.cs` 的 `MouseButtonMethod`（`Window.MainPicBox.PointToClient`）与 `BuiltinMethod/EeAndPatches.cs` 的 `HotkeyStateMethod`/`HotkeyStateInitMethod`（`Window.hotkeyState`）

量化：Runtime 侧 16 个文件、417 个调用点、触及 105 个不同的 `EmueraConsole` 成员。而各调用方真正需要的极窄——`ErbLoader` 只要 `PrintSystemLine`(20) / `PrintError`(12) / `NewLine`(5)，`ErhLoader` 只要 `PrintSystemLine`(1)，`ConstantData` 只要 `PrintError`(8) / `PrintSystemLine`(4)，`LogicalLineParser` 只在 411/676 各一处。

附带问题：

- 告警有两条通道：UI 文本 + 进程内 11 处 `System.Media.SystemSounds.Hand.Play()`（`ParserMediator.DeferParseErrorSound` 是对它的补丁）
- 线程亲和泄漏：`ErhLoader.cs:371, 384, 398` 在装载路径调 `Application.DoEvents()`；`ErbLoader.cs:867-880` 的注释自陈「runs on a worker thread … must not touch the UI」
- `Clipboard.cs:13, 41-45, 338` 的解释器类持 `Forms.MainWindow` 并自己 `Invoke`
- `ConsoleButtonString.cs:414-429` 经 `GlobalStatic.Console` 维护自己 parent 就持有的环形缓冲；`EmueraConsole.cs:2312-2314` 在自绘里读 `GlobalStatic.Console.GetLineNo`
- 表现层类型被放进解释器命名空间：`Runtime/Utils/EvilMask/ConsoleDivPart.cs:12`、`ConsoleEscapedParts.cs:9`
- `ProcessState(EmueraConsole)` + `EmueraConsole.IsRunning`（`:446`）使 `runScriptProc` 必须真 UI 才能构造

完成标准：

- Runtime 侧只依赖一个窄 seam：「向玩家报告（进度行 / 错误行 / 告警）+ 少量状态查询」；`EmueraConsole` 是它的生产 adapter，夹具用记录型 adapter
- 14 处 `…Window.*` 收成 console 内部请求，解释器不再持有 Form
- `SystemSounds` 收进同一 seam，`DeferParseErrorSound` 随之失去存在理由
- 111 个 `DoInstruction` 中 34 个只依赖 `(Argument, VariableEvaluator)` 的实现不再被强制接收会话对象
- 两个 adapter 都真实存在（生产 / 记录型），不引入只有一个实现的假想 seam

## 方案

待评估。仓库内已有做对的样板：`VariableEvaluator.UpdateInUpcheck(EmueraConsole window, bool skipPrint)`（`VariableEvaluator.cs:2248-2250`，注释明写「Data 侧操作 EmueraConsole 只在这里」）——建议以它为模板推广，而不是另立一套。

## 影响

- **测试面**：今天全部靠 `null!` 绕过——`TestBootstrap.cs:47, 58, 60` 的 `new ConstantData(csvDir, null!, false)` / `new ExpressionMediator(null!, vEvaluator, null!)` / `new Process(null!)`，`PluginManagerTests.cs:26` 的 `new ProcessState(null!)`，`ErbLoaderNestCheckTests.cs:44` 的 `new ErbLoader(null!, …)`。`HTML_STRINGLEN`（`BuiltinMethod/PrivateAdditions.cs` 的 `HtmlStringLenMethod` → `HtmlManager.cs:48-50` 读 `GlobalStatic.Console.StrMeasure`）在夹具里必然 NRE。改造后可断言「加载 N 个 erb 产生哪些告警行、顺序如何」
- **与既有票的关系**：`tracker/nullable-migration/issues/20-debug-eval-context-null.md:32, 42` 已记载 `exm.Process` 那一支无覆盖；`tracker/nullable-migration/issues/12-ui-periphery.md:218` 记了 `MainWindow.console` 可空后两条取值路径的判空责任待明确——本票是它们的结构性解法，不是重复
- **与既有决策的冲突**：`tracker/performance-optimization/issues/04-parse-parallel.md` 已就 `Application.DoEvents`、`SystemSounds`、`ParserMediator` 告警列表的并发纪律逐项定案（含把 `Application.DoEvents` 从并行区移除并入该票）。本票若动 seam，须与该票的并行边界结论对齐，不重复裁定并发问题
- 改动量估计：大（16 个文件、417 个调用点）。建议先切 `ErbLoader`/`ErhLoader`/`ConstantData`/`LogicalLineParser` 四个窄需求方，再动指令层
- 相关票：issues/11（诊断返回值化，同一 seam 的另一半）、issues/07（Config 静态量）
