---
id: nullable-migration-26
status: implemented
---

# Nullable:调试监视列表的空表达式解引用 null

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

### 实施结果（2026-10-05）

取值逻辑从 `DebugDialog.getValueString` 抽出为 `Runtime/Script/Statements/DebugWatchExpression.cs` 的 `GetValueString(string, ExpressionMediator)`(缝与 issues/19 的 `MemoryLineEntry` 同法：表单需要 WinForms/STA，不进用例)，表单只留 console 守卫与 `RunERBFromMemory` 开关。

按方案第 1 条落地：`ReduceExpressionTerm` 返回 null 时抛 `CodeEE(trerror.CanNotInterpretedExpression)`，由既有的 `catch (CodeEE)` 转成文本，`term!` 删除。空串仍在方法入口早退为空（「该项尚未填写」），只含空白的表达式走解析错误分支——正是完成标准要的「明确文本」而非静默。

新增用例 4 例（`Emuera.Tests/Runtime/Script/Statements/DebugWatchExpressionTests.cs`）：空白与制表符报 `CanNotInterpretedExpression`、空串为空、`1 + 1` 得 `2`、`(` 得诊断文本。红→绿实测：还原旧行为后空白用例报字符串差异（旧文本为 `System.NullReferenceException: …`）。

验证：`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` CS86xx = 0；`Emuera.Tests` 由 696 条增至 700 条，全绿。

## 影响

- 行为变化仅限调试窗口监视列表的取值路径：现状是显示空引用异常文本，改为解析错误文本
- 触及 `DebugDialog.getValueString`：取值逻辑已抽至新文件 `Emuera/Runtime/Script/Statements/DebugWatchExpression.cs`（与 issues/19 的 `MemoryLineEntry` 同法，表单只留 console 守卫与 `RunERBFromMemory` 开关）；正常表达式的取值路径不变
- 同一函数内 `value.ToString()!`（issues/21 判为 A，`SingleTerm.ToString` 的非空重写）不受影响
