# Nullable:其余成员与局部非空断言的复核

id: nullable-migration-21
Status: in-progress

## 任务

本票的处数继承自 issues/13 的收尾表，不是独立枚举。2026-09-21 在 HEAD `1ed33b5c` 上同口径复测（出现次数、剥注释与字符串字面量）：全库 **1088** 处（`Emuera` 981 / `Emuera.Tests` 107），其中 `null!` 393、`Argument` 可选字段 98、其他 `list[i]!` 78、`(x as T)!` 63、`JumpTo` 族 43，五类合计 675 处已由 issues/14 的复核对象 1/2/5/6/7 覆盖；**余下的「其他成员/局部 `!`」是减法残差 411 处（`Emuera` 内 356），占 37.8%**，不在其中任何一条里。本票承接 issues/14 新增的第 8 条复核对象。

**口径更正（2026-09-25，清单落地时实测）**：上段的 1088 与 411 都含匹配式 `\s*` 带来的**前缀逻辑非误计**——`case true when !v.IsString`、`return !(...)`、`operator !=` 实现体一类共 **12 处**（全在 `Emuera/` 内，且全部落在「其他成员/局部」这一类）。收紧为 `[\w\)\]]!(?!=)` 后：全库真实后缀断言 **1076 处**（`Emuera` 969 / `Emuera.Tests` 107），残差类 **399 处**（`Emuera` **344** / `Emuera.Tests` 55）。本票的清单即 `Emuera/` 内的 344 处，见 [bang-sites.md](../bang-sites.md)。

票面的 384/83/79/51/40/401 与 issues/13 的 1839 均不可复核（写票树上有四种口径，分别为 1694/1745/1904/1964）；issues/13 的 1040 与 issues/14 的 1075 也不是代码差异，而是「按匹配行数（不剥注释/字符串）」与「按出现次数（剥注释/字符串）」两个计数单位。两个「移除冗余的 `!`」提交（`e70ec779`/`cb8661ba`）只删了 19 处，3dd766f1→HEAD 净 +13（`Emuera.Tests` +25 来自新增测试、`Emuera` −12）。复测时匹配式应收紧为 `[\w\)\]]!(?!=)`：`[\w\)\]]\s*!(?!=)` 会把前缀逻辑非计入，HEAD 有 12 处会误计（`3dd766f1` 时为 0，故写票时不可见）。清单落地时已按此收紧并剔除这 12 处。

这类断言的形态是「接收者本身非空」而非「集合元素非空」，例如对字段、局部变量与容器成员的解引用——可空容器成员的索引（`dict![key]`，落点 `Runtime/Utils/PluginSystem/VariableDataWrappers.cs` 10 处、`PluginManager.cs` 2 处、`Rikaichan` 的 `edict![` 1 处）、集合字段（`Emuera/UI/Game/EmueraConsole.cs` 的 `escapedParts!` 2 处；`Emuera/Runtime/Utils/EvilMask/Shape.cs` 的 `radius!` 4 处与同目录 `Utils.cs` 的 `nums![i]` 1 处，后两者不在 `UI/` 下）、以及各类成员访问后的再断言。它们没有统一的可空性来源，因此需要先枚举再逐点判定。

## 方案

1. **先出清单**。按 issues/14「口径与清单」的口径枚举本类站点，给出稳定站点 ID（`<文件>#<类型>.<成员>#<序号>`，不含行号）与接收者类型。本类由减法定义，故清单前须先固定其余分类边界；机械可分的子桶为裸标识符/局部断言、`X.Member!`、`F(...)!`、`X![i]`、`default!`。排除项（2026-09-21 实测）：成员/属性声明的 `= null!` 238 处、`ArgumentListExtensions` 内 2 处、`Emuera.Tests` 107 处 / 19 个文件。清单入库，作为判定与验收的锚点。**已完成（2026-09-25）**，产物为 [bang-sites.md](../bang-sites.md)，构成见下「清单」。
2. **按接收者类型分组判定**，沿用 issues/14 的三类结论（成立 / 不成立但不可达 / 可达缺陷）。同类型接收者共用判据，避免逐点重复推理：
   - 容器字段（`Dictionary`、`List`、数组）：构造点是否保证非空、是否有 `Dispose`/重置路径把它置 null；
   - 局部变量：赋值路径是否覆盖全部读取点；
   - BCL 返回值：该成员是否可空、是否有越界/未命中语义。
