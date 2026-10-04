# 渲染字号反查:全角单元格宽对齐默认字体,em 基准保持不变

id: ui-modernization-03
Status: implemented

## 任务

换用非默认字体度量的字体(实测对象 **JetBrains Maple Mono**)后,同一个 `FontSize` 下的全角单元格比默认字体宽,列数变少、断行前移,并放大既有的版面拥挤与重叠。

本票的做法是**只反查一个渲染字号**:

```
RefCell          = advance_全角(默认字体, Config.FontSize)                  # 参照格宽
RenderFontSizePx = argmin_{1 ≤ n ≤ 2 × max(Config.FontSize, RefCell)}
                   |advance_全角(目标字体, n) - RefCell|                     # 等距取更窄的一侧
```

字体构造改用该派生量;`em` 基准保持现状的 `Config.FontSize` 不变(即 `100 em = Config.FontSize px`,仓库现状如此)。

本票承担:

1. 给出反查规则、可命中性与不可命中时的取舍;
2. 明确 `em` 基准**不**改为派生量,只派生字体构造用的字号;
3. 记入实测数据与判据;
4. 记入三处容易遗漏的改动点(启动顺序、输入框字号、度量缓存)。

完成标准见「方案 · 验收标准」。

## 方案

### 问题/背景

#### 单元格宽由字体决定,而 em 基准由 `Config.FontSize` 决定

版面几何统一写死读 `Config.FontSize`(形如 `num * Config.FontSize / 100`),共 14 个换算点:

| 位置 | 换算 |
| --- | --- |
| `Emuera/Runtime/Utils/EvilMask/Utils.cs` 的 `MixedNum.ToPixel`/`ToPixelf`(4 处)与 `ToString`/`BuilderString` | `num * Config.FontSize / 100`,供 `<div rect>`/`<img>`/`<shape>`/`<button pos>` 共用 |
| `Emuera/UI/Game/ConsoleButtonString.cs` 的 `LockPointX` | `rel_px * Config.FontSize / 100` |
| `Emuera/UI/Game/ConsoleImagePart.cs` 的 `computeGeometry`(宽度、`%` 高度、`top`) | `* Config.FontSize / 100` |
| `Emuera/UI/Game/ConsoleShapePart.cs` 的 `ConsoleSpacePart.computeGeometry` | `* Config.FontSize / 100f` |
| `Emuera/UI/Game/HtmlManager.cs` 的 `HtmlSubString` | `length * Config.FontSize / 2` |
| `Emuera/Runtime/Script/Statements/Function/Creator.Method.cs` 的 `HTML_STRINGLEN` 系 | `2 * len / Config.FontSize` |

于是「脚本写死的 em 位置」与「实际单元格」脱节:脚本 `rect='5500, 94, 4312, 2537'` 之类的坐标假定 1 个全角格 = `Config.FontSize` px,而比默认字体宽的字体会让同一个 em 面板装不下同样多的字,断行位置整体前移。

#### 全角单元格与默认字体对齐即可消除字体间差异

左菜单(实测用例 `ERB\起床前メニュー関連処理\起床前メニュー.ERB`)每行最多三列,靠全角空格填充到固定列宽;右侧提示面板由 `rect='5500, 94, 4312, 2537'` + `padding='50,25,50'` + `border='31'` 决定,面板左边界 = `5500 em`,即 55 个全角格。

只要目标字体的单元格宽与默认字体相同,左菜单与面板的像素落点就逐格一致,字体带来的额外重叠随之消失。

#### 实测数据

度量与排版同源:GDI `TextRenderer.MeasureText` + `TextFormatFlags.NoPadding`(`StringMeasure.GetDisplayLength` 的默认档路径),字体按 `new Font(族名, 字号, FontStyle.Regular, GraphicsUnit.Pixel)` 构造(与 `FontFactory` 同路径),探测字符 U+3042。

「字号 → 全角前进宽」(px):

