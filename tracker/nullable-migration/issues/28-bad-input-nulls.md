---
id: nullable-migration-28
status: implemented
---

# Nullable:坏输入路径解引用 null（配置/脚本/存档）

## 任务

issues/21 的断言审计在批次 C 判出四处 **C 类可达缺陷**：调用方/被调方以「无法读取或无法解析时返回 null」为契约，而读取点用 `!` 断言非空，于是坏输入（手改配置、编码异常的脚本、截断的存档）会以 `NullReferenceException` 结束，而不是给出可读的错误。

| 位置 | 现状 | 触发 |
| --- | --- | --- |
| `Config.cs` 的 `Config.DefaultFont` | `FontFactory.GetFont("", Regular)!`；`FontFactory.CreateFont` 在 `new Font(...)` 失败时 `catch { return null; }`，`GetFont` 也显式 `return null` | emuera.config 的字体名不可用/为空，或字体尺寸 ≤ 0（`FontMetrics.FindRenderSizePx` 对 `baseFontSize <= 0` 原样返回）→ 首个消费点（`EmueraConsole.Print.cs` 的 `.Size`、`MainWindow.cs` 的 `.FontFamily`、`GraphicsImage.cs`、`ClipBoardDialog.cs`）空引用 |
| `JSON/JSONConfig.cs` 的 `Load` | `Data = JsonSerializer.Deserialize<JSONConfigData>(json)!`；`Deserialize<T>` 对内容为 `null` 的文件返回 null 且不抛 | `<game>/setting.json` 内容为 `null`（手改/工具生成）→ 首个 `JSONConfig.Data.*` 读取空引用 |
| `EraDataStream.cs` 的 `EraDataReader.ReadInt64Array3DExtended` | 扩展数组 `{ … }` 块内 `reader.ReadLine()` 可返回 null，随后 `str!.Length` 解引用（代码注释自认保留了旧行为） | 存档在扩展数组块内被截断 → `VariableEvaluator.LoadFrom` 无 try/catch，用户只看到「予期せぬエラー」而非 `FileEE` |
| `EraStreamReader.cs` 的 `OpenOnCache` | `_fileLines = Preload.GetFileLines(path)!`；`Preload.readAllLinesDetectEncoding` 的内层 `catch` 会把 null 写入缓存（例如 BOM 为 UTF-8 但文件含非法 UTF-8 字节） | 缓存值为 null → 下一次 `ReadLine()` 的 `_fileLines.Length` 空引用；且 `OpenOnCache` 恒返回 true，使 `ErbLoader.loadErb` 的 `if (!OpenOnCache(...)) PrintError(FailedOpenFile)` 永不触发 |

四处均为**迁移前既有行为**（`!` 只是把当时的解引用显式化），故本票不改变「返回 null」的既有契约，只把 null 在读取点转成明确错误。

## 方案

1. `DefaultFont`：`CreateFont` 失败时回退 `FontFamily.GenericSansSerif`（或让 `DefaultFont` 可空并在 UI 层集中兜底）；
2. `JSONConfig.Load`：`?? new JSONConfigData()`，并在文件内容为 `null` 时重写为默认配置；
3. `ReadInt64Array3DExtended`：`str == null` 时抛 `FileEE`（把 EOF 变成明确的存档损坏错误）；
4. `OpenOnCache`：缓存值为 null 时返回 false（或抛 `FileEE`），让既有的 `FailedOpenFile` 分支生效；
5. 四处随之删掉 `!`；`Emuera.Tests` 各补一条「坏输入不得抛 `NullReferenceException`」的用例。

