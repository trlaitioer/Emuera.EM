---
id: ui-modernization-04
status: implemented
---

# GDI+ 文字绘制档:现状与缺陷修复

## 任务

`TextDrawingMode.GRAPHICS`(用 GDI+ 绘制正文文字)在仓库中已是完整路径(度量与绘制分支齐全、配置对话框可选),但长期处于未维护状态,存在资源泄漏、度量错位与对齐缺陷。本票:

1. 记录画布绘制的 GDI/GDI+ 分工现状与该档的缺陷;
2. 修复这些缺陷,使该档成为行为正确、可用的受支持路径;
3. 是否把默认档由 `TEXTRENDERER` 切换为 `GRAPHICS` **不在本票范围**,缺陷修完并把复测数据记入本票后再另行决定。

完成标准:缺陷修复 + 构建通过 + `dotnet test` 通过 + `GRAPHICS` 档下文字与形状/图片/`pos` 定位按钮的对齐经冒烟确认 + 复测数据记入本票。

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

### 缺陷

`ConfigData` 默认 `TEXTRENDERER`;配置对话框三项为 `WIN API (GDI)` / `TextRenderer (GDI&GDI+)` / `Graphics (GDI+)`;`Config.SetConfig` 会把 `WINAPI` 强制回退为 `TEXTRENDERER`。`GRAPHICS` 档的路径:`StringMeasure.GetDisplayLength` 走 `Graphics.MeasureCharacterRanges` 并按 `fontDisplaySize` 量化宽度,`ConsoleStyledString`/`ConsoleImagePart`/`ConsoleErrorShapePart.DrawTo` 走 `Graphics.DrawString`。该档现存缺陷:

1. **根因——度量语义错了**:`GetDisplayLength(string, Font)` 用 `MeasureCharacterRanges` 取**墨迹宽**,再用 `fontDisplaySize = FontSize / 2 * 1.04` 把它"量化"回前进宽。墨迹宽与前进宽是两个量,该式只有在字体满足「墨迹宽 ≈ 前进宽、半角 ≈ 0.5 em」时才勉强成立(MS Gothic 恰好如此);它既不能表达非均匀前进宽(全角/半角/符号混排),也不随字体变化。
2. 该式的量化除数与字体无关,对全角前进宽大于 `FontSize` 的字体会把每个字放大到 2 倍以上(实测 JetBrains Maple Mono @18:逐字量化 28px vs 真实 22px,整串高估 27%)。
3. `graph.MeasureCharacterRanges(...)` 返回的 `Region[]` **未 Dispose**:节点度量、`getDivideIndex` 二分、Rikaichan 逐前缀度量都经过这里,每次调用泄漏 native region(实测 144B/次 + native 句柄)。
4. `GetDisplayLength(ReadOnlySpan<char>, Font)` 在该档 `chars.ToString()`,`getDivideIndex` 的 span 路径每次调用多分配字符串(实测 72B vs 32B/次)。
5. `Config.DrawingParam_ShapePositionShift = max(2, FontSize/6)` 是「GDI 文字 vs GDI+ 图形」的横向补偿,但该档也照旧套用;文字与图形同为 GDI+ 时该补偿应归零,否则文字相对图片、形状、`pos` 定位按钮整体偏移。
6. `Graphics.DrawString` 以 em 框为原点且默认带 padding,而 GDI 侧用 `TextFormatFlags.NoPadding` 以行顶为原点;该档既无垂直补偿也未使用 `StringFormat.GenericTypographic`,文字相对行框、命中区(`AConsoleDisplayNode.Bottom` 取 `Config.FontSize`)、下划线与高亮的相对位置与默认档不同。
7. 按钮位图缓存高度写死 `Config.FontSize`(`ConsoleButtonString.DrawTo`),而 GDI+ 文字的墨迹超出 `FontSize` 行框(实测 Maple Mono @18 的 `Font.Height` 为 24),缓存底部被裁。
8. GRAPHICS 分支的 `SolidBrush` 在多处未释放:`ConsoleStyledString.DrawToBitmap`、`ConsoleImagePart.DrawTo`、`ConsoleErrorShapePart.DrawTo`。
9. GRAPHICS 分支不画 `JSONConfig.Data.UseButtonFocusBackgroundColor` 的焦点底色,也未传 `TextFormatFlags.PreserveGraphicsClipping` 的对应裁剪语义。
10. `scalePreview`(运行时缩放拖动预览)在**默认档**也把正文文字切到 GDI+(`ConsoleStyledString.DrawTo` 的 `mode == GRAPHICS || scalePreview`),因此默认档拖动预览期间同样复现上述度量与对齐缺陷;`Rikaichan.OnPaint` 的 GDI 文字则不跟随 `Graphics.ScaleTransform`,预览时文字与图形不一起缩放。

