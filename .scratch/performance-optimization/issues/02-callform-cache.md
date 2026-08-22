# 性能:CALLFORM 动态调用缓存

Status: needs-triage
Type: task

## 背景

`CALLFORM` 等动态函数名调用的函数名解析无缓存(已评估、未实施)。

## 方案

- `CALLFORM` 函数名槽值缓存(`FuncnameTerm.GetStrValue` 共 6 处)
- 缓存粒度 per-callsite,key 用槽值而非字符串

## Comments
