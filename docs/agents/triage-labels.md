# Triage 标签

这些技能用五个规范化的分流（triage）角色来沟通。本文件将这些角色映射为本仓库 issue tracker 中实际使用的标签字符串，并定义 tracker 原生的状态标签。

## 技能角色映射

| mattpocock/skills 中的角色 | 本 tracker 中的标签 | 含义 |
| -------------------------- | ------------------- | ---- |
| `needs-triage` | `needs-triage` | 维护者需要评估该议题 |
| `needs-info` | `needs-info` | 等待报告者补充更多信息 |
| `ready-for-agent` | `ready-for-agent` | 已完整描述，可供 AFK agent 处理 |
| `ready-for-human` | `ready-for-human` | 需要人类处理：实现，或对已实现内容做验收 |
| `wontfix` | `wontfix` | 不处理 |

当技能提到某个角色（例如“应用 AFK-ready 分流标签”）时，使用本表中对应的标签字符串。

## 状态标签全集

`Status:` 行可用的全部标签如下；`in-progress`、`implemented`、`superseded`、`draft` 为 tracker 原生扩展，不来自技能角色：

| 标签 | 作用域 | 含义 |
| ---- | ------ | ---- |
| `needs-triage` | issue | 维护者需要评估 |
| `needs-info` | issue | 等待报告者补充信息 |
| `ready-for-agent` | issue | 已完整描述，可供 agent 处理 |
| `ready-for-human` | issue | 需要人类处理（实现，或验收已实现内容） |
| `in-progress` | issue、spec | 进行中（已开工/推进中） |
| `implemented` | issue、spec | 终态：实现且验收通过 |
| `wontfix` | issue、spec | 终态：不处理/放弃 |
| `superseded` | issue | 终态：被其他票承接，「影响」节注明承接方 |
| `draft` | spec | 提出/评估中，尚未拆票开工 |

## 状态流转

- issue：`needs-triage` →（`needs-info` 往返）→ `ready-for-agent` / `ready-for-human` → `in-progress` → `implemented`；任意非终态可转 `wontfix` / `superseded`；验收不通过则回 `in-progress` 并修订方案
- spec：`draft` → `in-progress` → `implemented` / `wontfix`
- `Status:` 行只写单个标签，不附日期与明细；变更过程依赖 git 历史，spec 不罗列各 issue 的状态

wayfinder 票不使用上述状态机，沿用其技能内建的 `claimed` / `resolved`（见 `issue-tracker.md`）。

如需匹配实际使用的词汇，编辑本文件的标签列即可。
