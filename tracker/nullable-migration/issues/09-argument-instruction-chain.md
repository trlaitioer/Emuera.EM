# Nullable:参数与指令链启用

id: nullable-migration-09
Status: implemented

Blocked by: nullable-migration-05, nullable-migration-06

## 任务

参数解析与指令执行是全库耦合最强的区域:

- `ArgumentParser` 是 partial class,两半分居 ArgumentBuilder.cs 与 ArgumentParser.cs,必须同票处理(旧差分曾拆在 01/04 两票)
- ArgumentBuilder.cs 是全库单文件警告最多的文件(本票工作量主要在此;根因分布见「实测基线」,开工时按 spec.md 的计数口径重新实测)
- Instraction.Child.cs 是全库最大耦合枢纽:`InstructionLine` 出现在 133 行(141 处)、`ExpressionMediator` 117 处,并直接使用 UI 打印类型(`ConsoleDisplayLine` 8 处、`ConsoleButtonString` 8 处)、`Sound` 40 处、`Config` 18 处、`LangManager`
- Argument.cs 的 47 个直接继承 `Argument` 的子类字段大量"按构造路径可能为 null 但未标注"(`SpColorArgument.RGB`、`SpTInputsArgument`、`SpBarArgument` 等);`ArgumentBuilder.CreateArgument` 以 `warn + return null` 表达失败
- `CreateArgument` 的 null 返回**不**落到 `InstructionLine.Argument` 上:唯一消费点 `ArgumentParser.SetArgumentTo` 把它转成 `ArgParsingError`;`InstructionLine.Argument` 的 null 是 issues/07 的「参数尚未解析」语义,两种 null 不可混为一谈

范围(启用 + 清零 CS86xx):

- `Runtime/Script/Statements/`:Argument.cs、ArgumentBuilder.cs、ArgumentParser.cs(实测 0 条,按 spec「同链零警告文件一并启用」纳入)、Instruction.cs、FunctionIdentifier.cs、Instraction.Child.cs、CaseExpression.cs
- 同目录顶层的 `BuiltInFunctionCode.cs`、`FunctionArgType.cs` 同样零条 CS86xx 且不被 issues/11/12 的范围覆盖,一并加 `#nullable enable`
- FunctionIdentifier.cs 与 Instraction.Child.cs 是同一 partial 类,必须同批启用

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

按 spec.md 通用方法逐文件启用。Argument 子类字段逐构造路径核实后标注,拿不准的以 `?` + 使用点判断保行为;`CreateArgument` 的失败路径以 `ArgumentParser.SetArgumentTo` 的既有 `if (arg == null)` 分流为准(见「开工时需定的本票内契约」)。`#nullable enable` 对整文件生效,文件内分批按既有 `#region` 进行。

### 实测基线(2026-09-16)

`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable`,按完整警告行去重:

| 文件 | CS86xx |
| --- | --- |
| `ArgumentBuilder.cs` | 403 |
| `Instraction.Child.cs` | 259 |
| `Argument.cs` | 60 |
| `CaseExpression.cs` | 16 |
| `FunctionIdentifier.cs` | 8 |
| `Instruction.cs` | 1 |
| `ArgumentParser.cs` | 0 |

合计 747 条,与 issues/08「下游落点」的清单一致;同次构建全库 1,821 条 / 66 个文件。

码型:`ArgumentBuilder.cs` 为 CS8603 158、CS8600 86、CS8604 84、CS8602 43、CS8625 26、CS8601 3、CS8620 2、CS8618 1;`Instraction.Child.cs` 为 CS8602 130、CS8600 90、CS8604 29、CS8601 3、CS8625 3、CS8603 2、CS8629 2;`Argument.cs` 为 CS8618 44、CS8603 5、CS8600 3、CS8601 3、CS8604 3、CS8625 2;`CaseExpression.cs` 为 CS8602 14、CS8603 2;`FunctionIdentifier.cs` 为 CS8618 3、CS8625 3、CS8600 1、CS8603 1;`Instruction.cs` 的 1 条是 `AInstruction.ArgBuilder` 的 CS8618(由派生类赋值)。`ArgumentParser.cs` 另有 1 条 CS8981(`trerror` 别名),不属本迁移目标。

