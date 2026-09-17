# Nullable:变量与数据链启用

Status: implemented

Blocked by: 07

## 任务

变量簇自耦合度高:VariableToken.cs 单文件 2,933 行,含 8 个抽象 token 基类(VariableToken/CharaVariableToken/UserDefinedVariableToken/UserDefinedCharaVariableToken/ReferenceToken/LocalVariableToken,以及 VariableData partial 内嵌的 ConstantToken/PseudoVariableToken)与 57 个具体 token,文件尾部还是 partial `VariableData` 的工厂部分;存档读写以 VariableEvaluator.cs 的 `CheckData*`/`SaveToStreamBinary`/`LoadFromStreamBinary` 为入口,VariableData.cs 与 CharacterData.cs 提供按对象钩子,EraBinaryData*/EraDataStream.cs 提供流层。PluginSystem 从外部直连 VariableData(PluginManager 引用 70 次),是隐性强边(适配归 issues/12)。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/Statements/Variable/` 全部:VariableToken.cs、VariableData.cs、CharacterData.cs、VariableTerm.cs(含 FixedVariableTerm/VariableNoArgTerm)、VariableStrArgTerm.cs、VariableEvaluator.cs、VariableParser.cs、VariableIdentifier.cs、VariableCode.cs、VariableLocal.cs
- `Runtime/Script/Data/`:IdentifierDictionary.cs、UserDefinedVariable.cs、UserDefinedFunction.cs、ConstantData.cs、ParserMediator.cs、DefineMacro.cs
- `Runtime/Utils/`:EraBinaryDataReader.cs、EraBinaryDataWriter.cs、EraDataStream.cs

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

实测基线(2026-09-15,`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable`,去重后按文件归类;同次构建全库 2,805 条 / 92 个文件,与 issues/07 记录一致):

| 文件 | CS86xx | 文件 | CS86xx |
| --- | --- | --- | --- |
| `ConstantData.cs` | 88 | `VariableIdentifier.cs` | 8 |
| `VariableData.cs` | 45 | `UserDefinedVariable.cs` | 7 |
| `VariableToken.cs` | 27 | `ParserMediator.cs` | 4 |
| `VariableEvaluator.cs` | 27 | `EraBinaryDataWriter.cs` | 4 |
| `EraBinaryDataReader.cs` | 26 | `VariableStrArgTerm.cs` | 4 |
| `EraDataStream.cs` | 23 | `VariableTerm.cs` | 3 |
| `CharacterData.cs` | 22 | `VariableLocal.cs` | 3 |
| `IdentifierDictionary.cs` | 20 | `UserDefinedFunction.cs` | 3 |
| `VariableParser.cs` | 10 | `DefineMacro.cs` | 2 |

合计 326 条 / 18 个文件;码型分布为 CS8600 125、CS8625 98、CS8618 53、CS8603 26、CS8604 14、CS8602 7、CS8601 2、CS8629 1。`VariableCode.cs` 实测 0 条,按 spec「同链零警告文件一并启用」加指令。

三条失败模式不同的子链决定了分包:token 族的警告以字段未初始化(CS8618)与省略参数按 `null` 传递(CS8625)为主;存档族以读取结果赋给非空局部(CS8600)为主;ConstantData 以装载期字段与 CSV 表初始化为主。

### 边界契约

token 基类与变量侧:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `VariableToken.varData` 字段与其构造参数 | 保持非空声明,`UserDefinedVariableToken` 的唯一 `base(varCode, null)` 改 `null!` | 用户定义/引用/本地 token 构造时不持有 VariableData;`varData` 在 VariableToken.cs 内 136 处解引用,其中 115 处位于必得实体 varData 的 token 类,可空化会把 `!` 摊到全部解引用点(口径同 issues/06 的 exm 决策) |
| `VariableToken` 的 `long[] arguments` 系列参数(`GetIntValue`/`GetStrValue`/`SetValue` 四个重载/`PlusValue`/`CheckElement` 两个重载/`IsArrayRangeValid`/`throwOutOfRangeException`) | 保持非空,`VariableData.LoadVariableBinary` 中 0 维变量的 2 处 `SetValue(…, null)` 改 `null!` | 57 个 token override 均解引用 `arguments[i]`;`null` 只出现在这两个调用点,按 issues/06 口径不扩散 |
| `VariableToken.GetStrValue(ExpressionMediator, long[])` | 返回 `string?` | `StrVariableToken`/`Str1DVariableToken` 直接返回字符串标量数组元素,而该元素在 `VariableData.SetDefaultValue` 被置 null;`VariableTerm.GetStrValue` 已以 `ret == null` 兜底为空串,现有 `string` 声明与语义不符(SKILL:返回类型必须反映语义可空) |
| `VariableData`/`CharacterData` 的字符串标量数组元素 | 改 `string?` | `SetDefaultValue` 以 `dataString[i] = null` 清零,与上一条 `GetStrValue` 契约配套 |
| `VariableData`/`CharacterData` 的 `Data*Array`/`Data*Array2D` 槽位 | 保持非空声明 | 置 null 只发生在 `Dispose`(对象已死),按 SKILL 的 post-Dispose 规则不为它放宽注解 |
| `VariableParser.ReduceVariable(VariableToken, AExpression?, AExpression?, AExpression?)` | `p1`~`p3` 改可空 | 方法体全程以 `== null` 表达省略参数,入参来自 issues/05 贯通后的 `List<AExpression?>`(经 `ExpressionParser.ReduceVariableArgument`) |
| `VariableTerm.Identifier`/`arguments`/`transporter` | 保持非空,声明处 `= null!` | `protected VariableTerm(VariableToken)` 路径不赋值,由 `FixedVariableTerm`/`VariableNoArgTerm` 的构造路径补齐 |
| `VariableStrArgTerm.dic`/`errPos` | `Dictionary<string, int>?`/`string?` | `dic` 由 `GetKeywordDictionary` 赋值并在方法体内判空;`errPos` 为 `out` 参数,调用后判断 `errPos == null` |
| `VariableIdentifier.GetVariableId(string, string?)` | 返回 `VariableIdentifier?`,`subStr` 改可空 | 方法体以 `return null` 表达未命中;无 `subStr` 的重载自身传 `null` |
| `UserDefinedVariable`/`UserDefinedFunction`/`DefineMacro` 的装载期字段 | 保持非空,声明处 `= null!` | 由 ErhLoader 装载期赋值,构造后访问 |
| `CharaVariableToken.sizes` | 改 `int[]?`,四个查询方法内以 `this.sizes!` 取局部 | 用户定义字符变量先经基类构造(`CharacterData.CharacterVarLength` 返回 null)再由派生构造器赋值,构造期确实为 null,故字段可空;取用点都在派生构造器之后 |
| `ReferenceToken.array`/`arrayList` | 改 `Array?`/`List<Array?>`;`IsArrayRangeValid` 内解引用处用 `!` | `ScopeIn`/`ScopeOut` 以 `array = null` 表达引用未设置;`GetArray`/`GetLength` 已判空,`IsArrayRangeValid` 依赖先调用的 `CheckElement` 抛错 |
| `LocalVariableToken` 族的 `array` 字段 | 改 `long[]?`/`string[]?`(懒分配,取用前判空) | `GetIntValue`/`GetStrValue` 等以 `if (array == null) array = new …` 懒初始化,`resize` 置 null |
| `Static*VariableToken` 族的 `array` 字段 | 改可空 + `IfNullInitArray` 加 `[MemberNotNull(nameof(array))]` | 各取用点先调 `IfNullInitArray`,属性让编译器沿用该不变量 |
| `Private*VariableToken` 族的 `array` 字段 | 保持非空,声明处 `= null!`,`ScopeOut` 置 null 改 `null!` | 作用域外不可达,解引用点全在 `ScopeIn` 之后(口径同 issues/06 的 GlobalStatic 规则) |
| `VariableToken.MatchType(VariableToken? rother, …)` | 参数改可空 | 方法体首个判断即 `rother == null`,传 null 是既有契约 |
| `VariableToken.GetStrValue` 的 26 处 override | 只改可空的两处(`StrVariableToken`/`Str1DVariableToken`,及两处角色字符串 token),其余保持非空返回 | 只有取自身数据槽的 token 可能返回 null;常量与伪变量 token 恒非空,收窄返回不冲突 |

数据字典与查找:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `IdentifierDictionary.GetVariableToken(string key, string? subKey, bool allowPrivate)` | 返回 `VariableToken?`,`subKey` 改可空 | 方法体多处以 `return null` 表达未命中;消费方(`Process.ScriptProc`、`ArgumentBuilder`、`VariableParser`、`VariableData`、`CharacterData`)已在判空;`Emuera.Tests` 的 `VariableParserTests.GetToken` 已按 `VariableToken?` 书写,`subKey` 实参传 null |
| `IdentifierDictionary.GetFunctionMethod(LabelDictionary?, string, List<AExpression?>, bool)` | 返回 `AExpression?`,`labelDic` 改可空 | 方法体多处 `return null`;方法体前半段已判 `labelDic != null` |
| `IdentifierDictionary.GetMacro`/`GetFunctionIdentifier`/`GetRefMethod` | 分别返回 `DefineMacro?`/`FunctionIdentifier?`/`UserDefinedRefMethod?` | 方法体均以 `return null` 表达未命中 |
| `ConstantData.GetKeywordDictionary(out string? errPos, VariableCode, int, string? varname)` | 返回 `Dictionary<string, int>?`,`errPos` 与 `varname` 改可空 | 方法体以 `ret = null`/`errPos = null` 表达未命中;既有调用点传 null 的 varname,`VariableStrArgTerm` 两处消费均判空 |
| `ConstantData.GetCharacterTemplate`/`GetCharacterTemplate_UseSp`/`GetCharacterTemplateFromCsvNo` | 返回 `CharacterTemplate?` | `TryGetValue` 未命中返回 null;消费点在 `VariableEvaluator`(本票)与 `ConstantDataTests`(已用 `Assert.Null`/`!`) |
| `ParserMediator.console` 字段与 `Initialize(EmueraConsole?)` | 字段与参数改可空;`FlushWarningList`/`SingleLine` 的解引用保持现状(用 `!`) | 告警路径已判 `console != null`;EmueraConsole/PluginManager 与 `Emuera.Tests` 的 `TestBootstrap` 实传 null,`!` 精确保留现有行为 |
| `ConstantData.names` 与 `GetCsvNameList` | `string?[]?[]`;`GetCsvNameList` 返回 `string?[]`(取用时 `!`) | 槽位在 `LoadData` 前为 null、元素在 CSV 未填处为 null(既有 `if (names[i] == null)` 与调用点的 `paramName == null` 判断);方法只被名表 code 调用,槽位必已填充 |
| `ConstantData.loadDataTo` 的 `targetI` 参数、`toCharacterTemplate` 的 `chara` 参数 | 前者改 `long[]?`;后者保持非空(调用点已判空) | 30 处调用传 null 表达「该表无初始值」 |
| `CharacterTemplate.Name`/`Callname`/`Nickname`/`Mastername` | 改 `string?` | CSV 未写该列时保持 null(既有 `IsNullOrEmpty`/`!= null` 判断);连带 `CharacterData` 的字符串槽位与 `getRelationDic` 消费点 |
| `CharacterData` 的 `dataString`/`dataStringArray`/`dataStringArray2D` 元素 | 改 `string?` | 与 `CharacterTemplate` 的四个可空字段、`SetSortKey`/`GetStrValue` 的既有判空一致 |

存档读写:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `EraBinaryDataReader.CreateReader(Stream)` | 返回 `EraBinaryDataReader?` | XML 注释已声明「不正なファイルの場合は null を返す」;唯一消费点 `VariableEvaluator.CheckDataByFilename` 以 `== null` 判别 eramaker 格式 |
| `EraBinaryDataReader.Read*Array(long[]? refArray, bool)` 六个重载 | `refArray` 改可空 | 实现内以 `if (refArray == null)//読み捨て` 丢弃数据;`CharacterData`/`VariableData` 共 9 处传 null |
| `EraBinaryDataReader.ReadVariableCode()` | 返回 `KeyValuePair<string?, EraSaveDataType>` | EOC/EOF/Separator/Map/Xml/DT 分支以 `null` 作 Key;消费点以 `nameAndType.Key != null` 判断 |
| `EraBinaryDataReader` 的 `reader`/`data` 字段、`EraDataStream` 的 `file`/`reader` 字段 | 保持非空,声明处 `null!` | 仅私有构造路径与 `Dispose` 不赋值,初始化前不可达(issues/06 的 GlobalStatic 规则) |
| `VariableEvaluator.CheckDatFilename(string)` | 返回 `string?` | 方法体以 `return null` 表达「无错误」 |
| `VariableEvaluator.CheckDataByFilename(string?)` | 参数改可空 | 两个 `CheckData` 重载以 `string filename = null` 起步,仅未知 `EraSaveFileType` 时保持 null 并走到 `File.Exists` 的 FILENOTFOUND 分支,为现有行为 |
| `EraDataResult` 与 `VariableEvaluator.CheckData*` | 保持非空 | 全部路径 `new()`;跨界消费方在 issues/08、issues/11 |
| `EraDataStream` 的 `Write(string? str)`/`Write(string?[] array)`/`WriteExtended(string, string?)`/`WriteExtended(string, string?[])`/`WriteExtended(string, string?[,])`/`WriteExtended(string, string?[,,])`/`ReadStringArray(string?[] array)` | 字符串参数改可空(元素与整体) | 实现内已对 null 元素写空行;调用方是 `VariableData`/`CharacterData` 的可空字符串槽位 |
| `EraBinaryDataWriter.WriteWithKey(string key, object? v)` | 第二参数改可空 | `VariableData.SaveToStream` 系以 `dataString[i]` 调用;实现对 null 走「全部 `is` 判断不匹配」的既有静默跳过 |
| `VariableData`/`CharacterData` 的 `copyListToArray*` 辅助方法 | 源参数 `List<T>` → `IReadOnlyList<T>`(2D 为 `IReadOnlyList<T[]>`) | 目标数组元素改可空后,`List<string>` → `List<string?>` 不可协变;只读列表可协变,行为仍为逐元素拷贝 |

