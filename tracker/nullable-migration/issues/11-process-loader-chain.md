---
id: nullable-migration-11
status: implemented
blocked_by: [nullable-migration-07, nullable-migration-10]
---

# Nullable:进程与加载链启用

## 任务

进程与加载是总装层:Process 5 个 partial 文件经 GlobalStatic 调度 ErbLoader/ErhLoader 的解析产物(LogicalLine 家族)与符号表(LabelDictionary/IdentifierDictionary),运行期消费全部上游簇产物。

文件级不依赖 UI:本链 308 条警告中没有一条的根因落在 issues/12 归属的类型上(UI 只经 `GlobalStatic.Console` 抵达,调用点未产生告警)。本票的特殊之处在契约方向——它以「返回值确实可能为 null」的查找/工厂 API 为主,而这些 API 的消费点已分布在 07/08/09/10 已启用的文件里,故除清零本链外还要回收那些消费点(见「票外签名改动」)。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/`:Process.cs、Process.ScriptProc.cs、Process.State.cs、Process.CalledFunction.cs、Process.SystemProc.cs
- `GlobalStatic.cs`:位于项目根 `Emuera/`,不在 `Runtime/Script/` 下
- `Runtime/Script/Loader/`:ErbLoader.cs、ErhLoader.cs
- `Runtime/Script/Data/`:LabelDictionary.cs、GameBase.cs
- `Runtime/InputRequest.cs`:实测 2 条 CS8618,自洽,留本票

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;默认构建(不加 `-p:Nullable=enable`)的 CS86xx 仍为 0——本票契约会落到 07/08/09/10 已启用文件,须一并清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

### 实测基线(2026-09-16)

`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable`,按完整警告行去重(同一警告在内联与末尾汇总各出现一次):

| 文件 | CS86xx | 码型 |
| --- | ---: | --- |
| `Process.ScriptProc.cs` | 123 | CS8602 63、CS8600 45、CS8604 7、CS8601 4、CS8625 4 |
| `Loader/ErbLoader.cs` | 59 | CS8600 25、CS8602 17、CS8604 4、CS8625 4、CS8629 4、CS8601 3、CS8603 1、CS8618 1 |
| `Process.CalledFunction.cs` | 32 | CS8603 10、CS8602 8、CS8618 5、CS8625 5、CS8600 2、CS8629 2 |
| `Process.State.cs` | 24 | CS8629 9、CS8602 4、CS8603 4、CS8618 3、CS8625 2、CS8600 1、CS8604 1 |
| `Process.cs` | 24 | CS8618 9、CS8602 4、CS8604 3、CS8629 3、CS8600 2、CS8603 1、CS8619 1、CS8625 1 |
| `Data/LabelDictionary.cs` | 13 | CS8603 6、CS8600 4、CS8604 2、CS8629 1 |
| `Loader/ErhLoader.cs` | 12 | CS8600 9、CS8618 2、CS8625 1 |
| `GlobalStatic.cs` | 9 | CS8618 9 |
| `Process.SystemProc.cs` | 7 | CS8625 3、CS8618 2、CS8604 2 |
| `Data/GameBase.cs` | 3 | CS8600 2、CS8618 1 |
| `Runtime/InputRequest.cs` | 2 | CS8618 2 |
| **合计** | **308 / 11 个文件** | CS8602 96、CS8600 90、CS8618 34、CS8603 22、CS8625 20、CS8604 19、CS8629 19、CS8601 7、CS8619 1 |

同次构建全库 1,106 条 / 60 个文件,与 issues/09 的收尾记录一致(09 之后无漂移)。范围内 11 个文件全部有条目,不存在「同链零警告文件」。

复测(2026-09-16,HEAD `8df80a2`(`refactor(nullable): 参数与指令链可空启用(issues/09)`)):本表逐文件码型分布逐字复现;默认构建 CS86xx 为 0(50 个已启用文件);票外 49 个文件 / 798 条逐文件核对全部落在 issues/12 的范围清单内;范围内 308 条的消息文本中无一条引用 UI 或图像类型,「本链无 UI 根因」成立。

按根因分类:

| 根因 | 条数 | 说明 |
| --- | ---: | --- |
| 声明级初始化(CS8618) | 34 | GlobalStatic 9 个字段、Process 9 个、Process.State/CalledFunction 的构造器路径字段、InputRequest 2 个、GameBase.ScriptWindowTitle、ErhLoader 2 个容器 |
| 查找/工厂返回契约(CS8603) | 22 | LabelDictionary 的 4 个查找方法、CalledFunction 的工厂与 `ConvertArg`、ProcessState 的返回地址与 `Scope`、`Process.GetScaningLine`、`ErbLoader.addLine` |
| 上游契约消费点(CS8602/CS8600/CS8604/CS8629 等) | 252 | InstructionLine.Argument/JumpTo(07)、GetVariableToken/GetFunctionMethod/GetStrValue/字符串槽位(10)、`arguments` 元素(05/08)、Argument 子类字段与 ResolveCall/ResolveGotoForm(09);`Process.ScriptProc.cs` 的 123 条几乎全在此类 |

### 批次与顺序

批内条数为实测(见「实施结果」)。本票基线 308 条按批清零后合计 316 条:差额 8 条是前批契约落在后批文件上的净增(`Process.cs` +4、`ErbLoader.cs` +2、`Process.ScriptProc.cs` +2),对应各方括号内数字大于票内基线表的原因。

1. 声明级:GlobalStatic、InputRequest、GameBase(14 条)
2. 查找与返回契约:LabelDictionary、ErhLoader(26 条)——本批定下的契约立即在 07/08/09/10 的消费点回收,实测 18/24 条票外改动出自本批
3. 调用契约核心:Process.CalledFunction、Process.State(56 条)——工厂与 CalledFunction 族的返回契约在此定案
4. Process.cs、Process.SystemProc.cs(34 条)
5. ErbLoader(61 条)
6. Process.ScriptProc.cs(125 条,按 FunctionCode/指令类分批清零)

`GetScaningLine` 的返回契约提前到第 2 批:它只依赖 `LogicalLine` 类型本身,不依赖 LabelDictionary,却独占 11/24 条票外改动,留在第 4 批会让该批的票外回收量最重。

每批后:全量构建清零本批文件 + 默认构建确认 CS86xx 为 0(含消费点回收)+ 受影响测试。`#nullable enable` 对整文件生效,批次只是清零顺序,不是启用粒度。

