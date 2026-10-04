# 画布与字体缩放:运行时窗口缩放与高 DPI

id: ui-modernization-02
Status: implemented

## 任务

两个现象合并处理:

1. **高分屏物理尺寸过小**。manifest 与 csproj 均声明 PerMonitorV2,进程不做系统位图拉伸,配置像素 = 物理像素——4K/高缩放屏上 `WindowX`(默认 760)与 `FontSize`(`GraphicsUnit.Pixel`,默认 18)的物理尺寸约为 96DPI 屏的一半。
2. **窗口不能横向缩放,字号不跟随**。宽度被钉死,只能拖高;拖高只增加可见行数,字号不变。即使解除宽度锁定,已打印文本也不会重新换行(见「方案 · 问题/背景」),拉宽后右侧只是多出空白。

本票不做流式布局改造(见 issues/01 评估结论),改为引入**运行时窗口缩放**:画布宽度决定缩放比,字号、行高、窗口尺寸一起按该比例缩放,保持每行字符数不变;高度只决定可见行数。高 DPI 过小的问题由用户拖放一次解决,不引入 DPI 缩放因子。

完成标准见「方案 · 验收标准」。

## 方案

### 问题/背景

#### 宽度为何拖不动

`MainWindow.initControlSizeAndLocation` 末尾:

```csharp
MinimumSize = new Size(Width, minimamY);
MaximumSize = new Size(Width, maximamY);
```

`MinimumSize.Width == MaximumSize.Width == Form.Width`,WinForms 在 `WM_GETMINMAXINFO` 层面把宽度钳死,高度则在 `[minimamY, maximamY]` 内自由。`WndProc` 的 `SC_MAXIMIZE` 分支又把 `MaximizedBounds.Width` 钳到 `Config.WindowX`,使最大化只增加高度。唯一的逃生舱是 Tools →「ウィンドウ幅のロック変更」(`ウィンドウ幅のロック変更ToolStripMenuItem_Click`,由 `_isWidthLocked` 守卫),`Config.UseMenu == false` 时不可达。

全仓库无 `OnResize`/`SizeChanged`/`Move`/`LocationChanged` 处理器;`initControlSizeAndLocation` 的注释记录了设计意图:*「1819 リサイズ時の処理を全廃しAnchor&Dock処理にマルナゲ」*。

#### 为何解除锁定也不换行

换行宽度来自 `Config.DrawableWidth`,由 `Config.SetConfig` 在启动时算一次、全仓库仅此一处赋值:

```csharp
DrawingParam_ShapePositionShift = 0;
if (TextDrawingMode != TextDrawingMode.WINAPI)
    DrawingParam_ShapePositionShift = Math.Max(2, FontSize / 6);
DrawableWidth = WindowX - DrawingParam_ShapePositionShift;
```

消费它的路径全部在排版时读取该静态量,且结果被固化:

- `PrintStringBuffer.ButtonsToDisplayLines`:`int windowWidth = divWidth > 0 ? divWidth : Config.DrawableWidth;`,超出即调用 `getDivideIndex` 在字符粒度上切分;
- `ConsoleDisplayLine.SetAlignment` 的居中/右对齐;
- `StringMeasure` 构造函数以 `Config.WindowX * 2`、`Config.LineHeight`、`Config.DefaultFont.Size` 建立 `layoutSize`/`layoutRect`/`fontDisplaySize`,三者均为 `readonly`。

已打印的文本连同每个节点的 `Width`/`PointX`、每行的 `aligned` 标记、按钮的 `bitmapCache` 一起存入 `displayLineList`,没有失效机制。

#### 字号与每行字符数

不存在「每行多少字符」这个量,列数是实测累积出的结果:每个 `ConsoleStyledString` 首次排版时调 `StringMeasure.GetDisplayLength(text, font)` 实测像素宽并缓存在节点的 `Width` 上,`PrintStringBuffer` 逐个累加 `PointX` 直到超过 `DrawableWidth`,再由 `getDivideIndex` 二分/线性搜索切分位置。半角与全角宽度不同,故每行半角字符数非常数。