### 根因:度量必须给出前进宽

排版(断行、列数、按钮宽度、bitmap cache、命中判定、`PrintCPerLine`)全部建立在 `StringMeasure.GetDisplayLength` 上,而落笔是**逐节点**的:每个节点独立画在自己的 `PointX`,`PointX` 由前面各节点宽度累加(`CalcWidth`/`CalcPointX`,带 `XsubPixel` 亚像素进位)。因此度量必须同时满足三条:**是前进宽(不是墨迹宽或墨迹范围)**、**对空白成立**、**逐节点可加**。旧实现的量化式是用墨迹宽反推前进宽,三条都不满足。

### 实测:GDI+ 的度量路线

`Graphics.MeasureString` 的文档把 `MeasureCharacterRanges` 或 `MeasureString` + `GenericTypographic`(`TextRenderingHint` 置 `AntiAlias`)列为"适合相邻字符串排版"的度量方式。按该口径逐条实测(`AntiAlias` 已设;`SystemDefault`/`AntiAlias`/`ClearTypeGridFit` 三种 hint 数值完全相同——hint 影响落笔不影响度量):

| 路线 | `　` | `　　` | `あ　あ` | `あいうえお` | 可加性(逐节点 − 整串) |
| --- | --- | --- | --- | --- | --- |
| GDI `MeasureText`(NoPadding)——本票采用 | 18 | 36 | 54 | 90 | 0 |
| `MeasureCharacterRanges` + `GenericTypographic` | **0** | **0** | 54 | 90 | 0 |
| `MeasureCharacterRanges` + `MeasureTrailingSpaces` | 19 | 38 | 56 | 93 | **−7** |
| `MeasureString` + `GenericTypographic` | **0** | **0** | 54 | 90 | 0 |
| `MeasureString` + `MeasureTrailingSpaces` | 25 | 43 | 62 | 99 | **+15** |

