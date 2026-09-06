# CS8981:小写 alias 处理决策

Status: wontfix

## 任务

`trerror` 等小写别名触发 CS8981 警告,共 128 条。本票为决策记录:是否重命名消除。

## 方案

维持现状,不重命名,避免与 upstream 合并冲突;如警告成为干扰,再在 csproj 用 NoWarn 抑制。

## 影响

无代码改动;CS8981 警告维持现状。
