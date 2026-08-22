# CS8981:小写 alias 处理决策

Status: needs-triage
Type: task

## 背景

`trerror` 等小写别名触发 CS8981 警告,共 128 条。

## 现状与决策

- 暂不重命名,避免与 upstream 合并冲突
- 如需消除,优先在 csproj 用 NoWarn 抑制

## 备注

本票本质为决策记录,可能最终采取 NoWarn 抑制(近似 wontfix)或维持现状。

## Comments
