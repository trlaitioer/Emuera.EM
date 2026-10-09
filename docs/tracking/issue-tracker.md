# Issue tracker：仓库内 Markdown

本仓库的议题与规格（issues and specs）以 markdown 文件存放在 `tracker/` 下。

## 约定

- 每个功能一个目录：`tracker/<feature-slug>/`
- 规格文件为 `tracker/<feature-slug>/spec.md`
- 每个实现议题放置于单独文件：`tracker/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 开始递增编号
- 附属产物放置于`tracker/<feature-slug>`目录下，可建单独目录
- `status` 的标签全集与状态流转见 `triage-labels.md`
- 开头的 yaml 元数据格式由 pre-commit 检验

### 议题（issue）模板

```markdown
---
id: <feature-slug>-<NN>
status: <status>
type: task | bug
blocked_by: [<id1>, <id2>]
superseded_by: [<id3>]
---

# 标题

## 任务

## 方案

## 曾考虑的替代方案

1. 方案A: 放弃原因

## 影响
```

- `id` 必填，确认后不再变化：`<feature-slug>` 与所属目录一致，`<NN>` 与文件名编号一致
  - 正文里的票间引用一律写 `id`
- `type` 可省略，默认为 `task`
- 当且仅在被阻塞，需要等待其他 issue 完成时，`blocked_by` 标注对应 issue 的 `id`
- 当且仅当 `status` 为 `superseded` 时, `superseded_by` 标注对应 issue 的 `id`
- `## 任务` 描述该票承担的范围与完成标准
- `## 曾考虑的替代方案` 无内容则省略
- **结论进正文，过程靠 git**
  - 方案修订、决策、实施结论与验收发现的问题直接写入正文对应节
  - 被否决的方案连同放弃原因写进 `## 曾考虑的替代方案`
  - 验收过程不记录在 issue 中，状态为 `implemented` 即视为验收通过
- 不再修改已经为终态的 issue 的正文

### 规格（spec）模板

spec 不罗列各 issue 的状态；issue 与 spec 同目录分层，无需互相链接

```markdown
---
date: YYYY-MM-DD
status: <status>
---

# 标题

## 问题/背景

## 整体方案

## 曾考虑的替代方案

1. 方案A: 放弃原因

## 影响
```

- `## 曾考虑的替代方案` 无内容则省略

## 延后项的归属

| 发现的东西 | 去向 |
| --- | --- |
| 与当前 feature 同源、当场能修 | 就地修，并在当前票正文记一句 |
| 与当前 feature 同源、需另立票 | 该 feature 的新票 |
| 与当前工作无关 / 需单独排期 / feature 收尾时遗留 | 合并进对应 feature 或 `tracker/backlog/` 建 `status: draft` 最小票 |

- backlog 票可以转出（目标 feature 立新票 + 本票标 `superseded` 并标注 `superseded_by`）
