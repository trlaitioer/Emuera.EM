# 迁移方案:C# 可空注解(Nullable)

状态:进行中(方案 C)。

## 目标

- 项目所有源码文件启用可空注解并消除 CS86xx 警告
- 最终把 `Emuera/Emuera.csproj` 的 `<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`
- 迁移期间保持现有运行行为不变

## 现状

- 项目级已改为 `<Nullable>annotations</Nullable>`(注解全局生效、流分析警告逐文件开启)
- 已启用 `#nullable enable` 且无 CS86xx 警告:`Runtime/Script/Data/StrForm.cs`(本迁移样例,18 处 `null!` 中 11 处已替换;余 7 处为省略参数占位,贯通见 issues/05)、`Program.cs`、`UI/Game/StringStyle.cs`(后两者早于本迁移即已启用,见 git 历史)
- 剩余 97 个文件有 CS86xx 警告(临时 `-p:Nullable=enable` 全量构建时约 3,526 条)
- CS86xx 之外尚有既有警告,不属于本迁移目标:CS8981(小写 alias,处理决策见 `.scratch/performance-optimization/issues/06-cs8981-alias.md`)、CS0472(`WebPWrapper.cs` 内恒为 false 的 `nint` 判空)、CS4014(`MainWindow.cs` 未 await 的 `ReloadPartialErb` 调用)

计数口径:以上数字来自 `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable` 的输出原始行数(每条警告在输出中重复出现两次)。

## 迁移方法(通用)

1. 给目标文件顶部加 `#nullable enable`
2. 构建,清零该文件 CS86xx
3. 建议顺序:核心运行时(issues/01)→ 变量/表达式 + UI/Plugin(issues/02)→ 其余文件清尾(issues/04)→ csproj 收尾(issues/03);issues/05(省略参数可空贯通)独立执行,建议在 issues/01 后择机,不阻塞收尾
4. 全部清零后,把 csproj 的 `<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`

## 迁移期修复原则

- 优先用 `?` 注解与 `null!` 保持现有运行行为,少加运行时判断
- `Emuera.Tests` 已就绪(见 `.scratch/test-infrastructure/spec.md`);StrForm 的 `null!` 已替换 11/18(余 7 处省略参数占位,见 issues/05),后续文件迁移时同样以真检查或构造点保证替换 `!`

## 验收

- 全量构建(`-p:Nullable=enable`)CS86xx 警告为 0
- csproj 恢复 `<Nullable>enable</Nullable>` 后构建通过(CS8981 等非 CS86xx 既有警告不在消除目标内,见 `## 现状`)
- 迁移前后运行行为一致(用真实 era 游戏目录验证)