唯一的「字符数 ↔ 像素」换算是 `HtmlManager.HtmlSubString` 的 `length * Config.FontSize / 2`(把 `FontSize/2` 当半角宽)与 `Creator.Method.HtmlStringLenMethod` 的反向换算,服务于 ERB 的 `HTML_STRINGLEN`/`HTML_SUBSTRING`/`HTML_STRINGLINES`,与排版换行无关。二者互为逆运算且都随 `FontSize` 线性变化,故 `FontSize` 等比缩放不改变 ERB 语义。

#### 字体层

`FontFactory.GetFont` 构造 `new Font(name, Config.FontSize, style, GraphicsUnit.Pixel)` 并按 `(fontName, FontSize, style)` 静态缓存,`Config.DefaultFont` 是该缓存的属性封装。`ClearFont()` 全仓库零调用者。下列消费点不随 `Config.FontSize` 变化而自动跟随:

- `MainWindow` 构造函数对 `richTextBox1.Font` 的一次性赋值;
- `StringMeasure` 的 `fontDisplaySize`/`layoutSize`/`layoutRect`(均 `readonly`);
- `EmueraConsole` 的 `printCWidth*`(惰性计算后不再失效)与 `tooltip_fontsize` 字段初始化;
- `ConsoleStyledString` 构造时捕获的 `Font` 引用(`DivideAt` 分裂时把旧 `Font` 复制给新节点)。

#### 高度方向为何已经是动态的

`EmueraConsole.OnPaint` 每次重绘都从 `window.MainPicBox.Height` 重新计算可见行数与起点(`pointY = window.MainPicBox.Height - Config.LineHeight`),`GetLinePointY`、`MoveMouse` 的命中换算同理。这就是「只能拖高、拖高立刻生效」的原因。

#### 相关配置现状

`ConfigData.setDefault` 默认值:`WindowX` 760、`WindowY` 480、`FontName` `ＭＳ ゴシック`、`FontSize` 18、`LineHeight` 19、`SizableWindow` true、`TextDrawingMode` `TEXTRENDERER`。

`LineHeight` **不由字体度量推导**,全仓库唯一的字体度量调用在 `GraphicsImage.cs` 的 GDI+ `GraphicsPath.AddString` 路径,与 `Config.LineHeight` 无关;`Config.SetConfig` 只做 `LineHeight >= FontSize` 校验。

用户配置为 `<ExeDir>emuera.config`(UTF-8 BOM),`ConfigData.saveConfig` 按 `setDefault` 顺序**整文件重写**、**静默丢弃未知键**、无表头与版本号;缺失键取默认值。`fixed.config` 的 `Fixed` 使 `ConfigItem<T>.Value` 的 setter 静默 no-op。仓库内不含 `csv/`,`_default.config`/`fixed.config` 均不存在。

窗口位置与尺寸目前**从不回写**:`MainWindow_FormClosing` 只保存按键宏并释放控制台;`WindowPosX/Y` 仅由配置对话框编辑、经 `SetWindowPos` 开关在启动时生效。

### 缩放模型

`Config.WindowX`/`WindowY`/`FontSize`/`LineHeight` 的含义不变(配置值的语义,即用户在对话框里编辑的那组数),窗口不做任何 DPI 感知。运行时以本进程启动时的画布宽度为锚点:

```
s            = 当前有效画布宽 / W₀
显示 FontSize = round(FontSize₀ × s)
显示 LineHeight = round(LineHeight₀ × s)
```

`W₀` = 本进程启动时的有效画布宽(即 `MainPicBox.Width`,不含滚动条),`FontSize₀`/`LineHeight₀` = 启动配置值。三者是本次会话的不变量,每次尺寸变化都从锚点重算,故**会话内不累积误差**。

`Config.FontSize`/`Config.LineHeight` 只在尺寸变化的「确定」时刻(拖放松手、最大化、重置)更新为 `FontSize₀ × s`/`LineHeight₀ × s`,拖动过程中保持上一次确定的值(此时画布内容用变换缩放预览)。退出时被回写而成为下次会话的锚点,因此配置文件中存的始终是"锚点",不需要额外字段区分两种角色。`Config.DrawableWidth` 由 `Config.recomputeGeometry()` 按 `WindowX - max(2, FontSize/6)` 重算,不持久化。

`s` 同时作用于:游戏画布的排版与落笔、`vScrollBar` 宽度、`richTextBox1` 的字号与高度。

