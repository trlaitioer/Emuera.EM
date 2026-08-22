# 迁移方案:C# 可空注解(Nullable)

状态:进行中(方案 C)。

## 目标

- 项目所有源码文件启用可空注解并消除 CS86xx 警告
- 最终把 `Emuera/Emuera.csproj` 的 `<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`
- 迁移期间保持现有运行行为不变

## 现状

- 项目级已改为 `<Nullable>annotations</Nullable>`(注解全局生效、流分析警告逐文件开启)
- 已迁移:`Runtime/Script/Data/StrForm.cs`
- 剩余约 96 个文件有 CS86xx 警告(临时 `-p:Nullable=enable` 全量构建时约 3,606 条)

## 迁移方法(通用)

1. 给目标文件顶部加 `#nullable enable`
2. 构建,清零该文件 CS86xx
3. 建议顺序:核心运行时(见 issues/01)→ 变量/表达式 + UI/Plugin(见 issues/02)→ csproj 收尾(见 issues/03)
4. 全部清零后,把 csproj 的 `<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`

## 迁移期修复原则

- 优先用 `?` 注解与 `null!` 保持现有运行行为,少加运行时判断
- 等有测试后再把 `!` 逐步替换为真检查

## 验收

- 全量构建(`-p:Nullable=enable`)无新增 CS86xx 警告
- csproj 恢复 `<Nullable>enable</Nullable>` 后构建通过
- 迁移前后运行行为一致(用真实 era 游戏目录验证)
