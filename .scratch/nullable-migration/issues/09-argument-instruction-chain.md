# Nullable:参数与指令链启用

Status: needs-triage
Type: task
Blocked by: 05, 06

## 背景

参数解析与指令执行是全库耦合最强的区域:

- `ArgumentParser` 是 partial class,两半分居 ArgumentBuilder.cs 与 ArgumentParser.cs,必须同票处理(旧差分曾拆在 01/04 两票)
- Instraction.Child.cs(约 104 条)是全库最大耦合枢纽:InstructionLine ×141、ExpressionMediator ×117,并直接使用 UI 打印类型(ConsoleDisplayLine/ConsoleButtonString)、Sound、Config、Lang
- Argument.cs 约 45 个子类字段大量"按构造路径可能为 null 但未标注"(SpColorArgument.RGB、SpTInputsArgument、SpBarArgument 等);`ArgumentBuilder.CreateArgument` 以 `warn + return null` 表达失败

## 范围(启用 + 清零 CS86xx)

- `Runtime/Script/Statements/`:Argument.cs、ArgumentBuilder.cs、ArgumentParser.cs、Instruction.cs、FunctionIdentifier.cs、Instraction.Child.cs、CaseExpression.cs
- FunctionIdentifier.cs 与 Instraction.Child.cs 是同一 partial 类,必须同批启用

## 前置契约

- issues/05:`RowArgs`(SpCallFArgment/SpCallArgment/SpCallSharpArgment)与 `GetFunctionMethod` 9 个调用点(CALLF/CALLFORM/CALLFORMF/GETMETH 系)元素可空
- issues/06:ExpressionMediator exm、Config/Lang/Sound/UI 打印类型的叶子契约

## 方案

按 spec.md 通用方法逐文件启用。Argument 子类字段逐构造路径核实后标注,拿不准的以 `?` + 使用点判断保行为;`CreateArgument` 返回 null 的失败路径沿 `InstructionLine.Argument` 消费点(各指令的 DoInstruction)贯通。

## 备注

- 建议在 08 之后:指令方法体大量消费表达式链产物(求值/Restructure),先有契约再清本票警告更省
- UI 打印类型、Sound 本身的文件启用在 issues/12;本票只依赖其契约

## Comments

### 2026-08-29

- 新建:按调用链重切差分,原 issues/01 的 ArgumentBuilder.cs 与 issues/04 的 Statements 顶层(Argument、ArgumentParser、Instruction、FunctionIdentifier、Instraction.Child、CaseExpression)归入本票。
