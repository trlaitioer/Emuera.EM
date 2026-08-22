# Emuera.Tests

Emuera 解释器核心的单元测试工程(xUnit)。对应 `.scratch/` 中"测试基础设施"方向。

## 运行

```bash
dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio
```

**必须带 `-c Debug-NAudio`**(与仓库构建约定一致)。原因:`Emuera/Emuera.csproj` 的 NAudio 包引用是按配置条件声明的——只有 NAudio 配置才引入 `NAudio.*` 包并排除 `Sound.WMP.cs`;默认配置引用 WMPLib COM,`dotnet build`/`dotnet test` 无法解析。`-c Debug-NAudio` 使还原与构建全局一致,避免"还原出无 NAudio 的 assets、构建却要求 NAudio"的错配。

不要用 ProjectReference 的 `SetConfiguration`/`AdditionalProperties` 元数据代替:`SetConfiguration` 与 `dotnet test` 存在冲突(MSB3100),`AdditionalProperties` 不作用于还原阶段。

## 覆盖范围

测试文件按被测源码路径镜像组织(路径相对 `Emuera.Tests/`,与 `Emuera/` 下源码对应):

| 文件 | 被测对象 |
| --- | --- |
| `Runtime/Script/Parser/LexicalAnalyzerTests.cs`(19 例) | 词法分析(`Analyse`/`AnalyseFormattedString`):标识符/整数/运算符/字符串/注释/括号/FORM `%` 终止/错误用例 + FORM 字符串构造(`\@…?…#…\@` 三元、`%…%` 段、`{…}`、`%TEXTR(…)` 嵌套、全角空格) |
| `Runtime/Utils/EncodingHandlerTests.cs`(4 例) | 编码检测:UTF-8 带/不带 BOM、Shift-JIS 回落、`GetEncoding(932)` |
| `Runtime/Script/Statements/Function/FunctionMethodTests.cs`(11 例) | 内建函数纯函数(`TOUPPER`/`TOLOWER`/`ABS`/`MAX`/`MIN`/`SQRT`/`GETBIT`/`INRANGE`/`TOSTR`/`TOFULL`/`TOHALF`,经 `GetMethodList()` 取实例) |
| `Runtime/Script/Statements/Expression/ExpressionParserTests.cs`(4 例) | 表达式解析与常量求值(变量替换为常量) |

## 待办(Phase 2+)

- `StrForm`(覆盖已迁移文件的 `null!` 语义,需先探明 `GlobalStatic.VariableData`/`LangManager` 最小初始化)
- `ExpressionParser` 的函数标识符表达式、`VariableParser`
- `LogicalLineParser`(依赖 `EmueraConsole` WinForms 与 `IdentifierDictionary`,需 STA/初始化方案)
- eraBasic 脚本级冒烟测试(复用 `-Debug` 语法分析模式)
