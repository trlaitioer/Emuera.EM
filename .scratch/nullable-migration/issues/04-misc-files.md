# Nullable:其余文件清尾

Status: needs-triage
Type: task

## 背景

issues/01(核心运行时)与 issues/02(变量/表达式 + UI/Plugin)的范围之外,仍有大量文件存在 CS86xx 警告。临时 `-p:Nullable=enable` 全量构建(2026-08-29,Debug-NAudio)实测:范围外 48 个文件、约 1,820 条警告,约占全库(约 3,526 条、97 个文件)的 52%。本票补齐这一缺口,使 issues/03 的 csproj 收尾可以完整进行。

## 范围

未被 issues/01、issues/02 覆盖的全部源文件,按聚集地列出(标注者为单文件警告较多的位置):

- `Runtime/Script/Data/`:`ConstantData.cs`(166 条)、`IdentifierDictionary.cs`、`LabelDictionary.cs`、`UserDefinedVariable.cs`、`DefineMacro.cs`、`GameBase.cs`、`ParserMediator.cs`、`UserDefinedFunction.cs`
- `Runtime/Script/Parser/`:`LexicalAnalyzer.cs`、`LogicalLineParser.cs`、`SubWord.cs`、`WordCollection.cs`
- `Runtime/Script/Statements/` 顶层:`Instraction.Child.cs`(104 条)、`Argument.cs`(72 条)、`LogicalLine.cs`(64 条)、`ArgumentParser.cs`、`Instruction.cs`、`CaseExpression.cs`、`ExpressionMediator.cs`、`FunctionIdentifier.cs`、`CircularBuffer.cs`、`Clipboard.cs`
- `Runtime/Script/Statements/Function/`:`FunctionMethod.cs`、`UserDefinedMethodTerm.cs`、`UserDefinedRefMethod.cs`
- `Runtime/Utils/`:`EraBinaryDataReader.cs`、`EraBinaryDataWriter.cs`、`EraDataStream.cs`、`EraStreamReader.cs`、`EvilMask/`(`Lang`、`Shape`、`Utils`、`ConsoleDivPart`、`ConsoleEscapedParts`)、`LangManager.cs`、`Preload.cs`、`SFMT.cs`、`Sys.cs`、`Sound.NAudio.cs`、`WebPWrapper.cs`
- 其余:`GlobalStatic.cs`、`Runtime/Config/`(`Config`、`ConfigData`、`ConfigItem`、`JSON/JSONConfig`)、`Runtime/InputRequest.cs`、`Runtime/Script/KeyMacro.cs`、`Runtime/Script/Loader/ErhLoader.cs`

注:完成清单以构建输出为准。声音后端文件按配置互斥编译,以上以 `Debug-NAudio` 实测为准;`Runtime/Utils/Sound.WMP.cs` 需用 VS 的 MSBuild 构建对应配置后另行确认。

## 方案

按 spec.md 的通用方法:文件顶部加 `#nullable enable` → 构建 → 清零该文件 CS86xx。修复原则同 issues/01:优先 `?` 与 `null!` 保持现有行为,少加运行时判断;有测试支撑的组件(`Parser/`、`Data/` 中的纯解析部分)可逐步以真检查替换 `!`。

## 备注

建议在 issues/01、issues/02 之后进行;issues/03 的收尾以本票完成为前提(Blocked by: 01, 02, 04)。

## Comments

### 2026-08-29

- 新建:覆盖 issues/01、issues/02 范围外的 48 个文件(约 1,822 条 CS86xx,实测口径见 spec.md)。
- 更新:贯通试验回退后 `FunctionMethodTerm.cs` 移出范围;计数回到 48 文件/1,820 条(全库 3,526 条)。
