# GDI+ 文字绘制档:现状与缺陷修复

Status: ready-for-agent

## 任务

`TextDrawingMode.GRAPHICS`(用 GDI+ 绘制正文文字)在仓库中已是完整路径(度量与绘制分支齐全、配置对话框可选),但长期处于未维护状态,存在资源泄漏与对齐缺陷。本票:

1. 记录画布绘制的 GDI/GDI+ 分工现状与该档的缺陷;
2. 修复这些缺陷,使该档成为行为正确、可用的受支持路径;
3. 是否把默认档由 `TEXTRENDERER` 切换为 `GRAPHICS` **不在本票范围**,缺陷修完并把复测数据记入本票后再另行决定。

完成标准:缺陷修复 + 构建通过 + `GRAPHICS` 档下文字与形状/图片/`pos` 定位按钮的对齐经人工冒烟确认 + 复测数据记入本票。

## 方案

### 现状:各绘制点的 API 归属(默认配置 `TextDrawingMode = TEXTRENDERER`)

| 绘制点 | API |
| --- | --- |
| 正文 `ConsoleStyledString.DrawTo` | GDI(`TextRenderer.DrawText`) |
| 取不到图形时的替代文本 `ConsoleImagePart.DrawTo`、`ConsoleErrorShapePart.DrawTo` | GDI |
| Rikaichan 释义行 `Rikaichan.OnPaint` | GDI |
| 提示框文字 `EmueraConsole.ToolTip_Draw` | GDI |
| 背景 `EmueraConsole.OnPaint`(`Clear` + `DrawImage(bakedBackground)`) | GDI+ |
| 图片/精灵 `ASprite.GraphicsDraw`(`CroppedImage` 各实现) | GDI+ |
| 按钮位图缓存 `ConsoleButtonString.DrawTo`(`DrawImageUnscaled`) | GDI+ |
| 形状 `ConsoleRectangleShapePart.DrawTo`(`FillRectangle`) | GDI+ |
| `<div>` 边框 `BoxBorder.DrawBorder`(`SmoothingMode.AntiAlias` + `GraphicsPath`) | GDI+ |
| 选中高亮 `Rikaichan.OnPaint`(`FillRectangle`) | GDI+ |
| 输入框、菜单、按钮等 | WinForms/OS 自身绘制 |

即:**除文字外全部是 GDI+**,双缓冲的最终 blit 也是 GDI+;文字在默认档走 GDI,在 `GRAPHICS` 档走 GDI+。

### 现状:三档与该档缺陷

`ConfigData` 默认 `TEXTRENDERER`;配置对话框三项为 `WIN API (GDI)` / `TextRenderer (GDI&GDI+)` / `Graphics (GDI+)`;`Config.SetConfig` 会把 `WINAPI` 强制回退为 `TEXTRENDERER`。`GRAPHICS` 档的路径:`StringMeasure.GetDisplayLength` 走 `Graphics.MeasureCharacterRanges` 并按 `fontDisplaySize` 量化宽度,`ConsoleStyledString`/`ConsoleImagePart`/`ConsoleErrorShapePart.DrawTo` 走 `Graphics.DrawString`。

该档现存缺陷:

1. `StringMeasure.GetDisplayLength` 的 `graph.MeasureCharacterRanges(...)` 返回的 `Region[]` **未 Dispose**:节点度量、`getDivideIndex` 二分、Rikaichan 逐前缀度量都经过这里,每次调用泄漏 native region。
2. `GetDisplayLength(ReadOnlySpan<char>, Font)` 在该档 `chars.ToString()`,打印热路径每次分配字符串。
3. `Config.DrawingParam_ShapePositionShift = max(2, FontSize/6)` 是「GDI 文字 vs GDI+ 图形」的横向补偿,但该档也照旧套用;文字与图形同为 GDI+ 时该补偿应重新推导,否则文字相对图片、形状、`pos` 定位按钮整体偏移。
4. `Graphics.DrawString` 以 em 框为原点且默认带 padding,而 GDI 侧用 `TextFormatFlags.NoPadding` 以行顶为原点;该档既无垂直补偿也未使用 `StringFormat.GenericTypographic`,文字相对行框、命中区(`AConsoleDisplayNode.Bottom` 取 `Config.FontSize`)、下划线与高亮的相对位置与默认档不同。
5. 宽度按 `fontDisplaySize`(约半角宽)量化,是对默认档 GDI 推进宽度的近似,该档的列数与断行位置与默认档本就不同。

