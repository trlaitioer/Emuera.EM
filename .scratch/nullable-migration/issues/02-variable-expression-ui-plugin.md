# Nullable:变量/表达式 + UI/Plugin 迁移

Status: needs-triage
Type: task

## 背景

项目级 `<Nullable>annotations</Nullable>` 已开启。本票覆盖核心运行时之外的第二批文件。

## 范围

- `Statements/Variable/`(变量系统)
- `Statements/Expression/`(表达式)
- `UI/`(UI 层)
- `Runtime/Utils/PluginSystem/`(插件系统)

## 方案

按 spec.md 的通用方法:文件顶部加 `#nullable enable` → 构建 → 清零该文件 CS86xx。修复原则同 issues/01。

## 备注

建议在 issues/01(核心运行时)之后进行。

## Comments
