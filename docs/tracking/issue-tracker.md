# Issue tracker：仓库内 Markdown

本仓库的议题与规格（issues and specs）以入库(git 跟踪)的 markdown 文件存放在 `.scratch/` 下。

## 约定

- 每个功能一个目录：`.scratch/<feature-slug>/`
- 规格文件为 `.scratch/<feature-slug>/spec.md`
- 实现议题每个一张文件：`.scratch/<feature-slug>/issues/<NN>-<slug>.md`，从 `01` 开始编号——绝不要合并成单一的 tickets 文件
- 附属产物（清单、索引、实测数据等篇幅大的支撑材料）：`.scratch/<feature-slug>/<name>.md`，与 `spec.md` 同级、不编号；它不是票据，故不写 `Status:`/`Date:`、不套下方模板，首段说明由哪张票产出、用途为何，并由产出它的票链接过去（例：`.scratch/nullable-migration/bang-sites.md` 由 issues/21 产出）
- 状态记录在文件头部的 `Status:` 行，只写单个标签；标签全集与状态流转见 `triage-labels.md`
- spec 文件头部另有一行 `Date: <提出日期>`
- 正文使用下方模板；`## 曾考虑的替代方案` 无内容则省略
- 头部可选行：`Type: research|prototype|grilling|task|bug`（`task` 为默认、可省略；`bug` 用于缺陷，供「未处理 bug」这类查询筛选）、`Blocked by: NN, NN`（被其他票阻塞时标注）
- 内容规则：**结论进正文，过程靠 git**。方案修订、决策、实施结论与验收发现的问题写入正文对应节（就地改写或补小节，写结论不写经过）；被否决的方案连同放弃原因写进 `## 曾考虑的替代方案`；验收通过只把 `Status:` 改为 `implemented`，不另行记录验收过程。不按提交逐个追加变更摘要，修改历史由 git 提供

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

## 未处理票据视图

未处理 = 头部 `Status:` 不属于终态（`implemented` / `wontfix` / `superseded`）。视图由查询得到，不维护清单：

```powershell
Get-ChildItem .scratch -Recurse -File -Filter *.md | Where-Object { $_.FullName -match "\\issues\\" } | ForEach-Object {
  $s = (Select-String -Path $_.FullName -Pattern "^Status:\s*(\S+)" | Select-Object -First 1).Matches.Groups[1].Value
  if ($s -notin @("implemented","wontfix","superseded")) { "{0,-46} {1}" -f $_.Name, $s }
}
```

口径：只扫 `*/issues/`；`*/closed/` 是已结票的归档区，只在检索历史时纳入。

## 延后项的归属

| 发现的东西 | 去向 |
| --- | --- |
| 与当前 effort 同源、当场能修 | 就地修，并在当前票正文记一句 |
| 与当前 effort 同源、需另立票 | 该 effort 的 slug 新票 |
| 与当前工作无关 / 需单独排期 / effort 收尾时遗留 | `.scratch/backlog/` 的 `Status: needs-triage` 最小票 |

- 决定延后时立即落票；未入库的延后项不进入任何视图
- 落票前按现象关键词查重，命中则补进已有票正文，不新建
- backlog 票的出口只有两个：就地开做，或转出（目标 effort 立新票 + 本票标 `superseded` 并注明承接方）——文件不移动
- slug 内可设 `closed/`（与 `issues/` 同级）存放已结票；未处理视图只扫 `issues/`

## 导航（Wayfinding）操作

由 `/wayfinder` 使用。**map** 是一个文件，每个 **child** ticket 对应一个文件。wayfinder 票不套用上述模板与状态机，沿用其技能内建词汇：

- **Map**：`.scratch/<effort>/map.md` — Notes / Decisions-so-far / Fog 主体
- **Child ticket**：`.scratch/<effort>/issues/NN-<slug>.md`，从 `01` 开始编号，问题写在正文中。`Type:` 行记录 ticket 类型（`research`/`prototype`/`grilling`/`task`）；`Status:` 行记录 `claimed`/`resolved`
- **阻塞**：文件顶部附近的 `Blocked by: NN, NN` 行。当它列出的每个文件都标记为 `resolved` 时，该 ticket 解除阻塞
- **Frontier**：扫描 `.scratch/<effort>/issues/`，找出打开、未阻塞且未被认领的文件；按编号从小到大
- **认领**：开始任何工作前先设置 `Status: claimed` 并保存
- **解决**：在 `## Answer` 标题下追加答案，设置 `Status: resolved`，然后在 `map.md` 的 Decisions-so-far 中追加一条上下文指针（要点 + 链接）
