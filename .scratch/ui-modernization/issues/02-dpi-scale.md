# 画布与字体缩放:运行时窗口缩放与高 DPI

Status: ready-for-agent

## 任务

两个现象合并处理:

1. **高分屏物理尺寸过小**。manifest 与 csproj 均声明 PerMonitorV2,进程不做系统位图拉伸,配置像素 = 物理像素——4K/高缩放屏上 `WindowX`(默认 760)与 `FontSize`(`GraphicsUnit.Pixel`,默认 18)的物理尺寸约为 96DPI 屏的一半。
2. **窗口不能横向缩放,字号不跟随**。宽度被钉死,只能拖高;拖高只增加可见行数,字号不变。即使解除宽度锁定,已打印文本也不会重新换行(见「问题/背景」),拉宽后右侧只是多出空白。

本票不做流式布局改造(见 issues/01 评估结论),改为引入**运行时窗口缩放**:画布宽度决定缩放比,字号、行高、窗口尺寸一起按该比例缩放,保持每行字符数不变;高度只决定可见行数。高 DPI 过小的问题由用户拖放一次解决,不引入 DPI 缩放因子。

完成标准见「验收标准」。

## 问题/背景

### 宽度为何拖不动

`MainWindow.initControlSizeAndLocation` 末尾:

```csharp
MinimumSize = new Size(Width, minimamY);
MaximumSize = new Size(Width, maximamY);
```

`MinimumSize.Width == MaximumSize.Width == Form.Width`,WinForms 在 `WM_GETMINMAXINFO` 层面把宽度钳死,高度则在 `[minimamY, maximamY]` 内自由。`WndProc` 的 `SC_MAXIMIZE` 分支又把 `MaximizedBounds.Width` 钳到 `Config.WindowX`,使最大化只增加高度。唯一的逃生舱是 Tools →「ウィンドウ幅のロック変更」(`ウィンドウ幅のロック変更ToolStripMenuItem_Click`,由 `_isWidthLocked` 守卫),`Config.UseMenu == false` 时不可达。

全仓库无 `OnResize`/`SizeChanged`/`Move`/`LocationChanged` 处理器;`initControlSizeAndLocation` 的注释记录了设计意图:*「1819 リサイズ時の処理を全廃しAnchor&Dock処理にマルナゲ」*。

### 为何解除锁定也不换行

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

### 字号与每行字符数

不存在「每行多少字符」这个量,列数是实测累积出的结果:每个 `ConsoleStyledString` 首次排版时调 `StringMeasure.GetDisplayLength(text, font)` 实测像素宽并缓存在节点的 `Width` 上,`PrintStringBuffer` 逐个累加 `PointX` 直到超过 `DrawableWidth`,再由 `getDivideIndex` 二分/线性搜索切分位置。半角与全角宽度不同,故每行半角字符数非常数。

唯一的「字符数 ↔ 像素」换算是 `HtmlManager.HtmlSubString` 的 `length * Config.FontSize / 2`(把 `FontSize/2` 当半角宽)与 `Creator.Method.HtmlStringLenMethod` 的反向换算,服务于 ERB 的 `HTML_STRINGLEN`/`HTML_SUBSTRING`/`HTML_STRINGLINES`,与排版换行无关。二者互为逆运算且都随 `FontSize` 线性变化,故 `FontSize` 等比缩放不改变 ERB 语义。

### 字体层

`FontFactory.GetFont` 构造 `new Font(name, Config.FontSize, style, GraphicsUnit.Pixel)` 并按 `(fontName, FontSize, style)` 静态缓存,`Config.DefaultFont` 是该缓存的属性封装。`ClearFont()` 全仓库零调用者。下列消费点不随 `Config.FontSize` 变化而自动跟随:

- `MainWindow` 构造函数对 `richTextBox1.Font` 的一次性赋值;
- `StringMeasure` 的 `fontDisplaySize`/`layoutSize`/`layoutRect`(均 `readonly`);
- `EmueraConsole` 的 `printCWidth*`(惰性计算后不再失效)与 `tooltip_fontsize` 字段初始化;
- `ConsoleStyledString` 构造时捕获的 `Font` 引用(`DivideAt` 分裂时把旧 `Font` 复制给新节点)。

### 高度方向为何已经是动态的

`EmueraConsole.OnPaint` 每次重绘都从 `window.MainPicBox.Height` 重新计算可见行数与起点(`pointY = window.MainPicBox.Height - Config.LineHeight`),`GetLinePointY`、`MoveMouse` 的命中换算同理。这就是「只能拖高、拖高立刻生效」的原因。

### 相关配置现状

