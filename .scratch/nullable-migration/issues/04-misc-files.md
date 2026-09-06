# Nullable:其余文件清尾

Status: superseded

## 任务

issues/01(核心运行时)与 issues/02 的范围之外,仍有大量文件存在 CS86xx 警告。临时 `-p:Nullable=enable` 全量构建(2026-08-29,Debug-NAudio)实测,范围外 48 个文件、约 1,820 条警告,约占全库(约 3,526 条、97 个文件)的 52%。本票原以"其余文件"收纳箱补齐这一缺口,使 issues/03 的 csproj 收尾可以完整进行。

原范围按聚集地列出(标注者为单文件警告较多的位置):

- `Runtime/Script/Data/`:`ConstantData.cs`(166 条)、`IdentifierDictionary.cs`、`LabelDictionary.cs`、`UserDefinedVariable.cs`、`DefineMacro.cs`、`GameBase.cs`、`ParserMediator.cs`、`UserDefinedFunction.cs`
- `Runtime/Script/Parser/`:`LexicalAnalyzer.cs`、`LogicalLineParser.cs`、`SubWord.cs`、`WordCollection.cs`
- `Runtime/Script/Statements/` 顶层:`Instraction.Child.cs`(104 条)、`Argument.cs`(72 条)、`LogicalLine.cs`(64 条)、`ArgumentParser.cs`、`Instruction.cs`、`CaseExpression.cs`、`ExpressionMediator.cs`、`FunctionIdentifier.cs`、`CircularBuffer.cs`、`Clipboard.cs`
- `Runtime/Script/Statements/Function/`:`FunctionMethod.cs`、`UserDefinedMethodTerm.cs`、`UserDefinedRefMethod.cs`
- `Runtime/Utils/`:`EraBinaryDataReader.cs`、`EraBinaryDataWriter.cs`、`EraDataStream.cs`、`EraStreamReader.cs`、`EvilMask/`(`Lang`、`Shape`、`Utils`、`ConsoleDivPart`、`ConsoleEscapedParts`)、`LangManager.cs`、`Preload.cs`、`SFMT.cs`、`Sys.cs`、`Sound.NAudio.cs`、`WebPWrapper.cs`
- 其余:`GlobalStatic.cs`、`Runtime/Config/`(`Config`、`ConfigData`、`ConfigItem`、`JSON/JSONConfig`)、`Runtime/InputRequest.cs`、`Runtime/Script/KeyMacro.cs`、`Runtime/Script/Loader/ErhLoader.cs`

注:完成清单以构建输出为准。声音后端文件按配置互斥编译,以上以 `Debug-NAudio` 实测为准;`Runtime/Utils/Sound.WMP.cs` 需用 VS 的 MSBuild 构建对应配置后另行确认。

## 方案

原方案:按 spec.md 的通用方法,文件顶部加 `#nullable enable` → 构建 → 清零该文件 CS86xx;修复原则同 issues/01:优先 `?` 与 `null!` 保持现有行为,少加运行时判断;有测试支撑的组件(`Parser/`、`Data/` 中的纯解析部分)可逐步以真检查替换 `!`。

## 影响

差分按调用链重切(见 spec.md 迁移方法),本票"其余文件"收纳箱废止,文件按链簇重新归属:

| 新票 | 承接文件 |
| --- | --- |
| issues/07 | Parser/(LexicalAnalyzer、LogicalLineParser、SubWord、WordCollection)、Statements/LogicalLine.cs、Utils/EraStreamReader.cs |
| issues/08 | Statements/ExpressionMediator.cs、Statements/Function/(FunctionMethod、UserDefinedMethodTerm、UserDefinedRefMethod) |
| issues/09 | Statements 顶层(Argument、ArgumentParser、Instruction、FunctionIdentifier、Instraction.Child、CaseExpression) |
| issues/10 | Data/(ConstantData、IdentifierDictionary、UserDefinedVariable、UserDefinedFunction、ParserMediator、DefineMacro)、Utils/EraBinaryData* |
| issues/11 | Data/(GameBase、LabelDictionary)、Loader/ErhLoader、GlobalStatic、Runtime/InputRequest.cs(暂) |
| issues/12 | Runtime/Config/、Runtime/Script/KeyMacro.cs、Utils/(EvilMask/*、LangManager、Preload、SFMT、Sys、Sound.NAudio、Sound.WMP、WebPWrapper)、Statements/(CircularBuffer、Clipboard) |

文件归属以各链簇票实测警告为准微调;横切契约决策见 issues/06。
