# Nullable:解析链启用

Status: needs-triage

## 任务

解析链是全库的类型源头之一:`Word`/`SubWord`/`WordCollection` 是词法产物,`LogicalLine` 家族是行解析产物,被 Data、Loader、Statements、Process、UI 反向消费。先启用本链,下游票(08/10/11/12)可拿到稳定的可空输入契约。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/Parser/`:LexicalAnalyzer.cs、LogicalLineParser.cs、Word.cs、SubWord.cs、WordCollection.cs
- `Runtime/Script/Statements/LogicalLine.cs`:`LogicalLine` 家族(InstructionLine/FunctionLabelLine/GotoLabelLine/NullLine/InvalidLine 等),被约 25 个文件引用
- `Runtime/Utils/EraStreamReader.cs`:ERB/CSV 行读取,被 Loader 与 Config 消费

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

按 spec.md 通用方法逐文件启用。Word/SubWord 触点的修改方向已经 issues/01(StrForm 修订)验证。

### 边界契约(只定签名,文件启用随归属票)

- `StrFormWord.SubWords`、`YenAtSubWord.Right`/`Words` 等词法字段的可空性(StrForm 样例 issues/01 的触点)
- `InstructionLine.Argument` 默认 null 的表达方式(消费方在 issues/09)
- LexicalAnalyzer/LogicalLineParser 静态方法的输入输出签名(UI 直接复用,消费方在 issues/12)

执行顺序:在下游票之前先行;纯解析,无 UI/Sound 依赖,与声音后端配置无关。

## 影响

本链产出的可空契约被下游票消费:08(表达式以 WordCollection 为输入)、10(ParserMediator、UserDefinedVariable 引用解析类型)、11(ErbLoader)、12(UI 的 EmueraConsole/HtmlManager/ButtonStringCreator/DebugDialog 调用 LexicalAnalyzer)。