3. **可达缺陷不并入本票**，按根因另立票（与 issues/15~20 同法）；不成立但不可达的记入潜在缺陷清单。
4. **保留的断言由 issues/22 补理由注释**，本票只产出判定；冗余断言同票清理。

### 清单（2026-09-25 实测）

`Emuera/` 内残差类共 **344 处**，落在 63 个文件、198 个 `类型.成员` 内，稳定 ID 见 [bang-sites.md](../bang-sites.md)（同时给出接收者、接收者根标识符的声明类型、源码行，并附按声明类型分组的判定索引，ID 在站点表与分组索引中各出现一次，可核对无遗漏、无重复）：

| 子桶 | 处数 |
| --- | --- |
| 裸标识符/局部断言（`bare/local`） | 167 |
| `X.Member!` 成员访问 | 80 |
| `F(...)!` 方法调用结果 | 64 |
| `X![i]` 断言后索引（`dict![key]`） | 30 |
| 其他表达式（`other-expr`，括号表达式后断言） | 2 |
| `default!` | 1 |
| **合计** | **344** |

与票面的对账：344 = 残差 411 − 12（前缀逻辑非误计）− 55（`Emuera.Tests` 部分）。清单的「根声明类型」列去重后为 90 个声明类型（剥去 `(成员)`/`(局部)`/`(参数)` 角色后缀为 70 个；[bang-sites.md](../bang-sites.md) 的分组索引按「声明类型+成员」给出 146 组），其中 290 处的声明类型已解析（非调用 253 处、`F(...)!` 调用链的根 37 处）；余 54 处未解析，是继承成员（`Text`、`Position`、`CurrentLabel`）、其他类型的成员链与静态成员、强制转换表达式（`(InstructionLine)logicalLine`）等，判定时分担到对应组内核实。

清单口径已用**三种实现交叉验证**（2026-09-25）：同一收紧式在 `Emuera/` 内，IDE(Rider) 文本搜索与 PowerShell/.NET 正则各得 **979**（未剥注释/字符串），本脚本剥后得 **969**，差的 10 处位于注释或字符串字面量内；全库剥后 **1076** = 969 + `Emuera.Tests` 107。三种实现一致，说明枚举无遗漏、无重复。

**锚点与后续漂移（2026-09-26 复核）**：清单对应 `1ed33b5c`（2026-09-21）到落地提交 `03292664`（2026-09-25）之间的代码——该区间内 344 条源码片段逐一命中，是判定与验收的唯一基准。首个删除发生在 `e9eaaf32`（2026-09-25），此后实施类提交共删除 **53 条**站点，清单未逐条回填，读表时须以该区间为基准：

| 已消失的站点 | 处数（结论） | 处置提交 |
| --- | --- | --- |
| `ArgumentBuilder` 的 `SP_PRINT_IMG#1`、`SP_PRINT_SHAPE#1`/`#2` | 3（C） | `e9eaaf32`（issues/15） |
| `IdentifierDictionary.GetVariableToken#2`、`Process.handleException#1`/`#2`、`PluginManager.ExecuteLine#1`/`#2`、`PluginManager.GetStackTrace#1`/`#2` | 7（C 4 / B 1 / A 2） | `9fe67fb6`（issues/19，issues/25 由它承接） |
| `ExpressionParser.local#1`/`#2` | 2（A） | `ab93db4f` |
| `Dictionary<string, int>?` 局部的 `dict![key]` 21 处（清单「`X![i]`」桶，见下方判定表） | 21（A） | issues/29（`GetKeywordDictionary` 拆分） |
| `VariableStrArgTerm.GetIntValue` 的 `dic!`（已判空组，见下方判定表） | 1（A） | issues/29（同上） |
| `InstructionLine.PopArgumentPrimitive()!` 18 处（清单「`F(...)!`」桶）+ `GetConfigMethod.GetSingleTerm` 的 `term!`（「`bare/local`」桶） | 19（A） | `59cd307e`（Try 方法补抛异常入口） |

因此本清单在 HEAD 上只剩 **291 条**可定位站点（C 9 / B 8 / A 237 / D 37）；全库后缀断言数也随之变化（`1ed33b5c`：Emuera 969 / Tests 107；`ea66bed1`：Emuera 944 / Tests 125；`c82aa022`：943；issues/29 拆分后：Emuera 921 / Tests 125），计数变化不作为判定依据。该序列只记到 issues/29：`59cd307e` 起未续测绝对值（复刻计数脚本与 2026-09-25 实现的绝对值不对齐），该批只按站点级核对记为 `Emuera` −19 / `Emuera.Tests` +1。

