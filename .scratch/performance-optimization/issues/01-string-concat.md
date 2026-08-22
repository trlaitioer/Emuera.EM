# 性能:字符串拼接优化

Status: needs-triage
Type: task

## 背景

字符串拼接性能优化(已评估、未实施)。

## 方案

- `PlusStrStr` 链展平
- 给 `AExpression` 增加 `AppendStrValue` 写入器通道
- 注:`StrForm` 已使用 `DefaultInterpolatedStringHandler`

## Comments
