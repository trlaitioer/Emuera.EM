# Nullable:坏输入路径解引用 null（配置/脚本/存档）

id: nullable-migration-28
Status: ready-for-agent

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

## 影响

- 行为变化只在坏输入路径上：现状是空引用异常，改为回退或明确错误
- 触及 `Config`/`JSONConfig`/`EraDataStream`/`EraStreamReader` 四处；正常输入路径不变
- 与 issues/19 分列：本票不涉及插件 API，issues/19 覆盖的是插件与错误报告路径
