# 方案:UI 层现代化(WinForms / 显示)

Date: 2026-08-30
Status: in-progress

## 问题/背景

UI 代码在 `Emuera/UI/`,由 Framework 时代迁移而来。本 tracker 跟踪 WinForms API 采纳与显示质量(DPI 缩放、字体排版)相关的评估与改造;.NET 运行时/API 的迁移残留类议题不在此列,见 `.scratch/dotnet-modernization/`。

## 整体方案

1. WinForms 层升级评估(issues/01):入口/DPI/异步 API/布局逐项实测,InvokeAsync/ShowDialogAsync/流式布局均建议维持现状
2. 画布与字体缩放(issues/02):PMv2 下配置像素=物理像素,高分屏窗口/文字物理尺寸过小,且宽度被钉死、字号不跟随;改为运行时窗口缩放,画布宽度决定缩放比,字号/行高/窗口尺寸同比缩放以保持每行字符数不变,高度只决定可见行数;不引入 DPI 缩放因子,配置语义不变
3. 等宽网格渲染模式(issues/03):换非网格字体(如 Maple Mono)实测宽度漂移破坏字符数排版;新增字体自适应网格开关(实测半角宽、锁定 2:1),默认关

各项按各自 ticket 验收;决策记录票以文档定案。

## 影响

改动范围限于 `Emuera/UI/` 及显示相关配置(manifest/csproj),不触及解释器与脚本运行时。