完成标准：上述四种坏输入分别得到回退值或可读错误，不再出现 `NullReferenceException`；`Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0，测试通过。

### 实施结果（2026-10-05）

四处按方案落地，`!` 全部删除：

1. `Config.DefaultFont`：改为 `FontFactory.GetDefaultFont()`，回退入口 `FontFactory.GetFallbackFont(int fontSize)` 把通用家族（字号 `Math.Max(1, …)` 钳成正数）构造的字体**放进 `fontDic` 缓存**——该字体在绘制/测量路径每帧被读取，不进缓存就是每次 `new Font`、永不 `Dispose`、也不受 `ClearFont()` 管辖（GDI 句柄泄漏）。
   **可达性更正**（复核时补实测）：该断言在**生产路径不可达**——家族名被 GDI+ 静默替换（本机实测：空串、纯空白、未知名与 `null` 家族名都给替代字体 `Microsoft Sans Serif`，`new Font(string, …)` 一概不抛；票面所列的「字体名为空」触发由 `ConfigItem<string>.TryParse` 放行空值，但同样落在静默替换上），字号被配置加载的 `FontSize < 8 → 8` 钳住（缩放路径 `ApplyWindowGeometry` 亦钳到 ≥8），而 `DefaultFont` 的最早消费点（`MainWindow` 构造）晚于 `LoadConfig`。故按 **B 类（不变量不成立但当前不可达）** 记，改动作为防御性契约保留；票面「C 类可达缺陷」的判定据此更正。
   用例覆盖回退入口（任意字号可构造、逐字号缓存）；`GetFont("") ?? GetFallbackFont(...)` 这条接线**无 headless 用例**，理由复核后收窄为：`CreateFont` 只因**字号 ≤ 0** 返回 null（名称不合法不会），而 `Config` 没有公开入口能把字号放回非正值（`SetConfig` 与 `ApplyWindowGeometry` 都钳到 ≥8，`renderFontSizePx` 只由这两条路径间接写入）。夹具默认的「Config 未初始化」状态（`FontSize`/`renderFontSizePx` 皆为 0）本可跑到该分支，但 `ConsoleDisplayLineTests`/`FontMetricsTests`/`StringMeasureTests`/`StBarTests`/`ConsoleShapePartTests` 都会调用 `Config.ApplyWindowGeometry` 把它永久留在正值，而 xUnit v3 只保证串行、不保证发现顺序，据此写出的用例是顺序相关的，代价大于收益。
2. `JSONConfig.Load`：`Deserialize` 得 null（文件内容是 JSON 字面量 `null`）时回落 `new JSONConfigData()` 并把该数据重写回文件。同源的另一种坏输入（内容非法 JSON → `JsonException` 逃出启动链）经审查判定超出本票范围，已落 `backlog-21`。
3. `EraDataReader.ReadInt64Array3DExtended`：扩展数组块内读到 EOF 时抛 `FileEE(UnexpectedSaveDataEnd)`——与同函数两处外层循环同型，把截断存档变成明确的存档错误。**报错出口未变，已另立票**：`FileEE` 既非 `CodeEE` 也非 `ExeEE`，`Process.handleException` 会把它打进「予期せぬエラー」+ 堆栈，故「不再出现 `NullReferenceException`」满足，而「可读错误」只有文本改善、分级未变，承接票见「影响」节。
4. `EraStreamReader.OpenOnCache`：缓存值为 null（预读编码失败）时返回 false，并把 `_fileLines` 置为空集。三个调用方都把 false 转成 `FailedOpenFile`；其中 `ErbLoader` 在 false 后并不 return，空集保证它随后读到 EOF 而不是空引用。「路径未预读过」是另一回事（`GetFileLines` 的索引器仍抛 `KeyNotFoundException`），那是调用方漏了 `Preload` 的编程错误，保持响亮。

新增用例 5 例,按被测源码路径镜像分置:`Emuera.Tests/Runtime/Config/JSONConfigTests.cs`(1 例:`setting.json` 内容为 `null`)、`Emuera.Tests/Runtime/Utils/EraDataStreamTests.cs`(1 例:扩展数组块内截断)、`Emuera.Tests/Runtime/Utils/EraStreamReaderTests.cs`(1 例:BOM 为 UTF-8 但含非法字节的脚本缓存)与 `Emuera.Tests/Runtime/Config/DefaultFontTests.cs`(2 例:回退字体在任意字号下可构造、逐字号复用缓存实例)。红→绿实测:前三例直接报旧行为(`Data` 为 null、`NullReferenceException`、`OpenOnCache` 返回 true)。`JSONConfigTests` 现已还原进程级 `JSONConfig.Data`(审查发现:不还原会让 `UseScopedVariableInstruction` 这条静态构造门控按执行顺序失效)。

验证：`Emuera` 的 `Debug-NAudio` 与 `Debug`(WMP) 两配置 CS86xx = 0；`Emuera.Tests` 由 700 条增至 707 条，全绿。

## 影响

- 行为变化只在坏输入路径上：现状是空引用异常，改为回退或明确错误
- 触及 `Config`/`JSONConfig`/`EraDataStream`/`EraStreamReader` 四处；正常输入路径不变
- 与 issues/19 分列：本票不涉及插件 API，issues/19 覆盖的是插件与错误报告路径
- 完成标准「可读错误」只部分达成：站点 3 改抛的 `FileEE` 走 `handleException` 的兜底分支（「予期せぬエラー」+ 堆栈）。报错分级已另立 `nullable-migration-30` 承接，该票同时覆盖既有 88 处同型 `FileEE`
