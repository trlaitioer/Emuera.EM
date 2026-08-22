# Phase 1 测试用例

Status: needs-triage
Type: task

## 现状(已完成,全绿确认)

基础用例 21 个 + FORM/表达式/常用函数扩展用例 17 个,总计 38 例。测试文件按被测源码路径镜像组织(相对 `Emuera.Tests/`,见 README):
- `Runtime/Script/Parser/LexicalAnalyzerTests.cs`(19 例,基础 11 + FORM 字符串构造 8)
- `Runtime/Utils/EncodingHandlerTests.cs`(4 例)
- `Runtime/Script/Statements/Function/FunctionMethodTests.cs`(11 例,基础 6 + 其他常用函数 5)
- `Runtime/Script/Statements/Expression/ExpressionParserTests.cs`(4 例,表达式解析)

基础用例覆盖:标识符/整数/运算符、字符串(双引号 + 单引号两种 flag 语义)、`;` 行注释、括号、`LexEndWith.Percent` 终止、错误用例;`TOUPPER`/`TOLOWER`/`ABS`/`MAX`/`MIN`/`SQRT`;编码检测。

## 测试中发现的行为事实

- `AnalyzePrintV`(PRINTV 系):`'` 开头字符串直至逗号或行尾,**无闭合引号概念**(`'hi'` → `"hi'"`,结尾 `'` 属于内容)
- `AllowSingleQuotationStr`(HTML_PRINT 系):`'...'` 为成对引号字符串(`'hi'` → `"hi"`)
- `FunctionMethodCreator` 命名空间为 `MinorShift.Emuera.GameData.Function`(非 `Runtime.Script.Statements.Function`,CS0103 教训)

## 验证记录

- 全绿确认:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio` 全部通过
- `LexicalAnalyzer.UseMacro` 在测试中置 false,避免依赖 `GlobalStatic.IdentifierDictionary`(测试后还原)

## 备注

- `LogicalLineParser` 延后至 Phase 2+:其 `ParseLine` 依赖 `EmueraConsole`(WinForms 控件)与 `GlobalStatic.IdentifierDictionary`,需 STA/初始化方案

## Comments