### 判定结论

三类结论沿用 issues/14：**A** 不变量成立（保留 + 理由注释）；**B** 不变量不成立但当前不可达（记录潜在缺陷）；**C** 可达缺陷（按根因另立票）。本票另加 **D** 类：**断言内容为假，但被调方显式容忍该 null**——`!` 只在编译期存在，运行语义不变，正确修法是把类型写诚实（归 issues/22 §3），既非保留理由也非缺陷。

已完成判定 **344 / 344** 处，**不存在未判定项**（其余 227 处由并行审计产出，逐站点结论见 [bang-sites.md](../bang-sites.md) 的「结论」列与「判定进度」表）：

| 组（接收者声明类型） | 处数 | 结论 | 依据 → 去向 |
| --- | --- | --- | --- |
| `MixedNum?` 局部（`ref height!` 一类 `ref` 实参） | 15 | **D** | `HtmlManager.tagAnalyze` 的 `img`/`div` 分支把 `MixedNum? height = null` 等局部按 `ref` 传给 `Utils.ParseMixedNum`；该函数第一步就是 `if (num == null) num = new MixedNum(); else throw CodeEE(DuplicateAttribute)`——`ref` 参数正是为观察初始 null 而设，故这 15 处 `!` 的内容为假。被调方显式容忍 null，运行行为不变；参数声明应由 `ref MixedNum` 改为 `ref MixedNum?`，15 处 `!` 随之删除。同组 `ParseParam4IntNum(ref CreateIfNull(ref box!).color!, …)` 与 `CreateIfNull<T>(ref T obj) where T : new()` 同型。→ issues/22 §3 |
| `Dictionary<string, int>?` 局部（`dict![key]`） | 21 | **A**×21 | `ConstantData.GetKeywordDictionary` 是求值入口：`index >= 0` 时缺字典即抛 `CodeEE(CanNotSpecifiedByString)`，故断言在「函数为所查变量码返回字典」时成立：18 处 `VariableDataWrappers` 与 2 处 `PluginManager` 的 `variableCode` 全部命中主 switch（`CSTR`/`CFLAG`/`ABL`/`GLOBAL`/`DAY`…，构造点见 `PluginAPICharContext`/`PluginManager.CacheVariables`），`dic` 必非空，属 **A**。余 1 处 `ConstantData.KeywordToInteger` 曾判为 C 类（它把 `varname` 硬编码为 `null`，对 ERD-only 码必然拿到 null），**复核后更正为 A**：`KeywordToInteger` 的两处调用点（`VariableEvaluator.SetValueAllEachChara` 的两个重载）自身不带门禁，门禁在其调用方 `Instraction.Child` 的 `CVARSET_Instruction.DoInstruction` 内（`p.Identifier.IsArray1D && index is SingleStrTerm` 时调 `ConstantData.isDefined(code, key)`，不成立即抛 `CodeEE(NotDefinedKey)`）；而 `isDefined` 自身也以 `varname = null` 走同一探测，故对 ERD-only 码返回 false、门禁先抛 `CodeEE` —— ERD-only 码到不了该处；能到达的码必在主 switch 内，`dic` 非空。`UseERD` 默认为 `true`（`ConfigData.cs`）不改变结论。原票 issues/23 已按 `wontfix` 关闭。该组 21 处已由 issues/29 拆出 `TryGetKeywordDictionary` 与非空入口而全部删除（2026-09-27），归属从「保留 + 理由注释」改为「契约改造消除」 |
| `AExpression?` 成员（`CaseExpression.LeftTerm`/`RightTerm`） | 14 | **A** | 唯一构造点 `ExpressionParser.reduceCaseExpression` 在 `LeftTerm == null` 时抛 `CodeEE`（NothingAfterIs / CanNotOmitCaseArg），`RightTerm` 仅在 `CaseType == To` 时赋值且 null 时抛 `CodeEE`（NoExpressionAfterTo）；各读取处自带 `CaseType` 门禁（`Reduce` 仅 `To` 读 `RightTerm`；`ToString` 与两个 `GetBool` 都先判 `To`/`Is`）。保留 `!` 并注明「构造点已保证」。→ issues/22 |
| `VariableStrArgTerm.GetIntValue` 的 `dic!` | 1 | **A** | 同族函数但**已判空**：先以 `IsNullOrEmpty(key)` 抛 `CodeEE`，再以 `dic == null && key != ""` 抛 `CodeEE(NotDefinedErdKey)`，故断言处 `dic` 必非空。该处已由 issues/29 删除（2026-09-27）：守卫收窄为 `dic == null`（`key` 在上方 `IsNullOrEmpty` 处已抛，`&& key != ""` 恒真），`dic!` 随非空入口消失，断言与理由注释都不再需要，归属改为「契约改造消除」 |
| `EmueraConsole?` 成员（`console!`） | 15 | **A** | 两个来源。① `MainWindow` 13 处：`console` 只在构造器赋值一次（`console = new EmueraConsole(this)` 位于构造器直线代码内），全库无 `console = null`（其余出现全是 `console == null` 判空），故实例方法内恒非空。② `ParserMediator` 2 处：`Warn` 仅在 `console is { RunERBFromMemory: false }` 时才把警告加入 `warningList`，故 `FlushWarningList` 的循环体只在 console 非空时执行；`Initialize` 只在进程初始化（`Process.cs`）与分析模式启动（`Program.cs`，该模式不产生警告）各调一次。保留 `!`。**收敛机会**：`MainWindow.console` 构造后恒非空，字段应声明为 `private readonly EmueraConsole console;`，一次删掉 13 处 `!`，并使 20 余处 `if (console == null)` 成为可证死代码 → issues/22 §3 |
| `CircularBuffer<string>?` 成员（`lineBuffer!`）+ `System.Timers.Timer?` 成员（`minTimer!`） | 10 | **A** | 类级不变量「`Config.CBUseClipboard` 为真 ⇔ `Init()` 已执行（两字段非空）」由三个入口共同保证：构造器 `if (!CBUseClipboard) return;` 之后才 `Init()`（`ClipboardProcessor` 构造器）、菜单开关先 `Init()`/`Reset()` 再写配置（`MainWindow` 的菜单处理）、配置对话框**不含** `CBUseClipboard`（`ConfigDialog.SaveConfig` 只写其余 `CB*` 项），故 `Reset()` 只在功能开启时到达，`minTimer!.Dispose()` 的前提成立。各读取点另有 `if (!Config.Config.CBUseClipboard) return;` 或 `if (!Initialized) return;` 守卫。保留 `!` + 理由注释 → issues/22 |
| `int[]` 局部（`this.sizes!`） | 6 | **A** | `sizes` 由基类构造器依 `CharacterData.CharacterVarLength` 取得：该函数只对 `i >= 0xF0` 的用户定义码返回 null，普通码返回非空数组、标量码返回空数组（`sizes.Length == 0` 的 `CodeEE` 分支正对应后者）；用户定义码由派生构造器以 `data.Lengths` 替换。6 处都在构造完成后的方法内，恒非空。保留 `!` + 理由注释 → issues/22 |
| `ExpressionMediator` 成员的 `exm!` | 3 | **D** | 已在 issues/22 §3 列明：`Process.State.cs` 的 `IntoFunction` 的 `exm` 参数「调用方传 null 是既定语义」，参数应改可空。本票只登记，不另立动作 |
| `.Position!` 家族（`FunctionLabelLine`/`InstructionLine`/`LogicalLine`/`ProcessState` 的 `Position`） | 23 | **A** | `LogicalLine.Position => scriptPosition` 由各构造器的 `ScriptPosition? thePosition` 参数写入，而全库唯一的上游是 `ErbLoader.loadErb` 的 `position` 变量：该函数里两处 `new ScriptPosition(...)`（`(Filename, LineNo)` 与 `(Filename, -1)`）都经 `LogicalLineParser.ParseLine` 进入构造点，**没有** `new InstructionLine(null, …)` 一类。故「`Position` 可空」的签名只服务于未接线的路径（`LogicalLine` 的注释「position == null 的行不参与排序」在当前代码里没有构造点）。保留 `!` + 理由注释 → issues/22 |
| `Process.getCurrentLine!` | 1 | **A** | `Process.getCurrentLine => state.CurrentLine`，`currentLine` 确可被置空（`ProcessState` 在「呼ぶべき関数が無ければ null」时置空）。该处（`IntoFunction`）在 `if (Program.DebugMode)` 与 `if (exm != null)` 双重门禁内：`exm != null` 表示脚本驱动的调用，此时正在执行的行就是调用点，必非空；系统发起的事件调用传 `exm == null`（issues/22 §3 已记的既定语义），走的是只用 `label.Position` 的 `else` 分支。保留 `!` + 理由注释 → issues/22 |
| `CurrentLabel!` 家族（`CalledFunction.CurrentLabel`） | 8 | **A** | 构造点只有三个私有工厂且全部赋值非空：`CallFunction`、`CreateCalledFunctionMethod`、`CallEventFunction`（经 `ShiftNext()` 命中事件表——`GetEventLabels(key) != null` ⇒ `LabelDictionary` 注册时构造的 `[onlylist, prilist, normallist, laterlist]` 至少一组非空，且每个标签至少进一组，故从 `counter = -1` 起必命中）。唯一的置 null 路径是事件迭代耗尽（`ShiftNext`/`ShiftNextGroup`/`FinishEvent`），而它与 `functionList` 的移除发生在同一次 `ProcessState.Return` 内：CurrentLabel 为 null ⇒ `currentLine` 为 null ⇒ 随之 `RemoveAt`。因此 `functionList` 中任何条目的 `CurrentLabel` 恒非空，`ClearFunctionList`/`Begin`/`Return` 的遍历、`IsOnly`、`CallLabel`、`IntoFunction` 都建立在该不变量上。保留 `!` + 理由注释 → issues/22 |

