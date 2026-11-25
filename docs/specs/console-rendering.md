# 游戏画布

## 缩放与坐标

[`WindowZoom`](../../Emuera/UI/WindowZoom.cs) 按当前画布宽度相对于启动宽度的比例缩放逻辑字号和行高。`pos`、`rect` 的 100 单位对应当前逻辑 `Config.FontSize` 个像素。[`FontMetrics`](../../Emuera/UI/FontMetrics.cs) 调整渲染字号，使所用字体的全角字前进宽尽量接近 MS Gothic 在同一逻辑字号下的前进宽。

运行期间 `GETCONFIG` 读取配置中的基准值，`CLIENTWIDTH` 和 `CLIENTHEIGHT` 返回当前画布尺寸。启用窗口缩放时，[`MainWindow`](../../Emuera/UI/Framework/Forms/MainWindow.cs) 在退出时将变化后的画布尺寸、逻辑字号和行高写回配置。由 `fixed.config` 固定的项目在下次启动时仍采用固定值。

## 文本度量与绘制

`GRAPHICS` 档以 GDI 前进宽排字，包括纯空白节点，绘字使用 GDI+。默认 `TEXTRENDERER` 档以 GDI 绘字。拖动缩放时使用 GDI+ 绘制预览。
