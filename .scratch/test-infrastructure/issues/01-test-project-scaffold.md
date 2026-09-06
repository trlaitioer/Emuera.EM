# 测试工程搭建

Status: implemented

## 任务

搭建可运行的单元测试工程。完成标准:

- `Emuera.Tests/Emuera.Tests.csproj`(xUnit,`net10.0-windows`,普通 ProjectReference)就位
- `Emuera/Emuera.csproj` 添加 `<InternalsVisibleTo Include="Emuera.Tests" />`,主工程 Debug-NAudio 构建 0 错误(95 个既有 CA 警告不阻塞)
- `Emuera.Tests/README.md` 记录运行命令与踩坑结论

## 方案

- ProjectReference 用普通引用、不带元数据,调用方统一 `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio`(全局属性同时作用于 restore 与 build,与仓库构建约定一致)
- 运行命令必须带 `-c Debug-NAudio`:`Emuera.csproj` 的 NAudio 包引用按配置条件声明,默认配置引用 WMPLib COM(`dotnet build`/`dotnet test` 无法解析)

## 曾考虑的替代方案

1. ProjectReference 加 `AdditionalProperties="Configuration=Debug-NAudio"`:只作用于 Build 不作用于 Restore,restore 出的 `project.assets.json` 无 NAudio 包,构建报 CS0234(`NAudio.Wave`/`CoreAudioApi` 等),放弃
2. ProjectReference 加 `SetConfiguration="Debug-NAudio"`:与 `dotnet test` 冲突报 MSB3100(`Properties` 参数语法无效),放弃

## 影响

测试套件就位后,Nullable 迁移可将 `null!` 替换为真检查,性能优化可做回归验证;踩坑结论沉淀于 `Emuera.Tests/README.md` 与本文件。