| 字号 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 22 | 24 | 26 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ＭＳ ゴシック | 8 | 10 | 10 | 12 | 14 | 14 | 14 | 16 | 16 | 18 | 18 | 20 | 20 | 22 | 26 | 26 |
| JetBrains Maple Mono | 10 | 10 | 12 | 14 | 14 | 16 | 16 | 18 | 20 | 20 | 22 | 22 | 24 | 26 | 28 | 32 |
| Sarasa Gothic J | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 22 | 24 | 26 |
| Yu Gothic | 8 | 9 | 10 | 11 | 13 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 20 | 22 | 25 | 26 |
| MS PGothic | 8 | 8 | 9 | 10 | 12 | 12 | 13 | 14 | 15 | 16 | 17 | 18 | 19 | 21 | 24 | 24 |
| MS UI Gothic | 7 | 7 | 8 | 9 | 11 | 11 | 11 | 12 | 13 | 14 | 15 | 16 | 16 | 18 | 20 | 21 |

由该表得出、并直接进入判据的四条事实:

1. **默认字体不是「偶数档前进宽 == 字号」**:实测 12 得 14、24 得 26;奇数档取上偏的一档(17 得 18)。其前进宽只取偶数值;
2. **目标字体的全角前进宽 ≈ 1.2 × 字号**并量化到偶数值(半角 ≈ 0.6 × 字号,与该字体基于 JetBrains Mono 的 0.6 em 前进宽一致):`FontSize = 18` 下全角 22 px、半角 11 px。这就是本票现象的来源;
3. **全角拉丁 `Ａ` 与 CJK 全角 `あ` 不是同一个量**(实测 Maple `FontSize = 18`:18 px vs 22 px),探测字符必须取 CJK 全角;
4. 前进宽只取离散值,且具体数值只是度量快照,**不可作为断言**,判据只能用不变量表达。

#### 行槽由 `LineHeight` 决定,与字体无关

`LineHeight` 在绘制里只有一个作用:**行距**。它不是从字体度量推导出来的,也不随字体变化:

- 垂直步进:`EmueraConsole.OnPaint` 的 `pointY += Config.LineHeight`、`GetLinePointY`、`LayoutCanvasHeight / Config.LineHeight`(可见行数)、`ConsoleDivPart.DrawTo` 对其子节点(即 `<div>` 内的子行排布)、`ConsoleDivPart.GetPointingButton` 的 `relPointY += Config.LineHeight`,行距、行数与 div 内的行排布全部只由 `LineHeight` 决定;
- 槽内对齐:文字按**槽顶对齐**绘制(`ConsoleStyledString.DrawTo` 的 `TextRenderer.DrawText(..., new Point(PointX, pointY), ...)` 与 `GraphicsTextRenderer.Draw(..., pointY)`),没有垂直居中,也没有按行裁剪;
- 行盒判定:`ConsoleButtonString.FilterEscaped` 用 `Top < 0 || Bottom > Config.LineHeight` 判定部件是否越出行槽,而文本节点的 `AConsoleDisplayNode.Bottom` 取基准 `Config.FontSize`(≤ `LineHeight`),故文本节点不参与该判定;
- 高度裁剪只有两处,且都不以"行"为单位:按钮的位图缓存路径(`ConsoleButtonString.DrawTo` 在 `ParentLine.bitmapCacheEnabled && strArray.Length > 1` 时把该按钮渲染到 `new Bitmap(Width + 1, Config.LineHeight)` 并带 `PreserveGraphicsClipping`,按 `LineHeight` 裁)与 `<div>`(`ConsoleDivPart.DrawTo` 用 `graph.SetClip(rect)` 把子节点裁到 div 的 border/padding 之内)。其余路径的墨迹**不会被裁**——行背景刷新(`ConsoleDisplayLine.Clear`)已无调用者,画布只在每帧开头整体清底一次,超出槽底的墨迹会与下一行叠墨;
- 配置层只在**基准字号**上钳制:`Config.SetConfig` 的 `LineHeight < FontSize → LineHeight = FontSize` 与 `ApplyWindowGeometry` 的 `Math.Max(FontSize, lineHeight)` 都取 `FontSize`,派生字号不受该钳制;
- 测量盒高(`StringMeasure.RefreshLayout` 的 `layoutSize`)也取 `LineHeight`,单行测量下它只约束换行,宽度结果不受影响。

### 反查规则