### 边界契约

声明级:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| GlobalStatic 的 9 个字段(`Console`/`Process`/`GameBaseData`/`ConstantData`/`VariableData`/`VEvaluator`/`IdentifierDictionary`/`EMediator`/`LabelDictionary`) | 保持非空声明,声明处 `= null!` | issues/06 已定「初始化前不可达即保持非空」并把 `Reset()` 的置空改为 `null!`;残留的 9 条 CS8618 用声明处初始化表达同一语义 |
| `InputRequest.DefStrValue`/`TimeUpMes` | 保持非空,声明处 `= null!` | 由 INPUT 系指令按 `HasDefValue`/`DisplayTime` 配套赋值,读取点均在赋值之后 |
| `GameBase.ScriptWindowTitle` | 保持非空,声明处 `= null!` | `LoadGameBaseCsv` 结尾以 `== null` 兜底填默认标题,其后只读 |
| `Process.labelDic`/`vEvaluator`/`exm`/`gamebase`/`idDic`/`state`/`originalState`/`scaningLine`/`scaningLineForThread` | 保持非空,声明处 `= null!` | Initialize 与构造路径赋值,口径同 issues/06;`GetScaningLine` 的 null 语义由返回契约承担(见下) |
| `Process.SystemProc.TrainName` | 改 `string?[]` | 来源 `ConstantData.GetCsvNameList` 已是 `string?[]`(issues/10);`COMABLE` 系消费点本就以 `== null` 判元素 |
| `ErhLoader.dimlines`/`erdFileNames` | 保持非空,声明处 `= null!` | 装载期赋值,构造后访问 |
| `Process.CalledFunction.tostrMethod` | 保持非空,声明处 `= null!` | 方法体内以 `if (tostrMethod == null)` 懒加载后即非空 |