**没有任何 GDI+ 路线同时满足「是前进宽」「空白不为 0」「逐节点可加」。** 文档推荐的 `MeasureCharacterRanges` 面向整段格式化文本排版(整段一次排版,不需要逐节点可加),与 Emuera 的逐节点落笔不是同一个问题;它对空白返回 0 也不是实现缺陷,而是官方承认且**无修复计划**的取整误差:微软 Platform SDK 支持团队的 [MeasureCharacterRanges により文字列の末尾のスペース文字の情報が取得できない場合がある](https://learn.microsoft.com/en-us/archive/blogs/japan_platform_sdkwindows_sdk_support_team_blog/measurecharacterranges-%E3%81%AB%E3%82%88%E3%82%8A%E6%96%87%E5%AD%97%E5%88%97%E3%81%AE%E6%9C%AB%E5%B0%BE%E3%81%AE%E3%82%B9%E3%83%9A%E3%83%BC%E3%82%B9%E6%96%87%E5%AD%97%E3%81%AE%E6%83%85%E5%A0%B1)写明:「末尾のスペース文字」の X/Y/Width/Height に 0 が返される可能性があり、丸め誤差が原因、**修正の予定はない**、しかも 0 が返るかどうかは文字列とフォントに依存し**事前に判別できない**。

成本:该路线即便数值正确也不便宜——`MeasureCharacterRanges`+Dispose 8.7µs / 144B per call,GDI `MeasureText` 22.3µs / 32B per call;且前者仍需自行处理"空白返回 0"的不确定性。GDI 直接给出前进宽、无该不确定性,并让本档布局与默认档完全一致。

另有一条同属该 API 的坑:`TextRenderer.MeasureText` 的 `NoPadding` **只在带 `IDeviceContext` 的重载上生效**,不带 `IDeviceContext` 的重载会静默忽略它(`MeasureText` 文档的 Note)。本实现传 `Graphics`(实现 `IDeviceContext`),口径正确;文档同一页的官方示例 `DrawALineOfText` 也正是 `Size(int.MaxValue, int.MaxValue)` + `NoPadding` 后逐段累加 `size.Width`。

### 复测数据

`GRAPHICS` 档、`DrawingParam_ShapePositionShift` 实测为 0。度量取自 GDI,因此布局前进宽与默认档逐值相同:

| 字体@字号 | 修复前(量化式,20 全角字) | 修复后(同一 20 字样本) | 修复后每字前进宽 |
| --- | --- | --- | --- |
| MS Gothic 18 | 374 | 360 | 18 |
| Sarasa Gothic J 18 | — | 360 | 18 |
| Yu Gothic 18 | — | 360 | 18 |
| JetBrains Maple Mono 14 | — | 320 | 16 |
| JetBrains Maple Mono 18 | 449 | 440 | 22 |

- 量化式对 Maple Mono 的整串高估 +9、对 MS Gothic +14(同一样本 20 字);逐字度量时量化式对 Maple Mono 高估到 560(+120,约 27%);
- 修复后的 20 字整串 = 逐字求和(可加性残差 0);
- 未列出的字号/字体未逐一采样,量化式的偏差趋势同上(除数假定「半角前进宽 = 0.5 em」,只在 MS Gothic 上成立)。
- **量测与落笔同一套前进宽**:html 把 `<button>` 之间的间隔输出为**只含空格的独立节点**(`HtmlManager.Html2DisplayLine` → `ConsoleButtonString.StrArray`,各节点经 `CalcPointX` 依次落位),因此以「按钮 + 间隔节点 + 按钮」的 7 节点序列为准,节点位置与总宽在 `TEXTRENDERER`/`GRAPHICS` 两档**逐值相同**(x=0/198/360/396/504/648/864,总宽 1026);`[110] - 投放` + 5 全角空格 + `[111] - 经营` 的墨迹范围:GDI 逐节点 `[1,303]`、GDI+ 逐节点 `[1,304]`、GDI+ 整串 `[1,304]`;
- 与 GDI 的横向差:前进宽逐值相同;双引擎残差只剩逐字形光栅化(左缘 +1px、底 +1px 量级,不随行内字数累积)。

### 修复实现

1. **度量走 GDI**:新增 `Emuera/UI/Game/GraphicsTextRenderer.cs`,其 `MeasureAdvance` 用 `TextRenderer.MeasureText(..., NoPadding | NoPrefix)` 取前进宽(span 与 string 两个入口);`Emuera/UI/Game/StringMeasure.cs` 的 GRAPHICS 分支改为调用它,量化式、`fontDisplaySize`、`ranges`/`SetMeasurableCharacterRanges`/`MeasureCharacterRanges`/`layoutRect` 全部移除——泄漏项随之消失(不是靠补 Dispose);
2. **绘制走 GDI+**:同一 helper 的 `Draw` 用 `StringFormat.GenericTypographic` 实例(不能改为自建实例:用同一组 flags 构造出来的对象会退回 `GenericDefault` 的 padding,实测墨迹右移 3px),落笔在 `PointF(PointX + XsubPixel, pointY)`,即行顶原点、无 padding;
3. **`DrawingParam_ShapePositionShift` 在 GRAPHICS 档归零**(`Emuera/Runtime/Config/Config.cs` 的 `recomputeGeometry()`):文字与图形同引擎同原点;默认档保持 `max(2, FontSize/6)`。`DrawableWidth` 在 GRAPHICS 档等于 `WindowX`;
4. **绘制路径统一**:`ConsoleStyledString.DrawTo`/`DrawToBitmap`、`ConsoleImagePart.DrawTo`、`ConsoleErrorShapePart.DrawTo` 共用 helper(`using` 释放 brush、保留 `PreserveGraphicsClipping` 语义),焦点底色改为先 `FillRectangle` 再落字;
5. **bitmap cache**:高度由 `Config.FontSize` 改为 `Config.LineHeight`,并把 `XsubPixel` 传入 `DrawToBitmap`;
6. **`scalePreview`**:默认档拖动预览的 GDI+ 文字与 `Rikaichan.OnPaint` 复用同一 helper 与同一落笔口径,预览期文字随 `ScaleTransform` 一起缩放;
7. **span 重载**:`GetDisplayLength(ReadOnlySpan<char>, Font)` 的 `GRAPHICS` 分支直接调用 helper 的 span 重载(显式分支;转发会让该路径继续 `chars.ToString()`)。未识别的档(如 `WINAPI`)由该分支最后的 `else` 抛 `WIN32APIモードはサポートされていません`,与 string 重载一致;空文本在进入分支前返回 0,与 string 重载的 `string.IsNullOrEmpty` 一致。实测 `MeasureText` 的 span 与 string 重载分配相同(32B/次,GDI 两种重载都复制进自己的缓冲区),span 省下的是调用方那次 `ToString()`(72B → 32B)。`Draw` 只提供 string 入口——所有调用点的文本都来自 `ConsoleStyledString.Text` 等 string 属性,`DrawString` 的 span 重载既无分配收益也无渲染差异;
8. **测试**:`Emuera.Tests/UI/Game/StringMeasureTests.cs`(度量分段/逐字自洽、只含空格的节点必须占宽、逐字落笔=整串绘制、空格间隔节点之后的按钮落位、多字体自洽性理论、MS Gothic 前进宽与 GDI 逐值一致、span 与 string 两重载一致、未支持档两重载一致抛异常、`ShapePositionShift` 仅默认档非零),并把 `ConsoleDisplayLineTests` 中依赖"补偿恒定存在"的断言改为按档断言。渲染类断言使用测试自有的墨色(`Config.ForeColor` 在测试中为 `Color.Empty`,否则断言会因画不出墨迹而形同虚设)。

### 验证状态

- **构建**:`dotnet build Emuera.sln -c Debug-NAudio -p:Platform=x64` 通过(0 错误);
- **测试**:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio` 通过,118 例(基线 105);
- **自动冒烟**:两档节点位置与总宽逐值一致、墨迹与 GDI 参考相差 ≤1px(以断言形式保留在测试中);
- **未完成**:`GRAPHICS` 档下带真实游戏数据的真人冒烟(打印/翻页/滚动各一次、`GDRAWTEXT` 基准线交叉验证)。本仓库不含 `csv/`+`erb/` 游戏数据,该步需要在带游戏数据的目录下由人执行。

### 遗留决策(不在本票范围,修完后另定)

- 默认档是否由 `TEXTRENDERER` 切换为 `GRAPHICS`;
- 若切换,验收基线需从默认档"逐像素一致"改为"断行与列数在可接受偏差内 + 对齐像素级复测";
- 配置对话框的档位命名(`TextRenderer (GDI&GDI+)` 实际是 GDI,`Graphics (GDI+)` 与 `WIN API` 均不可用)随该决策一并处理。

### 与 issues/03 的关系

- 本票修完后,GRAPHICS 档与默认档对同一字体的前进宽**逐值相同**(度量同取自 GDI),故 03 的渲染字号反查在两档下基于同一数值,不需要按档分别标定;
- 两档的 `DrawableWidth` 仍不同(本档 `ShapePositionShift` 为 0、`DrawableWidth` 等于 `WindowX`,默认档为 `WindowX - max(2, FontSize/6)`),故同一字体在两档下列数可能差一个字符。

## 曾考虑的替代方案

1. **直接把默认档改为 `GRAPHICS`**(不修缺陷):放弃原因:会立即带上 region 泄漏、文字与图形偏移、断行基线无验收标准三个问题。
2. **只修资源缺陷(泄漏/分配),对齐留待需要时**:放弃原因:对齐缺陷会让该档停留在"能跑但不能用"的状态,无法作为受支持路径,也就无法回答"是否切换默认档"。
3. **维持现状,仅把该档标注为实验性**:放弃原因:region 泄漏属资源缺陷,会随游戏运行时长持续累积,不应以文档标注替代修复。
4. **反向统一到 GDI**(把图片/形状/div 改回 GDI 绘制):放弃原因:需重写抗锯齿、圆角、透明度与位图缩放,且会丢失现有的高质量缩放,收益不明。
5. **保留 `MeasureCharacterRanges`,只补 `Region[]` 的 Dispose**(即只修泄漏、保留量化式):放弃原因:泄漏只是表象。量化式量的是墨迹宽,要用一个与字体无关的单元宽反推前进宽,对非 MS Gothic 字体逐字高估 27%;且其数据源本身不可靠(末尾空格的取整误差官方无修复计划、无法事先判定)。
6. **度量与绘制都用 GDI+**:放弃原因:四条 GDI+ 度量路线实测均不可用,见"实测:GDI+ 的度量路线"。
7. **度量与绘制都用 GDI**(即该档只换绘制引擎不改度量档):放弃原因:该档的存在意义就是用 GDI+ 渲染,把文字落笔退回 GDI 等于取消该档,无法回答"是否切换默认档"。
8. **span 重载转发给 string 重载**(保留原结构不改分支):放弃原因:该路径会继续每次 `chars.ToString()`(72B vs 32B/call),而它本是 span 入口存在的唯一理由;改为显式分支后未支持档的检查仍各自成立。
9. **用哨兵字符补测末尾空白**(`MeasureString(s + "|") - MeasureString("|")`):放弃原因:依赖"padding 与内容无关"这一未文档化的性质,且对含空白的串仍需额外一次度量。
10. **度量与绘制用两个不同的 StringFormat**(前者带 `MeasureTrailingSpaces`):实测:该 flag 在 `MeasureString` 上不只是保留末尾空白,还会带来与 `GenericDefault` 同量的 padding,使落笔右移 3px 并破坏可加性;两个 format 无法同时满足"量得准"和"落得准"。
11. **为垂直/左缘残差新增配置项**:放弃原因:实测残差是常数(1px)而不是随行累积的量,与 `ShapePositionShift` 那类需要按字号标定的补偿不同,不需要暴露给用户。

## 影响

- 改动限于显示路径:`Emuera/UI/Game/StringMeasure.cs`、新增 `Emuera/UI/Game/GraphicsTextRenderer.cs`(度量与绘制)、`Emuera/Runtime/Config/Config.cs`(`DrawingParam_ShapePositionShift`)、`ConsoleStyledString`/`ConsoleImagePart`/`ConsoleShapePart`/`ConsoleButtonString`(绘制)、`Rikaichan`(释义行与 `scalePreview`),以及新增 `Emuera.Tests/UI/Game/StringMeasureTests.cs`。
- 默认档(`TEXTRENDERER`)的正常绘制行为不受影响;默认档的 `scalePreview` 期间文字落笔口径随本票统一(该窗口期本来就复用 GDI+ 文字,不存在既有验收基线)。
- `GRAPHICS` 档的对齐与列数会在修复后变化,该档没有既有验收基线;`ShapePositionShift` 归零会使该档 `DrawableWidth` 增大到 `WindowX`。
- 该档的度量成本从"每次调用创建一个 native region"(8.7µs / 144B,且需 Dispose)变为 GDI 度量(22.3µs / 32B):单次更慢但不再有 native 泄漏与 region 压力,且布局与默认档一致。
