# Nullable:无保护的 as 转换三处

Status: ready-for-agent

## 任务

`(x as T)!` 的多数位置有前置类型检查（`FunctionMethod` 的实参类型表、`Word.Type` 分派、`VariableCode` 分派等），issues/22 为其补理由注释即可。但有三处没有保护：转换失败时 `as` 返回 null，`!` 只是把编译期警告压掉，运行期在此解引用。本票修正这三处。

### 范围

| 位置 | 为什么无保护 | 触发 |
| --- | --- | --- |
| `Creator.Method.cs` 的 `XmlGetMethod.GetIntValue` | 第一条实参规则把第三个实参声明为 `ArgType.Int`（`OmitStart = 2`），即允许任意整型表达式；`else` 分支恰在该表达式求值为 0 时进入，随后取 `(arguments[2] as VariableTerm)!`，紧随其后的 `.Identifier.GetArray() as string[]` 对整型数组变量同样得 null | `XML_GET(xml, path, 0)` |
| `Creator.Method.cs` 的 `MatchMethod.GetIntValue`（`CMATCH` 注册） | `CMATCH` 的实参规则首项为 `ArgType.Any`，`CheckArgumentType` 因 `Any` 而不做类型约束，首个实参可以是任意表达式 | `CMATCH(0, 1)` |
| `Creator.Method.cs` 的 `InRangeArrayMethod.GetIntValue` | 解析期规则为 `RefInt1D \| AllowConstRef`，确实保证是变量；但该类未设 `HasUniqueRestructure`，`FunctionMethodTerm.Restructure` 会对实参执行 `Restructure`（非 unique 分支就地改写入参列表），常量一维数组变量会被折叠成 `SingleTerm` | `INRANGEARRAY`（角色版 `INRANGECARRAY`）传常量数组变量 |

对照：`MATCH` 与查找函数（注册名 `FINDELEMENT`/`FINDLASTELEMENT`）安全，因为它们的实参规则含 `AllowConstRef` 且以 `UniqueRestructure` 丢弃 `Restructure` 的结果，变量不会被替换。注意 `CMATCH` 走 `MatchMethod(true)`，规则首项是 `ArgType.Any`，不含 `AllowConstRef`。

### 修法约束

三处都**不能**用 `(T)x` 替换：直接转换会把类型不符时的 `NullReferenceException` 变成 `InvalidCastException`；值为 null 时两种写法都在转换后的第一次解引用处抛 `NullReferenceException`（转换本身对 null 不抛）。差异只在「非 null 但类型不符」这一种情形——改它是可观测的行为变更，不是可空迁移的元数据改动。

可选方案，按优先级：

1. 收紧契约：把 `XmlGetMethod` 第三条实参规则由 `ArgType.Int` 改为引用类型（`RefString1D`），把 `CMATCH` 首项由 `ArgType.Any` 改为 `RefAny1D`；由 `CheckArgumentType` 在解析期拒绝。`XmlGetMethod` 的两条规则只在 `OmitStart` 上不同（2/3），且第二条的第三个实参已是 `RefString1D`，收紧第一条后两条会重合，需先定「合并为一条」还是「保留两条的区分」。需确认 eraBasic 侧是否有依赖「传常量的 `CMATCH`」的用法。
2. 补模式匹配分支：`if (arguments[2] is VariableTerm v) { ... } else { throw new CodeEE(...) }`，把断言换成明确诊断。
3. 给 `InRangeArrayMethod` 补 `UniqueRestructure` 或在折叠前拒绝改写实参。

方案 1 最贴合「契约由解析期保证」的既有设计，优先评估其脚本兼容性。

## 方案

1. 逐个写出最小复现（`XML_GET(xml, path, 0)` 需要一个已加载的 XML 变量；`CMATCH(0, 1)` 需注册的匹配目标；`IN_RANGE_ARRAY` 需常量一维数组变量），确认当前行为确为 `NullReferenceException`。
2. 按上表选定修法并记录理由；涉及 eraBasic 语法的兼容性判断需在票内写明依据。
3. 补 `Emuera.Tests` 用例：三种写法各一条，断言得到 `CodeEE` 或得到明确结果，不再是 `NullReferenceException`。

## 影响

- 本票只动三处 `as` 转换及其契约，不触碰 issues/22 的其余 50 处（那些保留 `as`+`!` 并补注释）
- 若采用方案 1，属解析期行为变更：原本能通过解析、执行到该分支才崩的脚本会改为解析报错
