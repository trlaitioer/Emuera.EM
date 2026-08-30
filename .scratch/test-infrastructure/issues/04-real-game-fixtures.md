# 测试用例扩展:FORM 字符串 / 表达式 / 常用函数

Status: 已完成(2026-08-29)
Type: task

## 素材

fixture 采用内联字符串常量(行数 <10 内联、不加来源注释)。

## 现状(已完成并全绿)

17 个扩展用例(FORM 字符串/表达式/常用函数,总计 38 个,全绿确认)。同类测试合并进对应组件的测试文件,并按源码路径镜像组织(相对 `Emuera.Tests/`):

- `Runtime/Script/Parser/LexicalAnalyzerTests.cs` 的"FORM 字符串与三元构造"区域(8 例):PRINTFORML FORM 参数(`\@…?…#…\@` 三元 + `{…}` + 尾部文本)、`%CALLNAME:対象キャラ%` 段、`%TEXTR("…", @"…")%` 嵌套、全角空格默认拒绝、`;` 注释行、CurlyBrace/Percent/YenAt 段解析
- `Runtime/Script/Statements/Expression/ExpressionParserTests.cs`(4 例):含括号四则运算、取模、比较、逻辑与组合比较(变量替换为常量)解析+常量求值
- `Runtime/Script/Statements/Function/FunctionMethodTests.cs` 的"其他常用函数"区域(5 例):`GETBIT`、`INRANGE`、`TOSTR`、`TOFULL`、`TOHALF` 纯计算

## 测试中发现的事实与坑(重要)

1. **PRINTFORML 的 FORM 参数由 `AnalyseFormattedString` 解析**(见 Instraction.Child.cs L208),主 `Analyse` 解析整行遇裸 `{` 会报 UnexpectedCharacter(L994)——测试须对 FORM 参数文本直接调 `AnalyseFormattedString`
2. **全角空格默认拒绝**:依赖 `Config.SystemAllowFullSpace`(默认 false);且未调 `SetConfig` 时,拒绝路径的 `GetConfigName` 因 `nameDic` 为 null 会 **NRE**(潜在健壮性问题,真实运行中 SetConfig 总先执行)。测试用 `Assert.ThrowsAny<Exception>` 记录之
3. **函数标识符表达式**的解析需 `GlobalStatic.IdentifierDictionary`,其构造依赖 `VariableData(GameBase, ConstantData)`——与 `StrForm` 同属运行时初始化范畴,**决策:推迟**(函数本身纯计算已直调覆盖);另 `DECIMAL_STRING` 未在内建注册表中(自定义函数)
4. **共享静态状态竞态**:`LexicalAnalyzer.UseMacro` 等跨测试类并行保存/恢复会互相干扰(UseMacro=true 且 IdentifierDictionary 为 null 时 expandMacro NRE,失败用例随机漂移)→ 已在 `AssemblyInfo.cs` 关闭程序集并行化(`DisableTestParallelization`)

## 备注

- 未做 Emuera/ 代码改动(遵守"不动代码,StrForm 推迟"决策)

## Comments

### 2026-08-29

- 关闭:17 个扩展用例全绿(总计 38 例);`LogicalLineParser` 与运行时初始化范畴用例移入 issues/03。