**列数保持不变是上述缩放的推论**:`s` 同时缩放 `DrawableWidth` 与字符前进宽,二者相除后断行位置不变。故 `DrawableWidth` 不需要「冻结」,重算即可自洽。

### 拖放行为

- 拖宽:窗口跟光标变化,`s` 按画布宽实时重算用于预览;松手时字号/行高/滚动条宽度/输入框字号一并确定并重排;
- 拖高:只改变可见行数,字号与列数不变;
- 最大化:保持宽高比等比放大,直到宽或高其一触及显示器工作区(`Screen.WorkingArea`),并同时受现有 `maximamY = 2560` 约束(工作区高度超过 2560 时按 2560 反算宽度以保持比例);放大结果在显示器工作区内水平、垂直居中(不贴左上角);
- 重置:Tools 菜单新增一项,把 `s` 复位到 1,即窗口宽度回到 `W₀ + 滚动条宽` 尺寸与启动字号(高度保持用户当前值);
- `MinimumSize` 宽度下限取 `max(128, ceil(W₀ × 8 / FontSize₀)) + 滚动条宽 + 边框`,保证缩到最小时 `FontSize` 恰好落在下限 8 而不被钳制(否则断行不变会溢出画布);高度下限沿用现有 `minimamY = max(100, menuHeight + LineHeight × 2)`;删除宽度钉死与 `MaximizedBounds` 的宽度钳制;
- 删除 Tools →「ウィンドウ幅のロック変更」菜单项、`_isWidthLocked` 字段及其处理器 `ウィンドウ幅のロック変更ToolStripMenuItem_Click`(其写回 `ConfigData` 的行为与本票的写回路径冲突)。

### 拖放中的渲染

拖动过程中实时更新廉价量(`s`、窗口几何、输入框字号、滚动条宽度),画布内容用 `Graphics.ScaleTransform` 临时缩放绘制,松手后按新字号重排。

**`TextFormatFlags.ApplyTransform` 在 .NET 中不存在**(实测 `System.Windows.Forms.TextFormatFlags` 只有 `PreserveGraphicsClipping` 与 `PreserveGraphicsTranslateTransform`,后者仅作用于平移)。实测结论:同一 `Graphics` 上 4 倍缩放,`TextRenderer.DrawText` 的着墨范围与未缩放时逐像素相同,`Graphics.DrawString` 则随之放大。故临时缩放路径上的文字改用 GDI+ 绘制:

- `ConsoleStyledString.DrawTo` 在预览标志为真时走 `Graphics.DrawString`,与 `TextDrawingMode.GRAPHICS` 分支合并为同一条件;
- 其余节点(`ConsoleImagePart`、`ConsoleShapePart`、`ConsoleDivPart`)本就使用 GDI+,随 `Graphics` 变换一起缩放;
- 预览期间 `Config.FontSize`/`LineHeight`/`DrawableWidth` **保持不变**,版面仍是上一次确定后的坐标;
- **变换的基准是「已确定的版面」,不是启动尺寸**:`WindowZoom.PreviewScale` 取 `新画布宽 / 已确定画布宽`。若按启动宽计算,第二次拖放会把已经缩放过的版面再乘一次绝对倍率,预览尺寸严重超出画布(松手后回跳很大);
- **可视行数按实际画布高换算**:`EmueraConsole.LayoutCanvasHeight` 在预览期返回 `MainPicBox.Height / 预览倍率`,于是内容底部恰好落在画布底部。若固定用「已确定的画布高」当坐标系,放大时底部若干行会越过下边缘、缩小时底部留出空白带;
- 背景位图保持「已确定尺寸」不重建(避免拖动中每次尺寸变化都重新烘焙),绘制时拉伸到当前基准尺寸,画布不会露出未覆盖的黑带;
- `EmueraConsole.MoveMouse` 在预览期间把坐标除以缩放比,还原到变换基准坐标后再命中判定;
- `OnPaint` 结束前 `ResetTransform`,避免复用的 `Graphics` 把变换带到下一次绘制。

代价:GDI 与 GDI+ 的字形度量不同,拖动中的文字与松手后的最终态观感有细微差异(仅限拖动期间)。


### 重排

松手后(`ResizeEnd`)或最大化等非拖放路径的尺寸变化时,按新字号重排:断行位置沿用(比例缩放不改断行),仅按新字号重测节点宽度。

