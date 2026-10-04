# 折行 / 按钮切分 / 定位不是 module，测试只好手抄生产逻辑

id: backlog-09
Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（表现层）。

折行、按钮切分与定位没有 interface：策略从 `Config` 静态量读、测量藏在 WinForms 控件后面。

- 折行：`UI/Game/PrintStringBuffer.cs:223-356`（读 `Config.DrawableWidth`(`:248`)、`ButtonWrap`/`CompatiLinefeedAs1739`(`:277`)、`RikaiEnabled`(`:295, 338`)，并原地 `Insert`(`:302`)/`RemoveAt`(`:260`)）、`:524-606`（`setWidthToButtonList`，`private static`）、`:611-660` 与 `:665-727`（两个 `getDivideIndex`，后者对像素宽度做二分）
- 切分：`UI/Game/ConsoleButtonString.cs:194-268`（`DivideAt`；`:253` `new(null,…)`、`:258` 再拷 parent）、`:303-327`、`:333-367`、`:378-447`
- 重排：`UI/Game/ConsoleDisplayLine.cs:53-72, 100-175, 189-270`
- 第二调用方：`UI/Game/HtmlManager.cs:859-868`

所有权错位：位图缓存的环形缓冲在 `EmueraConsole.cs:84-87`，却由 `ConsoleButtonString.cs:414-429` 经 `GlobalStatic.Console` 维护，而该对象已持有 parent（`:133`）。

测试已自认手抄生产逻辑：`ConsoleDisplayLineTests.cs:232-244` 注释「Mirrors `PrintStringBuffer.setWidthToButtonList`」、`:216-222`「Joins lines the same way `EmueraConsole.addDisplayLine` does」、`StBarTests.cs:137-153` 把旧实现抄成 oracle。

完成标准：

- 确立「节点序列 + 测量能力 + 折行策略 → 若干行及每行位置」的 module；测量与策略作参数传入，不再从 `Config` 静态量读
- `ConsoleButtonString` 对 console 的引用收缩到绘制期的 focus/hit；位图缓存归还 console，或独立成以「行号 + 版本」为键的 module
- 用例：假测量（每全宽字符 1 像素）覆盖折行边界、切分点、div 子分区、Rikai 链接、不可切分整行的下移、`PointXisLocked` 定位
- 删掉上述三处手抄 oracle

## 方案

待评估。**应模仿的 interface 形状**：`EmueraConsole.Print.cs:1022-1052` 的 `BuildStBar`——静态、测量装置作参数接受、无静态量，`StBarTests.cs:62-77` 逐字节对照。

## 影响

- **测试面**：`PrintStringBuffer` 全类 0 覆盖，`getDivideIndex` 的二分 0 覆盖；`ConsoleDisplayLineTests.cs:229, 239` 传 `null!` 参数
- **与既有票的关系**：`.scratch/ui-modernization/issues/03-monospace-grid.md` 与 `04-graphics-text-mode.md` 已处理字体单元格宽/em 关系与 GDI+ 文字绘制档的测量缺陷。本票不动测量实现本身（`StringMeasure`/`GraphicsTextRenderer`/`FontMetrics` 三方经审查判为健康），只处理折行/切分的 module 边界与策略来源；落地时须与该两票的测量口径对齐
- 改动量估计：中到大
- 相关票：issues/07（Config 静态量）、issues/05（console seam）
