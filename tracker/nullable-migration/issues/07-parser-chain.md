# Nullable:解析链启用

id: nullable-migration-07
Status: implemented

## 任务

解析链是全库的类型源头之一:`Word`/`SubWord`/`WordCollection` 是词法产物,`LogicalLine` 家族是行解析产物,被 Data、Loader、Statements、Process、UI 反向消费。先启用本链,下游票(08/10/11/12)可拿到稳定的可空输入契约。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/Parser/`:LexicalAnalyzer.cs、LogicalLineParser.cs、Word.cs、SubWord.cs、WordCollection.cs
- `Runtime/Script/Statements/LogicalLine.cs`:`LogicalLine` 家族(InstructionLine/FunctionLabelLine/InvalidLabelLine/GotoLabelLine/NullLine/InvalidLine),被 23 个文件引用(另有 3 个文件的 `IsLogicalLine` 为同名误命中)
- `Runtime/Utils/EraStreamReader.cs`:ERB/CSV 行读取,被 ErbLoader、ErhLoader、ConfigData、ConstantData、GameBase、KeyMacro 六个文件消费

实测基线(2026-09-15,`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable`,去重后按文件归类):

| 文件 | CS86xx |
| --- | --- |
| `Runtime/Script/Statements/LogicalLine.cs` | 32 |
| `Runtime/Script/Parser/WordCollection.cs` | 13 |
| `Runtime/Script/Parser/LexicalAnalyzer.cs` | 12 |
| `Runtime/Utils/EraStreamReader.cs` | 9 |
| `Runtime/Script/Parser/LogicalLineParser.cs` | 7 |
| `Runtime/Script/Parser/SubWord.cs` | 1 |
| `Runtime/Script/Parser/Word.cs` | 0 |

合计 74 条;同次构建全库 2,547 条 / 99 个文件,与 spec.md 记录一致。`LexicalAnalyzer.cs`、`LogicalLineParser.cs`、`EraStreamReader.cs` 另各有 1 条 CS8981(`trerror` 别名),不属本迁移目标,不计入清零口径。

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;默认构建(不加 `-p:Nullable=enable`)的 CS86xx 仍为 0——本票需改动已启用的 `StrForm.cs`,见「票外签名改动」;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

按 spec.md 通用方法逐文件启用;`Word.cs` 虽零警告,按「同链零警告文件一并启用」规则加指令。改动遵循「签名跟链走」:先定本链输出的可空契约,再逐文件清零。

本链与 UI/Sound 的边界:`LogicalLineParser` 以 `EmueraConsole` 为参数(调用方实传 null,故参数需 `EmueraConsole?`)并调用 `System.Media.SystemSounds`,`LogicalLine` 反向引用 `Process`;与两种声音后端配置无关,计数口径只需 `Debug-NAudio`。

issues/06 的 exm 非空决策在本票的落点是 `LogicalLineParser.ParseLine` 内 `arg.Restructure(null)` 调用点,按该票结论改用 `null!`。

### 边界契约(只定签名、字段声明与列表元素类型)

词法层:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `SubWord.Words` | `WordCollection?` | `TripleSymbolSubWord` 构造器以 `null` 调用基类 |
| `YenAtSubWord.Right` | `StrFormWord?` | `LexicalAnalyzer.AnalyseYenAt` 的缺 `#` 告警分支构造 `null` |
| `YenAtSubWord.Left`、`StrFormWord.SubWords`、`StrFormWord.Strs` | 保持非空 | 构造点均非空;`StrForm.FromWordToken` 直接取 `SubWords.Length` |
| `WordCollection.Pointer` | `LinkedListNode<Word>?` | `EOL` 以 `Pointer == null` 表达,多处赋 `Collection.First`/`Next` |
| `WordCollection.Next`、`WordCollection.Remove` | 解引用处用 `!` | 以 null 作 EOL 哨兵,`!` 精确保留现有行为 |
| `LexicalAnalyzer.ReadSingleIdentifierWord` | `IdentifierWord?` | 方法体 `return null`;唯一消费方是 `ArgumentBuilder`(issues/09) |
| `SubWord.SetIsMacro`、`YenAtSubWord.SetIsMacro` 内的 `Words`/`Right` | 解引用处用 `!` | 现状为抛 NRE,`?.` 属运行行为变更 |