查找与返回契约:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `LabelDictionary.GetSameNameLabel` | 返回 `FunctionLabelLine?` | 3 处 `return null`;唯一消费点 `ErbLoader.loadErb` 已判空 |
| `LabelDictionary.GetNonEventLabel` | 返回 `FunctionLabelLine?` | `TryGetValue` 未命中返回 null;消费点全部已在判空(见票外改动) |
| `LabelDictionary.GetEventLabels` | 返回 `List<FunctionLabelLine>[]?` | 同上;消费点只在 `Process.CalledFunction` 的工厂内 |
| `LabelDictionary.GetLabelDollar` | 返回 `GotoLabelLine?` | 同上;消费点(`ErbLoader.loadErb`、`CalledFunction.CallLabel`、`Instraction.Child` 的 GOTO)已在可空上下文 |
| `CalledFunction.CallFunction`/`CallEventFunction` | 返回 `CalledFunction?`;`retAddress` 参数改 `LogicalLine?` | 两方法体均以 `return null` 表达「找不到函数」(TRY 系据此继续);`callFunction` 显式传 `null` 表示无返回地址 |
| `CalledFunction.ConvertArg` | 返回 `UserDefinedFunctionArgument?`,`errMes` 保持 `out string`(赋值处 `null!`) | 失败路径以「null + errMes」表达,所有 `return null` 路径都先赋 `errMes`;两个消费点(`UserDefinedMethodTerm.Create`、`UserDefinedRefMethodTerm.Argument`)都先判 `arg == null` 再读,`Create` 再经 `IdentifierDictionary` 的 `if (ret == null) throw new CodeEE(errMes)` 消费。不改 `out string?`:没有适配「返回 null 时 out 非空」的特性(`[NotNullWhen]` 只作用于 bool 返回),改可空会把负担推给 `Create` 的 `out string` 与已启用的 `IdentifierDictionary.cs`,反而扩大票外改动 |
| `CalledFunction.CurrentLabel` | 改 `FunctionLabelLine?` | `ShiftNext`/`ShiftNextGroup`/`FinishEvent` 以 `null` 表达事件函数取尽,`Process.State.Return` 已判空;`IsOnly` 等解引用处按现有 NRE 行为 `!` |
| `CalledFunction.TopLabel` | 保持非空,声明处 `= null!` | 两处工厂以对象初始化器赋非空 `labelline`;`CallEventFunction` 的 `TopLabel = CurrentLabel` 需 `!`,依据是 `SortLabels` 保证四个分组至少一组非空、故 `ShiftNext()` 后 `CurrentLabel` 非空 |
| `CalledFunction.eventLabelList` | 改 `List<FunctionLabelLine>[]?` | `CallEventFunction` 把 `GetEventLabels` 的结果直接存入该字段、随即以 `== null` 表达「非事件函数」;声明非空须在赋值处写 `!`,而该 `!` 立刻被下一行的判空推翻。代价是 `ShiftNext()` 内 1 处断言(事件路径内),属票内 |
| `CalledFunction.ReturnAddress`、`updateRetAddress` 参数 | 改 `LogicalLine?` | `CreateCalledFunctionMethod` 显式置 null,`Process.State` 各处判空 |
| `CalledFunction.CallLabel` | 返回 `GotoLabelLine?` | 直接返回 `GetLabelDollar` 结果;消费点(`Argument.cs` 的 `ResolveGotoForm`)已在可空上下文 |
| `ProcessState.Scope` | 改 `string?` | 以 `return null` 表达「无执行中函数」(调试命令路径) |
| `ProcessState.GetCurrentReturnAddress`/`GetReturnAddressSequensial` | 改 `LogicalLine?` | 两处 `return null`;前者无消费点,后者消费点已判空 |
| `ProcessState.CurrentLine`/`ErrorLine` | 改 `LogicalLine?` | `currentLine` 在 Return 路径被置 null(构造器上的 CS8618 即此),`ErrorLine` 直通它 |
| `Process.GetScaningLine` | 改 `LogicalLine?`,`return default` 改写为 `return null` | `state.ErrorLine` 为 null 时以 null 表达「不在脚本行上」;SKILL 禁止用 `default!` 把 null 藏在非空签名后,且消费点本就写着 `if (line == null)` |
| `ErbLoader.addLine` | 返回 `LogicalLine?` | 首行以 `return null` 表达「无前驱」;私有方法,消费点在票内 |

方法体适配口径:

| 场景 | 口径 | 依据 |
| --- | --- | --- |
| CS8629(`Position`/`Position.Value` 等可空值类型) | 注解层 `!` 或就地取局部,不加运行时判断 | 现状即 NRE/InvalidOperationException,`?.` 属行为变更(口径同 issues/08) |
| `Exception.StackTrace` 的 `Split` | `!` | `StackTrace` 为 null 时现状即 NRE;改 `?.` 会静默跳过错误栈打印,属行为变更 |
| 元素容器的 null 初值(如 ARRAYCOPY 分支的 `vars`/`names`) | 容器按真实可空性标注(`VariableToken?[]`/`string?[]`);已判空分支沿用流分析收窄,未判空分支按现有 NRE 行为 `!` | 与 issues/08 的「必填槽位 `!`、省略槽位按方法体既有语义」同构 |
| 上游已落地契约的消费点(`InstructionLine.Argument`/`JumpTo`、`GetVariableToken`、`arguments[i]` 等) | 只做注解适配,`!` 需有「指令码/解析期已赋值」一类依据 | issues/05/07/08/09/10 的既有决策 |
| `ParserMediator.Warn(string, LogicalLine, …)` 收到可能为 null 的行 | 在调用点解决,不改该方法签名 | 方法体直接解引用 `line` 写入解析产物,可空化会把负担推给全部解析路径 |
| 属性(如 `CalledFunction.ReturnAddress`)的重复解引用 | 先取局部再判空 | 流分析不跟踪属性槽,连续两次访问第二次会失去收窄 |

### 票内新增契约(实施中定案)

「边界契约」原表未覆盖、实施中才发现必须定案的成员:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `Process.GetValue(SuperUserDefinedMethodTerm)` | 返回 `SingleTerm?` | 方法体以 `SingleTerm ret = null` 起手,`#FUNCTION` 无返回值时原样返回;唯一消费点 `SuperUserDefinedMethodTerm` 本就以 `is not SingleLongTerm`/`if (term == null)` 判空(issues/08 已落地)。维持非空签名则只能在方法内写 `= null!`,即把 null 藏在非空签名后 |
| `ProcessState.MethodReturnValue`、`ReturnF` 的参数 | 改 `SingleTerm?` | `Return(long)` 的 IsFunctionMethod 分支显式传 `null`,该 null 就是「无返回值」语义 |
| `Process.getCurrentLine` | 返回 `LogicalLine?` | 直通 `ProcessState.CurrentLine`;消费点 `Creator.Method` 本就以 `if (getCurrentLine != null)` 判空,非空声明与语义相悖 |
| `Process.handleException`/`handleExceptionInSystemProc` 的 `current` 参数 | 改 `LogicalLine?` | 唯一调用点以可空局部传入,且 `handleException` 方法体内本就有 `current != null` 分支 |
| `ErbLoader.addLine` 的 `nextLine`/`lastLine` 参数 | 改 `LogicalLine?` | 方法体以 `nextLine == null` 与 `lastLine.NextLine` 表达既有语义;私有方法,消费点在票内 |
| `ErbLoader.parseLabel` 的 `subNames` 局部 | 改 `SingleTerm?[]` | 元素由 `term as SingleTerm` 赋入并立即判空,容器按真实可空性标注 |

### 票外签名改动(实测,2026-09-16)

做法:只把「边界契约」表的注解落到签名(不动 `= null!`、不启用文件),全量构建与默认构建各跑一次。构建通过,证明决策本身可实施;下表条数为**默认构建**新增的 CS86xx,即必须随对应批次当场回收的消费点。

| 契约决策 | 条数 | 文件(归属票)与位置 |
| --- | ---: | --- |
| `Process.GetScaningLine → LogicalLine?` | 11 | `IdentifierDictionary.cs` 的 `GetVariableToken`(3 处)、`VariableToken.cs` 的 `Debug__FILE__Token`/`Debug__FUNCTION__Token`/`Debug__LINE__Token`(各 1 处)、`Creator.Method.cs` 的 `MesSkipMethod` 与 `GetDoingFunctionMethod`(各 1 处)、`LexicalAnalyzer.cs` 的 `AnalyseFormattedString`(1 处)、`ExpressionParser.cs`(1 处)、`VariableLocal.cs`(1 处) |
| `GetNonEventLabel → FunctionLabelLine?` | 7 | `Creator.Method.cs` 的 `ExistFunctionMethod`(4 处)、`IdentifierDictionary.cs` 的 `GetOverloadedList` 与 `GetFunctionMethod`(各 1 处)、`ArgumentBuilder.cs` 的 `SP_REF_ArgumentBuilder`(1 处) |
| `CallFunction`/`CallEventFunction → CalledFunction?` | 2 | `Instraction.Child.cs` 的 `CALL_Instruction.SetJumpTo` 与 `CALLEVENT_Instruction.DoInstruction` |
| `ConvertArg → UserDefinedFunctionArgument?` | 2 | `UserDefinedMethodTerm.cs` 的 `UserDefinedMethodTerm` 与 `UserDefinedRefMethodTerm`(各 1 处) |
| `GetLabelDollar → GotoLabelLine?` | 1 | `Instraction.Child.cs` 的 `GOTO_Instruction.SetJumpTo` |
| `ProcessState.Scope → string?` | 1 | `Instraction.Child.cs` 的 `SAVELOADGAME_Instruction.DoInstruction` |
| **合计** | **24** | 8 个已启用文件 |

