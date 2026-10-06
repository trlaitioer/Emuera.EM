# JSONConfig:setting.json 内容非法 JSON 时抛 JsonException,启动链无处处置

id: backlog-21
Status: needs-triage
Type: bug

## 任务

`Emuera/Runtime/Config/JSON/JSONConfig.cs` 的 `Load()` 直接 `JsonSerializer.Deserialize<JSONConfigData>(json)`。`nullable-migration-28` 只处置了「内容为 JSON 字面量 `null`」这一种坏输入(回落默认值并重写文件);**内容非法 JSON**(缺右括号、尾随逗号、手改坏)会让 `Deserialize` 抛 `JsonException`。

调用点是启动链 `Emuera/Program.cs` 的 `JSONConfig.Load()`,它晚于 `ConfigData.Instance.LoadConfig()`、早于 `MainWindow`(控制台与日志尚未建立),且调用点无 try/catch ⇒ 未处理异常终止启动,用户只看到 CLR 的异常文本,而 setting.json 里其余设置已经读不出来。

完成标准(待评估后细化):

- 非法 JSON 得到可读的处置:要么回落到默认配置使游戏可启动,且**不静默丢弃用户文件**(例如先把坏文件改名为 `setting.json.bad` 再写默认值),要么给出明确错误并停止
- 处置方式与既有两条坏输入分支(文件不存在 → 写默认值;内容为 `null` → 回落默认值并重写)在代码与文档上一致
- `Emuera.Tests` 补一例:非法 JSON 不产生未处理异常

## 方案

待评估。可读错误的出口受限:该时机 console 尚不存在,`ParserMediator.Warn` 的警告会被静默丢弃;`Dialog.Show` 可用,但需要新的 lang 文案(3 份 xml + `Lang.cs`)。

## 曾考虑的替代方案

1. 就地 `catch (JsonException)` 静默回落默认值并重写文件:实现最省,但一次语法错误会**静默清空**用户的全部设置,比现在的「响亮崩溃」更差,故未在 `nullable-migration-28` 内顺手做。
2. 让 `JsonException` 直接冒泡:现状,即本票要解决的问题。

## 影响

- 行为变化集中在「setting.json 非法」这一违规输入路径;合法配置与既有的两条坏输入分支不受影响
- 与 `nullable-migration-28` 分列:那张票的范围是「读取/反序列化**返回 null**」的四处站点,本票是同一文件的另一种失败模式(抛异常)
