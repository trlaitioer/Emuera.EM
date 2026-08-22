# Nullable:核心运行时迁移

Status: needs-triage
Type: task

## 背景

项目级 `<Nullable>annotations</Nullable>` 已开启(注解全局生效、流分析警告逐文件开启)。本票覆盖核心运行时文件。

## 范围

- `Process*.cs`(`Process.cs` 及 `Process.ScriptProc.cs` / `Process.State.cs` / `Process.CalledFunction.cs` / `Process.SystemProc.cs`)
- `ErbLoader.cs`
- `Creator.Method.cs`
- `ArgumentBuilder.cs`

## 方案

按 spec.md 的通用方法:文件顶部加 `#nullable enable` → 构建 → 清零该文件 CS86xx。修复原则:优先 `?` 与 `null!` 保持现有行为,少加运行时判断;待有测试后再将 `!` 替换为真检查。

## StrForm.cs 迁移现状(已迁移,待完善)

已迁移文件 `Runtime/Script/Data/StrForm.cs`(`#nullable enable`)是当前唯一迁移样例,含 **18 处 `null!`**。按 spec.md 修复原则(“优先 `?`/`null!` 保持行为,等有测试后再把 `!` 替换为真检查”),这些 `null!` 需在相关模块迁移后进一步修订:

- **实例字段(2)**:`strs`、`terms` —— 私有构造 + `FromWordToken` 初始化,可考虑改为构造器注入
- **静态字段(8)**:`formatCurlyBrace/formatPercent/formatYenAt`、`NameTarget/CallnameMaster/CallnamePlayer/NameAssi/CallnameTarget` —— 依赖 `StrForm.Initialize()`(`Process.cs` L182 调用)与系统变量 `TARGET/MASTER/PLAYER/ASSI/NAME/CALLNAME`(`GlobalStatic.VariableData.GetSystemVariableToken`)
- **参数占位(5)**:`[nametarget, null!, null!]` 等 —— 语义为“省略参数”(`FormatPercent.GetStrValue` 运行时判 `arguments[1] == null`),宜改为显式可空参数
- **局部变量(2)**:`second`、`third` —— 分支赋值后无条件入参,语义同“省略参数”
- **继承类字段(1)**:`argumentTypeArray = null!`(`FormattedStringMethod` 构造;`FunctionMethod.argumentTypeArray` 运行时可为 null,`FunctionMethod.cs` L270 判空)

### 关联模块/文件(修订时需联动)

**调用方(StrForm 的消费者):**
- `Runtime/Script/Statements/Instraction.Child.cs`(L208-209)— STRFORM/PRINT 指令执行时 `StrForm.FromWordToken`
- `Runtime/Script/Statements/ArgumentBuilder.cs`(L584-585、689-690、859-860、1147-1148、1293-1294)— 参数解析 `ExpressionParser.ToStrFormTerm`
- `Runtime/Script/Statements/Expression/ExpressionParser.cs`(L179-184、L402)— `ToStrFormTerm` → `FromWordToken`
- `Runtime/Script/Statements/Function/Creator.Method.cs`(L4875-4919)— `StrFormMethod`(STRFORM 内建函数)
- `Runtime/Script/Process.cs`(L182)— `StrForm.Initialize()` 静态字段初始化入口

**承载与输入:**
- `Runtime/Script/Statements/Expression/Term.cs`(L112-121)— `StrFormTerm : AExpression` 持有 StrForm
- `Runtime/Script/Parser/Word.cs`(L85-88)— `StrFormWord`(`FromWordToken` 输入类型)
- `Runtime/Script/Parser/SubWord.cs`(L38-47)— `YenAtSubWord` 持有 StrFormWord
- `Runtime/Script/Parser/LexicalAnalyzer.cs`(L989、1156、1270、1286、1296)— `AnalyseFormattedString` 产出 StrFormWord

**依赖(StrForm 引用的类型):**
- `Runtime/Script/Statements/Function/FunctionMethodTerm.cs` — `FunctionMethodTerm`(静态字段 NameTarget 等)
- `Runtime/Script/Statements/Function/FunctionMethod.cs` — 基类字段 `argumentTypeArray`(L14,运行时可为 null,L270 判空)
- `GlobalStatic.cs` — `VariableData.GetSystemVariableToken`(系统变量)
- `Runtime/Utils/EvilMask/Lang.cs` — `trerror.StrFormUnexpected` 等错误文本

## 备注

- 全量临时 `-p:Nullable=enable` 构建时 CS86xx 约 3,606 条,其中相当部分集中在本票范围

## Comments
