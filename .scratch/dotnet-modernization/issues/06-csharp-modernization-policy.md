# 决策:C# 语言特性现代化策略

Status: needs-triage

## 任务

评估 C# 新语法采纳策略。项目 `LangVersion` 未显式设置,net10.0-windows 默认即 C# 14,全部源码已在 C# 14 下编译——不存在"升版本"动作,本票只评估新语法采纳(2026-08-30 全库 grep 扫描,136 个 .cs)。

- EM 迁移过程中已顺带完成部分现代化:含 namespace 的 134 个文件中 112 个已是 file-scoped;collection expressions(`[.. _args, "-Debug"]` 形态)与 target-typed `new()` 已零星使用;匿名方法(`delegate { }` 表达式)已无残留,余下 `delegate` 均为类型声明。
- NRT 属 OPT-IN,由 `.scratch/nullable-migration/` 独立承接,不在本票范围。

完成标准:策略决策记录于本文,提交 triage 定案。

## 方案

### 扫描结果(触发点计数)

| 候选变换 | 触点 | 判断 |
|---|---|---|
| `is null` / `is not null` 模式 | `== null` 752 处 + `!= null` 565 处 | 纯风格 churn,不立项 |
| target-typed `new()` | 显式 `= new Foo(` 2318 处 | 与仓库显式类型偏好(editorconfig var 系 = false)冲突,不立项 |
| string 插值替代 `string.Format` | 1032 处 | churn 过大,不立项 |
| switch 表达式 | `switch (` 166 处 | 需逐点语义判断,按需 |
| collection expressions | `new List<T>()` / `new T[...]` 186 处 | 已部分采用,按需 |
| using 声明 | `using (` 29 处 | 量小价值低,按需 |
| raw string literals | verbatim 字符串内转义引号 ≈0 处 | 无适用场景,不立项 |
| records / primary constructor / required members / global usings | — | OPT-IN,改 API 面或架构,不适用 |

Phase 0(C# 编译器破坏性变更)为空:无 LangVersion 变更,现有代码已编译通过。editorconfig 现有风格规则多为 `silent`/`suggestion`,无分析器强制现代化路径。

### 决策建议

- 整体不立项全量语法现代化(wontfix);采纳 on-touch 策略:改动某文件时顺手使用现代写法,不做专门扫描。
- `LangVersion` 维持不显式设置,随 TFM 默认(当前 C# 14)。

## 影响

待拍板的不一致:`Emuera/.editorconfig` 声明 `csharp_style_namespace_declarations = block_scoped`,与现状(80% file-scoped)矛盾。若要统一需另立票,余下 22 个块作用域文件集中在 `UI/Framework/Forms`(含 Designer)、`Runtime/Utils/PluginSystem` 与 NAudio 后端;不统一则现状维持。
