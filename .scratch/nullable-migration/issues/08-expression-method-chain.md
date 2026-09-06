# Nullable:表达式与方法链启用

Status: needs-triage

Blocked by: 05

## 任务

表达式求值与方法调用是全库最大的类型簇:`AExpression` 继承体系(Expression/)与方法体系(Function/ 的 FunctionMethod/FunctionMethodTerm/UserDefined*),含两个大警告文件 Creator.Method.cs(约 336 条)与 ExpressionParser。issues/05 已把 `List<AExpression>` 元素可空契约贯通到方法链签名,本票启用文件并适配方法体。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/Statements/Expression/`:AExpression.cs、Term.cs、ExpressionParser.cs、OperatorMethod.cs、ExpressionMediator.cs
- `Runtime/Script/Statements/Function/`:FunctionMethod.cs、FunctionMethodTerm.cs、UserDefinedMethodTerm.cs、UserDefinedRefMethod.cs、Creator.cs、Creator.Method.cs

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

按 spec.md 通用方法逐文件启用。Creator.Method.cs 体量大(约 200 个内置方法类),建议按方法族分批启用,每批以构建清零为准。

### 前置契约

- issues/05:`List<AExpression>` → `List<AExpression?>` 沿 FunctionMethod 6 个签名 → override(Creator.Method.cs 约 204 处、OperatorMethod.cs 40 处、StrForm.cs 6 处等)→ `GetFunctionMethod` → RowArgs/ReduceArguments 贯通;方法体适配在本票随文件启用进行
- issues/06:ExpressionMediator 的 exm 可空决策;OperatorMethodManager 内 `new FunctionMethodTerm` 的参数契约

### 跨票联动(预期)

- `VariableTerm`/`VariableStrArgTerm`(`AExpression` 子类,定义在 Variable/)的签名契约由 issues/10 承接;本票如需先改其签名,在票内正文记录
- `StrForm`/`StrFormTerm`(issues/01 已启用)的 `GetAExpression()` 契约已定

执行顺序:建议在 07(词法输入契约)与 10(VariableTerm 契约)之后执行,不强制;`FunctionMethod.argumentTypeArray` 已为 `Type[]?`(issues/01 完成)。

## 影响

表达式与方法链的元素可空语义落地到方法体;本链契约确定后,issues/09 的指令方法体适配以其为输入。
