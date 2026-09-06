# Issue tracker：本地 Markdown

本仓库的议题与规格（issues and specs）以 markdown 文件形式存放在 `.scratch/` 下。

## 约定

- 每个功能一个目录：`.scratch/<feature-slug>/`
- 规格文件为 `.scratch/<feature-slug>/spec.md`
- 实现议题每个一张文件：`.scratch/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 开始编号——绝不要合并成单一的 tickets 文件
- 状态记录在文件头部的 `Status:` 行，只写单个标签；标签全集与状态流转见 `triage-labels.md`
- spec 文件头部另有一行 `Date: <提出日期>`
- 正文使用下方模板；`## 曾考虑的替代方案` 无内容则省略
- 头部可选行：`Type: research|prototype|grilling|task`（task 票默认，可省略）、`Blocked by: NN, NN`（被其他票阻塞时标注）
- 内容规则：**结论进正文，过程靠 git**。方案修订、决策、验收发现的问题写入正文对应节；被否决的方案连同放弃原因写进 `## 曾考虑的替代方案`；验收通过不记录结论，只把 `Status:` 改为 `implemented`。不在文件中追加变更摘要，修改历史由 git 提供

### 议题（issue）模板

```markdown
# 标题

Status: <状态>

## 任务

## 方案

## 曾考虑的替代方案

1. 方案A: 放弃原因

## 影响
```

「任务」节描述该票承担的范围与完成标准。

### 规格（spec）模板

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

spec 不罗列各 issue 的状态；issue 与 spec 同目录分层，无需互相链接。

## 当技能要求“发布到 issue tracker”

在 `.scratch/<feature-slug>/` 下新建文件（必要时先创建目录）。

## 当技能要求“获取相关 ticket”

直接读取所引用路径的文件。用户通常会直接传入路径或议题编号。

## 导航（Wayfinding）操作

由 `/wayfinder` 使用。**map** 是一个文件，每个 **child** ticket 对应一个文件。wayfinder 票不套用上述模板与状态机，沿用其技能内建词汇：

- **Map**：`.scratch/<effort>/map.md` — Notes / Decisions-so-far / Fog 主体
- **Child ticket**：`.scratch/<effort>/issues/NN-<slug>.md`，从 `01` 开始编号，问题写在正文中。`Type:` 行记录 ticket 类型（`research`/`prototype`/`grilling`/`task`）；`Status:` 行记录 `claimed`/`resolved`
- **阻塞**：文件顶部附近的 `Blocked by: NN, NN` 行。当它列出的每个文件都标记为 `resolved` 时，该 ticket 解除阻塞
- **Frontier**：扫描 `.scratch/<effort>/issues/`，找出打开、未阻塞且未被认领的文件；按编号从小到大
- **认领**：开始任何工作前先设置 `Status: claimed` 并保存
- **解决**：在 `## Answer` 标题下追加答案，设置 `Status: resolved`，然后在 `map.md` 的 Decisions-so-far 中追加一条上下文指针（要点 + 链接）
