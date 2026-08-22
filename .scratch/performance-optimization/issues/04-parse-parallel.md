# 性能:解析并行化

Status: needs-triage
Type: task

## 背景

ERB 加载解析目前串行(已评估、未实施)。

## 方案

- `Preload` 已并行读文件;`loadErb` / `ParseScript` 仍串行
- 并行前需先处理 `Depth` 调用图发现顺序,以及 `scaningLine` / `ParserMediator` 等共享状态

## Comments