### 票外签名改动

本链对外的可空契约全部落在范围内文件,不触及票外文件:本链给出的是返回可空与参数可空(`GetVariableToken`/`GetFunctionMethod`/`GetMacro`/`GetFunctionIdentifier`/`GetCharacterTemplate*`/`GetStrValue`),消费点的局部标注由消费票在各自文件启用时适配。

### 下游契约分发

| 下游票 | 本链提供的契约 |
| --- | --- |
| 08 | `GetVariableToken → VariableToken?`(Creator.Method、ExpressionParser)、`GetFunctionMethod → AExpression?`(同上)、`GetMacro → DefineMacro?`、`ConstantData.GetCharacterTemplate* → CharacterTemplate?`、`VariableToken.GetStrValue → string?` |
| 09 | `GetVariableToken → VariableToken?`(ArgumentBuilder、Instraction.Child)、`GetFunctionMethod → AExpression?`(Argument.cs、Instraction.Child) |
| 11 | `GetVariableToken → VariableToken?`(Process.ScriptProc)、`ParserMediator.Initialize(EmueraConsole?)`、`EraDataResult`/`CheckData*` 保持非空(Process.ScriptProc、Process.SystemProc 消费) |
| 12 | `VariableToken.GetStrValue → string?` 与字符串标量数组元素 `string?`(VariableDataWrappers、PluginManager);VariableData 直连面(`GetVarTokenDic`/`DataDataTables`/`CharacterList`)签名不变 |
| 03 | `Emuera.Tests` 的交接:`GetVariableToken` 的 `subKey` 与 `ParserMediator.Initialize` 契约消掉 `VariableParserTests` 1 条与 `TestBootstrap` 1 条 CS8625;另新增 `ConstantDataTests` 3 条 CS8602(`GetCharacterTemplate*` 改可空返回);`TestBootstrap` 的 `ExpressionMediator` 两点来自 issues/06(第一参数 `Process` 非空)与 `Process` 构造(issues/11) |