按所在语句的根因归类,本票的工作并不集中在 issues/05 的元素可空上:

| 根因 | ArgumentBuilder.cs | Instraction.Child.cs |
| --- | --- | --- |
| `return null` 失败路径(`CreateArgument` 的既有契约) | 157 | 2 |
| 元素索引(issues/05、10 的元素可空) | 120 | 19 |
| issues/07 的 `Pop*` 出口(`PopArgumentPrimitive` 等 → `CharStream?`/`WordCollection?`) | 16 | 2 |
| issues/07 的 `JumpTo`/`JumpToEndCatch` → `LogicalLine?` | 0 | 41 |
| issues/07 的 `InstructionLine.Argument` → `Argument?` | 0 | 95 |
| 传 null 给非可空成员(Argument 子类字段与构造器标注) | 50 | 16 |
| 其他(含上一项可空后的级联解引用、CS8618 构造器) | 60 | 84 |

主因是 issues/07 的 LogicalLine 契约与本票自身的失败路径、子类字段标注;issues/05 的元素可空是次要来源,issues/10 的 `GetFunctionMethod → AExpression?` 另在 `CallformCache.ResolveCallF` 带出 2 条。

### 前置契约

- issues/05:`RowArgs`(SpCallFArgment/SpCallArgment/SpCallSharpArgment)与 `GetFunctionMethod` 9 个调用点(CALLF/CALLFORM/CALLFORMF/GETMETH 系)元素可空
- issues/06:ExpressionMediator exm、Config/Lang/Sound/UI 打印类型的叶子契约
- issues/07:LogicalLine 家族——`InstructionLine.Argument`(`Argument?`)、`PopArgumentPrimitive`/`PopAssignmentDestStr`/`PopRowArgs`、`JumpTo`/`JumpToEndCatch`、`LoopCounter`、`LexicalAnalyzer.ReadSingleIdentifierWord`、`StrAsignArgument(string?)`
- issues/08:`arguments[i]` 的处理规则(必填槽位 `!`、省略槽位按方法体既有判空)与 `FunctionMethod.CheckArgumentType → string?`
- issues/10:`IdentifierDictionary.GetFunctionMethod`(`List<AExpression?>?` 参数、`AExpression?` 返回)、`VariableToken.GetStrValue → string?`

以上契约均已落地,`Blocked by` 仍按 spec.md 只列 05、06(阻塞边只表达「先有契约再清警告」);本票开工时 issues/07、08、10 的 Status 均未转 `implemented`,故先确认其收尾以免清零基线变动;三者现已随运行行为确认转 `implemented`,其间未变动本票基线。

### 上游对本票范围文件的签名改动