`ConfigData.setDefault` 默认值:`WindowX` 760、`WindowY` 480、`FontName` `ＭＳ ゴシック`、`FontSize` 18、`LineHeight` 19、`SizableWindow` true、`TextDrawingMode` `TEXTRENDERER`。

`LineHeight` **不由字体度量推导**,全仓库唯一的字体度量调用在 `GraphicsImage.cs` 的 GDI+ `GraphicsPath.AddString` 路径,与 `Config.LineHeight` 无关;`Config.SetConfig` 只做 `LineHeight >= FontSize` 校验。

用户配置为 `<ExeDir>emuera.config`(UTF-8 BOM),`ConfigData.saveConfig` 按 `setDefault` 顺序**整文件重写**、**静默丢弃未知键**、无表头与版本号;缺失键取默认值。`fixed.config` 的 `Fixed` 使 `ConfigItem<T>.Value` 的 setter 静默 no-op。仓库内不含 `csv/`,`_default.config`/`fixed.config` 均不存在。

窗口位置与尺寸目前**从不回写**:`MainWindow_FormClosing` 只保存按键宏并释放控制台;`WindowPosX/Y` 仅由配置对话框编辑、经 `SetWindowPos` 开关在启动时生效。

## 方案

### 缩放模型

`Config.WindowX`/`WindowY`/`FontSize`/`LineHeight` 的含义不变(配置值的语义,即用户在对话框里编辑的那组数),窗口不做任何 DPI 感知。运行时以本进程启动时的画布宽度为锚点:

```
s            = 当前有效画布宽 / W₀
显示 FontSize = round(FontSize₀ × s)
显示 LineHeight = round(LineHeight₀ × s)
```

`W₀` = 本进程启动时的有效画布宽(即 `MainPicBox.Width`,不含滚动条),`FontSize₀`/`LineHeight₀` = 启动配置值。三者是本次会话的不变量,每次拖放都从锚点重算,故**会话内不累积误差**。

`Config.FontSize`/`Config.LineHeight` 在拖放期间是实时显示值(`FontSize₀ × s`),退出时被回写而成为下次会话的锚点,因此配置文件中存的始终是"锚点",不需要额外字段区分两种角色。`Config.DrawableWidth` 由 `RecomputeGeometry()` 按 `WindowX - max(2, FontSize/6)` 重算,不持久化。

`s` 同时作用于:游戏画布的排版与落笔、`vScrollBar` 宽度、`richTextBox1` 的字号与高度。

**列数保持不变是上述缩放的推论**:`s` 同时缩放 `DrawableWidth` 与字符前进宽,二者相除后断行位置不变。故 `DrawableWidth` 不需要「冻结」,重算即可自洽。

### 拖放行为

- 拖宽:窗口宽高跟光标变化,`s` 按画布宽实时重算,字号/行高/滚动条/输入框同步跟随;
- 拖高:只改变可见行数,字号与列数不变;
- 最大化:保持宽高比等比放大,直到宽或高其一触及显示器工作区(`Screen.WorkingArea`),并同时受现有 `maximamY = 2560` 约束(工作区高度超过 2560 时按 2560 反算宽度以保持比例);
- 重置:Tools 菜单新增一项,把 `s` 复位到 1,即窗口回到 `W₀` 尺寸与启动字号;
- `MinimumSize` 宽度下限取 `128 + vScrollBar.Width`(与 `ConfigDialog` 中 `WindowX` 的既有 `Minimum = 128` 对齐),高度下限沿用现有 `minimamY = max(100, menuHeight + LineHeight × 2)`;删除宽度钉死与 `MaximizedBounds` 的宽度钳制;
- 删除 Tools →「ウィンドウ幅のロック変更」菜单项、`_isWidthLocked` 字段及其处理器 `ウィンドウ幅のロック変更ToolStripMenuItem_Click`(其写回 `ConfigData` 的行为与本票的写回路径冲突)。

### 拖放中的渲染

拖动过程中实时更新廉价量(`s`、`Config.FontSize`、`Config.LineHeight`、窗口几何、滚动条宽度、输入框字号),并按 `s` 对既有版面做临时缩放绘制,松手后按新字号重排一次。

**临时缩放需要 `TextFormatFlags.ApplyTransform`**:默认渲染模式 `TextDrawingMode.TEXTRENDERER` 下,`ConsoleStyledString.DrawTo` 使用 `TextRenderer.DrawText`,该 API 为 GDI 实现且默认**忽略 `Graphics` 的世界变换**。`TextFormatFlags.ApplyTransform` 是 .NET `TextRenderer` 提供的、使 GDI 文本服从该变换的标志;仅在拖动期间把它加进 `DrawTo` 的 flags,即可让文字与背景/图片/形状一起跟随 `Graphics.ScaleTransform`,同时保持矢量清晰、无重排开销。

