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
| `Runtime/Script/Statements/Expression/ExpressionParserTests.cs`(9 例) | 表达式解析与求值:常量四则/比较/逻辑(传 null mediator)+ 变量引用/方法调用/三元(`?` `#`)/未知标识符异常(需 TestBootstrap) |
| `Runtime/Script/Statements/Variable/VariableParserTests.cs`(5 例) | 变量解析:`IsVariable`、`ZeroTerm`/`TARGET` 静态项、`ReduceVariable`(含下标/角色字符串变量) |
| `Runtime/Script/Data/StrFormTests.cs`(14 例) | `StrForm`(`AnalyseFormattedString` → `FromWordToken` → 求值):字面量/`{}`/`%%`/对齐(语言字节宽)/三连符号绑定/`\@…\@` 三元/`GetAExpression`/`Restructure` 折叠/错误路径 |
| `Runtime/Script/Parser/LogicalLineParserTests.cs`(11 例) | `LogicalLineParser`:空行/指令行/赋值行/前置自增/无效行/`@`/`$` 标签/`#FUNCTION`/`#DIM` + 脚本级冒烟(按 ErbLoader 路由逐行解析无错误) |
| `TestBootstrapTests.cs`(4 例) | TestBootstrap 最小初始化夹具的 sanity 验证 |

## 运行时初始化(TestBootstrap)

Phase 2 起的用例(变量求值/StrForm/LogicalLineParser)依赖 `TestBootstrap.Initialize()`——
等价 `Process.Initialize` 的解析器子集,无 UI/ERB/ERH 依赖。关键点:

- `ParserMediator.Initialize(null)` + `console` 传 null:`LogicalLineParser` 在非 AnalysisMode 下不参与解析,无需 STA/WinForms
- `Config.SetReplace(ConfigData.Instance)`:填充 `PalamLvDef` 等 replace 系静态默认值(`VariableData.SetDefaultValue` 依赖)
- `JSONConfig.Data = new JSONConfigData()`:`FunctionIdentifier` 静态构造读取;`Load()` 依赖 `Program.ExeDir`(测试中未设)
- `LangManager.setEncode(932)`:`GetStrlenLang` 非 ASCII 路径不设必 NRE
- `GlobalStatic.Process.scaningLine` 置占位行:`GetScaningLine` 在其为 null 时解引用仅 `Initialize` 创建的 `state`