| 文件 | 改动 | 来源 |
| --- | --- | --- |
| Argument.cs | `ExpressionsArgument`/`ExpressionArrayArgument`/`SpPrintVArgument` 的列表字段与参数、`SpCallFArgment`/`SpCallArgment`/`SpCallSharpArgment` 的 `SubNames`/`RowArgs`、`SpSetArrayArgument` 的 `TermList` 与构造器改 `List<AExpression?>` | issues/05 |
| Argument.cs | `StrAsignArgument` 的 `Value` 字段与构造器 `value` 参数改 `string?` | issues/07(VARS 无初值分支以 `default` 传 null) |
| Argument.cs | `CallformCache.longTerm` 改 `AExpression?` | `StrForm.SingleIntFormTerm` 以 `null` 表达无匹配形态,该字段本就以判空使用(见 issues/05 的 `SingleIntFormTerm` 返回契约) |
| ArgumentBuilder.cs | `checkArgumentType`/`getChangeableVariable`/`popTerms` 的签名与方法内 4 处局部改 `List<AExpression?>` | issues/05 |
| ArgumentBuilder.cs | 13 处 `exm` 实参改 `null!`(常量折叠路径,接收方为 `SingleTerm`) | issues/06 的 exm 非空决策 |
| ArgumentBuilder.cs | `METHOD_ArgumentBuilder.CreateArgument` 的 `string errmes` 局部改 `string?` | 跟随 `FunctionMethod.CheckArgumentType` 的返回契约(issues/08) |
| CaseExpression.cs | `LeftTerm`/`RightTerm` 改 `AExpression?` | 类内本就以 `if (LeftTerm != null)` 表达未赋值,`ExpressionParser.reduceCaseExpression` 赋入的正是可能为 null 的 `reduceTerm` 结果;16 条警告全由此带出(issues/08) |

### 开工时需定的本票内契约

| 成员 | 决策点 | 依据 |
| --- | --- | --- |
| `ArgumentBuilder.CreateArgument`/`AInstruction.CreateArgument` 的返回类型 | 改 `Argument?`,而非在各实现内用 `!` 断言 | 157 条 CS8603 全部来自实现内的 `return null`;唯一消费点 `ArgumentParser.SetArgumentTo` 已以 `if (arg == null)` 转 `ArgParsingError`,`InstructionLine.Argument` 本身也是 `Argument?`;改签名只动本票范围内的文件(ArgumentBuilder.cs、Instruction.cs、Instraction.Child.cs 的 4 个 override、ArgumentParser.cs 的局部) |
| `CallformCache.Lookup` 的 `out string strKey` | 是否改 `out string?` | 长键分支现状赋 `null`,`Store` 亦以 null 参与;`ResolveCall`/`ResolveGotoForm`/`ResolveCallF` 已用 `strKey ?? ...` 兜底,可空化后这三个调用点需同步 |
| `Argument` 子类字段 | 逐构造路径核实:`SpInputsArgument`、`SpSetArgument`、`SpVarSetArgument`、`SpPrintImgArgument` 等由 `ArgumentBuilder` 在多分支传 null 的槽位按 `?` 标注;构造点保证非空的以 `= null!` | 44 条 CS8618 与 50 条 CS8625 集中在此 |
| `CallformCache`/`SpCallFArgment` 等类的延迟赋值字段(`longTerm` 除外) | 保持非空 + 声明处 `= null!` | `FuncTerm`/`Cache`/`UDFArgument`/`CallFunc` 由调用点按路径赋值,口径同 issues/06 的 GlobalStatic 规则;若某路径确会读到未赋值状态,按该路径真实语义改可空并记录 |
| `FunctionIdentifier` 的 `method`/`Instruction`/`arg` 与 `getMatchFunction` | 按构造路径核实:两个私有构造器分别置 `method = null`/`Instruction = null`;`getMatchFunction` 以 `return null` 表达未命中 | 8 条警告集中在此 |
| `AInstruction.ArgBuilder` | 保持非空 + 声明处 `= null!` | 由各派生类在构造期以 `ArgBuilder = ArgumentParser.GetArgumentBuilder(...)` 赋值(127 处),赋值后访问 |

执行顺序:建议在 08 之后——指令方法体大量消费表达式链产物(求值/Restructure),先有契约再清本票警告更省;UI 打印类型、Sound 本身的文件启用在 issues/12,本票只依赖其契约。

### 验收

- 逐文件/逐批:`#nullable enable` → `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable --nologo -v:minimal` → 清零本票范围 CS86xx(同一警告在内联与末尾汇总各出现一次,以 MSBuild 末尾汇总为准)
- 默认构建(不加 `-p:Nullable=enable`)CS86xx 必须为 0
- 测试:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`
- 改动只有 `?`/`!`/`null!`/`#nullable` 与注解,不得新增 `?.`、新增分支或删除既有判空
- 后端覆盖:本票范围与 `Sound.*.cs` 无关,但按 spec.md 的完成标准,`Debug`(WMP,需用 VS 的 MSBuild 构建)另需确认 CS86xx 为 0