需要在实现时验证的假设:若该标志实际未让 GDI 文本服从变换(或对 `TextFormatFlags.PreserveGraphicsClipping` 等既有标志有交互影响),回退为**每次窗口尺寸变化即真实重排**——此时文字仍清晰,代价是拖动中每次尺寸变化都要重测节点宽度。回退可行性的依据:节点宽度随字号**线性变化**(实测像素宽 ∝ `Font` 的 em 尺寸;`GRAPHICS` 模式下 `StringMeasure.GetDisplayLength` 的 `fontDisplaySize` 量化亦随字号同步),且比例缩放不改断行位置,故重排只需按新字号重测并重绘,不必重新决定断行。

`LineHeight` 必须实时更新:否则 `EmueraConsole.OnPaint` 会按旧的 `Config.LineHeight` 算出错误的行数(临时缩放路径下为 `s` 倍)。

### 重排

松手后(`ResizeEnd`)按新字号重跑一次排版:断行位置沿用(比例缩放不改断行),仅按新字号重测节点宽度。为此需失效下列既有缓存:

- `StringMeasure` 的 `layoutSize`/`layoutRect`/`fontDisplaySize`(现为 `readonly`,需改为可变或按需持有);
- `EmueraConsole` 的 `printCWidth*`;
- 每个节点的 `Width`/`PointX`/`XsubPixel`;
- `ConsoleDisplayLine.aligned`;
- `ConsoleButtonString.bitmapCache`;
- `ConsoleStyledString` 捕获的 `Font` 引用。

在 `EmueraConsole` 上新增明确的入口(如 `ReLayoutAllLines()`)由 `MainWindow.ResizeEnd` 调用,不复用 `RefreshStrings`,避免与既有「刷新并重绘」语义混淆。

### 派生量的重算

`Config.SetConfig` 只在启动与一处剪贴板开关里被调用,内部含会弹 `Dialog.Show` 的校验(`FontSize < 8`、`LineHeight < FontSize`)。拖放路径不复用它,另抽一个只重算几何派生量的方法(如 `Config.RecomputeGeometry()`),从 `WindowX`/`FontSize` 重算 `DrawableWidth` 与 `DrawingParam_ShapePositionShift`,避免拖拽中弹出模态框打断操作。

### 持久化

`FormClosing` 时把当前 `WindowX`/`WindowY`/`FontSize` 回写 `emuera.config`,下次启动即为配置值(窗口尺寸与字号保持)。不新增任何配置项,不做迁移:旧 `emuera.config` 缺键时取默认值,行为与改动前逐像素一致。

窗口位置**保持原有行为,不回写**(`WindowPosX/Y` 仍只由配置对话框编辑)。

`fixed.config` 若锁定 `WindowX`/`WindowY`/`FontSize`,`SetValue` 静默失败 ⇒ 当前会话内拖放照常生效,仅下次启动回到锁定值。

### 配置对话框

`WindowX`/`WindowY`/`FontSize` 三个数值框维持现状:由 `ConfigDialog.SetConfig` 经 `setNumericUpDown` 从 `ConfigData` 取值,即显示配置值,不自动反映当前窗口几何。取当前值由既有的 `現在のウィンドウサイズを取得` 按钮(`button1_Click`)完成,与 `現在のウィンドウ位置を取得`(`button3_Click`,窗口位置)**行为对称**;窗口位置本就从不自动回写,故两者一并维持。

改 `FontSize` 不动窗口宽度,故下次启动的列数会变化——与今天的行为一致。

因此 `ConfigDialog` 无需改动。

### ERB 兼容

`GETCONFIG`/`GETCONFIGS` 读到的仍是配置基准值(不随拖放变化);`CLIENTWIDTH`/`CLIENTHEIGHT` 继续反映实时画布尺寸。不存在 `SETCONFIG`。`HTML_STRINGLEN`/`HTML_SUBSTRING` 的 `FontSize/2` 单位换算在等比缩放下自洽。

## 曾考虑的替代方案