行解析层:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `InstructionLine.Argument` | `Argument?` | `Process.ScriptProc` 主循环以 `func.Argument == null` 判断「参数尚未解析」,null 是有效状态 |
| `InstructionLine.argprimitive`、`PopArgumentPrimitive()` | `CharStream?` | 方法体置 null;`LogicalLineParser.ParseLine` 的前置自增/自减分支传 null |
| `InstructionLine.assigndest`、`PopAssignmentDestStr()` | `WordCollection?` | `PopAssignmentDestStr` 取走后置 null,表达「已取走」 |
| `InstructionLine` 5 参数构造器的 `CharStream` 参数 | `CharStream?` | 前置自增/自减分支传 null |
| `JumpTo`、`JumpToEndCatch` | `LogicalLine?` | `Instraction.Child` 多处赋 `null`,并以 `== null`/`!= null` 判断 |
| `LoopCounter` 及字段 `cnt` | `VariableTerm?` | `Instraction.Child` 以 `jumpTo.LoopCounter == null` 判断 |
| `IfCaseList`、`dataList`、`callList` | 保持非空,声明处 `= null!` | `ErbLoader.nestCheck` 在解析期按指令码赋值,消费点(`Process.ScriptProc`、`Instraction.Child`)按同一指令码访问,初始化前不可达 |
| `ParentLabelLine` | `FunctionLabelLine?` | `LogicalLineParser.ParseLine` 的 `parentLine` 默认 null 且只有 VARI/VARS 与 InvalidLine 分支赋值;`Process.ScriptProc`、`IdentifierDictionary`、`Creator.Method`、`VariableToken` 四处已在判空 |
| `NextLine` 及字段 `nextLine` | 保持非空,声明处 `= null!` | 链尾 `NullLine` 的 `NextLine` 未被赋值且从不读取;`ErbLoader.addLine` 为其余每条线赋值 |
| `FunctionLabelLine.wc`、`PopRowArgs()` | `WordCollection?` | 私有字段,方法体置 null,仅此一处出口 |
| `FunctionLabelLine.Arg`、`Def` | `VariableTerm[]?`、`SingleTerm[]?` | `ErbLoader.setLabelsArg` 以 `label.Arg != null` 判断参数解析是否已完成 |
| `FunctionLabelLine.LabelName`、`MethodType` | 保持非空,声明处 `= null!` | `protected FunctionLabelLine()` 路径由 `InvalidLabelLine` 自行赋值 |
| `FunctionLabelLine.GetPrivateVariable` | `UserDefinedVariableToken?` | 以 `Dictionary.TryGetValue` 是否命中表达 |
| `FunctionLabelLine.CompareTo` | 参数改 `FunctionLabelLine?`,体内解引用处用 `!` | CS8767:`IComparable<T>` 的参数在 BCL 注解下为 `T?`;`List.Sort` 不传 null |
| `GotoLabelLine.Equals` | 参数改 `GotoLabelLine?` | 方法体已判空,零行为变化 |
| `GotoLabelLine.GetHashCode` | 体内 `ParentLabelLine` 解引用处用 `!` | 与 `ParentLabelLine` 可空声明配套 |
| `LogicalLineParser.ParseLine(string, EmueraConsole?)` | 返回 `LogicalLine?`,参数改 `EmueraConsole?` | 调用方 EmueraConsole 与 PluginManager 实传 null;`stream.EOS` 时返回 null |
| `LogicalLineParser.ParseLine(CharStream, ScriptPosition?, EmueraConsole?, FunctionLabelLine?)` | 同上;`parentLine` 改 `FunctionLabelLine?`,VARI/VARS 分支的 `parentLine.AddPrivateVariable` 处用 `!` | `parentLine` 默认 null(调试命令行路径),该分支解引用 null 时现状即抛 NRE,`!` 保持行为 |