- **参照量取默认字体的实测格宽,不取 `Config.FontSize`**:后者是"脚本假定的格宽",而默认字体自身并不总等于它(事实 1)。参照字体取仓库默认配置的 `ＭＳ ゴシック`(即 `ConfigData` 中 `ConfigCode.FontName` 的默认值),**不取 `Config.FontName`**——后者是用户可改的当前字体,拿它做参照会让反查退化成「自己与自己比」的恒等变换,对任何字体都不产生效果;参照字体不可度量时(未安装)回落 `RefCell = Config.FontSize`;
- **目标可达时误差为 0**:实测参照格宽在 8–30 全档上都是偶数值,而各候选字体在这些值上普遍可达,故正常情况下命中精确格宽;
- **两侧等距时取更窄的一侧**:单元格偏宽会让同一行内容占用更多像素、更容易越过面板的 em 边界(见下节),取窄侧与该方向一致;
- 按**整数**枚举,不用二分求出的小数解再取整(前进宽只取离散值,小数解不是「达到该格宽的整数下界」);
- 最小误差超过 1 px 时按「该字体无法复现基准版面」处理,直接返回 `Config.FontSize`(不做归一化),避免为追一个达不到的格宽把字号放大到荒谬档位。实测各候选字体的最小误差 ∈ {0, 1} px;
- 度量与排版同源,字体构造路径与 `FontFactory` 一致(先 `GlobalStatic.Pfc` 再族名),`GraphicsUnit.Pixel`。

该规则的可判定性质只有两条,**默认字体恒等**是其中最重要的:

- `RefCell` 与候选值取自同一字体的同一次度量,`n = Config.FontSize` 处误差为 0,故默认字体在**任意**字号(奇数档同样)下 `RenderFontSizePx == Config.FontSize`,单元格逐像素不变。这不是特判,而是构造性质:以 `Config.FontSize` 为参照量、或用「不小于配置值」的不等式来保护配置值,都做不到这一点(见「曾考虑的替代方案」第 2、3 条);
- 其他字体与默认字体逐档同宽,实测例外的档位各差 1 px(见「影响」)。

### 为什么参照量必须是默认字体的实测格宽

判据要同时满足两件事:默认字体零改动、字体之间逐档一致。参照量取 `Config.FontSize` 时二者不可兼得:默认字体自身在 12、24 档超出配置值(14、26),任何"误差最小"规则都会把它在这两档改窄 2 px,奇数档还会额外改 11 档——实测 `FontSize` 8–30 共 23 档中有 13 档被改动;而且它把各字体对齐到"配置字号"而非彼此,能把每个整数值都取到的 `Sarasa Gothic J` 在奇数档仍与默认字体差 2 px。

实测 `FontSize = 18`(默认档)下各规则的单元格:

| 字体 | 现状 | 「≥配置值」规则 | 「参照配置值」规则 | 本票规则(渲染字号) |
| --- | --- | --- | --- | --- |
| ＭＳ ゴシック | 18 | 18 | 18 | 18(18) |
| JetBrains Maple Mono | **22** | **22** | 18 | **18**(15) |
| Sarasa Gothic J | 18 | 18 | 18 | 18(18) |
| Yu Gothic | 18 | 18 | 18 | 18(18) |
| MS PGothic | 17 | 18 | 18 | 18(19) |
| MS UI Gothic | 15 | 18 | 18 | 18(22) |

对 `FontSize` 8–30 共 23 档,与"默认字体当前单元格"不一致的档位数:

| 字体 | 现状 | 「≥配置值」规则 | 「参照配置值」规则 | 本票规则 |
| --- | --- | --- | --- | --- |
| ＭＳ ゴシック | 0 | 0 | **13** | 0 |
| JetBrains Maple Mono | 21 | **21** | 13 | 0 |
| Sarasa Gothic J | 13 | 13 | 13 | 0 |
| Yu Gothic | 13 | 13 | 13 | 2 |
| MS PGothic | 22 | 11 | 13 | 0 |
| MS UI Gothic | 23 | 14 | 14 | 2 |

### em 基准**不**改为派生量

反查只产出「用多大字号构造字体」,`em → 像素` 的 14 个换算点继续读 `Config.FontSize`。理由:

