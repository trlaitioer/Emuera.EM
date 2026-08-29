# Nullable:UI 与外围启用

Status: needs-triage
Type: task

## 背景

UI 与外围是最后一批,双向依赖(UI 既被 Process/指令调用,又主动调 Process、Config、Lang,甚至解析器,经 GlobalStatic 打通)。UI 直接复用解析簇(EmueraConsole/HtmlManager/ButtonStringCreator/DebugDialog 调用 LexicalAnalyzer);PluginSystem 直连 VariableData。Config(76 个文件引用)、Lang(57 个文件引用)作为普适叶子,契约已由 issues/06 先定。

## 范围(启用 + 清零 CS86xx)

- `UI/` 全部(EmueraConsole、EmueraConsole.Print、HtmlManager、ButtonStringCreator、MainWindow、ConsoleDisplayLine、ConsoleButtonString、StringMeasure、ConsoleStyledString、Image/* 等;StringStyle.cs 已启用)
- `Runtime/Utils/PluginSystem/`(PluginManager、PluginAPICharContext、VariableDataWrappers、PluginMethodParameter 等)
- `Runtime/Config/`:Config.cs、ConfigData.cs、ConfigItem.cs、ConfigCode.cs、JSON/JSONConfig
- `Runtime/Utils/` 剩余:EvilMask/(Lang、Shape、Utils、ConsoleDivPart、ConsoleEscapedParts)、LangManager.cs、Preload.cs、SFMT.cs、Sys.cs、Sound.NAudio.cs、Sound.WMP.cs、WebPWrapper.cs
- `Runtime/Script/KeyMacro.cs`、`Runtime/Script/Statements/`(CircularBuffer.cs、Clipboard.cs,调试辅助,消费 MainWindow)

## 前置契约

- issues/06:Config/Lang 叶子契约
- issues/07:UI 直接消费的 LexicalAnalyzer/WordCollection 输出契约
- issues/10:PluginSystem 直连的 VariableData 契约

## 方案

按 spec.md 通用方法逐文件启用。Instraction.Child(UI 打印类型的消费方)已先行启用,ConsoleDisplayLine/ConsoleButtonString 的契约以其调用点为准。

## 备注

- 最后执行:双向依赖 + 消费所有上游簇,先有上下游契约再清警告
- `Sound.WMP.cs` 按配置互斥编译,需用 VS 的 MSBuild 构建对应配置后另行确认(同 issues/04 的口径)
- `WebPWrapper.cs` 的 CS0472 为既有警告,不属于本迁移目标(见 issues/03 非目标)

## Comments

### 2026-08-29

- 新建:按调用链重切差分,原 issues/02 的 UI/ 与 PluginSystem、issues/04 的 Config/ 与 Utils/ 剩余归入本票。
