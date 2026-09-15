# Nullable:表达式与方法链启用

Status: in-progress

Blocked by: 05, 06, 10

## 任务

表达式求值与方法调用是全库最大的类型簇:`AExpression` 继承体系(Expression/)与方法体系(Function/ 的 FunctionMethod/FunctionMethodTerm/UserDefined*),也是全库单文件警告最集中的区域。issues/05 已把 `List<AExpression>` 元素可空契约贯通到方法链签名(范围内已无 `List<AExpression>`),本票启用文件并适配方法体。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/Statements/Expression/`:AExpression.cs、Term.cs、ExpressionParser.cs、OperatorMethod.cs、OperatorCode.cs(实测 0 条,按 spec「同链零警告文件一并启用」纳入)
- `Runtime/Script/Statements/ExpressionMediator.cs`:位于 `Statements/` 顶层,不在 `Expression/` 目录下
- `Runtime/Script/Statements/Function/`:FunctionMethod.cs、FunctionMethodTerm.cs、UserDefinedMethodTerm.cs、UserDefinedRefMethod.cs、Creator.cs、Creator.Method.cs

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;默认构建(不加 `-p:Nullable=enable`)的 CS86xx 仍为 0——本票改动的方法链签名被已启用的 `StrForm.cs` 消费;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

实测基线(2026-09-15,`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable`,去重后按文件归类;同次构建全库 2,516 条 / 75 个文件,与 issues/10 记录一致):

| 文件 | CS86xx | 文件 | CS86xx |
| --- | --- | --- | --- |
| `Creator.Method.cs` | 585 | `UserDefinedRefMethod.cs` | 5 |
| `OperatorMethod.cs` | 87 | `ExpressionMediator.cs` | 3 |
| `ExpressionParser.cs` | 22 | `UserDefinedMethodTerm.cs` | 3 |
| `FunctionMethod.cs` | 12 | `Term.cs` | 1 |
| `FunctionMethodTerm.cs` | 1 | `AExpression.cs`、`Creator.cs`、`OperatorCode.cs` | 0 |

合计 719 条 / 12 个文件,占全库 28.6%;`Creator.Method.cs` 585 条为全库单文件最多(7,645 行、186 个 sealed 方法类、247 个 override,其中 595 行含 `arguments[`)。码型分布为 CS8602 576、CS8600 92、CS8603 16、CS8604 14、CS8618 7、CS8619 6、CS8625 3、CS8620 2、CS8601 2、CS8629 1。

按根因分类,527/719(73%)落在含 `arguments[` 的语句上,即 issues/05 元素可空化的直接后果;其余约 192 条需要本票的契约决策,见「边界契约」。

### 执行顺序与分批

1. 先定 `FunctionMethod` 基类契约与 `arguments[i]` 处理规则,再按 `#region` 分批清 `Creator.Method.cs`
2. `Expression/`、`Function/` 其余文件按「先签名后方法体」逐文件清零
3. `ExpressionMediator.cs` 最后(其 3 条来自 VB `Strings.StrConv` 的返回契约)

`#nullable enable` 无法只作用于文件的一部分(命令行 `-p:Nullable=enable` 下更是全文件同时开启流分析),故「分批」指整文件启用一次后按文件内既有 `#region` 分批清零:`Creator.Method.cs` 的区域边界为 EM_私家版_追加関数、CSVデータ関係、汎用処理系、定数取得、数学関数、変数操作系、文字列操作系、html系、画像処理系(含各 EE_/EM_ 子区域),其后为 EE_MOUSEB、LoadText＆SaveText、EE_EXISTSOUND/EE_EXISTFUNCTION/EE_GETMEMORYUSAGE/EE_CLEARMEMORY、EE_textbox拡張、EE_GETERDNAME/EE_GETDISPLAYLINE/EE_GETDOINGFUNCTION/EE_SystemInput拡張、daughter-patch追加、Bitmap Cache、EE_OUTPUTLOG拡張。每批后以全量构建核对本文件剩余条数递减。

### 边界契约

基类与方法体:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `arguments[i]` 解引用(全链统一规则) | 必填槽位以 `!` 断言;省略槽位按方法体既有语义判空 | `CheckArgumentType`/`CheckArgumentTypeEx` 在类型检查阶段拒绝必填位置的 null(`argumentTypeArray` 路径全部槽位、`argumentTypeArrayEx` 路径下 `i < OmitStart`),故 `!` 是可证的注解层断言;`i >= OmitStart` 的槽位允许省略,`OmitStart` 实测取值为 0/1/2/3/6/10,方法体里存在 `arguments.Count > n` 之后才取值的分支(如 `XmlGetMethod` 的 `tooltip`),一律 `!` 会抹掉省略语义 |
| 判空后仍被索引的 `arguments[i]` | 就地 `!` | 索引器解引用不会被同一索引上的空判断收窄(`FunctionMethod.CheckArgumentTypeEx` 中 `if (arguments[i] == null) continue;` 之后的 `GetOperandType()` 即此类),此处 `!` 有刚判空的直接依据,不必为此重构 |
| `FunctionMethod.argumentTypeArrayEx` | 改 `ArgTypeList[]?`,在 `CheckArgumentTypeEx` 内以局部变量一次性收窄 | 180 个类只设 `argumentTypeArray`、92 个只设 `argumentTypeArrayEx`,`CheckArgumentType` 显式以 `!= null` 分流,且 `argumentTypeArray` 已是 `Type[]?`;声明非空会把多数类的真实状态藏起来,并需要在方法体内多处 `!` |
| `FunctionMethod.Name` | 保持非空,声明处 `= null!` | 由 `Creator` 的注册表经 `SetMethodName` 在注册期赋值,之后不可达 null;186 个内置类均不自行赋值(口径同 issues/06 的 GlobalStatic 规则) |
| `FunctionMethod.ReturnType` | 保持非空,声明处 `= null!` | 派生构造器赋值(197 处覆盖 186 个类),`FunctionMethodTerm` 构造器随即读取;开工时核实无遗漏构造路径,若有未赋值且被读取的路径,按该路径真实语义改可空并在票内记录 |
| `UserDefinedRefMethod` 的 `CalledFunction`/`Name`/`RetType`/`ArgTypeList` | 保持非空,声明处 `= null!` | 由 `Create` 的对象初始化器与 `SetReference` 赋值,构造后访问;`MatchType` 内的解引用随之清零 |
| `Term.cs` 的 `StrFormTerm.Restructure` 中 `sfValue.GetAExpression()` | 局部改 `AExpression?`,沿用既有 `if (term != null)` 分支 | 契约由 `StrForm.GetAExpression()` 定为 `AExpression?`(StrForm.cs 已启用、0 条);这是 `Term.cs` 的唯一警告 |

两侧消费:

| 成员 | 决策 | 依据 |
| --- | --- | --- |
| `ExpressionMediator.ConvertStringType` | 保持返回 `string`,对 `Strings.StrConv` 结果以 `!` 断言 | 3 条 CS8603 全在此(StrConv 返回 `string?`);唯一调用点是 issues/09 的 `Instraction.Child.cs` 两处 `str = exm.ConvertStringType(str)`,改可空会把警告外溢到下游票,`!` 精确保留现有行为 |
| 数组可空性处(CS8619 6、CS8620 2) | 别名与拷贝处的局部、以及私有辅助方法(`XmlGetMethod.OutPutNode`、`RegexpMatchMethod.Output`)的数组参数改 `string?[]`;仅在元素于本地被保证填充时才用 `!` | 8 条全部是 `exm.VEvaluator.RESULTS_ARRAY`/`DataStringArray` 流向 `string[]` 局部与上述参数;元素可空是 issues/10 定下的契约(`VariableData`/`CharacterData` 字符串槽位),按「签名跟链走」以 `?` 反映 |

### 票外签名改动(实测)

`-p:Nullable=enable` 下所有文件同处 enable 语境,票外成员即便未启用也参与判定(与 issues/07「本票期间保持 nullable-oblivious」的预期不同)。本票实际改动:

| 文件(归属票) | 改动 | 原因 |
| --- | --- | --- |
| `Runtime/Script/Data/StrForm.cs`(issues/01,已启用) | `FormattedStringMethod.CheckArgumentType` override 返回改 `string?`;`FromWordToken` 内 `AExpression operand` 局部改 `AExpression?` | 跟随基类 `CheckArgumentType` 与 `ReduceExpressionTerm` 的可空契约 |
| `Runtime/Script/Data/IdentifierDictionary.cs`(issues/10,已启用) | `GetFunctionMethod` 的 `arguments` 参数改 `List<AExpression?>?`(方法体本以 `arguments == null` 表达「仅按名检索」);`GetFunctionMethod` 内 `string errmes`、`UserDefinedMethodTerm.Create` 的结果局部改 `string?`/`AExpression?` | 跟随 `CheckArgumentType` 与 `UserDefinedMethodTerm.Create` 的返回契约 |
| `Runtime/Script/Statements/ArgumentBuilder.cs`(issues/09) | `METHOD_ArgumentBuilder.CreateArgument` 的 `string errmes` 局部改 `string?` | 同上 |
| `Runtime/Script/Statements/CaseExpression.cs`(issues/09) | `LeftTerm`/`RightTerm` 字段改 `AExpression?` | 该类自身以 `if (LeftTerm != null)` 表达未赋值,`ExpressionParser.reduceCaseExpression` 赋入的正是可能为 null 的 `reduceTerm` 结果 |
| `Runtime/Config/ConfigData.cs`(issues/12) | `GetConfigValueInERB` 返回改 `SingleTerm?`、`errMes` 参数改 `ref string?` | 方法体以 `errMes = ...; return null;` 表达失败,非空声明与语义不符(口径同 issues/06 的查找 API) |
| `UI/Game/EmueraConsole.cs`(issues/12) | `CBG_SetButtonImage` 的 `tooltip` 参数与 `ClientBackGroundImage.tooltipString` 字段改 `string?` | 参数默认值即 `null`,唯一读点用 `string.IsNullOrEmpty`;只改调用点局部不足以消除 CS8604 |
| `Emuera.Tests/Runtime/Script/Statements/Expression/ExpressionParserTests.cs`(issues/03) | 测试辅助 `Reduce` 以 `!` 承接 `AExpression?` | `ReduceExpressionTerm` 契约带出的唯一测试工程新增点 |

### 跨票联动

- issues/05:`List<AExpression>` → `List<AExpression?>` 已沿 `FunctionMethod` 6 个签名贯通至 override;本票消费其元素可空契约,527 条 `arguments[i]` 警告即其后果
- issues/06:`ExpressionMediator` 的 exm 保持非空(19 处实参 `null!`),本票不重开该决策;`OperatorMethodManager` 的 4 处 `new FunctionMethodTerm(...)` 均在 `if (method != null)` 之后,实测零 CS86xx,其构造器契约归 issues/05
- issues/06:`GlobalStatic`/`Config`/`Lang` 的「初始化前不可达即保持非空 + 声明处 `null!`」规则,适用于本票的 `FunctionMethod.Name`/`ReturnType` 与 `UserDefinedRefMethod` 四个属性
- issues/07:`LogicalLine.Position`(`ScriptPosition?`,record struct)在 `Creator.Method.cs` 的 `GlobalStatic.Process.getCurrentLine.Position.Value` 处产生 1 条 CS8629,故 issues/07「08 不消费本票新增的可空签名」对本条不成立;本票以注解层 `!` 消化
- issues/10:本链消费 `GetVariableToken → VariableToken?`、`GetFunctionMethod → AExpression?`、`GetMacro → DefineMacro?`、`ConstantData.GetCharacterTemplate* → CharacterTemplate?`、`VariableToken.GetStrValue → string?` 与字符串标量数组元素 `string?`;后两者是本票数组与局部标注的输入
- issues/09:本票提供 `arguments` 元素可空的处理规则与 `FunctionMethod` 方法体适配口径,09 的 `ArgumentBuilder.cs`(本票后 403 条)、`Instraction.Child.cs`(259 条)、`CaseExpression.cs`(16 条)按同一规则消化

### 验收

- 逐文件/逐批:`#nullable enable` → `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-incremental -p:Nullable=enable --nologo -v:minimal` → 清零该文件 CS86xx(同一警告在内联与末尾汇总各出现一次,以 MSBuild 末尾汇总为准)
- 默认构建回归:同上但不加 `-p:Nullable=enable`,CS86xx 必须为 0(已启用的 `StrForm.cs` 等不得因本票签名改动回归)
- 测试:`dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`;表达式侧由 `Runtime/Script/Data/StrFormTests.cs` 等覆盖,有测试支撑处以真检查替换 `!`
- 改动只有 `?`/`!`/`null!`/`#nullable` 与注解,不得新增 `?.`、新增分支或删除既有判空(本票实测的两处例外见「实施结果」)

#### 实施结果(2026-09-16)

- 范围内 12 个文件全部 `#nullable enable` 且各自 CS86xx 清零:范围内 719 → 0。
- 全量构建(`-p:Nullable=enable`)2,516 → 1,821;差额 = 本链清零 719 − 下游净增 24。
- 默认构建(不加 `-p:Nullable=enable`)CS86xx 为 0,已启用文件无回归(`StrForm.cs`、`IdentifierDictionary.cs`、`LogicalLine.cs`、`LexicalAnalyzer.cs`、`LogicalLineParser.cs`、`EraStreamReader.cs`、`VariableToken.cs`、`ConstantData.cs`、`VariableData.cs` 均 0)。
- `dotnet test`(`Debug-NAudio`,x64)全通过:144/144。
- `Emuera.Tests` 的 CS86xx 现为 19 条,清尾仍归 issues/03。
- `!` 口径:`Creator.Method.cs` 新增 390 处、`OperatorMethod.cs` 71 处,依据是 `CheckArgumentType`/`CheckArgumentTypeEx` 已在类型检查阶段拒绝必填槽位的 null;省略槽位(`OmitStart` 之后)按方法体既有分支处理,未被一律断言。
- 唯一非注解改动:`Creator.Method.cs` 的 ENUMFILES 方法类内把两个 `List.CopyTo` 换成逐元素拷贝(`VariableData.DataStringArray` 元素为 `string?`,BCL 的 `CopyTo` 不接受可空元素目标;截断长度与原先的 if/else 一致,口径同 issues/10 的 `copyListToArray*`);另有一处经确认的运行行为修正(`&&` → `||`),见下方既存问题 2。

下游落点(全量构建,按文件去重):

| 下游票 | 文件 | 变化 | 来源 |
| --- | --- | --- | --- |
| 09 | `ArgumentBuilder.cs` | +13 | `ReduceExpressionTerm → AExpression?`、`GetFunctionMethod` 的 `arguments` 可空 |
| 09 | `CaseExpression.cs` | +12 | `LeftTerm`/`RightTerm` 改 `AExpression?` |
| 12 | `DebugDialog.cs` | +2 | `ReduceExpressionTerm → AExpression?` |
| 12 | `EmueraConsole.cs` | −1 | `CBG_SetButtonImage` 契约澄清 |
| 12 | `ConfigData.cs` | −2 | `GetConfigValueInERB` 契约澄清 |

开工时发现的问题(2026-09-16 处置):

1. `UserDefinedRefMethodTerm.Restructure` 直接解引用 `srcArgs[i]`,而同链的 `FunctionMethodTerm.Restructure` 对 null 元素 `continue`;省略实参的引用函数调用会 NRE。**已就地补 `TODO` 注释**,迁移期不改行为。
2. `Creator.Method.cs` 的 SPRITEANIME 方法类内 `if (img == null && !img.IsCreated)`:`&&` 在 `img == null` 时仍求值右侧,现状即抛 NRE。**已按同文件其余精灵分支的口径(SPRITE 系为 `img == null || !img.IsCreated`)改为 `||`**,属本票唯一的运行行为变更(修正该分支本应返回 0 的路径)。
3. `ExpressionMediator.ConvertStringType` 的 `Strings.StrConv` 返回 `string?`,现以 `!` 断言(唯一调用点按非空打印消费)。**已补 `TODO` 注释**。
4. `Creator.Method.cs` 打印告警分支只判 `getCurrentLine != null`,未判 `Position`,现以 `!` 断言。**已补 `TODO` 注释**。

待办:按 spec 的完成标准,另需真实 era 游戏目录做一次运行行为确认后本票方可转 `implemented`。

## 曾考虑的替代方案

1. 把 `Creator.Method.cs` 拆成独立票或按其 `#region` 再切成多票:同一文件只能整体 `#nullable enable`,拆票不会减少同一文件内的相互影响,只会把一次清零验收拆碎;改以「整文件启用 + 按 region 分批清零」在单票内获得同样的定位能力
2. `argumentTypeArrayEx` 保持非空 + `null!`:与 `argumentTypeArray` 已是 `Type[]?` 的口径不对称,且 180 个类只设 `argumentTypeArray`、`CheckArgumentType` 显式判空,非空声明会把多数类的真实状态藏起来
3. 对 `arguments[i]` 一律 `!`、不区分位置:省略槽位允许 null 且方法体确有依赖该语义的分支,一律 `!` 会把省略参数语义从注解层抹掉
4. 把 `FunctionMethod` 的 `arguments` 恢复为非空 `List<AExpression>`、以哨兵项表达省略:推翻 issues/05/06 已落地的决策,并让 305 处触点回退
5. `ExpressionMediator` 的 exm 改 `ExpressionMediator?` 以避免调用点 `!`:已由 issues/06 否决(会把约 230 个实现的判空负担放大),本票不重开
6. `ExpressionMediator.ConvertStringType` 改返回 `string?`:会把可空外溢到 issues/09 的 `Instraction.Child.cs` 调用点,而该处现状即按非空消费

## 影响

表达式与方法链的元素可空语义落地到方法体,「`arguments` 列表中的 null 表示省略参数」成为全链显式契约;本票的 `arguments[i]` 规则与 `FunctionMethod` 基类契约是 issues/09 指令方法体适配的输入。本票完成后全库剩余 1,821 条,链簇为 issues/09(约 747 条,ArgumentBuilder 403 + Instraction.Child 259 + Argument 60 + FunctionIdentifier 8 + CaseExpression 16 + Instruction 1)、issues/11、issues/12 与收尾票 issues/03。