对手册的两点补充（供 issues/22 收录）：A 类的理由注释需求不止 `(x as T)!`（其 §1），本票产出的成员断言组同样需要；D 类是同票 §3「契约诚实化」的新增项。

### 判定结论：批次 1/2/3（并行审计，177 处）

批次按文件区域切分后委派并行审计（三个批次合计 177 处）。**逐站点结论已写入 [bang-sites.md](../bang-sites.md) 的「结论」列**（344 行逐点可核对，表尾「判定进度」给出计数），本节只列分组判据与去向。

| 组 | 处数 | 结论 | 依据 → 去向 |
| --- | --- | --- | --- |
| `InstructionLine.PopArgumentPrimitive()!` | 18 | A | `argprimitive` 全库只在 `InstructionLine` 构造器赋值（由 `LogicalLineParser.ParseLine` 喂入非空 `CharStream`），`PopArgumentPrimitive()` 首次取走后置 null；`ArgumentParser.SetArgumentTo` 以 `line.Argument != null` 早退，保证每个 `CreateArgument` 至多执行一次 → issues/22。该组 18 处已由 `59cd307e` 改为非可空返回、重复取用抛 `ExeEE(MissingArg)` 而全部删除（2026-09-27），归属从「保留 + 理由注释」改为「契约改造消除」 |
| `XmlDocument/XmlNode.SelectNodes(path)!` | 6 | A | .NET 契约：`SelectNodes` 只在 XPath 为空串或文档无根元素时返回 null；6 处传字面量或脚本实参（空串的 XPath 语义是「上下文节点的全部子节点」，不是空表达式），且 `doc` 均已 `LoadXml` 成功 → issues/22 |
| `AConsoleDisplayNode.Text` 家族（`ConsoleStyledString`/`ConsoleImagePart`/`ConsoleDivPart`/`ConsoleShapePart`） | 14 | A | 声明为 `string?`，但 5 个具体子类的构造器都无条件赋非空值（`Text = str` / `""` / `AltText` / `errMes`）→ issues/22 |
| `ConsoleStyledString.Font` | 4 A / 1 B | A（4） | `Font == null ⟺ Error`（置 `Error` 只在构造器与 `ResetLayout`），四个读取点都先 `if (Error)` 返回。B（1，`PrintStringBuffer.getDivideIndex`）：该方法不查 `Error`，正常排版下 `Error` 部件宽度为 0 故不可达；破坏者是「Error 部件参与宽度累加」→ issues/19 |
| `ConsoleStyledString.Text` | 5 | A | 公有构造器 `Text = str` 与 `DivideAt` 的对象初始化器都无条件赋值，与 `Error` 无关 → issues/22 |
| `Strings.StrConv(...)!` | 5 | D | VB `StrConv` 的可空签名源于「入参为 null 时原样返回 null」，5 处入参类型上非空 → 断言内容为假但运行行为不变；`ExpressionMediator`/`BuiltinMethod/StringOperations.cs` 已有同义 TODO → issues/22 §3 |
| `StyledBoxModel? box` / `box.color` / `ButtonValueStr` / `fontname` / `RealBitmap` / `ConsoleButtonString` 构造器 `console` / `CBGSETButtonSprite` 的 `imgN`/`imgB` / `SP_REF` 的 `name!` / `CircularBuffer<T>.Dequeue` 的 `default!` / `parseLabel` 的 `def` / `CALLSHARP` 的 `pluginArgs[i].strValue!` | 14 | D | 断言内容为假但被调方显式容忍：`Utils.CreateIfNull<T>`/`TryParseStyledBoxModel` 以 null 为正常入参、`StringStyle` 构造器对空 `fontname` 回退到配置值、`ImgUtils.LoadImage` 结果紧接着被判空、`CBG_SetButtonImage` 以 null 表示「无名图」、`RefArgument` 把 null 存进可空字段 `SrcTerm`、`_buffer` 为 `T[]` 写入 `default` 而槽位在下次 `Enqueue` 前不被读 → issues/22 §3 |
| `WordCollection.Pointer` / `Next` / `Remove` | 5 A / 1 B | A（5） | `Next` 的 `index + 1 >= Collection.Count` 守卫与 `EOL => Pointer == null` 共同保证「未到链尾」；`Remove()` 的调用点都在 `while (!EOL)` 内。B（1）：`LexicalAnalyzer.expandFunctionlikeMacro` 的循环条件 `while (macroEnd == wc.Pointer)` 疑似写反（`macroStart` 与 `macroEnd` 不相等，循环体不执行），若改成 `!=` 则行尾 `)` 会让 `wc.Pointer` 变 null → issues/19 |
| `Clipboard.GetDataObject()` / `sender as BackgroundWorker` / `ASpriteSingle.Bitmap` / `escapedParts` / `Parent` / `SynchronizationContext.Current` 等 UI 组 | 24 | A | 各有同方法内的前置门禁（`GetDataObject() == null \|\| !ContainsText()`、`DoWork += bwDoWork` 的委托契约、`IsCreated` 判断、`addDisplayLine` 里 `p.Parent = button` 紧接唯一一处 `ConsoleEscapedParts.Add`） → issues/22 |
| `ConstantData.output` | 2 | B | 唯一赋值点在 `LoadData`，生产调用链的 `console` 恒非空；但同方法内自带 `output != null` 守卫、且 `Emuera.Tests` 的 `TestBootstrap`/`ConstantDataTests` 以 `LoadData(dir, null!, false)` 传 null。破坏者：非空 output 的调用者传 null（当前仅测试）→ issues/19 |
| `EnumNameMethod.array` / `FunctionLabelLine.CompareTo(other!)` / `YenAtSubWord.Right` / `deleteLine` 的 `BufferToSingleLine` | 4 | B | 均为「当前不可达但存在破坏者」：`EType` 新增值漏改 `switch`；以 null 显式调 `CompareTo`；`SetIsMacro` 链当前无外部调用者；窗口销毁后仍调 `deleteLine` → issues/19 |
| `AExpression?` 局部（`SP_PRINT_IMG`/`SP_PRINT_SHAPE` 的 `arg`） | 3 | C | `ReduceExpressionTerm` 在空 `TermStack` 上遇 `,`/`\0` 返回 null，而循环只挡 `!wc.EOL`，省略实参照样进循环 → 与 issues/15 同根因，建议补入其范围表 |
| `CALLSHARP_Instruction.DoInstruction` 的 `term!` | 1 | C | `CALLSHARP "m", 1, , 3` 一类省略实参使 `arg.RowArgs` 含 null → issues/15 范围表第 2 行已记录 |
| `BREAK_Instruction` 的 `jumpTo.LoopCounter!` | 1 | C（判据更正） | 原判据「`WHILE`/`DO` 中的 `BREAK` 空引用」不成立：解引用前有 `if (jumpTo.FunctionCode != FunctionCode.WHILE && jumpTo.FunctionCode != FunctionCode.DO)` 守卫（`Instraction.Child.cs` 的 `BREAK_Instruction.DoInstruction`），`WHILE`/`DO` 直接跳出、不读计数器；且 `LoopCounter` 由 `REPEAT_Instruction` 写入并同时服务于 `REPEAT` 与 `FOR`（`FunctionIdentifier.cs` 把 `FunctionCode.FOR` 映射到 `REPEAT_Instruction(true)`），并非「只由 `REPEAT` 赋值」。真实缺口是：`jumpTo` 由 `nestCheck` 按词法嵌套配对，`GOTO` 跳进 `REPEAT`/`FOR` 体内的 `$标签`（`GOTO_Instruction.SetJumpTo` 只校验标签存在，不禁止跳入循环体）时循环头从未执行，`LoopCounter` 仍为 null，而 `BREAK` 没有判空短路（同文件的 `CONTINUE_Instruction`/`REND_Instruction` 都有） → issues/27（判据与复现路径已同步更正） |
| `IdentifierDictionary.GetVariableToken` 的 `line!.ParentLabelLine!` | 1 | C | 显式 `@子键` 分支缺少空子键分支的 `line?.ParentLabelLine == null ⇒ CodeEE` 收窄，而 `GetNewLocalVariableToken` 立即读 `func.LocalLength` 等 → issues/25 |
| `LogicalLineParser.ParseLine` 的 `parentLine!`（VARI/VARS） | 2 | C | 首个 `@函数` 之前 `lastLabelLine` 为 null，`ErbLoader.loadErb` 只警告后继续解析 → issues/24 |
| `DebugDialog.getValueString` 的 `term!` | 1 | C | `ReduceExpressionTerm` 对空白表达式返回 null，`IsNullOrEmpty` 挡不住；被同函数 `catch (Exception)` 兜住，属低危 → issues/26 |
| 其余 65 处 A（`PopAssignmentDestStr`、`SP_REF` 的 `refVar!`、`XmlAttribute.OwnerElement`、`FunctionMethod.CheckArgumentTypeEx`、`ReferenceToken.IsArrayRangeValid`、`VariableIdentifier` 的 `Enum.GetName(...)!`、`GetScaningLine()!`、`SubWord.Words`、`addLine` 的 `lastLine`、`PopRowArgs()!` 等） | 65 | A | 逐站点依据见 [bang-sites.md](../bang-sites.md) 的「结论」列与该行源码；共同点是「构造点/前置门禁/类型分派保证非空，编译器看不到」 → issues/22 |

