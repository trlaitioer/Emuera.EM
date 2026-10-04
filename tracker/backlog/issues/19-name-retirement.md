# 内建函数把名字当行为用：7 个类靠 `switch (Name)` 分派、16 处名字字面量散在实现里

id: backlog-19
Status: needs-triage

## 任务

[issues/18](18-builtin-method-registration.md) 把注册名收敛成类体上的 `[BuiltinMethod("NAME")]` 工厂，但没动「名字是不是行为的一部分」。本票承接该票「曾考虑的替代方案」7 与「影响」节留下的这一笔。

现象（2026-10 实测，口径 = `Emuera/Runtime/Script/Statements/Function/BuiltinMethod/`）：

1. **7 个类按运行期 `switch (Name)` 分派行为**，共 22 个 `case`：`GraphicsStateMethod`(8)、`GraphicsStateStrMethod`(1)、`SpriteStateMethod`(5)、`SpriteSetPosMethod`(2)、`ClientSizeMethod`(2)、`GetKeyStateMethod`(2)、`MousePosMethod`(2)——即同一类注册为多个名字、每名字一个独立实例，实例靠运行期的 `Name` 分派（`Graphics.cs`）。名字漏进 switch 时不会取默认值，而是落到 switch 之后的 `throw new ExeEE(…AbnormalBranch…)`，属**运行到该函数才炸**的路径。
2. **16 处名字以普通字面量出现在实现里**：`funcName = "MAXARRAY"`/`"MAXCARRAY"`/`"GETCONFIG(S)"`、`funcName = isLast ? "FINDLASTELEMENT" : "FINDELEMENT"`、`p.IsArrayRangeValid(…, "SUMARRAY"`/`"MATCH"`/`"INRANGEARRAY"`/`"STRJOIN", …)`、`ParserMediator.Warn(…, "MESSKIP")`、两条 `CodeEE(…, "GETCONFIG(S)")`。
3. `FunctionMethod.Name` 是 `protected` 只读，只在注册层静态构造末尾回填（`Creator.cs` 的 `FunctionMethodCreator` 静态构造）。

完成标准（待评估后细化）：

- 22 处 `case` 所属的 7 个类不再按字符串分派：改用同文件 `Xml*`/`Map*`/`DT*` 族既有的 `Operation` 枚举范式（ctor 携带），或按名字拆成独立类
- 16 处 `funcName`/文案改引该枚举或类常量，不再各写一份字符串
- `Name` 不再靠静态构造末尾回填：`FunctionMethod.Name` 的 setter 已是 `private`（`FunctionMethod.cs`，本票不动它），要退场的是 `FunctionMethodCreator` 静态构造里的 `SetMethodName` 回填循环——名字改为在构造期注入（ctor 携带，或 attribute 工厂传名）
- Gate D（`BuiltinMethodRegistryTests`，现状 7 类 / 22 个 `case` 逐类集合相等）换成等价断言（枚举 ↔ 注册名，或类型 ↔ 注册名），并**删除现在的实现与它的输入**。现状的三个漂移面本次保留、随改造一并清掉：①闸门读 `BuiltinMethod/*.cs` 的源码文本，正则 + 花括号配对不做词法处理（注释/字符串里的花括号与 `case` 文本都可能干扰；漏解析只被「7 类 / 22 case」两个总数兜住，**不按类兜底**，将来换一种分派写法——如 `switch (this.Name)`、`if (Name == …)`——会静默漏检）；②期望值 `ExpectedDispatchClasses/Cases` 的权威写在票面，不是独立 oracle；③该花括号配对是全仓第 3 份同形实现（另有 `builtin-contract.py`、`arg-slots.py`），且 `Emuera.Tests.csproj` 为它把 11 个生产源文件作为 `Content` 拷进测试产物——改造后该 `Content` 一并删除

## 方案

待评估。18 号票已实测的两个候选：① `Operation` 枚举（同文件既有范式，≈ +30 行）；② 按名字拆成 22 个类。两者都要动基类，与 18 号票的注册机制正交。

## 影响

- 这 7 个类今天**没有任何行为用例**（`CheckArgumentTypeEx` 只管实参契约），按名字拆分等于一次无回归网的行为重写 ⇒ 动手前先补行为用例
- 实例数/内存/启动/调用性能收益全为 0（每实例 ~90 B；字符串 `switch` 每次调用 ~10 ns 量级），唯一实质收益是消掉 22 处「字符串即行为」耦合（18 号票的实测结论）
- 名字只要还进错误文案，`Name` 就不能整体删除；本票只把它从**行为分派**里退役