实测落点(2026-09-15,`-p:Nullable=enable` 全量构建,按文件去重;正数为本链引入、需由该票消化):

| 下游票 | 文件 | 变化 |
| --- | --- | --- |
| 09 | `ArgumentBuilder.cs` +2、`Instraction.Child.cs` +2、`Argument.cs` +4 | `GetVariableToken`/`GetFunctionMethod` 改可空返回与 `subKey` 可空 |
| 08 | `Creator.Method.cs` +7、`ExpressionParser.cs` −1 | 同上,另 `GetMacro`/`GetCharacterTemplate*`/`GetStrValue` 改可空 |
| 11 | `Process.cs` +1、`Process.SystemProc.cs` +2、`Process.ScriptProc.cs` +2、`ErbLoader.cs` −2 | `SAVEDATA_TEXT`→`string?`、`GetCharacterParamString`→`string?`、`GetVariableToken`→`VariableToken?`;`ErbLoader` 因 `GetFunctionMethod` 可空收益 2 条 |
| 12 | `VariableDataWrappers.cs` +22、`PluginManager.cs` +3、`ConfigData.cs` −5 | `VariableData`/`CharacterData` 字符串槽位元素→`string?`、`GetStrValue`→`string?`;`ConfigData` 因 `GetKeywordDictionary` 契约收益 5 条 |

