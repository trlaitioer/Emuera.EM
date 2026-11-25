# 文档索引

仓库的长期参考文档，以及文档分层与单一真相的归属表。

## 文档分层

同一事实只写一处；规则归归属文档，其他文档只留指针。

| 文档 | 负责 | 不写 |
| --- | --- | --- |
| [README.md](../README.md) | 项目定位、平台、仓库布局、构建/测试入口 | 文档分层表、issue 规则、生成物策略 |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | 代码风格、编码、commit 规范、文档/注释引用与行号规则、提交前检查 | 文档分层表、issue 流程、生成物策略 |
| [AGENTS.md](../AGENTS.md) | agent 操作入口：分支/commit 触发、构建测试要求、先方案后改、文件放置/文档入口 | 模板、状态表、生成物策略、流程细节、本机信息 |
| [issue-tracker.md](issue-tracker.md) | GitHub Issues 规则，议题、PR、Spec 与 ADR 的分工 | 代码风格、全局文档分层 |
| GitHub Issues | 工作项状态、计划、验收证据 | 现行规格、决策理由 |
| `docs/specs/<module>.md` | 现行行为、契约、不变量 | 计划/进度、决策理由 |
| `docs/adr/NNNN-<topic>.md` | 决策、否决理由、后果 | 现行行为细节 |
| `docs/` 下的其他参考文档 | 长期参考：架构、契约、用法 | 过程历史、状态注解 |

- 规则只在归属文档写一次；其他文档只写一句“见 X”，不复制条件、清单、示例。
- 全局文档不复制 issue 的过程状态；指向工作时用 `#N`，指向决策时用 `ADR-NNNN`。
- 需要跨文档复用的规则拆成“策略 + 应用”：策略归归属文档，使用方只写自己的差异。

## 文档索引

| 文档 | 内容 |
| ---- | ---- |
| [architecture.md](architecture.md) | 架构大图景：仓库与历史、启动链、解释器核心、UI 层、配置与多语言、插件系统、杂项 |
| [csv-erb-syntax.md](csv-erb-syntax.md) | eraBasic / `.erb` / `csv` 语法速查 |
| [builtin-functions.md](builtin-functions.md) | 内建函数契约表，由 [builtin-contract.py](../scripts/arg-contract/builtin-contract.py) 生成 |
| [issue-tracker.md](issue-tracker.md) | GitHub Issues 规则 |
| [legacy-tracker.md](legacy-tracker.md) | 旧 tracker 归档及议题查找方法 |
| [../Emuera.Tests/README.md](../Emuera.Tests/README.md) | 测试工程：配置差异、运行时初始化(TestBootstrap) |

## 现行规格与决策

- [游戏路径与文件名](specs/paths.md)
- [内建函数实参槽](specs/argument-slots.md)
- [C# 可空引用契约](specs/nullable.md)
- [读档暂存与扩展数据](specs/save-load.md)
- [游戏画布](specs/console-rendering.md)
- [ADR-0001 按需采用 C# 新特性和 .NET API](adr/0001-dotnet-modernization.md)

## 生成文档与审计报告

在仓库根运行以下命令。内建函数契约或说明变化后，重新生成并检查 `docs/builtin-functions.md` 的差异。两个实参审计报告保存在 `.temp/arg-contract/`，非空断言计数输出到终端。

```powershell
python3.14 scripts/arg-contract/builtin-contract.py
python3.14 scripts/arg-contract/arg-slots.py
python3.14 scripts/arg-contract/instruction-args.py
pwsh -File scripts/count-bang-sites.ps1
```