行读取:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `EraStreamReader.ReadLine()` | `string?` | 方法体以 `null` 表达无后续行 |
| `EraStreamReader.ReadEnabledLine()` | `CharStream?` | `ReadLine` 返回 null 时直接返回 null |
| `filepath`、`filename`、`_fileLines` | 保持非空,声明处 `= null!` | 仅 `Dispose` 置空,套用 issues/06 的 GlobalStatic 规则 |

### 票外签名改动

| 文件(归属票) | 改动 | 原因 |
| --- | --- | --- |
| `Runtime/Script/Data/StrForm.cs`(issues/01 已废止;适配由 issues/05 承担) | `FromWordToken` 中 `yenat.Words` 与 `SWT.Words` 两处取值改 `!` | `SubWord.Words` 变可空;后一处已在 `TripleSymbolSubWord` 分支之外,`CurlyBraceSubWord`/`PercentSubWord` 的 `Words` 恒非空,前一处 `YenAtSubWord` 的 `Words` 由 `AnalyseYenAt` 传入恒非空。`!` 只改注解,运行期仍走既有的 null 判断。不改则默认构建出现 CS86xx 回归 |
| `Runtime/Script/Statements/Argument.cs`(issues/09) | `StrAsignArgument` 的 `value` 参数与 `Value` 字段改 `string?` | `LogicalLineParser.ParseLine` 的 `VARS` 无初值分支以 `default` 传 null;`Instraction.Child` 的 `SetValue(arg.Value, ...)` 需相应适配 |
| `Runtime/Utils/CharStream.cs`(issues/12) | 本票不启用 | 它是解析链的输入类型,但范围归 issues/12;本票期间它对 `LexicalAnalyzer` 是 nullable-oblivious,不影响本票清零 |

### 下游契约分发

| 下游票 | 本链提供的契约 |
| --- | --- |
| 09 | `InstructionLine.Argument`、`PopArgumentPrimitive`/`PopAssignmentDestStr`、`ReadSingleIdentifierWord`、`StrAsignArgument(string?)` |
| 10 | `EraStreamReader.ReadLine`/`ReadEnabledLine`(ConstantData、ConfigData 消费) |
| 11 | `PopRowArgs`、`NextLine`/`ParentLabelLine`、`LogicalLineParser.ParseLine → LogicalLine?` 与 `ErbLoader.addLine` 的 null 返回、`EraStreamReader`(ErbLoader、ErhLoader、GameBase 消费) |
| 12 | `LogicalLineParser.ParseLine → LogicalLine?`、其 `EmueraConsole?` 参数与 `FunctionLabelLine? parentLine` 参数(EmueraConsole、PluginManager 消费) |
| 08 | `FunctionLabelLine.Arg`/`Def` 改 `VariableTerm[]?`/`SingleTerm[]?`(消费点为 `UserDefinedRefMethod.MatchType` 的 `label.Arg.Length`) |

### 执行顺序与验收

- 本票无 `Blocked by`,在下游票之前执行;建议顺序 07 → 10 → 08 → 09 → 11 → 12 不变。
- 逐文件:`#nullable enable` → 构建 → 清零该文件 CS86xx;改动须只有 `?`/`!`/`null!`/`#nullable` 与注解,不得新增 `?.`、新增分支或删除既有判空。
- 本链计数:`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable --nologo -v:minimal`。同一警告在内联与末尾汇总各出现一次,直接数行会翻倍,以 MSBuild 末尾汇总为准。
- 默认构建回归:同上但不加 `-p:Nullable=enable`,CS86xx 必须为 0(当前实测为 0,其余为 CS8981 等既有警告)。
- 测试:`Emuera.Tests` 已覆盖本链(`Runtime/Script/Parser/LexicalAnalyzerTests.cs`、`Runtime/Script/Parser/LogicalLineParserTests.cs`、`Runtime/Script/Data/StrFormTests.cs`),以 `dotnet test` 作回归网;有测试覆盖处按 spec.md 的修复原则将 `!` 替换为真检查。