**惰性重排**(`EmueraConsole.layoutVersion` / `ConsoleDisplayLine.LayoutVersion`):`ResizeEnd` 只把 `EmueraConsole.InvalidateLayout()` 的版本号递增;每一行在「即将被绘制或命中判定」时才由 `EmueraConsole.ensureLineLayout` 调 `ConsoleDisplayLine.ReLayout` 重测。`displayLineList` 上限是 `Config.MaxLog`(默认 5000 行),全量重排需数万次 `MeasureText`,惰性化后每次只处理可见行与命中范围,拖动与松手都不再有卡顿风险。

`ConsoleDisplayLine.ReLayout` 重测的内容:

- 每个按钮 `ResetLayout`(节点宽度失效、重新取字体、位置固定按钮按新 `Config.FontSize` 重算 `PointX`、`bitmapCache` 释放);
- 复刻打印期的累计逻辑(`PrintStringBuffer.setWidthToButtonList`)重算 `PointX`;
- 按断片重放位置对齐(见下)。

**位置对齐按断片重放**。`PRINT` 的「行末継続」会把后续输出并进同一显示行(`EmueraConsole.addDisplayLine` 中的 `ShiftPositionX` + `ChangeStr`),此时新并入的部分是在「自成一行的对齐」之上再平移的。直接从 0 重排整行会丢掉各段各自的对齐,故 `ConsoleDisplayLine` 新增 `LineFragment` 列表:每次 `SetAlignment` 记录一段(起始下标、按钮数、对齐方式、`customWidth`、是否 LEFT 且逻辑行),`PrependButtons` 在合并时按位移接续断片;`ReLayout` 逐段重放「本段自 0 累计 → 应用本段对齐 → 接到上一段末尾」。

缓存失效清单(在 `ConsoleDisplayLine.ReLayout` 与各节点 `ResetLayout` 中):

- `StringMeasure` 的 `layoutSize`/`layoutRect`/`fontDisplaySize`(改为可变字段 + `RefreshLayout()`);
- `EmueraConsole` 的 `printCWidth*`;
- 每个节点的 `Width`/`XsubPixel`、`ConsoleDisplayLine.aligned`(由断片重放取代)、`ConsoleButtonString.bitmapCache`;
- `ConsoleStyledString` 捕获的 `Font` 引用(按 `StringStyle` 重新取)与 Rikaichan 的 `Ends[]`;
- `EmueraConsole._htmlElementList`(HTML island)与各显示行;
- `ConsoleDivPart`(HTML `<div>`):保留标签原始 `MixedNum`,重算字体相对单位带来的偏移与尺寸,再递归重排其子行;`ConsoleEscapedParts` 改为保存像素值、检索时按当前 `Config.LineHeight` 折算行号。

`ConsoleImagePart`(`<img>`)与 `ConsoleShapePart`(`<shape type='space'|'rect'>`)同样保留标签原始 `MixedNum` 并在重排时重新解析:

- 未指定尺寸 / 百分比的参数以 `Config.FontSize` 为基准,随缩放变化(此前只在打印那一刻解析一次并固化,重排后图像退回旧尺寸、要重启才正常);
- 显式 `px` 的参数保持绝对像素(ERB 语义,像素画不被拉伸);
- 图像节点的 `top`/`bottom`/`destRect`、形状的 `RectangleF` 与 `Top`/`Bottom` 一并重算,命中判定与 escaped parts 的范围随之更新。

`SPRITE`/`CBG` 等由 ERB 直接绘制的位图不在本列:其大小由脚本自身的调用与资源决定,与本票无关。


### 派生量的重算

`Config.SetConfig` 只在启动与一处剪贴板开关里被调用,内部含会弹 `Dialog.Show` 的校验(`FontSize < 8`、`LineHeight < FontSize`)。拖放路径不复用它,抽出只重算几何派生量的 `Config.recomputeGeometry()`,以及不校验、不落盘的 `Config.ApplyWindowGeometry(WindowX, WindowY, FontSize, LineHeight)`,从 `WindowX`/`FontSize` 重算 `DrawableWidth` 与 `DrawingParam_ShapePositionShift`,避免拖拽中弹出模态框打断操作。

