# ConfigData.FindByName 的键形态与写入方不对称

Status: needs-triage
Type: bug

## 任务

发现于 issues/21 的断言审计。两处同源：

- `Emuera/Runtime/Config/ConfigData.cs` 的 `FindByName`：`item.Name == key` 恒不成立——`key` 先 `ToUpper()`，而 `Name`（`Enums.AsString(code)`，如 `"FontName"`）未大写化；`AConfigItem.Name` 全库仅此一处读取，也没有任何写入方写枚举名
- `GetDebugItem(string)` 传 `matchEnglishName: false` 不认 `EngText`，而 `SaveDebugConfig` 经 `AConfigItem.ToString()` 在 `EnglishConfigOutput=true` 时写出的正是 `EngText` → debug.config 自写自读不回环，重启后设置静默丢失。可达链：ConfigDialog 勾 `EnglishConfigOutput` → DebugConfigDialog 保存 → 启动时 `Program.cs` 的 `LoadDebugConfig`

完成标准：debug.config 自写自读成环（覆盖 `EnglishConfigOutput=true`）；`Name` 死比较清理；未命中时的静默处置（补告警，或写明「静默是既定语义」）二选一并有取证。

## 方案

待评估。

## 影响

未知；改动量估计：小到中（`ConfigData.cs` 三处查找 + 一个用例）。`_Replace.csv` 那半边不在范围内：该文件由游戏作者手写、Emuera 从不写它，没有写入方与之对称。