形态均为「局部声明补 `?`」或传参处 `!`,随对应批次一并清零。`Creator.Method.cs` 与 `IdentifierDictionary.cs` 的落点分散在多于票内先前点名的那些方法上,以本表为准。

上表 24 条在上例预演里逐行命中;落地时另有 2 条被「只落注解」的预演掩盖、改完注解后才暴露,已一并清零:

- `IdentifierDictionary.cs` 的 `GetVariableToken`:`line` 改成可空局部后,`line.ParentLabelLine!` 处的收窄失效,补 `line!`;
- `Instraction.Child.cs` 的 `GOTO_Instruction.SetJumpTo`:`jumpto` 局部改 `GotoLabelLine?`。

即票外改动实际为 26 处。另有 4 条落在**未启用**文件,默认构建不产生,属 issues/12,本票不动:`EmueraConsole` 的 `newGeneration`(`Process.getCurrentLine`)、`EmueraConsole` 的 `GetDebugTraceLog`(`Process.GetScaningLine`)、`PluginManager` 的 `GetStackTrace` 内 2 处(`ProcessState.GetReturnAddressSequensial`,即「下游契约分发」给 12 的契约)。

同次实测的其余数字:全量构建 1,106 → 1,130(净 +24),新增 55 条、消失 31 条(消失的 CS8603 21 条集中在 `Process.CalledFunction.cs`、`LabelDictionary.cs`,即本票「查找/工厂返回契约」的主体);新增 55 条中 29 条落在本票范围(Process.State 9、CalledFunction 7、Process 6、SystemProc 3、ErbLoader 2、ScriptProc 2),24 条即上表,2 条归 issues/12。

不在票外改动之列(票内解决):

- `Process.SystemProc.callFunction` 把 `VEvaluator.SAVEDATA_TEXT`(issues/10 已改 `string?`)传给 `VariableEvaluator.SaveTo(int, string)`:票内 `!`,与 issues/08 对 `ExpressionMediator.ConvertStringType` 的口径一致,不改 issues/10 的签名;
- `Process.Initialize` 把 `Program.AnalysisFiles`(`List<string>?`)传给 `ErbLoader.LoadErbList`:票内 `!`(AnalysisMode 分支保证已初始化,见 Program.cs 的赋值点);
- `ErbLoader.nestCheck` 结尾把可能为 null 的 `func` 传给 `ParserMediator.Warn`:该 else 分支不可达(`Stack<InstructionLine>.Pop()` 在非空栈上不返回 null),票内以 `func!` 消费并作为开工发现记录。

### 前置契约

- issues/05:UDF 参数数组(`UserDefinedFunctionArgument.Arguments`、`ConvertArg` 的 `convertedArg`/`term`)已改可空——**已落在本票范围文件 Process.CalledFunction.cs**
- issues/06:GlobalStatic/exm/Lang 契约,以及「初始化前不可达即保持非空 + 声明处 `null!`」的通用规则
- issues/07:LogicalLine 家族(`InstructionLine.Argument`/`JumpTo`/`LoopCounter`、`Pop*` 出口、`LogicalLineParser.ParseLine → LogicalLine?`、`EraStreamReader` 行读取)
- issues/08:`arguments[i]` 处理规则与 FunctionMethod 契约
- issues/09:`Process.State.IntoFunction` 的 `srcArgs` 已改 `UserDefinedFunctionArgument?`;`CreateArgument` 家族已改 `Argument?`
- issues/10:`GetVariableToken`/`GetFunctionMethod`/`GetStrValue`/`GetCsvNameList`/`ParserMediator.Initialize` 的可空契约

开工条件:本票开工时 07/08/10 的 Status 为 `in-progress`(代码已落地),按 issues/09 的先例不等待三者转 `implemented` 即开工;三者现已随运行行为确认转 `implemented`,其间未推翻本票契约,基线无需重测(开工时实测与 issues/09 的记录一致,1,106 条 / 60 个文件)。

