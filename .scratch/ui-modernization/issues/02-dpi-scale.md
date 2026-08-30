# 高分屏 DPI 缩放:画布与字体按系统缩放

Status: needs-triage
Type: task

## 背景

- manifest 与 csproj 均声明 PerMonitorV2,进程不做系统位图拉伸:配置像素 = 物理像素。在 4K/高缩放屏上,`WindowX`(默认 760)与 `FontSize`(`GraphicsUnit.Pixel`)的物理尺寸约为 96DPI 屏的一半,窗口与文字显示过小。
- 现有缓解不足:`SizableWindow` 拖大只增加可见行列,字号不放大;手动调大 config 的 FontSize/LineHeight/WindowX 时,与目标显示效果的换算需自行计算。
- UI 层无任何 DPI 处理代码;本 tracker issues/01(WinForms 层评估)已记录"高 DPI 流式布局改造不立项"的结论——本票不改布局,只做等比缩放。
- 尺寸几乎全部从少量配置值推导:`FontSize`/`LineHeight` → `FontFactory`(含 `Config.DefaultFont`,输入行 richTextBox1 亦用之)→ `StringMeasure` 度量 → `MainWindow.initControlSizeAndLocation` 的 ClientSize/画布。注入点集中。

## 方案

- 缩放比 `scale = DeviceDpi / 96`(取启动时主屏;窗口跨不同 DPI 显示器拖动不重算,记录为已知限制);
- 在配置装载完成后对尺寸类配置统一乘 scale(`FontSize`、`LineHeight`、`WindowX`、`WindowY`;窗口位置 WindowPosX/Y 不缩放,避免小屏上出界),下游字体/度量/画布/按钮命中自动跟随;
- 做成配置项(如 UI 缩放:跟随系统/100%/125%/150%),**默认 100% = 行为与现状逐像素一致**;
- 文字为矢量缩放,高分屏清晰不发糊;菜单栏等 point 基准控件由 WinForms PMv2 自行处理,不在本票范围。

## 验收

- 默认 100%:与改动前渲染/布局逐像素一致;
- 缩放开启:窗口与游戏文字物理尺寸正确放大、清晰,按钮命中区域跟随(点击冒烟);
- 构建通过。

## Comments

### 2026-08-30

- 评估完成:现象、注入点与方案写入正文。Status: needs-triage。