1. em 基准是脚本可见的契约(`pos`/`rect` 的 em 值由脚本写死),它必须与默认字体一致才能让同一份脚本得到同一份版面;
2. 若把 em 基准也改成派生量,面板、缩进、图片尺寸会随渲染字号同比例缩小,容字量随之回到原值,反查的收益被抵消(实测:配置 18 时面板 `4312 em` 按 18 计为 776 px、容 43 格;若按渲染字号 15 计则为 647 px、容 36 格);
3. 该做法使面板左边界、面板尺寸、行槽在与默认字体之间完全一致,是与默认字体对齐的关键。

### 改动点

1. 新增 `Emuera/UI/FontMetrics.cs`:上面的反查。字体名变更需要重启,故缓存只需覆盖**当前字体**:把当前字体的前进宽曲线按字号记忆、按需向上扩展即可,不需要按字体族建表,也不需要失效逻辑;参照格宽在基准字号处量一次(当前字体就是参照字体时与该曲线共用同一份度量);
2. `Emuera/Runtime/Config/Config.cs`:新增只读派生量 `RenderFontSizePx`;`recomputeGeometry()` 末尾重算。现有两条触发路径都被覆盖:02 的运行时缩放走 `ApplyWindowGeometry` → `recomputeGeometry`,配置装载走 `Config.SetConfig` → `recomputeGeometry`;字体名变更需要重启,重启后仍走 `SetConfig`,故覆盖是完整的;
3. `Emuera/UI/FontFactory.cs`:字体构造与缓存键改用派生字号——配置字体取 `Config.RenderFontSizePx`;脚本用 `<font face>`/`FONTFACE` 指名到别的字族时按该族自己反查一次,否则它会以配置字体的派生字号构造,单元格既不是基准格宽也不等于配置字体的格宽。反查只量前进宽、不建字体对象,按 (字族, 字号) 记忆;脚本线程与窗口线程都可能触发,故度量与探针设备上下文加锁;
4. `Emuera/Program.cs` 的 `Main`:把 `EE_フォントファイル対応` 的 PFC 装载块移到 `ConfigData.Instance.LoadConfig()` 之前——`LoadConfig` 内部经 `Config.SetConfig` → `recomputeGeometry` 触发反查,而此时 `font/` 目录尚未装入 `GlobalStatic.Pfc`,偏偏 `FontFactory` 优先取 PFC 族,于是反查量到的是系统替代字体、渲染用的却是 PFC 字体。`SetDirPaths` 已在更早处调用,`FontDir` 此时可用;
5. `Emuera/UI/Framework/Forms/MainWindow.cs` 的 `SetInputFont`:入参仍是基准字号(缩放预览与提交都经它,预览时 `Config.FontSize` 尚未更新),在其内部反查派生量后再构造字体;
6. `Emuera/UI/Game/EmueraConsole.cs`:`tooltip_fontsize` 初值取 `RenderFontSizePx`(ERB 的 `TOOLTIPFONTSIZE` 仍按 px 覆盖,属脚本显式选择);`MainWindow` 的 0.6 倍提示字号从 `Config.DefaultFont` 派生,自动跟随;
7. **不改动**:上文 14 个 em → 像素换算点、`Config.FontSize`(配置对话框、`ConfigData`、存盘、`GETCONFIG`/`GETCONFIGS` 的语义)、ERB 的 `GETFONT`/`GETTOOLTIPFONT` 与 `Config.SetConfig` 的校验(仍返回/校验基准值,派生量不出现在脚本可见的接口上)、`Config.LineHeight` 与全部行槽/垂直几何(`AConsoleDisplayNode.Bottom`、`ConsoleStyledString.DrawTo` 的聚焦底色高度、`ConsoleShapePart` 的单行矩形高、`ConsoleImagePart` 无尺寸分支、`DrawingParam_ShapePositionShift`、`DrawableWidth`)、`StringMeasure`。

### 与 issues/02、04 的接口

