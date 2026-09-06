# 性能:字符串拼接优化

Status: needs-triage

## 任务

字符串拼接性能优化(已评估、未实施)。

## 方案

- `PlusStrStr` 链展平
- 给 `AExpression` 增加 `AppendStrValue` 写入器通道
- 注:`StrForm` 已使用 `DefaultInterpolatedStringHandler`

## 影响

求值路径减少中间字符串分配,求值结果与现有行为一致。
