# 测试工程搭建

Status: needs-triage
Type: task

## 现状(已完成)

- `Emuera.Tests/Emuera.Tests.csproj`(xUnit,`net10.0-windows`,普通 ProjectReference)
- `Emuera/Emuera.csproj` 添加 `<InternalsVisibleTo Include="Emuera.Tests" />`;主工程 Debug-NAudio 构建 0 错误(95 个既有 CA 警告不阻塞)
- `Emuera.Tests/README.md` 记录运行命令与踩坑结论

## 踩坑记录(重要)

1. `AdditionalProperties="Configuration=Debug-NAudio"` 只作用于 Build,不作用于 Restore → restore 出的 `project.assets.json` 无 NAudio 包,构建报 CS0234(`NAudio.Wave`/`CoreAudioApi` 等)
2. `SetConfiguration="Debug-NAudio"` 与 `dotnet test` 冲突 → MSB3100(Properties 参数语法无效)
3. 定案:ProjectReference 不带元数据,调用方统一 `-c Debug-NAudio`(全局属性同时作用于 restore 与 build,与仓库构建约定一致)

## Comments