#### 实施结果(2026-09-16)

- 范围内 9 个文件全部 `#nullable enable` 且各自 CS86xx 清零:范围内 747 → 0(ArgumentBuilder.cs −403、Instraction.Child.cs −259、Argument.cs −60、CaseExpression.cs −16、FunctionIdentifier.cs −8、Instruction.cs −1;ArgumentParser.cs、BuiltInFunctionCode.cs、FunctionArgType.cs 本为 0)。
- 全量构建(`-p:Nullable=enable`)2,547 → 1,821 → 1,106,涉及文件 66 → 60;差额 = 本链清零 747 − 下游净增 32(见「下游落点」)。
- 默认构建(不加 `-p:Nullable=enable`)CS86xx 为 0,已启用文件无回归。
- `Debug`(WMP,VS 的 MSBuild 构建,`/t:Rebuild`)全库 1,083 条,本票范围 0;32 位 `MSBuild.exe` 会在 `ResolveComReference` 处以 MSB4216 失败,需改用 amd64 版本。
- `dotnet test`(`Debug-NAudio`,x64)全通过:144/144。
- 改动只有 `?`/`!`/`null!`/`#nullable` 与注释:新增 `!` 536 处(含 `null!` 18 处),未新增 `?.`、未新增分支、未删除既有判空;唯一非注解的代码改动是 ArgumentBuilder.cs 的 SP_SORTCHARA 把复用的模式变量 `id` 换名为新局部 `nextId`(`as` 的结果可能为 null,模式变量不能承接地赋值),行为不变。

决策记录:

- `CreateArgument` 家族改 `Argument?`:ArgumentBuilder 的 1 个抽象与 50 个 override、`AInstruction` 的 virtual、Instraction.Child 的 4 个 override 一并改。157 条 CS8603 全部由此消除,未在任何实现内写 `!`;唯一消费点 `ArgumentParser.SetArgumentTo` 已以 `if (arg == null)` 分流,`InstructionLine.Argument` 的「参数尚未解析」语义不受影响。
- Argument 子类字段逐构造路径标注:构造器可能收到 null 的槽位(`SpInputsArgument` 的 3 个、`SpTInputsArgument` 的 6 个、`SpSetArgument.Term`、`SpVarSetArgument`/`SpCVarSetArgument` 的 Start/End、`SpArrayShiftArgument`/`SpArraySortArgument` 的后两个、`SpPrintImgArgument` 的 nameb/namem/param、`SpHtmlPrint.Opt`、`PrintDataArgument.Var`)标 `?`;同一构造器互斥分支的字段(`SpColorArgument` 的 RGB 与 R/G/B、`SpSetArrayArgument` 的两个常量列表、`RefArgument` 的 5 组源字段)标 `?`;元素类型按 issues/05 的元素可空契约改 `AExpression?[]`/`List<AExpression?>`(`BitArgument.Term`、`SpDtColumnOptions.Values`)。
- 由对象初始化器或构造后赋值、消费点在读取前已保证非空的字段,以声明处 `= null!` 表达,口径同 issues/06 的 GlobalStatic 规则:`MixedIntegerExprTerm.num`、`Argument.ConstStr`、`CallformEntry.LabelName`、`CallformCache` 的两个键字典(构造器按 `longTerm` 只建其一,读写同受该条件门控)、`AInstruction.ArgBuilder`(CUSTOMDRAWLINE 与 VARI/VARS 系显式留 null 转为可空,其余 127 处构造期赋值)。
- `CallformCache.Lookup` → `CallformEntry?` 与 `out string? strKey`,`Store` 的 `strKey` 参数改 `string?`;`ResolveCall`/`ResolveGotoForm`/`ResolveCallF` 的返回与 `out` 参数按同一语义改可空,三个方法内的 `strKey ?? ...` 与 `if (arg == null)` 原本就是兜底路径。
- `FunctionIdentifier` 的 `Instruction`/`Method`/`getMatchFunction` 改可空:构造器与 `TryGetValue` 未命中都以 null 表达,多数消费点本已判空(PluginManager.ExecuteLine 未判空,是既有的 NRE 风险,落到 issues/12)。
- 指令方法体统一口径:`func.Argument`/`line.Argument`/`func.JumpTo` 在本指令的 `DoInstruction`/`SetJumpTo` 内已由解析期赋值,解引用处按注解层 `!` 断言;`Line.PopArgumentPrimitive`/`PopAssignmentDestStr` 同理(ArgumentBuilder 路径的 InstructionLine 由解析器带入参数原始文本)。省略槽位不一律断言,按各方法体既有判空或分支处理。