## 曾考虑的替代方案

1. `LogicalLine.NextLine` 声明为 `LogicalLine?`:会让 issues/11 的 `ErbLoader.setArgument`、`Instraction.Child` 等处约 20 个解引用补 `!`,而链尾 `NullLine` 的 `NextLine` 从不被读取;改用声明处 `= null!`,与 issues/06 的 GlobalStatic 规则一致。
2. `InstructionLine.Argument` 保持非空并 `= null!`:与 `Process.ScriptProc` 主循环的 `func.Argument == null` 判断相悖——null 是「参数尚未解析」的有效状态;非空声明会掩盖该状态,且下游 `(IntAsignArgument)func.Argument` 一类转换失去编译器提示。
3. `LogicalLine.ParentLabelLine` 保持非空并 `= null!`:null 会被 `LogicalLineParser.ParseLine` 实际赋入(该方法的 `parentLine` 默认 null),且四处消费点已在判空;声明非空会让这些判空失去意义,UI 侧 `EmueraConsole.GetDebugTraceLog` 的未判空解引用也失去提示。
4. `SubWord.SetIsMacro`/`YenAtSubWord.SetIsMacro` 的 `Words`/`Right` 改用 `?.`:属运行行为变更(跳过调用 vs 抛 NRE),违反 spec.md 的迁移期零行为变更原则;且该链实际不可达(`WordCollection.SetIsMacro` 无调用方,`Word.SetIsMacro` 只被 `ErhLoader` 用于 IdentifierWord),`?.` 无收益。
5. 把 `Runtime/Utils/CharStream.cs` 纳入本票:它是解析链的输入类型,但与 issues/12 的范围重叠;本票期间它作为 nullable-oblivious 类型不影响清零,范围划分保持不变。

## 影响

本链产出的可空契约被下游票消费:09(InstructionLine 的参数表示与两个 `Pop*` 出口、`ReadSingleIdentifierWord`)、10(ConstantData/ConfigData 消费的 EraStreamReader)、11(ErbLoader/ErhLoader/GameBase 消费的 EraStreamReader 与 `PopRowArgs`、`LogicalLineParser.ParseLine`)、12(EmueraConsole/PluginManager 消费的 `LogicalLineParser.ParseLine`)。08 消费本票 1 处可空签名:`FunctionLabelLine.Arg` 改 `VariableTerm[]?` 后 `UserDefinedRefMethod.MatchType` 的 `label.Arg.Length`(`Word`/`SubWord` 家族的签名不变)。

`SubWord`/`YenAtSubWord` 可空字段(`Words`、`Right`)的消费点只有已启用的 `StrForm.cs` 一处,故本票的票外改动集中在 `StrForm.cs`;`Runtime/Utils/CharStream.cs` 归 issues/12,本票期间保持 nullable-oblivious。

全量构建(`-p:Nullable=enable`)实测(2026-09-16 复核,于本票前后两次构建;按文件与警告文本去重):本链 74 条清零(`LogicalLine.cs` −32、`WordCollection.cs` −13、`LexicalAnalyzer.cs` −12、`EraStreamReader.cs` −9、`LogicalLineParser.cs` −7、`SubWord.cs` −1),全库由 2,547 条 / 99 个文件变为 2,805 条 / 92 个文件;新增消费点的分布为 09(Instraction.Child +199、ArgumentBuilder +33、Argument.cs −1、ArgumentParser.cs −1)、11(Process.ScriptProc +75,Process 及其 State/CalledFunction、ErbLoader、ErhLoader、LabelDictionary、GameBase 合计 +11)、10(ConstantData +5、IdentifierDictionary +2)、12(ConfigData +3、PluginManager +3、KeyMacro +1、EmueraConsole +1)、08(UserDefinedRefMethod.cs +1);合计 +334 − 2 − 74 = +258,与 2,547 → 2,805 一致;默认构建的 CS86xx 保持 0。