该剪贴板开关(`クリップボードにコピーToolStripMenuItem_Click_1`)原先调用 `Config.SetConfig(ConfigData.Instance)`,会把运行中缩放的 `WindowX`/`FontSize`/`DrawableWidth` 一起重置回配置文件值,故改为只重读该开关的 `Config.UpdateClipboardSetting`。

### 持久化

`FormClosing` 时把当前 `WindowX`/`WindowY`/`FontSize`/`LineHeight` 回写 `emuera.config`,下次启动即为配置值(窗口尺寸与字号保持)。不新增任何配置项,不做迁移:旧 `emuera.config` 缺键时取默认值,行为与改动前逐像素一致。

`LineHeight` 必须一并回写:它同样按 `s` 缩放,若只写 `FontSize`,下次启动会因 `LineHeight < FontSize` 触发 `Config.SetConfig` 的错误对话框并被强设为 `FontSize`。

写回策略:仅在值确实变化时 `SetValue` + `SaveConfig`,避免每次退出都整文件重写;`SetValue` 对 `fixed.config` 固定项静默失败,故固定项在会话内照常生效、下次启动回到锁定值。

窗口位置**保持原有行为,不回写**(`WindowPosX/Y` 仍只由配置对话框编辑)。

### 配置对话框

`WindowX`/`WindowY`/`FontSize` 三个数值框维持现状:由 `ConfigDialog.SetConfig` 经 `setNumericUpDown` 从 `ConfigData` 取值,即显示配置值,不自动反映当前窗口几何。取当前值由既有的 `現在のウィンドウサイズを取得` 按钮(`button1_Click`)完成,与 `現在のウィンドウ位置を取得`(`button3_Click`,窗口位置)**行为对称**;窗口位置本就从不自动回写,故两者一并维持。

改 `FontSize` 不动窗口宽度,故下次启动的列数会变化——与今天的行为一致。

因此 `ConfigDialog` 无需改动。

### ERB 兼容

`GETCONFIG`/`GETCONFIGS` 读到的仍是配置基准值(不随拖放变化);`CLIENTWIDTH`/`CLIENTHEIGHT` 继续反映实时画布尺寸。不存在 `SETCONFIG`。`HTML_STRINGLEN`/`HTML_SUBSTRING` 的 `FontSize/2` 单位换算在等比缩放下自洽。

### 代码落点

- `Emuera/Runtime/Config/Config.cs`:`recomputeGeometry`(私有)、`ApplyWindowGeometry`、`UpdateClipboardSetting`;
- `Emuera/UI/Framework/Forms/MainWindow.cs` / `MainWindow.Designer.cs`:起动基准尺寸与 `layoutControls`、`MainWindow_ResizeBegin/ResizeEnd/Resize`、`UpdateZoomPreview`、`CommitZoom`、`SetMaximizedBounds`、`MinWindowWidth`、`SetInputFont`、`SaveWindowGeometry`、`ウィンドウズームをリセットToolStripMenuItem_Click`;删除 `_isWidthLocked` 与宽度钉死、`SC_MAXIMIZE` 分支改调 `SetMaximizedBounds`;
- `Emuera/UI/Game/EmueraConsole.cs`:`layoutVersion`/`InvalidateLayout`/`ensureLineLayout`、`resizePreviewScale`/`UpdateResizePreview`/`ClearResizePreview`/`LayoutCanvasHeight`、`OnPaint` 的变换与基准尺寸、`MoveMouse` 的坐标还原;
- `Emuera/UI/Game/ConsoleDisplayLine.cs`:`LineFragment`、`LayoutVersion`、`ReLayout`、`PrependButtons`;
- `Emuera/UI/Game/ConsoleButtonString.cs`、`AConsoleDisplayNode.cs`、`ConsoleStyledString.cs`、`ConsoleImagePart.cs`、`ConsoleShapePart.cs`:节点级 `ResetLayout` 与按钮级 `ResetLayout`(含 `bitmapCache` 释放与 `PointXisLocked` 重算);`ConsoleImagePart`/`ConsoleShapePart` 另抽出 `computeGeometry`,把标签原始值重新解析为像素;`DrawTo` 增加预览标志;
- `Emuera/UI/Game/StringMeasure.cs`:`RefreshLayout`;
- `Emuera/UI/Game/EmueraConsole.Print.cs`:`addDisplayLine` 记录 `LayoutVersion` 并改调 `PrependButtons`、`PrintHTMLIsland` 记录版本、`ConsoleEscapedParts.Add` 改传像素值;
- `Emuera/Runtime/Utils/EvilMask/ConsoleDivPart.cs`:`computeGeometry`/`ResetLayout`;`ConsoleEscapedParts.cs`:`Add` 存像素、`MarkChanged`、检索时折算行号;
- `Emuera/Runtime/Utils/EvilMask/Lang.cs` 与 `Emuera/Properties/lang/emuera-{eng,zhs}.xml`:新增 `UI.MainWindow.Tools.ResetZoom`;
- `Emuera/UI/WindowZoom.cs`:放缩几何的纯函数(`Compute` 由启动尺寸算绝对值,`PreviewScale` 以已确定版面为基准,`MinCanvasWidth` 保证字号下限又不反过来撑大窗口,`MaximizedBounds` 按工作区居中的最大化区域),由 `MainWindow` 与 `EmueraConsole` 共用。
- 测试:`Emuera.Tests/UI/Game/ConsoleDisplayLineTests.cs`、`Emuera.Tests/UI/WindowZoomTests.cs`、`Emuera.Tests/UI/Game/ConsoleShapePartTests.cs`(共 15 例,覆盖重排不变量、缩放几何与 `<shape>` 尺寸折算)。