**去向汇总**（批次 1/2/3 共 177 处）：A 141 / B 8 / C 9 / D 19。C 类 9 处中 4 处归既有 issues/15、2 处 issues/24、1 处 issues/25、1 处 issues/26、1 处 issues/27（`SP_PRINT_SHAPE` 的 2 处与 `SP_PRINT_IMG` 的 1 处同根因，计入 issues/15）；B 类 8 处记入 issues/19 的潜在缺陷清单；D 类 19 处归 issues/22 §3。

三张表的处数按清单逐站点结论对齐（2026-09-26 复核）：主表 117 处（A 99 / D 18）、批次 1/2/3 177 处（A 141 / B 8 / C 9 / D 19）、批次 4 50 处（A 42 / B 1 / C 7），合计 344 = A 282 / B 9 / C 16 / D 37。

一处**判定更正**：`ArrayMultiSortExMethod.GetConvertedTerm` 的 `term!` 一度被判为 C，取证 `CheckVariableTerm` 的首行 `if (!(arg is VariableTerm varTerm) || …) return …` 后确认它对 null 安全（`is` 模式对 null 为 false，随即 `throw CodeEE`），故改判 **A**。

### 判定结论：批次 4（并行审计，50 处）

| 组 | 处数 | 结论 | 依据 → 去向 |
| --- | --- | --- | --- |
| `ConfigData.GetRequired*`（五个入口） | 5 | A | 契约见声明处注释（调用点全传已注册的 `ConfigCode` 常量） → issues/22 补契约注释 |
| `ConfigItem<T>.val`（`ValueToString` 2 处） | 2 | A（`!` 冗余） | `val` 只在构造器与 `Value` setter 赋值，全库无 `.Value = null`；T 的实际实例化均非空 → issues/22 |
| `ProcessState.CurrentLine`（`runScriptProc`、`ShiftNextLine`）/ `eventLabelList` / `CallEventFunction` 的 `line` / `ExpressionArgument.Term`（4）/ `SAVEDATA_TEXT`（2）/ `Program.AnalysisFiles` / `Exception.StackTrace`（2）/ `AssemblyData` 两处 / `SelectNodes` / `loadLangXML` 的节点与属性 / `innerEllipse`（2）/ `radius`（4）/ `CreateIfNull` 后的 `nums` / `Path.ChangeExtension` / WebP 的 `bmp`（4） | 35 | A | 各有构造点、前置门禁或「被调方补出非空」的依据；`radius` 与 `ConfigItem.val` 的 `!` 属可删冗余（形参本就是非可空 `int[]`；T 未约束时编译器本不报警） → issues/22 |
| `Config.DefaultFont` / `JSONConfig.Load` / `ReadInt64Array3DExtended` / `OpenOnCache` | 4 | C | 契约即「读取或构造失败返回 null」（`CreateFont` 的 `catch { return null; }`、`Deserialize<T>` 对 `null` 文件返回 null、EOF 处的 `ReadLine()`、`Preload` 缓存写入 null），读取点却断言非空 → **issues/28**（坏输入路径） |
| `Process.handleException` 的 `current!` | 1 | C | `handleException(e, null, true)` 是合法调用（`Process.Initialize` 就这么调），而 `position != null` 分支内无条件解引用 `current` → 已在 issues/19 的范围表内 |
| `PluginManager.ExecuteLine` 的两处 | 2 | C | 入参未校验：空行/非指令行、`FunctionIdentifier.Instruction == null` 的命令 → 已补入 issues/19 的范围表 |
| `PluginManager.GetStackTrace` 的 `ProcessState.CurrentLine!` | 1 | B | 插件从自建线程调用即破坏；仓库内唯一入口是 ERB 调用的 `IPluginMethod.Execute` → issues/19 |