### 分包与执行顺序

按失败模式分包,每包以全量构建清零为准:

1. 装载期字段族:UserDefinedVariable、UserDefinedFunction、DefineMacro、VariableCode、VariableLocal、VariableIdentifier、VariableParser(`ReduceVariable` 的 `AExpression?` 契约在此定下)
2. 存档族:EraDataStream → EraBinaryDataReader/EraBinaryDataWriter → VariableData/CharacterData 的 Save/Load → VariableEvaluator(流元素可空契约一次贯通)
3. token 族:VariableTerm/VariableStrArgTerm(先定签名)→ VariableToken.cs 按 token 族分批(标量/数组 → constant/pseudo → local → static/private → reference → user-defined chara)
4. IdentifierDictionary(`GetVariableToken`/`GetFunctionMethod` 返回契约)
5. ConstantData(88 条,本链最多)

总体顺序不变:10 在 07 之后、08 之前执行(给表达式链供契约),Data/GameBase.cs 与 LabelDictionary.cs 归 issues/11。

### 验收

- 逐文件/逐批:`#nullable enable` → `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable --nologo -v:minimal` → 清零该文件 CS86xx。改动只有 `?`/`!`/`null!`/`#nullable` 与注解,不得新增 `?.`、新增分支或删除既有判空。
- 默认构建回归:同上但不加 `-p:Nullable=enable`,CS86xx 必须为 0。
- 后端覆盖:按 spec.md 的完成标准,`Debug`(WMP,需用 VS 的 MSBuild 构建)另需确认 CS86xx 为 0。
- 测试:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`;本链由 `Runtime/Script/Statements/Variable/VariableParserTests.cs`、`Runtime/Script/Data/ConstantDataTests.cs` 覆盖,有测试支撑处以真检查替换 `!`。

#### 实施结果(2026-09-15)

- 范围内 19 个文件全部加 `#nullable enable`,18 个文件各自 CS86xx 清零(`VariableCode.cs` 本就为零):范围内 326 → 0。
- 全库(`-p:Nullable=enable`)2,805 → 2,516;差额 = 本链清零 326 − 下游新增 37,下游落点见上表。
- 默认构建(`Debug-NAudio` 不加 `-p:Nullable=enable`)CS86xx 为 0,已启用文件无回归。
- `dotnet test`(`Debug-NAudio`,x64)全通过:144/144。
- `Emuera.Tests` 的 CS86xx 由 16 条(按列去重)变为 17 条:本链消掉 2 条、引入 3 条,清尾归 issues/03。
- 改动已 review;运行行为确认已按 spec 的完成标准执行(真实 era 游戏目录),本票转 `implemented`。

