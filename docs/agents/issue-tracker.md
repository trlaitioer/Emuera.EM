# Issue tracker：本地 Markdown

本仓库的议题与规格（issues and specs）以 markdown 文件形式存放在 `.scratch/` 下。

## 约定

- 每个功能一个目录：`.scratch/<feature-slug>/`
- 规格文件为 `.scratch/<feature-slug>/spec.md`
- 实现议题每个一张文件：`.scratch/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 开始编号——绝不要合并成单一的 tickets 文件
- 分流（triage）状态记录在每个议题文件顶部附近的 `Status:` 行（角色字符串见 `triage-labels.md`）
- 评论与对话历史追加到文件底部的 `## Comments` 标题下

## 当技能要求“发布到 issue tracker”

在 `.scratch/<feature-slug>/` 下新建文件（必要时先创建目录）。

## 当技能要求“获取相关 ticket”

直接读取所引用路径的文件。用户通常会直接传入路径或议题编号。

## 导航（Wayfinding）操作

由 `/wayfinder` 使用。**map** 是一个文件，每个 **child** ticket 对应一个文件。

- **Map**：`.scratch/<effort>/map.md` — Notes / Decisions-so-far / Fog 主体
- **Child ticket**：`.scratch/<effort>/issues/NN-<slug>.md`，从 `01` 开始编号，问题写在正文中。`Type:` 行记录 ticket 类型（`research`/`prototype`/`grilling`/`task`）；`Status:` 行记录 `claimed`/`resolved`
- **阻塞**：文件顶部附近的 `Blocked by: NN, NN` 行。当它列出的每个文件都标记为 `resolved` 时，该 ticket 解除阻塞
- **Frontier**：扫描 `.scratch/<effort>/issues/`，找出打开、未阻塞且未被认领的文件；按编号从小到大
- **认领**：开始任何工作前先设置 `Status: claimed` 并保存
- **解决**：在 `## Answer` 标题下追加答案，设置 `Status: resolved`，然后在 `map.md` 的 Decisions-so-far 中追加一条上下文指针（要点 + 链接）
