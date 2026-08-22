# Phase 2+:StrForm 与后续

Status: needs-triage
Type: task

## 目标

- `StrForm` 测试:覆盖已迁移文件的 `null!` 语义,为"把 `!` 替换为真检查"铺路(联动模块清单见 `../nullable-migration/issues/01-core-runtime.md`)
- `ExpressionParser`、`VariableParser` 测试
- `LogicalLineParser` 测试(需先解决 `EmueraConsole` STA 线程与 `IdentifierDictionary` 初始化)
- eraBasic 脚本级冒烟测试(复用 `-Debug` 语法分析模式)

## 前置

- 探明 `GlobalStatic.VariableData` / `LangManager` 的最小初始化序列(`StrForm.FromWordToken` 依赖静态字段 `Initialize()` 与系统变量 `TARGET/MASTER/PLAYER/ASSI/NAME/CALLNAME`)

## Comments
