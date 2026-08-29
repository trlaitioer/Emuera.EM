# Nullable:变量与数据链启用

Status: needs-triage
Type: task

## 背景

变量簇自耦合度高:VariableToken.cs 单文件约 2,900 行,含 6 层 token 抽象(VariableToken/CharaVariableToken/UserDefinedVariableToken/UserDefinedCharaVariableToken/ReferenceToken/LocalVariableToken)与约 40 个具体 token,文件尾部还是 partial `VariableData` 的工厂部分;存档读写分居 VariableData.cs 与 CharacterData.cs。PluginSystem 从外部直连 VariableData(PluginManager 引用 70 次),是隐性强边(适配归 issues/12)。

## 范围(启用 + 清零 CS86xx)

- `Runtime/Script/Statements/Variable/` 全部:VariableToken.cs、VariableData.cs、CharacterData.cs、VariableTerm.cs(含 FixedVariableTerm/VariableNoArgTerm)、VariableStrArgTerm.cs、VariableEvaluator.cs、VariableParser.cs、VariableIdentifier.cs、VariableCode.cs、VariableLocal.cs
- `Runtime/Script/Data/`:IdentifierDictionary.cs、UserDefinedVariable.cs、UserDefinedFunction.cs、ConstantData.cs、ParserMediator.cs、DefineMacro.cs
- `Runtime/Utils/`:EraBinaryDataReader.cs、EraBinaryDataWriter.cs、EraDataStream.cs(仅被 Variable 侧三个文件引用)

## 边界契约

- `VariableTerm : AExpression`(表达式链叶)与 `VariableEvaluator`(被 ExpressionMediator、Instraction.Child 消费)的签名:issues/08 需要此处先定
- `IdentifierDictionary.GetVariableToken`/`GetFunctionMethod` 返回契约(被 Process.ScriptProc、ExpressionParser、ArgumentBuilder、Instraction.Child、Creator.Method 等 18 个文件引用)
- 存档读写(EraBinaryData*)对流元素可空性的表达

## 方案

按 spec.md 通用方法逐文件启用;VariableToken.cs 体量大,按 token 族分批。有测试支撑的纯解析部分(见 spec 修复原则)可逐步以真检查替换 `!`。

## 备注

- 建议在 08 之前执行(给表达式链供契约),不强制
- Data/GameBase.cs 与 LabelDictionary.cs 归 issues/11(加载期初始化)

## Comments

### 2026-08-29

- 新建:按调用链重切差分,原 issues/02 的 Variable/、issues/04 的 Data/ 主体与 EraBinaryData* 归入本票。
