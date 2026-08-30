# 测试基础设施(Test Infrastructure)

状态:已完成(2026-08-29,77/77 全绿;Phase 2+ 用例含 StrForm/变量解析/LogicalLineParser/脚本级冒烟,运行时最小初始化沉淀于 `TestBootstrap`)。来源:开发方向推荐——补齐"无测试项目"的结构性短板,解锁 Nullable 迁移收尾(把 `null!` 替换为真检查)与性能优化验收(基准/回归)。

## 目标

- 建立可运行的单元测试工程,覆盖解释器核心纯组件
- 测试就位后:Nullable 迁移可将 `!` 替换为真检查(`StrForm.cs` 优先,联动清单见 `../nullable-migration/issues/01-core-runtime.md`);性能优化可用基准/回归验收

## 已确认决策

- 新增 `Emuera.Tests/` 目录(仓库根,xUnit);**不纳入根 `Emuera.sln`**
- 唯一 `Emuera/` 内改动:`Emuera.csproj` 添加 `<InternalsVisibleTo Include="Emuera.Tests" />`(核心类多为 internal)
- 分阶段:Phase 1 轻初始化组件 → Phase 2 需初始化序列(`StrForm` 等)→ Phase 3 脚本级冒烟

## 关键结论(构建配置,踩坑后定案)

- **运行命令必须带 `-c Debug-NAudio`**:`Emuera.csproj` 的 NAudio 包引用按配置条件声明;默认配置引用 WMPLib COM(`dotnet build`/`dotnet test` 无法解析)
- ProjectReference 的 `AdditionalProperties` **不作用于 restore** → assets 无 NAudio 包,构建报 CS0234
- ProjectReference 的 `SetConfiguration` 与 `dotnet test` **冲突** → MSB3100
- 定案:ProjectReference 用普通引用,统一 `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio`

## 验收

- `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio` 全部通过
- 主工程 `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` 0 错误(已确认)
