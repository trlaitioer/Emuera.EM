# 方案:UI 层现代化(WinForms / 显示)

Date: 2026-08-30
Status: draft

## 问题/背景

UI 代码在 `Emuera/UI/`,由 Framework 时代迁移而来。本 tracker 跟踪 WinForms API 采纳与显示质量(DPI 缩放、字体排版)相关的评估与改造;.NET 运行时/API 的迁移残留类议题不在此列,见 `.scratch/dotnet-modernization/`。

## 整体方案

1. WinForms 层升级评估(issues/01):入口/DPI/异步 API/布局逐项实测,InvokeAsync/ShowDialogAsync/流式布局均建议维持现状
2. 高分屏 DPI 缩放(issues/02):PMv2 下配置像素=物理像素,高分屏窗口/文字物理尺寸过小;配置装载后按 DeviceDpi/96 统一缩放尺寸类配置,默认 100% 行为不变
3. 等宽网格渲染模式(issues/03):换非网格字体(如 Maple Mono)实测宽度漂移破坏字符数排版;新增字体自适应网格开关(实测半角宽、锁定 2:1),默认关

各项按各自 ticket 验收;决策记录票以文档定案。

## 影响

改动范围限于 `Emuera/UI/` 及显示相关配置(manifest/csproj),不触及解释器与脚本运行时。
