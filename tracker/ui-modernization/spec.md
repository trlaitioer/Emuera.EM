# 方案:UI 层现代化(WinForms / 显示)

Date: 2026-08-30
Status: in-progress

## 问题/背景

UI 代码在 `Emuera/UI/`,由 Framework 时代迁移而来。本 tracker 跟踪 WinForms API 采纳与显示质量(DPI 缩放、字体排版)相关的评估与改造;.NET 运行时/API 的迁移残留类议题不在此列,见 `.scratch/dotnet-modernization/`。

## 整体方案

1. WinForms 层升级评估(issues/01):入口/DPI/异步 API/布局逐项实测,InvokeAsync/ShowDialogAsync/流式布局均建议维持现状
2. 画布与字体缩放(issues/02):PMv2 下配置像素=物理像素,高分屏窗口/文字物理尺寸过小,且宽度被钉死、字号不跟随;改为运行时窗口缩放,画布宽度决定缩放比,字号/行高/窗口尺寸同比缩放以保持每行字符数不变,高度只决定可见行数;不引入 DPI 缩放因子,配置语义不变
3. 字体单元格宽与 em 的相对关系(issues/03):`pos`/`rect` 等定位原语的单位是 em(`100 单位 = FontSize` px),而字体的全角前进宽不随 `FontSize` 走——MS Gothic 恰为 1.0 em,Maple Mono 为 1.14–1.22 em,同一 em 尺寸的面板因此装不下同样多的全角字符,列布局错位;改为启动时归一化渲染字号使全角单元格回到 1.0 em,并在配置对话框的字体大小标签标注单位 px
4. GDI+ 文字绘制档(issues/04):记录画布 GDI/GDI+ 分工现状与该档缺陷;根因是该档以 `MeasureCharacterRanges` 的墨迹宽配合 `FontSize/2*1.04` 量化式反推前进宽,量的对象错且只有 MS Gothic 成立(实测 Maple Mono 逐字高估 27%),并伴随 region 泄漏、em 框原点与 `GenericDefault` padding 造成的累积偏移;改为**度量走 GDI、绘制走 GDI+**(GDI+ 的 `MeasureString` 会把只含空格的节点量成 0 宽,或附带逐次调用的 padding,两者都无法支撑逐节点布局),并归零该档的 `ShapePositionShift`、统一绘制 helper(含 `scalePreview`);本票修复缺陷使该档成为受支持路径,是否把默认档切到 `GRAPHICS` 修完后另定

各项按各自 ticket 验收;决策记录票以文档定案。

## 影响

改动范围限于 `Emuera/UI/`、显示相关配置(manifest/csproj)与显示派生量,不改变解释器与脚本运行时的语义(字体归一化只影响渲染,配置项与 `GETCONFIG` 读到的是基准值)。