### 验收标准

1. 默认配置下与改动前渲染、布局逐像素一致;
2. 拖宽 ⇒ 字号、行高、画布、滚动条、输入框按 `s` 同步放大,断行位置不变,列数变化在 ±1 字符内;
3. 拖高 ⇒ 只增加可见行数,字号与列数不变;
4. 窗口四边均可拖拽;`MinimumSize`/`MaximumSize` 的宽度钉死与 `MaximizedBounds` 宽度钳制已移除;宽度锁定菜单项与 `_isWidthLocked` 已删除;
5. 最大化按等比放大至工作区或 `maximamY` 约束,保持宽高比;
6. Tools 菜单的重置项可将 `s` 复位到 1;
7. 退出后重启,窗口尺寸与字号保持;
8. 构建通过,点击冒烟:缩放后按钮命中区域与实际绘制位置一致。

## 曾考虑的替代方案

1. **DPI 作为独立缩放因子**(原 02 方案):`scale = DeviceDpi/96` 乘进 `FontSize`/`LineHeight`/`WindowX`/`WindowY`,并做成「跟随系统/100%/125%」配置项。放弃原因:字号在运行时已由画布宽度派生,再乘一次 DPI 属重复计算;且用户拖放一次即可解决高 DPI 过小问题,不值得为此引入独立机制与配置项。**该目标未放弃,由本文的运行时缩放承接。**
2. **把「基准宽/当前宽」做成两套配置键**:列数跨重启精确不变。放弃原因:需要新增配置项,且配置文件中出现两份窗口宽度易混淆;实测 `max(2, FontSize/6)` 整数边距带来的偏差仅 ±1 字符,已在验收标准内接受。
3. **`DrawableWidth` 在拖放期间冻结**:放弃原因:`DrawableWidth = WindowX - max(2, FontSize/6)` 是像素量且直接作为换行阈值(`PrintStringBuffer.ButtonsToDisplayLines`),冻结会导致一行容纳的字符数随字号放大而减半;正确做法是让它与字号一起按 `s` 缩放。
4. **Ctrl+滚轮调字号**(保持列数版:等价于改窗口尺寸;独立字号版:窗口不变、允许换行位置随字号变化):放弃原因:保持列数版无法提供边框拖拽之外的额外能力,且需处理 `richTextBox1` 抢占滚轮消息;独立字号版必须重排**已打印的历史**——`displayLineList` 及其各索引(滚动条 `Value`/`Maximum`、行号、`ConsoleEscapedParts`、`ClipboardProcessor`、Rikaichan 链接、bitmap cache)都以显示行为粒度,全量重排按实测度量推算约 0.8~3.2 s(`MaxLog` 默认 5000 行),惰性重排则需先把滚动模型虚拟化到逻辑行。改动量过大,暂时不做。
5. **窗口高度吸附到整行**:放弃原因:高度不参与字号计算,不吸附只会让窗口底边切掉一条不足一行的空白,而滚动到历史位置时顶部本就存在被裁切的一行(`OnPaint` 的 `pointY` 由滚动偏移决定),用户已在接受这一观感,吸附收益为零。
6. **`s` 每次以「上一次拖放结束时的画布宽」为锚点**:放弃原因:整数值 `FontSize` 每次取整会把误差带进下一次拖放并累积;以不变量 `W₀`/`FontSize₀` 为锚点则每次从零重算,无累积。
7. **`FontSize` 不回写、每次从窗口宽派生**:放弃原因:派生需要跨重启可用的参考宽,而窗口尺寸又要回写,二者互相冲突,不新增配置键无法同时满足。
8. **一并消除滚动产生的半截行**:放弃原因:该现象由 `OnPaint` 的滚动偏移造成,与窗口高度无关;消除它需改可见行数公式、`MoveMouse` 命中换算、`GetLinePointY` 与滚动条 `Value` 的粒度语义,并会让滚动出现跳行感。本票只处理「窗口高度导致的半截行」,滚动态维持现状,如需处理另立票。
9. **拖拽中每帧重排全部历史节点**:放弃,原因是拖动中每次尺寸变化都要重测全部节点宽度,存在卡顿风险;实现改为「拖动中 GDI+ 临时缩放 + 松手后惰性重排可见行」。
10. **`TextFormatFlags.ApplyTransform` 让 GDI 文本跟随变换**(原方案首选):放弃,该值在 .NET 的 `TextFormatFlags` 中不存在,且实测 `TextRenderer.DrawText` 不响应 `Graphics.ScaleTransform`;拖动预览改用 `Graphics.DrawString`(GDI+),代价是预览与最终态的 GDI 字形度量有细微差异。
11. **拖动预览改为「按基准尺寸离屏渲染整屏再缩放贴图」**:放弃,贴图缩放会让拖动中的文字发虚,而 DrawString 保持矢量清晰;若实测 DrawString 观感不可接受可回退到此方案。
12. **每次尺寸变化都真实重排(原方案回退路径)**:放弃,理由同 9。
13. **惰性重排只覆盖可见行**,历史行滚动到时按需重排:采纳。副作用是缩放后立即滚动到很久以前的行时,该行会在首次绘制时现测一次(单行成本可忽略)。

