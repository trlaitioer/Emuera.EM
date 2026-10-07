# Issue tracker：仓库内 Markdown

本仓库的议题与规格（issues and specs）以入库(git 跟踪)的 markdown 文件存放在 `tracker/` 下。

## 约定

- 每个功能一个目录：`tracker/<feature-slug>/`
- 规格文件为 `tracker/<feature-slug>/spec.md`
- 每个实现议题放置于单独文件：`tracker/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 开始编号
- 附属产物放置于`tracker/<feature-slug>`目录下，可建单独目录

### 议题（issue）模板

```markdown
# 标题

id: <feature-slug>-<NN>
Status: <状态>
Type: research | prototype | grilling | task | bug
Blocked by: <id1>, <id2>

## 任务

## 方案

## 曾考虑的替代方案

1. 方案A: 放弃原因

## 影响
```

- `id` 在落票时分配，此后**不变**：`<feature-slug>` 与所属目录一致，`<NN>` 与文件名编号一致
  - 正文里的票间引用一律写 `id`
- 标签全集与状态流转见 `triage-labels.md`
- `Type` 默认为 `task` ，可省略
- `Blocked by` 仅在被其他票阻塞时标注对应票 `id`
- `## 任务` 描述该票承担的范围与完成标准
- `## 曾考虑的替代方案` 无内容则省略
- **结论进正文，过程靠 git**
  - 方案修订、决策、实施结论与验收发现的问题直接写入正文对应节
  - 被否决的方案连同放弃原因写进 `## 曾考虑的替代方案`
  - 验收过程不记录在 issue 中，状态为 `implemented` 即视为验收通过
- 不再修改已经到达终态的 issue 的内容

### 规格（spec）模板

spec 不罗列各 issue 的状态；issue 与 spec 同目录分层，无需互相链接。

```markdown
# 标题

Date: <提出日期>
Status: <状态>

## 问题/背景

## 整体方案

## 曾考虑的替代方案

1. 方案A: 放弃原因

## 影响
```

- 标签全集与状态流转见 `triage-labels.md`
- `## 曾考虑的替代方案` 无内容则省略

## 延后项的归属

| 发现的东西 | 去向 |
| --- | --- |
| 与当前 feature 同源、当场能修 | 就地修，并在当前票正文记一句 |
| 与当前 feature 同源、需另立票 | 该 feature 的新票 |
| 与当前工作无关 / 需单独排期 / feature 收尾时遗留 | `tracker/backlog/` 的 `Status: needs-triage` 最小票 |

- 决定延后时立即落票；未入库的延后项不进入任何视图
- 落票前按现象关键词查重，命中则补进已有票正文，不新建
- backlog 票可以转出（目标 feature 立新票 + 本票标 `superseded` 并注明承接方 ）