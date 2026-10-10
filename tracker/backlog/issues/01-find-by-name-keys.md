---
id: backlog-01
status: implemented
type: bug
---

# ConfigData.FindByName 的键形态与写入方不对称

## 任务

发现于 `nullable-migration-21` 的断言审计。两处同源：

- `Emuera/Runtime/Config/ConfigData.cs` 的 `FindByName`（实施后改名 `FindByKey`，见方案）：`item.Name == key` 对全库没有一项是唯一命中路径——`key` 先 `ToUpper()`，而 `Name`（`Enums.AsString(code)`，如 `"FontName"`）未大写化；`ConfigCode` 中只有 `FPS` 全大写因而命中，但该项的 `EngText` 同为 `"FPS"`，`Text`/`EngText` 分支已覆盖。`AConfigItem.Name` 全库仅此一处读取，也没有任何写入方写枚举名
- `GetDebugItem(string)` 传 `matchEnglishName: false` 不认 `EngText`，而 `SaveDebugConfig` 经 `AConfigItem.ToString()` 在 `EnglishConfigOutput=true` 时写出的正是 `EngText` → debug.config 自写自读不回环，重启后设置静默丢失。可达链：ConfigDialog 勾 `EnglishConfigOutput` → DebugConfigDialog 保存 → 启动时 `Program.cs` 的 `LoadDebugConfig`（`Program.cs:175` 的 `LoadConfig` 已先行设好该静态，顺序成立）

完成标准：debug.config 自写自读成环（覆盖 `EnglishConfigOutput=true`）；`Name` 死比较清理；未命中时的静默处置（补告警，或写明「静默是既定语义」）二选一并有取证。

## 方案

把「写入方写哪个键」与「读取方认哪些键」收成 `AConfigItem` 上的一处声明（`ConfigItem.cs`）：

```csharp
// WrittenKey 是写出用的键，Matches 认回它以及另一种拼写；两者同一集合是刻意的。
public string WrittenKey => Config.EnglishConfigOutput ? EngText : Text;
public bool Matches(string key) =>
    Text.Equals(key, StringComparison.OrdinalIgnoreCase) || EngText.Equals(key, StringComparison.OrdinalIgnoreCase);
```

- 删 `AConfigItem.Name`（连同 `Enums.AsString` 的唯一调用）；`FindByName` 改名 `FindByKey`（`Name` 已无对应物）并去掉 `matchEnglishName` 参数与 `Name` 项，改 `item.Matches(key)`；大小写归一化移进 `Matches`，调用方不再需要「key 必须先大写」的前置约定
- `GetReplaceItem(string)`/`GetDebugItem(string)` 不再传 `false`：三个数组共用一个键空间。写入侧 `ToString()`（`ConfigItem.cs`）与 `SaveConfig` 的 `ValidExtension` 分支（`ConfigData.cs`，原先各自抄一份 `EnglishConfigOutput ? EngText : Text`）改用 `WrittenKey`
- 未命中的静默处置选「写明既定语义」：`loadConfig` 的未命中 `continue` 写全理由（配置文件寿命长于项集合：手写键、跨版本键），`LoadReplaceFile` 与 `LoadDebugConfig` 的 `item?.TryParse(...)` 各一行指回它；不加告警——`csv/_default.config`、`_fixed.config`、`_Replace.csv` 都是游戏作者手写，加 `ParserMediator.ConfigWarn` 会在降级/跨版本场景刷屏；而它此前真正的危害（Emuera 自己写的键被丢）已由上面的同一处声明结构性消除

测试 `Emuera.Tests/Runtime/Config/ConfigKeySymmetryTests.cs`（7 例）：

1. 遍历 `Enum.GetValues<ConfigCode>()`，每项断言 `GetItem(Text)` 与 `GetItem(EngText)` 都返回该项本身（`Assert.Same`，顺带挡住「两个项共用同一个键」的误命中）——实现前为红，是「问题仍在」的可执行取证；删 `Name` 的行为等价性也由它兜住
2. debug.config 端到端：`EnglishConfigOutput=true` 下设两个 debug 项的值 → `SaveDebugConfig()` → 把两项改成别的值扰动 → `LoadDebugConfig()` → 断言两项回到写盘值。走真入口（含文件解析、`TryParse`、`Config.SetDebugConfig`）；`SetDebugConfig` 只写 7 个 `Debug*` 静态，用例把它们快照并在 `Dispose` 还原，故不必回避 `LoadDebugConfig`
3. `SaveConfig()` 在两种输出形态下的键值全部可读（configArray 那半边既有行为固定）
4. 三条读路径各一个未命中用例（`loadConfig` / `LoadReplaceFile` / `LoadDebugConfig`）：文件里放「未知名 + 已知键正控行 + 近似误配键」三行，正控必须生效、近似键不得部分命中（只放一个未知键会空转：值本来就不会变）；`loadConfig` 的返回值不断言——它在 catch 之后同样 `return true`，返回值不携带信息

## 曾考虑的替代方案

1. 最小改动：只删 `Name` 比较 + 让 `GetDebugItem` 认 `EngText`，保留 `matchEnglishName` 开关。放弃原因：该开关把「这行来自哪个文件」当键语义，四个调用点各传各的，同类不对称可复发；且 `ValidExtension` 分支的第三份 `Text`/`EngText` 判断留在原地
2. 给未知键补告警（`ParserMediator.ConfigWarn`）。放弃原因见上：手写配置文件天然含未知键，告警的噪音大于收益，且不能替代「写出的键必可读」这条结构性保证
3. 让 `Name` 参与匹配（改 `OrdinalIgnoreCase` 比较）。放弃原因：等于新增第三种键拼写，而没有任何写入方产出它，是同一类不对称的另一种形态

## 影响

- 生产代码：`ConfigData.cs` +13/−7、`ConfigItem.cs` +12/−2（含注释），净 +16；`Fixed` 语义、`TryParse`、单例语义均未动
- 可观测行为变化只有两条：① debug.config 在 `EnglishConfigOutput=true` 下自写自读成环（本票目标，此前有写入方 `SaveDebugConfig` 却没有读侧与之对称，正是这个 bug）；② `_Replace.csv` 与 debug 项开始接受英文键（`GETCONFIG`/`GetItem` 亦然）——`_Replace.csv` 没有写入方（作者手写，本票范围外，此处是有意的能力增加），debug 半边则是 ① 的必然结果
- 大小写归一化改 `OrdinalIgnoreCase` 后不再依赖 `ToUpper()` 的 culture；`Program.Main` 开头设 `InvariantCulture`（`Program.cs:54`）一事与本改动正交

