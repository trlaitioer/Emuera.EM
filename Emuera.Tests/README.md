# Emuera.Tests

Emuera 解释器核心的单元测试工程(xUnit v3 + Microsoft.Testing.Platform)。对应 `tracker/` 中"测试基础设施"方向。

## 运行

```bash
# 主命令:直接执行测试应用(MTP),构建 + 运行
dotnet run --project Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64
```

测试工程是可执行文件,已构建后也可直接跑产物(最快,不经 dotnet 命令):

```bash
Emuera.Tests/bin/x64/Debug-NAudio/net10.0-windows/Emuera.Tests.exe
```

需要 `dotnet test` 的场合(CI、脚本),等价命令是(依赖仓库根 `global.json`,见下):

```bash
dotnet test --project Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64
```

**必须带 `-c Debug-NAudio`**(与仓库构建约定一致)。原因:`Emuera/Emuera.csproj` 的 NAudio 包引用是按配置条件声明的——只有 NAudio 配置才引入 `NAudio.*` 包并排除 `Sound.WMP.cs`;默认配置引用 WMPLib COM,`dotnet build`/`dotnet test` 无法解析。`-c Debug-NAudio` 使还原与构建全局一致,避免"还原出无 NAudio 的 assets、构建却要求 NAudio"的错配。

不要用 ProjectReference 的 `SetConfiguration`/`AdditionalProperties` 元数据代替:`SetConfiguration` 与 `dotnet test` 存在冲突(MSB3100),`AdditionalProperties` 不作用于还原阶段。

MTP 相关注意:

- `dotnet run`/直接跑 exe 不需要额外配置;`dotnet test` 依赖仓库根 `global.json` 的 `test.runner`(.NET 10 原生 MTP 模式),缺了它 .NET 10 会拒绝 VSTest 目标而直接报错
- 不要给测试应用传未识别参数:在 `dotnet test` 下会被转发给测试应用,`--nologo` 实测退出码 5
- 与 VSTest 的行为差异:跑了 0 个用例时 MTP 返回退出码 8(VSTest 视为成功),过滤条件改用 `--filter-class`/`--filter-method` 等

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

## 共享全局状态(写过的系统变量必须还原)

`TestBootstrap.Initialize()` 只在**首次调用**时生效(进程内单例),`GlobalStatic.VEvaluator`/`VariableData`
随之在整个程序集的全部用例之间存活;`AssemblyInfo.cs` 的禁用并行(`[assembly: Parallelization(Mode = ParallelMode.None)]`,
xUnit v3 4.0 起取代 `CollectionBehavior(DisableTestParallelization)`)只保证串行执行,既不保证发现顺序,
也不在用例之间重置任何变量。两条硬性约定:

- **写过的系统变量必须还原**:`RESULT`、`RESULTS`、`TFLAG`、`SAVESTR`、`VariableData.DataDataTables` 等
  都指向同一份单例状态。常规做法是构造函数快照 + `IDisposable.Dispose` 还原,
  详见 `DtColumnOptionsInstructionTests`、`UnguardedAsCastTests`、`CallformCacheTests`。
- **只读共享变量同样危险**:用例直接读别的用例可能写过的变量,会让它"有时过、有时挂",
  失败信息指向本用例而根因在别处。需要读系统变量的用例应先自己写入确定值,或改用本类自控的变量。

实测教训:`CallformCacheTests.MultiInterpolationForm_UsesStrKey` 曾用 `{RESULT}` 拼调用名,
`UnguardedAsCastTests` 的 `RESULT = MAXARRAY(...)` 退出时留下 9,两者执行顺序一变本用例就报
`Expected "A_0_0" / Actual "A_0_9"`(全量跑约 1/4 概率复现)。排查顺序依赖时先怀疑跨用例残留的全局量。