1. **缩放链路保持整型**:`WindowZoom.Compute` 求出的基准字号、`MainWindow.computeZoomGeometry`/`CommitZoom` 的 `out int fontSize`、`SetInputFont(int)`、`Config.ApplyWindowGeometry` 的 `fontSize` 参数、`Config.FontSize` 都无需改为小数;归一化发生在基准字号确定之后,`RenderFontSizePx` 也在整数域,列数在缩放过程中保持不变;
2. **写回路径不受影响**:`SaveWindowGeometry` 写回的是基准值 `Config.FontSize`,`RenderFontSizePx` 从不落盘,不存在"每次重启字号递减";不新增配置项;
3. **两档度量同源**:04 把默认档与 `GRAPHICS` 档的度量统一为 GDI 前进宽后,反查无论走哪个档都基于同一数值,不需要按档分别标定;
4. **两档的 `DrawableWidth` 仍不同**(默认档 `WindowX - max(2, FontSize / 6)`、`GRAPHICS` 档 `WindowX`),同一字体在两档下列数可能差一个字符,验收需分档记录。

### 配置对话框的标注

`Config.FontSize` 同时是「em 基准」(100 单位 = 1 em)与「目标全角格宽」,归一化后实际渲染字号可能与它不同。在字体大小标签(`Emuera/UI/Framework/Forms/ConfigDialog.cs` 的 `label8`,文案取自 `Lang.UI.ConfigDialog.Font.FontSize`)上标注单位 px 即可消除该值同时表达两种含义带来的误解。配置项本身仍是整数,存盘与 `GETCONFIG` 的语义不变。

### 验收标准

1. 默认字体(实测 `ＭＳ ゴシック`)在**任意** `FontSize` 下 `RenderFontSizePx == Config.FontSize`,单元格与现状逐像素一致(构造性质,不靠特判);
2. 任意目标字体归一化后的全角单元格与默认字体单元格相差不超过 1 px(实测 `Yu Gothic` 取不到 12/24、`MS UI Gothic` 取不到 10,各 2 档差 1 px,其余字体 0 档);两侧等距时取更窄的一侧;
3. **em 基准不变**:同一 `FontSize` 下,面板左边界、面板尺寸、行槽高度与字体无关;纯 CJK 全角文本的每行全角字符数与默认字体一致(实测 `FontSize` 18、样例面板 `4312 em`:两者同为 43 格);
4. 反查与绘制用同一字体:反查按 `FontFactory` 的构造路径取字体(先 `GlobalStatic.Pfc` 再族名),`font/` 目录字体在反查时已装载;
5. 单元测试只断言不变量(第 1、2 条),不断言具体字号——同一字体在同一字号上的前进宽只是度量快照;
6. 构建通过(警告增量为 0),`dotnet test` 通过;
7. 端到端冒烟(带游戏数据):换字体后左菜单与右侧面板的相对关系与默认字体一致;`pos` 定位按钮、图片/形状的落点与默认字体一致;窄字体放大方向的墨迹越界按「影响」中的已知限制处理。本仓库不含 `csv/`+`erb/`,该步在带游戏数据的目录执行。

## 曾考虑的替代方案