批次 4 与协调者自身的独立取证**相互印证**：`radius`/`innerEllipse`/`GetRequired*`/`SAVEDATA_TEXT`/WebP `bmp` 五组由双方分别读码得出同一结论（`radius` 的 `!` 冗余、`innerEllipse` 受同一局部 bool 门禁、WebP 的 `bmpData != null` 蕴含 `bmp` 非空）。

**最终去向汇总**（344 处 = 主表 117 + 批次 1/2/3 177 + 批次 4 50）：A 282（保留 + 理由注释 → issues/22）、B 9（潜在缺陷 → issues/19）、C 16（缺陷票：issues/15 4 处、issues/19 3 处、issues/24 2 处、issues/26 1 处、issues/27 1 处、issues/28 4 处、issues/25 1 处已 superseded 给 issues/19）、D 37（契约诚实化 → issues/22 §3）。

**A 类的载荷性抽样验证**（2026-09-25）：从 10 个 A 组各取 1 处（`MainWindow.console`、`ParserMediator.console`、`lineBuffer`、`minTimer`、`this.sizes`、`CaseExpression.LeftTerm`、`called.CurrentLabel`、`getCurrentLine`、`line.Position`、`VariableStrArgTerm.dic`），临时删掉该处 `!` 后全量构建——10 处全部报出 CS86xx（共 12 条，`VariableToken.cs` 与 `Process.State.cs` 各有 1 处连带 2 条），随后回退、构建恢复为 0。说明这些断言确实承担编译期职责（编译器看不到跨方法的不变量），「保留 + 理由注释」的处理正确，没有出现「删掉也不报错」的冗余断言。