## 曾考虑的替代方案

1. `VariableToken.varData` 声明为 `VariableData?`:可空化会把 `!` 摊到文件内 136 处解引用中的绝大部分,收益只是 `UserDefinedVariableToken` 构造路径的一处 `base(varCode, null)`;按 issues/06 的 exm 决策口径,保持非空并在该构造点用 `null!`。
2. `long[] arguments` 系列参数改可空:57 个 token override 全部解引用 `arguments[i]`,而 null 只出现在 0 维变量的 2 个调用点,同上按 issues/06 口径处理。
3. `VariableToken.GetStrValue` 保持 `string` 并在 override 内用 `!`:该方法确实可能返回 null(`DataString` 元素在 `SetDefaultValue` 被清零),`VariableTerm.GetStrValue` 已显式判空兜底;用 `!` 会把 null 藏在非空签名之后,违反 SKILL 的返回类型规则。
4. 把 `EraDataStream.cs` 拆给 issues/12:其 `EraDataReader`/`EraDataWriter` 只被 `Variable/` 的三个文件消费,拆出去会让存档读写的元素可空契约跨票;`EraDataResult` 的跨界消费点在 08/11,签名保持非空即无需随票划分。
5. 一次性给 `Statements/Variable/` 整目录启用:token 族与存档族的失败模式不同,混批后难以定位回归来源。
6. 把 `ConstantData.cs` 拆出独立票:其 `GetKeywordDictionary` 与 `GetCharacterTemplate*` 契约被本票的 `VariableStrArgTerm`、`VariableEvaluator` 消费,留在一票内契约闭合。

## 影响

变量与数据链契约确定后,表达式链(issues/08 的 VariableTerm 叶)与存档读写路径(issues/11 的 Process 侧)以其为输入;PluginSystem 的 VariableData 适配归 issues/12。

- `EraDataResult` 定义在 EraDataStream.cs,但经 `VariableEvaluator.CheckData*` 跨界到 Process.ScriptProc/Process.SystemProc(11)与 Creator.Method(08),保持非空返回。
- 本链新增的可空返回/参数会在下游产生新警告,分布见「下游契约分发」。
- `Emuera.Tests` 已在 `Nullable=enable` 下构建,是本链契约的实时消费者:其 CS86xx 清尾归 issues/03,`TestBootstrap` 的 `ExpressionMediator`/`Process` 构造两点来自 issues/06、issues/11 的既有决策。
- 变量与数据链完成后,issues/08 可直接消费 `VariableTerm`/`VariableStrArgTerm` 的可空签名与 `GetStrValue` 契约,issues/11 可消费 `GetVariableToken`/`ParserMediator`/`EraDataResult` 契约,issues/12 的 PluginSystem 适配以其 `GetVarTokenDic`/`Data*` 直连面为准。
