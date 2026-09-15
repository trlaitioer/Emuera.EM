# Nullable:UI 与外围启用

Status: needs-triage

Blocked by: 06, 07, 10

## 任务

UI 与外围是最后一批,双向依赖(UI 既被 Process/指令调用,又主动调 Process、Config、Lang,甚至解析器,经 GlobalStatic 打通)。UI 直接复用解析簇(EmueraConsole/HtmlManager/ButtonStringCreator/DebugDialog 调用 LexicalAnalyzer);PluginSystem 直连 VariableData。Config(76 个文件引用)、Lang(57 个文件引用)作为普适叶子,契约已由 issues/06 先定。

范围(启用 + 清零 CS86xx):

- `UI/` 全部(EmueraConsole、EmueraConsole.Print、HtmlManager、ButtonStringCreator、MainWindow、ConsoleDisplayLine、ConsoleButtonString、StringMeasure、ConsoleStyledString、Image/* 等;StringStyle.cs 已启用)
- `Runtime/Utils/PluginSystem/`(PluginManager、PluginAPICharContext、VariableDataWrappers、PluginMethodParameter 等)
- `Runtime/Config/`:Config.cs、ConfigData.cs、ConfigItem.cs、ConfigCode.cs、JSON/JSONConfig.cs、JSON/JSONConfigData.cs
- `Runtime/Utils/` 剩余:EvilMask/(Lang、Shape、Utils、ConsoleDivPart、ConsoleEscapedParts)、LangManager.cs、Preload.cs、SFMT.cs、Sys.cs、Sound.NAudio.cs、Sound.WMP.cs、WebPWrapper.cs、CharStream.cs、CtrlZ.cs、EmueraException.cs、EncodingHandler.cs、RegexFactory.cs、WinInput.cs、WinmmTimer.cs、NAudio_LoopStream.cs
- `Runtime/Script/KeyMacro.cs`、`Runtime/Script/Statements/`(CircularBuffer.cs、Clipboard.cs,调试辅助,消费 MainWindow)

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

按 spec.md 通用方法逐文件启用。Instraction.Child(UI 打印类型的消费方)在 issues/09 先行启用,ConsoleDisplayLine/ConsoleButtonString 的契约以其调用点为准。

### 前置契约

- issues/06:Config/Lang 叶子契约。其中 `ConfigData` 的 8 个查找 API 已按该票决策标为 `AConfigItem?`,其消费点(`ConfigDialog.cs`、`DebugConfigDialog.cs`、`MainWindow.cs`)会带出"查找结果可能为 null"的警告,在本票内消化;若逐处 `!` 过重,可在本票内增补按 `ConfigCode` 的泛型非空访问器
- issues/07:UI 直接消费的 LexicalAnalyzer/WordCollection 输出契约
- issues/10:PluginSystem 直连的 VariableData 契约

执行顺序:最后执行——双向依赖 + 消费所有上游簇,先有上下游契约再清警告;`Sound.WMP.cs` 与 `Sound.NAudio.cs`/`NAudio_LoopStream.cs` 按配置互斥编译,`Debug-NAudio` 下不编译 `Sound.WMP.cs`,需用 VS 的 MSBuild 构建 `Debug` 后另行确认其警告数。

## 影响

本票完成后全部链簇票收敛,issues/03 的 csproj `<Nullable>enable</Nullable>` 恢复即可执行。