完成标准：清单内每处给出结论与依据；本类断言在收尾时不存在「未判定」项——这是 issues/14「每处给出结论」能够成立的前提。

## 影响

- 本票是 issues/14 完成标准的必要条件：清单产出前，剩余断言无法声称已逐点复核
- 预计产出若干缺陷票；`Dictionary`/`List` 成员断言如果集中在少数构造点，可能像 issues/13 那样收敛为单点入口，届时在同一票内评估
- 本票不改动 issues/14 已判定的六类断言
- 截至 2026-09-26，方案第 4 步的「冗余断言同票清理」尚未执行：标为可删冗余的 6 处 `!`（`radius` 4 处、`ConfigItem<T>.val` 2 处）仍在树上；A 类理由注释、D 类契约诚实化由 issues/22 承接。这是本票保持 `in-progress` 的原因
- 2026-09-27：`59cd307e` 按「Try 方法补抛异常入口」删除 19 处 A 类站点——18 处 `PopArgumentPrimitive()!`（改为非可空返回、重复取用抛 `ExeEE`）与 `GetConfigMethod.GetSingleTerm` 的 `term!`（`GetConfigValueInERB` 拆 Try/非空入口），清单可定位 310 → 291；同批新增 5 个测试文件 15 例，`Debug-NAudio` 全量 357 通过、两种声音后端配置 CS86xx 均为 0