### 下游契约分发

| 下游票 | 本链提供的契约 |
| --- | --- |
| 12 | `ProcessState.Scope → string?` 与 `GetReturnAddressSequensial → LogicalLine?`(`PluginManager` 的调用栈遍历已判空)、`Process`/`ProcessState` 的字段契约、`InputRequest` 对象本身 |
| 03 | `Emuera.Tests` 的 `TestBootstrap` 中 `new Process(null)`/`ExpressionMediator` 相关点,在 Process 构造与 Initialize 签名定案后清尾 |

### 验收

- 逐批:`#nullable enable` → `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable --nologo -v:minimal` → 清零本批文件(同一警告在内联与末尾汇总各出现一次,按完整警告行去重)
- 默认构建(不加 `-p:Nullable=enable`)CS86xx 必须为 0:这是「票外消费点已回收」的硬指标,覆盖全部已启用文件
- 全量构建的新增警告只允许落在未启用文件,且清单恰为 `EmueraConsole` 的 `newGeneration`/`GetDebugTraceLog` 与 `PluginManager.GetStackTrace` 内 2 处(归 issues/12,本票不动);出现其他新增即为本票未清零
- `Debug`(WMP,VS 的 MSBuild)另需确认本票范围 CS86xx 为 0
- 测试:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`
- 改动只有 `?`/`!`/`null!`/`#nullable` 与注解,不得新增 `?.`、新增分支或删除既有判空

#### 实施结果(2026-09-16)

- 范围内 11 个文件全部 `#nullable enable` 且各自 CS86xx 清零:全量构建 1,106 → 802,差额 308 条全部出自本链。各批清零量为 14 / 26 / 56 / 34 / 61 / 125,合计 316 = 基线 308 + 前批契约落在后批文件的净增 8(`Process.cs` +4、`ErbLoader.cs` +2、`Process.ScriptProc.cs` +2)。
- 默认构建(不加 `-p:Nullable=enable`)CS86xx 为 0。
- 全量构建的新增警告为 4 条,全部落在 issues/12 的文件、默认构建不产生:`EmueraConsole.newGeneration`(CS8601,`Process.getCurrentLine`)、`EmueraConsole.GetDebugTraceLog`(CS8600,`Process.GetScaningLine`)、`PluginManager.GetStackTrace` 内 2 处(CS8602 与 CS8600,`ProcessState.GetReturnAddressSequensial`)。四者即「下游契约分发」给 12 的契约。
- 票外改动实测 26 处:「票外签名改动」表的 24 条逐行命中,另 2 条(`IdentifierDictionary.GetVariableToken`、`Instraction.Child.GOTO_Instruction.SetJumpTo`)在「只落注解」的预演里被可空局部掩盖、落地后才暴露,已一并清零。
- `dotnet test`(`Debug-NAudio`,x64)全通过:144/144。
- 改动只有 `?`/`!`/`null!`/`#nullable` 与注释:对每个改动文件剔除全部 `?`/`!` 后与改动前逐行比对,除新增的 `#nullable enable` 行外无差异;`ErbLoader.cs` 另经 IL 比对确认方法体字节一致(仅构造器多出 `labelDic = null` 的字段初始化,字段原值即 null)。
- 未新增 `?.`、未新增分支、未删除既有判空。`Emuera.Tests` 仍有 7 条 CS86xx(`ConstantDataTests.cs` 3、`LogicalLineParserTests.cs` 2、`LexicalAnalyzerTests.cs` 2),均落在 issues/07、10 已定契约(`GetCharacterTemplate`、`ParseLabelLine`/`ParseLine`、`AnalyseFormattedString`)的消费点上,本票未触及这些 API,按 spec 由 issues/03 收尾。
- `Debug`(WMP):用 VS 的 amd64 `MSBuild.exe` 并清空 `*_PROXY` 后构建通过;32 位 `MSBuild.exe` 会在 `ResolveComReference` 处报 MSB4216。该配置的 CS86xx 在 issues/12 完成态实测为生成文件 `RikaichanAutogenerated.cs` 的 3 条,与 `Debug-NAudio` 相同。

实施中发现的既有缺陷(按 spec 只注解、不就地修改):

