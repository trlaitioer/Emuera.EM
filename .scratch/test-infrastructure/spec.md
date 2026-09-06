# 测试基础设施(Test Infrastructure)

Date: 2026-08-22
Status: implemented

## 问题/背景

项目缺少测试工程,属结构性短板:测试就位后,Nullable 迁移可将 `!` 替换为真检查(`StrForm.cs` 优先,联动清单见 `../nullable-migration/issues/01-core-runtime.md`),性能优化可用基准/回归验收。

## 整体方案

- 新增 `Emuera.Tests/` 目录(仓库根,xUnit);**不纳入根 `Emuera.sln`**
- 唯一 `Emuera/` 内改动:`Emuera.csproj` 添加 `<InternalsVisibleTo Include="Emuera.Tests" />`(核心类多为 internal)
- 分阶段:Phase 1 轻初始化组件 → Phase 2 需初始化序列(`StrForm` 等)→ Phase 3 脚本级冒烟;已交付覆盖含 StrForm/变量解析/LogicalLineParser/脚本级冒烟,运行时最小初始化沉淀于 `TestBootstrap`
- 构建配置(踩坑后定案):运行命令必须带 `-c Debug-NAudio`——`Emuera.csproj` 的 NAudio 包引用按配置条件声明,默认配置引用 WMPLib COM(`dotnet build`/`dotnet test` 无法解析);ProjectReference 用普通引用,统一 `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio`(定案理由见 issues/01)
- 完成标准:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio` 全部通过;主工程 `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` 0 错误

## 影响

- 新增 `Emuera.Tests/` 测试工程;`Emuera.csproj` 增加 `<InternalsVisibleTo Include="Emuera.Tests" />`
- Nullable 迁移收尾与性能优化验收以本测试套件为安全网