开工时发现的问题(2026-09-16 处置):

1. `CALLSHARP` 的实参经 `RowArgs` 省略时为 null,而 `PluginMethodParameterBuilder.ConvertTerm` 立即解引用即抛 NRE。**已就地补 `TODO` 注释**,迁移期不改行为。
2. `EmueraConsole.PrintImg` 与 `ConsoleImagePart` 把按钮名、映射图名与三个尺寸标记当可选使用(调用点显式传 null),签名却声明非空。**已就地补 `TODO` 注释**并在调用点保留 `!`;该契约的收窄归 issues/12。
3. `CallformCache` 的负结果(Call/JumpTo/FuncTerm 为 null)在 `CallformEntry` 的字段注释里已声明,本次只把该注释落到注解上。

### 票外签名改动

| 文件(归属票) | 改动 | 原因 |
| --- | --- | --- |
| `Runtime/Script/Process.State.cs`(issues/11) | `IntoFunction` 的 `srcArgs` 参数改 `UserDefinedFunctionArgument?` | 方法体本身以 `if (srcArgs != null)` 分流「系统调用无实参」;CALLEVENT 与 Process.SystemProc 两处实参即 `null` |
| `Emuera.Tests/Runtime/Script/Statements/CallformCacheTests.cs`(issues/03) | 两处 `out string strKey` 改 `out string?`,`LabelName = strKey!`,命中结果以 `Lookup(...)!` 承接 | 跟随 `CallformCache.Lookup` 的新契约(先例见 issues/08 对 ExpressionParserTests 的适配) |

### 下游落点(全量构建,按文件去重)

| 下游票 | 文件 | 变化 | 来源 |
| --- | --- | --- | --- |
| 11 | `Process.ScriptProc.cs` | +31 | `SpColorArgument.R/G/B` 可空(6)、`ExpressionArgument.Term` 可空(约 10)、`ResolveCall` 的 `CalledFunction?` 返回与 `out UserDefinedFunctionArgument?`(2)与零散解引用 |
| 11 | `ErbLoader.cs` | +1 | `FunctionIdentifier.getMatchFunction → string?` |
| 11 | `Process.SystemProc.cs` | −1 | `IntoFunction` 的 `srcArgs` 可空后该处 `null` 不再告警 |
| 12 | `PluginManager.cs` | +1 | `FunctionIdentifier.Instruction → AInstruction?` |

## 影响

指令执行路径的 null 语义显式化:`CreateArgument` 的失败返回以 `Argument?` 表达,由 `ArgumentParser.SetArgumentTo` 的既有判空分流;`InstructionLine.Argument` 的「参数尚未解析」语义保持独立。Argument 子类与 `FunctionIdentifier` 的字段按构造路径标注。本票完成后全库 CS86xx 仅剩 issues/11、issues/12 与收尾票 issues/03 的范围(全量构建 1,106 条 / 60 个文件)。
