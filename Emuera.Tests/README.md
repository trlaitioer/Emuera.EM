# Emuera.Tests

Emuera 解释器核心的单元测试工程(xUnit)。对应 `.scratch/` 中"测试基础设施"方向。

## 运行

```bash
dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio
```

**必须带 `-c Debug-NAudio`**(与仓库构建约定一致)。原因:`Emuera/Emuera.csproj` 的 NAudio 包引用是按配置条件声明的——只有 NAudio 配置才引入 `NAudio.*` 包并排除 `Sound.WMP.cs`;默认配置引用 WMPLib COM,`dotnet build`/`dotnet test` 无法解析。`-c Debug-NAudio` 使还原与构建全局一致,避免"还原出无 NAudio 的 assets、构建却要求 NAudio"的错配。

不要用 ProjectReference 的 `SetConfiguration`/`AdditionalProperties` 元数据代替:`SetConfiguration` 与 `dotnet test` 存在冲突(MSB3100),`AdditionalProperties` 不作用于还原阶段。

## 组织方式

测试文件按被测源码路径镜像组织(路径相对 `Emuera.Tests/`,与 `Emuera/` 下源码对应),每个文件头部用 `<summary>` 说明被测对象;本文件不维护文件清单。

## 运行时初始化(TestBootstrap)

Phase 2 起的用例(变量求值/StrForm/LogicalLineParser)依赖 `TestBootstrap.Initialize()`——
等价 `Process.Initialize` 的解析器子集,无 UI/ERB/ERH 依赖。关键点:

- `ParserMediator.Initialize(null)` + `console` 传 null:`LogicalLineParser` 在非 AnalysisMode 下不参与解析,无需 STA/WinForms
- `Config.SetReplace(ConfigData.Instance)`:填充 `PalamLvDef` 等 replace 系静态默认值(`VariableData.SetDefaultValue` 依赖)
- `JSONConfig.Data = new JSONConfigData()`:`FunctionIdentifier` 静态构造读取;`Load()` 依赖 `Program.GameRoot`(由 `SetDirPaths` 设定,本夹具不设游戏目录)
- `LangManager.setEncode(932)`:`GetStrlenLang` 非 ASCII 路径不设必 NRE
- `GlobalStatic.Process.scaningLine` 置占位行:`GetScaningLine` 在其为 null 时解引用仅 `Initialize` 创建的 `state`
