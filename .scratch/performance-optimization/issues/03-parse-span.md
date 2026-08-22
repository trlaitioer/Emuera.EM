# 性能:解析期 Span 化

Status: needs-triage
Type: task

## 背景

解析期字符串切片开销(已评估、未实施)。

## 方案

- `st.Substring()`(12 处)与 `ReadSingleIdentifier` 调用点逐步换 ROS(只读 span)
- `st.Substring().Split(',')` 等需要配套 span 辅助

## Comments
