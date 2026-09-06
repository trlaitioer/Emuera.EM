# 高分屏 DPI 缩放:画布与字体按系统缩放

Status: needs-triage

## 任务

高分屏下窗口与游戏文字物理尺寸过小:manifest 与 csproj 均声明 PerMonitorV2,进程不做系统位图拉伸,配置像素 = 物理像素——4K/高缩放屏上 `WindowX`(默认 760)与 `FontSize`(`GraphicsUnit.Pixel`)的物理尺寸约为 96DPI 屏的一半。现有缓解不足:`SizableWindow` 拖大只增加可见行列,字号不放大;手动调大 config 的 FontSize/LineHeight/WindowX 时,与目标显示效果的换算需自行计算。

本票不改布局,只做等比缩放(高 DPI 流式布局改造不立项,见 issues/01 评估结论)。完成标准:默认 100% 与改动前渲染/布局逐像素一致;缩放开启后窗口与游戏文字物理尺寸正确放大、清晰,按钮命中区域跟随(点击冒烟);构建通过。

## 方案

- 尺寸几乎全部从少量配置值推导:`FontSize`/`LineHeight` → `FontFactory`(含 `Config.DefaultFont`,输入行 richTextBox1 亦用之)→ `StringMeasure` 度量 → `MainWindow.initControlSizeAndLocation` 的 ClientSize/画布,注入点集中。
- 缩放比 `scale = DeviceDpi / 96`(取启动时主屏);在配置装载完成后对尺寸类配置统一乘 scale(`FontSize`、`LineHeight`、`WindowX`、`WindowY`;窗口位置 WindowPosX/Y 不缩放,避免小屏上出界),下游字体/度量/画布/按钮命中自动跟随。
- 做成配置项(如 UI 缩放:跟随系统/100%/125%/150%),**默认 100% = 行为与现状逐像素一致**。
- 文字为矢量缩放,高分屏清晰不发糊;菜单栏等 point 基准控件由 WinForms PMv2 自行处理,不在本票范围。

## 影响

- 显示尺寸类配置的语义变化:配置值为 96DPI 基准,装载时按 scale 放大(默认 100% 时无差异)。
- 已知限制:窗口跨不同 DPI 显示器拖动不重算缩放比。