1. **归一化渲染字号,并把 em 基准改为派生量**:放弃原因:em 基准随派生字号同比缩小,面板与单元格同比例变小,容字量回到原值(实测 `4312 em` 面板:按配置值 18 计为 776 px/43 格,按渲染字号计为 647 px/36 格),反查的收益被抵消;
2. **判据取「`advance_全角(Config.FontSize) >= Config.FontSize` 时不动,否则向上取首个达标字号」**:放弃原因:该不等式对**宽**字体恒成立(其单元格本就宽于配置值),规则直接取配置值,即本票要修的现象它不解决——实测 JetBrains Maple Mono 在 8–30 全部 23 档上与原样取配置值逐值相同;对窄字体虽会放大字号,但配合第 1 条的 em 基准缩放,容字量几乎不变;
3. **参照量取 `Config.FontSize`**:放弃原因:默认字体自身的格宽并不等于配置值,任何「误差最小」规则都会把它在这些档位改窄——实测 23 档中有 13 档(全部奇数档 + 12 + 24)被改动,违反本票「默认字体零改动」的目标;且它把各字体对齐到配置字号而非彼此,能把每个整数值都取到的 `Sarasa Gothic J` 在奇数档仍与默认字体差 2 px;
4. **按字符分类 × 单元宽重写 `GetDisplayLength` 与落笔前进**(锁定半角:全角 = 1:2):放弃原因:两字体的半角:全角前进宽本就精确 2:1,该式在常见文本上不改变任何落笔位置,而 CJK 全角前进宽仍大于配置值,同一个 em 面板装不下的字数不变;且制表符、几何图形、箭头、带圈数字、全角英数等常用符号的自然前进宽不落在 {半角, 全角} 上,硬锁会引入叠墨;
5. **只允许缩小**(参照量与本文相同,但限制 `RenderFontSizePx ≤ Config.FontSize`):未采纳,留作退路。理由:窄字体(`MS PGothic`、`MS UI Gothic`)会保持现状的窄格,列数仍与默认字体不同,即本票目标对这些字体不达成;换来的是字形绝不放大。实测放大方向的越界只出现在大字号档案(`MS UI Gothic` 在 `FontSize` 18 下渲染 22、墨迹高 17 px,仍在默认 `LineHeight` 19 之内;`FontSize` 30 档下渲染 37、墨迹高 33 px,超出 `LineHeight` 30),而该档位提高 `LineHeight` 即可规避;
6. **移动面板(改 `rect` 的 x 或宽度)/ 修改游戏脚本的 em 值**:放弃原因:脚本不在本仓库;改后换回默认字体会反向错位,需为每种字体维护一套 em 值;
7. **在解释器侧检测「面板左边界 < 已绘制内容右缘」并换行**:放弃原因:需要为 `<div>` 引入「按内容流后推」的语义,影响面超出本票,留作后续单独议题;
8. **按列数反解 em 基准**(自动适配窗口):放弃原因:保住列语义,但字面尺寸由窗口与列数决定,配置值与渲染字号不再线性对应,面板几何也不再与默认字体一致;
9. **逐字符下发 `TextRenderer.DrawText` 以强制落笔落在网格上**:放弃原因:落笔前进已是单元格宽的整数倍,逐字符下发不带来对齐收益,而单节点绘制成本按字符数上升。

## 影响

- 改动范围见「方案 · 改动点」;`Config.FontSize`、`ConfigData`、配置文件、`GETCONFIG`/`GETCONFIGS` 与 `pos`/`rect` 的 em 语义均不变,不新增配置项;
- 实测数据(默认档、`FontSize = 18`):

  | 字体 | 现状单元格 | 渲染字号 | 归一化后单元格 |
  | --- | --- | --- | --- |
  | ＭＳ ゴシック | 18 | 18 | 18 |
  | JetBrains Maple Mono | 22 | 15 | 18 |
  | Sarasa Gothic J | 18 | 18 | 18 |
  | Yu Gothic | 18 | 18 | 18 |
  | MS PGothic | 17 | 19 | 18 |
  | MS UI Gothic | 15 | 22 | 18 |

- 已知取舍:单元格宽与字面高是两个独立量,对齐单元格后**字形大小仍随字体变化**(实测 `FontSize = 18`:JetBrains Maple Mono 渲染 15、墨迹高 13 px;默认字体渲染 18、墨迹高 16 px),该观感差异无法通过本票消除;
- 已知限制:窄字体需放大字号才能填满单元格,墨迹随之变高。行槽只由 `LineHeight` 决定,配置层的钳制也只针对基准字号,故派生字号大于 `LineHeight` 时墨迹会越出槽底并与下一行**叠墨**(按槽顶对齐绘制,且行背景刷新路径已无调用者,不会把越界部分裁掉或抹掉,见「方案 · 问题/背景 · 行槽由 `LineHeight` 决定」);实测 `MS UI Gothic` 在 `FontSize` 18(`LineHeight` 19)下渲染 22、墨迹高 17 px,仍在槽内;`FontSize` 30(`LineHeight` 30)下渲染 37、墨迹高 33 px,越出 3 px。反方向(宽字体缩小字号)没有该风险:字形变小且仍按槽顶对齐,只是行间留白增加。该档位由 `LineHeight` 单独决定行槽高度,故提高 `LineHeight` 即可规避;若要让字形本身不越界,须改用「只允许缩小」(见「曾考虑的替代方案」第 5 条);
- 已知限制:参照格宽与该字体的可达格宽不一致时最多差 1 px(实测 `Yu Gothic`、`MS UI Gothic`)。
