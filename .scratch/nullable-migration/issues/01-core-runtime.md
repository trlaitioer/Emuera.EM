# Nullable:核心运行时迁移

Status: superseded

## 任务

项目级 `<Nullable>annotations</Nullable>` 已开启(注解全局生效、流分析警告逐文件开启)。本票原覆盖核心运行时文件:

- `Process*.cs`(`Process.cs` 及 `Process.ScriptProc.cs` / `Process.State.cs` / `Process.CalledFunction.cs` / `Process.SystemProc.cs`)
- `ErbLoader.cs`
- `Creator.Method.cs`
- `ArgumentBuilder.cs`

原方案:按 spec.md 的通用方法,文件顶部加 `#nullable enable` → 构建 → 清零该文件 CS86xx。修复原则:优先 `?` 与 `null!` 保持现有行为,少加运行时判断;待有测试后再将 `!` 替换为真检查。

临时 `-p:Nullable=enable` 全量构建(2026-08-29,Debug-NAudio)时,本票范围 8 个文件约 1,140 条 CS86xx(全库约 3,526 条、97 个文件);`ArgumentBuilder.cs`(528 条)与 `Creator.Method.cs`(336 条)为全库最大两处。

## 方案

本票已执行部分的结论保留如下,作为 issues/07~09 的输入。

### StrForm.cs 修订

`Runtime/Script/Data/StrForm.cs` 的 18 处 `null!` 中 11 处已替换,余 7 处为省略参数占位(5 处占位参数 + `second`/`third` 2 处局部变量),贯通票 issues/05 处理。修订要点:

- **实例字段 `strs`/`terms`**:改为构造器注入(`private StrForm(string[], AExpression[])`),`FromWordToken` 结尾直接构造返回
- **静态成员初始化并入 static 构造器**:`Initialize()` 方法与 `Process.Initialize`、`TestBootstrap.Initialize` 中的显式调用点删除;8 个私有静态字段(3 个書式メソッド + 5 个 FunctionMethodTerm)恢复原始形态(去除 `= null!`),由 cctor 一次性赋值
- **NameTarget 等 5 个 FunctionMethodTerm**:触发时点由 CLR 保证(首次静态成员访问前恰好执行一次,见 Microsoft Learn《Static constructors》);过早触发属初始化顺序 bug 且 cctor 不可重入,依赖(`GlobalStatic.VariableData` 于 `Process.Initialize` 的 CSV 読込后生成)已在 cctor 注释中标明;`Ready<T>()` 守卫与 `StrFormNotInitialized` 文案随之移除(未初始化访问不可达)
- **省略参数占位(残留 `null!` ×7)**:基类 `FunctionMethod` 以 `arguments[i] == null` 表达省略,元素可空化需沿 FunctionMethodTerm → override 签名 → RowArgs/ReduceArguments 边界链整体贯通,已回退并归入 issues/05
- **`FunctionMethod.argumentTypeArray`**:声明改为 `Type[]?`,`FormattedStringMethod` 构造器中的 `= null!` 删除

### 关联模块/文件(后续迁移联动)

**调用方(StrForm 的消费者):**
- `Runtime/Script/Statements/Instraction.Child.cs` — 各指令类 `DoInstruction` override 中执行 STRFORM/PRINT 指令时 `StrForm.FromWordToken`
- `Runtime/Script/Statements/ArgumentBuilder.cs` — `FORM_STR_ANY`/`FORM_STR`/`SP_CALL`/`SP_SET`/`SP_INPUTS` 各嵌套 ArgumentBuilder 的 `CreateArgument`,经 `ExpressionParser.ToStrFormTerm` 解析参数
- `Runtime/Script/Statements/Expression/ExpressionParser.cs` — `ToStrFormTerm` 与 `reduceTerm` 中调用 `FromWordToken`
- `Runtime/Script/Statements/Function/Creator.Method.cs` — `StrFormMethod`(STRFORM 内建函数)
- `Runtime/Script/Process.cs` — `Process.Initialize` 调用 `StrForm.Initialize()` 静态字段初始化入口

**承载与输入:**
- `Runtime/Script/Statements/Expression/Term.cs` — `StrFormTerm : AExpression` 持有 StrForm(`Restructure` 中访问)
- `Runtime/Script/Parser/Word.cs` — `StrFormWord`(`FromWordToken` 输入类型)
- `Runtime/Script/Parser/SubWord.cs` — `YenAtSubWord` 构造器持有 StrFormWord
- `Runtime/Script/Parser/LexicalAnalyzer.cs` — `AnalyseFormattedString` 产出 StrFormWord;`Analyse`、`AnalyseYenAt` 处理相关子词

**依赖(StrForm 引用的类型):**
- `Runtime/Script/Statements/Function/FunctionMethodTerm.cs` — `FunctionMethodTerm`(静态字段 NameTarget 等)
- `Runtime/Script/Statements/Function/FunctionMethod.cs` — 基类字段 `argumentTypeArray`(运行时可为 null,`CheckArgumentType` 判空)
- `GlobalStatic.cs` — `VariableData.GetSystemVariableToken`(系统变量)
- `Runtime/Utils/EvilMask/Lang.cs` — `trerror.StrFormUnexpected` 等错误文本

## 影响

差分按调用链重切(见 spec.md 迁移方法),本票按文件目录分摊的模式废止,范围由以下票承接:

- Process*.cs、ErbLoader.cs → issues/11(进程与加载链)
- Creator.Method.cs → issues/08(表达式与方法链);ArgumentBuilder.cs → issues/09(参数与指令链)
- issues/05(方法调用链元素可空)提升为先行契约票

StrForm 修订历史与「关联模块/文件」清单保留于本文,作为 issues/07~09 的输入。
