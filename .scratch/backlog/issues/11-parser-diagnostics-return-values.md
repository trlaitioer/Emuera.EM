# 诊断返回值化：告警是副作用，console 为空时被静默丢弃

Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（测试面测绘 + 装载层，两方独立命中）。

`ParserMediator` 的告警 interface 很窄（`Warn` / `ConfigWarn` / `FlushWarningList` / `HasWarning`），被数百个解析点使用——是本领域杠杆最好的小 module。但它的实现把「报告」与「UI」焊在一起：

- `ParserMediator.cs:34` 的 `console` 是 `private static EmueraConsole?`；`:154`/`:194` 在 console 为 null 时直接 `return` → **`level < 2` 的告警全部被静默丢弃**，只有 `level >= 2` 写 `line.IsError`/`ErrMes`（`:173-182`）
- 而 console 为 null 正是所有测试与 `--analysis` 模式的处境：`TestBootstrap.cs:32` 调 `ParserMediator.Initialize(null)`，`LogicalLineParser` 的 console 参数在非 AnalysisMode 下也传 null
- 受影响面：`ErbLoader.cs` 有 **85 处** `ParserMediator.Warn`，全部是副作用式
- 同一个静态槽里还借住了 4 件无关事实：`RenameDic`/`LoadEraExRenameFile`（`:96-141`，装载期文件替换，属于 `EraStreamReader`/装载器那层）、`CallEdgeRecorder`（`:83-92`，生产者与消费者都在 `ErbLoader`）、`DeferParseErrorSound`/`TakeParseErrorCount`（`:47-73`，表达「本段跑在 worker 上」）、`SingleLine`（`:263-267`，只有 `console!` 转发）
- 附带死分支：`ParserMediator.RenameDic { get; } = []` 永不为 null，故 `LexicalAnalyzer.cs:1079` 的 `if (ParserMediator.RenameDic == null)` 是死分支——「rename 未加载」在类型上无法表达

完成标准：

- 装载/解析**返回**诊断（告警列表 + 错误行），由调用方决定展示或断言；console=null 不再意味着信息丢失
- `ErbLoader` 的 85 个告警点变为可断言
- 4 件借住事实各归其位：`RenameDic` 归装载/读取层、`CallEdgeRecorder` 归 `ErbLoader`、`DeferParseErrorSound` 归 `ErbLoader.ParseScript`、`SingleLine` 随 console seam 一起处理
- 用例：加载 N 个 erb 后断言告警列表的内容与顺序

## 方案

待评估。这是审查给出的**最便宜杠杆**：一处改动解锁 85 个告警点与 4 个既有票的验收手段。

## 影响

- **现象已被记录，本票不重复立题**：`.scratch/performance-optimization/issues/07-chara-duplicate-warning.md:9` 已写明「告警条数差异无法经现有单测断言（`ParserMediator.Warn` 在 console 为 null 时不落 warningList）」，并把「不做脚本冒烟」写进该票完成标准；`.scratch/test-infrastructure/issues/03-phase2-beyond.md:29` 也记了同一现象。两者的处置都是「绕开」，本票是结构性解法
- **与既有决策的边界**：`.scratch/performance-optimization/issues/04-parse-parallel.md` 已就 `ParserMediator` 告警列表的并发纪律（`warningListLock`、「worker 线程收集、UI 线程 flush」）与 `DeferParseErrorSound` 的现有语义逐项定案，并明确 `ParserMediator.RenameDic`「确认只读，无需处理」。本票若把告警改为返回值，须保留该票已定的并发边界（返回值化会让「锁内 add、锁外打印」自然退化，但不能在新形状里引入数据竞争）；返回值化**不推翻**该票的结论
- **测试面**：`OmittedArgumentTests.cs:23-30` 手动抑制 `DeferParseErrorSound`（否则 `ParserMediator.cs:64` 播系统提示音）——这正是该耦合的证据；`LexicalAnalyzerTests.cs:179` 只能断言 `Assert.ThrowsAny<Exception>`（NRE 也算过，注释已承认可能是 `GetConfigName` NRE），返回值化后可收窄
- 改动量估计：中（`ParserMediator` + `ErbLoader` 的 85 个调用点的消费方式）
- 相关票：issues/05（console seam 的另一半）、issues/14（`ErbLoader` 装载顺序）