## 影响

- 改动范围限于 `Emuera/UI/`、`Emuera/Runtime/Config/Config.cs` 与两处显示节点(`Emuera/Runtime/Utils/EvilMask/ConsoleDivPart.cs`、`ConsoleEscapedParts.cs`),不触及解释器与脚本运行时。
- `Config.WindowX`/`WindowY`/`FontSize`/`LineHeight` 的语义不变,但运行中会被回写到 `emuera.config`——这是本票新增的写盘行为。
- **已知取舍**:高 DPI 显示器上首次启动仍然偏小,需用户手动拖放一次;拖放结果回写后,后续启动保持。
- **已知限制**:不处理跨显示器 DPI 变化;窗口拖到不同缩放比例的显示器上时 `s` 不重算。
- **已知限制**:`IMG`/`SHAPE`/HTML `<div>` 中**显式 `px`** 的几何不随缩放重算(绝对像素是 ERB 语义),缩放后与文字的相对大小会变化;未指定单位或百分比的尺寸、以及文字与按钮命中区域均随缩放同步。`SPRITE`/`CBG` 等由 ERB 绘制并指定尺寸的位图同理。
- **已知限制**:由于 `max(2, FontSize / 6)` 的整数除法边距,`FontSize` 跨过 6 的倍数时 `DrawableWidth` 会跳变 1px,可能导致重启后列数变化 1 字符。
- **已知限制**:拖动预览用 GDI+ 绘制文字,与松手后的 GDI 字形度量存在细微差异(仅拖动期间可见)。
- **已知限制**:`GETTOOLTIPFONT`/`SETTOOLTIPFONT` 设定的提示框字号(默认取启动时的 `Config.FontSize`)不随缩放重算;缩放后提示文字会比正文小。
- **已知限制**:缩放会改变 `Config.WindowX`,而 `ClipBoardDialog` 依 `Config.WindowX` 决定自身宽度,故日志复制窗口会随缩放变宽。
- 拖动过程中画布由 `Graphics` 变换缩放,文字用 GDI+ 绘制;松手后按新字号惰性重排,文字恢复 GDI 渲染。
