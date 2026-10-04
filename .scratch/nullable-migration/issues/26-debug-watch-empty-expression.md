# Nullable:调试监视列表的空表达式解引用 null

id: nullable-migration-26
Status: ready-for-agent

## 任务

`DebugDialog.getValueString`（调试窗口的监视列表取值）只排除 null 与空串，随后断言解析结果非空：

```csharp
if (string.IsNullOrEmpty(str))
{
    return "";
}
...
AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
SingleTerm value = term!.GetValue(GlobalStatic.EMediator);
```

`ReduceExpressionTerm` 的返回类型就是 `AExpression?`，同族的 `ReduceIntegerTerm` 在拿到 null 时显式 `throw new CodeEE(CanNotInterpretedExpression)`，说明「无法构成表达式」时返回 null 是它的既定语义。于是监视列表里填入**只含空白**的表达式（`" "` 一类，`IsNullOrEmpty` 挡不住）时 `term` 为 null，`term!` 立即 `NullReferenceException`。

该异常被同一 `try` 的 `catch (Exception e)` 兜住并作为文本返回，**不会崩进程**，但监视列表显示 `System.NullReferenceException: ...` 而不是预期的解析错误文本。属于可达但低危的缺陷。

本票为断言审计（issues/21）判定的 **C 类可达缺陷**，对应站点：

- `Emuera/UI/Framework/Forms/DebugDialog.cs#DebugDialog.getValueString#1`

## 方案

比照 `ReduceIntegerTerm` 的写法，对 `ReduceExpressionTerm` 的结果判空后给出明确文本：

1. `getValueString` 内 `term == null` 时直接 `return` 一条解析失败提示（或抛 `CodeEE`，由既有的 `catch (CodeEE)` 转成文本）；
2. 或者把「空白串」也并入前置早退条件（`string.IsNullOrWhiteSpace`），但那样只是把空表达式与空串一并静默，不如（1）能提示用户表达式无效；
3. 随后删掉 `term!`。

完成标准：监视列表输入空白或无法解析的表达式时显示明确的解析错误文本，不再出现 `NullReferenceException`；`Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0。

## 影响

- 行为变化仅限调试窗口监视列表的取值路径：现状是显示空引用异常文本，改为解析错误文本
- 触及 `DebugDialog.getValueString`；正常表达式的取值路径不变
- 同一函数内 `value.ToString()!`（issues/21 判为 A，`SingleTerm.ToString` 的非空重写）不受影响