1. **DPI 作为独立缩放因子**(原 02 方案):`scale = DeviceDpi/96` 乘进 `FontSize`/`LineHeight`/`WindowX`/`WindowY`,并做成「跟随系统/100%/125%」配置项。放弃原因:字号在运行时已由画布宽度派生,再乘一次 DPI 属重复计算;且用户拖放一次即可解决高 DPI 过小问题,不值得为此引入独立机制与配置项。**该目标未放弃,由本文的运行时缩放承接。**
2. **把「基准宽/当前宽」做成两套配置键**:列数跨重启精确不变。放弃原因:需要新增配置项,且配置文件中出现两份窗口宽度易混淆;实测 `max(2, FontSize/6)` 整数边距带来的偏差仅 ±1 字符,已在验收标准内接受。
3. **`DrawableWidth` 在拖放期间冻结**:放弃原因:`DrawableWidth = WindowX - max(2, FontSize/6)` 是像素量且直接作为换行阈值(`PrintStringBuffer.ButtonsToDisplayLines`),冻结会导致一行容纳的字符数随字号放大而减半;正确做法是让它与字号一起按 `s` 缩放。
4. **Ctrl+滚轮缩放**:放弃原因:每行字符数固定,缩放等价于改窗口尺寸,而鼠标已在边框上;滚轮无法提供额外能力,且需处理 `richTextBox1` 抢占滚轮消息的问题。
5. **窗口高度吸附到整行**:放弃原因:高度不参与字号计算,不吸附只会让窗口底边切掉一条不足一行的空白,而滚动到历史位置时顶部本就存在被裁切的一行(`OnPaint` 的 `pointY` 由滚动偏移决定),用户已在接受这一观感,吸附收益为零。
6. **`s` 每次以「上一次拖放结束时的画布宽」为锚点**:放弃原因:整数值 `FontSize` 每次取整会把误差带进下一次拖放并累积;以不变量 `W₀`/`FontSize₀` 为锚点则每次从零重算,无累积。
7. **`FontSize` 不回写、每次从窗口宽派生**:放弃原因:派生需要跨重启可用的参考宽,而窗口尺寸又要回写,二者互相冲突,不新增配置键无法同时满足。
8. **一并消除滚动产生的半截行**:放弃原因:该现象由 `OnPaint` 的滚动偏移造成,与窗口高度无关;消除它需改可见行数公式、`MoveMouse` 命中换算、`GetLinePointY` 与滚动条 `Value` 的粒度语义,并会让滚动出现跳行感。本票只处理「窗口高度导致的半截行」,滚动态维持现状,如需处理另立票。
9. **拖拽中每帧重排全部历史节点**:作为首选方案放弃(改用拖动中临时缩放 + 松手后重排一次),原因是拖动中每次尺寸变化都要重测全部节点宽度,存在卡顿风险;但作为 `TextFormatFlags.ApplyTransform` 不可用时的回退方案保留,见「拖放中的渲染」。

## 验收标准

1. 默认配置下与改动前渲染、布局逐像素一致;
2. 拖宽 ⇒ 字号、行高、画布、滚动条、输入框按 `s` 同步放大,断行位置不变,列数变化在 ±1 字符内;
3. 拖高 ⇒ 只增加可见行数,字号与列数不变;
4. 窗口四边均可拖拽;`MinimumSize`/`MaximumSize` 的宽度钉死与 `MaximizedBounds` 宽度钳制已移除;宽度锁定菜单项与 `_isWidthLocked` 已删除;
5. 最大化按等比放大至工作区或 `maximamY` 约束,保持宽高比;
6. Tools 菜单的重置项可将 `s` 复位到 1;
7. 退出后重启,窗口尺寸与字号保持;
8. 构建通过,点击冒烟:缩放后按钮命中区域与实际绘制位置一致。

## 影响

- 改动范围限于 `Emuera/UI/` 与 `Emuera/Runtime/Config/Config.cs`(`RecomputeGeometry` 抽取),不触及解释器与脚本运行时。
- `Config.WindowX`/`WindowY`/`FontSize` 的语义不变,但运行中会被回写到 `emuera.config`——这是本票新增的写盘行为。
- **已知取舍**:高 DPI 显示器上首次启动仍然偏小,需用户手动拖放一次;拖放结果回写后,后续启动保持。
- **已知限制**:不处理跨显示器 DPI 变化;窗口拖到不同缩放比例的显示器上时 `s` 不重算。
- **待实测风险**:`Reboot()` 为 `Process.Start` 新进程 + `Application.ExitThread()`,重启时新进程可能早于旧进程 `FormClosing` 的写盘完成。若实测复现"重启后拖放结果丢失",把写盘时机改为 `ResizeEnd`。
- **已知限制**:由于 `max(2, FontSize / 6)` 的整数除法边距,`FontSize` 跨过 6 的倍数时 `DrawableWidth` 会跳变 1px,可能导致重启后列数变化 1 字符。
- 拖动过程中画布由 `Graphics` 变换缩放,文字是否随之缩放取决于 `TextFormatFlags.ApplyTransform` 的实际效果;若不生效则回退为拖动中真实重排(文字仍清晰,但可能卡顿)。