1. `ErbLoader.nestCheck` 结尾的 `func != null`/else 不可达分支(即票内「开工时需核实」第 1 项),已按 `func!` 消费;
2. `Process.ScriptProc.cs` 的 ARRAYCOPY 分支:同名变量查找结果为 null 时直接 NRE,而并列的另一路径会抛 `CodeEE`,同一处两种错误处置不一致;
3. `ErbLoader.parseLabel` 的 `defs[i] = def` 可能存入 null,而 `FunctionLabelLine.Def` 的元素类型非空、`Process.CalledFunction.ConvertArg` 会解引用它(收窄需跨文件契约改动);
4. `ErbLoader.addLine` 的 `nextLine == null` 分支在两个调用点均不可达,`lastLine` 实际非空;
5. `CalledFunction.IsOnly` 直接解引用 `CurrentLabel`,与其上方 `HasSingleFlag` 的判空不一致(即票内「开工时需核实」第 5 项)。

### 开工时需核实

1. `ErbLoader.nestCheck` 结尾的 `func != null` / else 分支:`Stack<InstructionLine>` 的 `Pop` 不返回 null,该 else 分支不可达,却把 null 行送进 `ParserMediator.Warn`。按票内处置(调用点 `func!`),若确认是既有缺陷则另立独立提交,不与注解改动混提。
2. `Process.handleException`/`handleExceptionInSystemProc`/`getRawTextFormFilewithLine`:`exc.StackTrace` 为 null(未抛出的异常)与 `ScriptPosition?` 为 null 时现状即抛异常,属错误处理路径自身的风险,本票只保证行为不变。
3. `CalledFunction` 的 `ReturnAddress`/`CurrentLabel` 可空化后,`Process.State.Return`/`GetReturnAddressSequensial` 中的连续解引用需改取局部。
4. `CalledFunction.CallEventFunction` 对 `GetNonEventLabel` 连续调用两次(先赋给非空局部 `line`,再对同一表达式判空):可空化后 `line` 的解引用无法被第二次调用的判空收窄。按注解改动最小化取 `line!`(两次查找都保留);若改为用局部判空,则少一次字典查找,行为不变但已超出「只加注解」的范围,需单独记录。
5. `CalledFunction.IsOnly` 直接解引用 `CurrentLabel`,而相邻的 `HasSingleFlag` 以 `if (CurrentLabel == null)` 兜底:本票按现有 NRE 行为断言,不修改该不一致;如需补判空另立独立提交。

## 曾考虑的替代方案

1. `Process.GetScaningLine` 保持 `LogicalLine` 并以 `return default!` 兜底:SKILL 禁止用 `default!` 把 null 藏在非空签名后;10 处已启用消费点中 7 处本就写着 `if (line == null)`,非空声明与语义相悖。
2. `LabelDictionary` 的 4 个查找方法保持非空、在调用点用 `null!`:与 issues/10 已定的查找 API 口径(未命中即 `?`)不一致,并把 30 余处既有判空降级为 `!`。
3. `CalledFunction.CallFunction`/`ConvertArg` 保持非空:方法体确有 `return null`,消费点(CALL/CALLEVENT/CALLFORM 的 TRY 系)已按 null 分支处理。
4. 把 CalledFunction 的工厂与 `ConvertArg` 返回契约回推给 issues/05/09 重开:两票的决策只覆盖 UDF 参数数组与 `IntoFunction` 参数,工厂/转换的返回契约天然属于本票。
5. 把 `Process.ScriptProc.cs` 拆成独立票:`#nullable enable` 对整文件生效,拆票不减少同文件内的相互影响;改以「整文件启用 + 按指令类分批清零」获得同样的定位能力。
6. 把 GlobalStatic 的 9 个字段声明为可空:会把判空负担推给 42 个引用文件,与 issues/06 的决策相悖。
7. 使 `ParserMediator.Warn(string, LogicalLine, …)` 的 `line` 参数可空以吸收本链的调用点:方法体要写解析产物,可空化会把判空负担推给全部解析路径。

## 影响

进程与加载链为总装层,其契约收敛后 issues/03 的 csproj 收尾仅剩 UI 与外围(issues/12)依赖:本票完成后全库 CS86xx 为 802 条 / 50 个文件,全部落在 issues/12 的范围清单内(基线 798 条 + 本票契约下发给 12 的 4 条)。

本链产出的可空契约另有 07/08/09/10 已启用文件的回收成本(实测 24 处,逐行清单见「票外签名改动」),这些改动属 spec「签名跟链走、启用跟票走」的允许范围,但必须在默认构建下当场清零,不能留作回归。