### 实测(微基准)

`MS Gothic` 18px、16 字日文样本、20000 次调用(500 次预热),单位 µs/次;数值随字号、字串长度与机器变化:

| 调用 | µs/次 |
| --- | --- |
| `TextRenderer.MeasureText`(span, NoPadding) | 32.0 |
| `Graphics.MeasureCharacterRanges`(含 Dispose) | 22.3 |
| `Graphics.DrawString`(单节点) | 36.3 |
| `TextRenderer.DrawText`(单节点) | 46.5 |

即该档的度量与绘制并不比默认档更重;上表未计入每次调用的 `SetMeasurableCharacterRanges` 与缺陷 1 的泄漏(GC 与 native 句柄压力),这两项修复后需复测。换算参考:全屏约 30 行 × 约 10 节点 ≈ 300 节点,默认档重绘约 14 ms/帧、重测可见行约 10 ms。

### 量测与绘制必须同源

排版(断行、列数、按钮宽度、bitmap cache、命中判定、`PrintCPerLine`)全部建立在 `StringMeasure.GetDisplayLength` 上,因此该档的度量实现与绘制实现必须一起对待,修缺陷时不要只改其中一侧。

### 修复项

1. Dispose `MeasureCharacterRanges` 返回的 `Region[]`(或改用不产生 region 的测量方式),并复用 `StringFormat`/`CharacterRange`;
2. 消除 span 重载在该档的 `chars.ToString()` 分配;
3. 补齐该档的水平/垂直对齐(或改用 `StringFormat.GenericTypographic` 并对齐行顶原点),在 `GRAPHICS` 档实测文字与形状/图片/`pos` 定位按钮/高亮的对齐;
4. 按 3 的结果重新标定 `DrawingParam_ShapePositionShift`;
5. 修复后复测(打印、翻页、滚动各一次),把数据与对齐结论记入本票。

### 遗留决策(不在本票范围,修完后另定)

- 默认档是否由 `TEXTRENDERER` 切换为 `GRAPHICS`;
- 若切换,验收基线需从默认档"逐像素一致"改为"断行与列数在可接受偏差内 + 对齐像素级复测"。

## 曾考虑的替代方案

1. **直接把默认档改为 `GRAPHICS`**(不修缺陷):放弃原因:会立即带上 region 泄漏、文字与图形偏移、断行基线无验收标准三个问题。
2. **只修资源缺陷(泄漏/分配),对齐留待需要时**:放弃原因:对齐缺陷会让该档停留在"能跑但不能用"的状态,无法作为受支持路径,也就无法回答"是否切换默认档"。
3. **维持现状,仅把该档标注为实验性**:放弃原因:region 泄漏属资源缺陷,会随游戏运行时长持续累积,不应以文档标注替代修复。
4. **反向统一到 GDI**(把图片/形状/div 改回 GDI 绘制):放弃原因:需重写抗锯齿、圆角、透明度与位图缩放,且会丢失现有的高质量缩放,收益不明。

## 影响

- 改动限于显示路径:`Emuera/UI/Game/StringMeasure.cs`(度量)、`Emuera/Runtime/Config/Config.cs`(`DrawingParam_ShapePositionShift`)、`ConsoleStyledString`/`ConsoleImagePart`/`ConsoleShapePart`/`ConsoleErrorShapePart`(绘制)。
- 默认档(`TEXTRENDERER`)行为不受影响;`GRAPHICS` 档的对齐与列数会在修复后变化,该档没有既有验收基线。
