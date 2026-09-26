# Nullable:解析期以 null 求值上下文折叠元素数表达式

Status: in-progress

## 任务

`#LOCALSIZE`/`#LOCALSSIZE` 与 `#DIM` 的元素数解析处，把通用表达式交给 `Restructure` 时传入 `null!` 作为求值上下文。多数表达式在折叠时不触碰该上下文，但可折叠（`CanRestructure` 为真）的项里有一部分要经上下文取值，分两类：调试标识符（`__LINE__` 等）经 `exm.Process` 取当前行；`EXISTCSV`/`GETNUM`/`GETNUMB` 等函数经 `exm.VEvaluator` 读 csv/常量表。前者仅在运行期 `Program.DebugMode`（命令行 `-DEBUG`）为真时注册——该开关为假时这些标识符注册为 `EmptyIntToken`/`EmptyStrToken`，其 `CanRestructure` 仍为 true 但取值不触碰上下文；后者与 `Program.DebugMode` 无关，故普通模式下 `#DIM ARR, EXISTCSV(1)`、`#LOCALSIZE MAX(EXISTCSV(4), 1)` 解析即命中 NRE。这里的开关是运行期配置，不是 `#if DEBUG` 条件编译。

后果随路径不同：ERB 内的两处被 `ParseSharpLine` 的 `catch (Exception)` 转成等级 2 警告并丢弃该指令；ERH 的全局 `#DIM` 走 `ErhLoader.analyzeSharpDimLines`（其 catch 只接 `CodeEE`），NRE 逃逸到 `Process.Initialize` 的 `catch (Exception)` 才被处理。故复现判据不是「是否崩溃」。

本票让这两处持有真实求值上下文。

### 范围

| 位置 | 现状 |
| --- | --- |
| `Parser/LogicalLineParser.cs`（`#LOCALSIZE`/`#LOCALSSIZE` 处理） | `arg.Restructure(null!)`，`arg` 来自通用整型表达式归约。现形态为 `is not SingleLongTerm { IsInteger: true } sizeTerm`，由 `83a959af style: 使用 IsString / IsInteger 简化…` 从 `is not SingleLongTerm sizeTerm \|\| sizeTerm.GetOperandType() != typeof(long)` 改写而来；`{ IsInteger: true }` 对 `SingleLongTerm` 恒真，原判据被当冗余删除，`Restructure(null!)` 未动 |
| `Data/UserDefinedVariable.cs`（`#DIM` 元素数处理，`UserDefinedVariableData.Create`） | 同上（写票时路径即为 `Data/`，非 `Statements/Variable/`）；`arg` 来自 `ExpressionParser.ReduceIntegerTerm(wc, TermEndWith.Comma_Assignment)` |

### 相关但不属本票

常量折叠处的 `GetIntValue(null!)`/`GetStrValue(null!)` 实参共 18 处（`ArgumentBuilder.cs` 14、`VariableEvaluator.cs` 的 `FindChara` 两重载 4），另有 `ExpressionParser.ToStrFormTerm` 的 1 处 `strf.GetString(null!)`（票面原先记的「约 65 处」是 issues/14 对全部「调用实参位 `null!`」的计数，口径不同）。它们是安全的，但判据分三类：`ArgumentBuilder` 各处由 `is SingleTerm`/`allConst` 支配，`ToStrFormTerm` 由 `strf.IsConst` 支配，`FindChara` 两重载没有此类判据——它靠 `FindcharaMethod` 的 `ArgType.CharacterData` 保证实参是 character-data 变量，而该类 token 的取值不读 `exm`；单值 term 的取值也确实不触碰上下文。它们不是缺陷，但契约不实（安全性依赖局部判据而非不变量），其具名入口的引入归 issues/22。本票不重复处理。

## 方案

1. 两处把 `null!` 换成 `GlobalStatic.EMediator`：`LogicalLineParser.cs` 的 `#LOCALSIZE`/`#LOCALSSIZE` 分支、`UserDefinedVariable.cs` 的 `#DIM` 元素数循环。理由写在 `UserDefinedVariableData.Create` 一处（它的两个调用方覆盖 ERH/ERB，且同函数的初值折叠早已同口径），`ParseSharpLine` 处只留指向该处的指针。

2. 判定依据——这两处不是「不该在无上下文的阶段折叠」，而是「折叠时缺上下文」。`CanRestructure` 的语义是「实参全为常量即可在解析期定值」，而 `EXISTCSV`/`GETNUM`/`GETNUMB` 这类函数的值本就只依赖解析前已就绪的数据（csv、常量表），解析期折叠是设计意图；缺的只是求值上下文实例本身，而它在全部调用路径上都已存在：`Process.Initialize` 在载入 ERH 之前赋值 `GlobalStatic.EMediator`，ERB 更在其后，`ReloadErb`/`ReloadPartialErb` 继承同一实例。同一个 `Create` 的初值折叠早已用 `GlobalStatic.EMediator`，本次是让元素数与初值同口径。

3. 已知残留（本票不改）：`__LINE__` 作为元素数折叠出的是「解析期当前行」，无当前行时为 `-1`，随后被既有的元素数范围检查拒绝（`#LOCALSIZE` 报 `LocalsizeLessThan1`、`#DIM` 抛 `CodeEE`）；reload 等有当前行的场合会折出一个正数而被接受。这与 `#DIM X = __LINE__` 的既有行为一致，不是本次新增；若要把它定为硬约束，需另行处理 `CanRestructure` 语义、不并入本票。

4. 测试（`Emuera.Tests`）：不复现字面 `__LINE__` 用例。两条理由：其一，`Program.DebugMode` 是 `private set`、token 身份在 `VariableData` 构造时就按它定死，字面覆盖需先把该开关翻成真（生产代码加 seam 或反射改私有字段），再重建 `VariableEvaluator`/`IdentifierDictionary`/`VariableData`——而 TestBootstrap 的初始化是一次性单例（`TestBootstrap.cs:19-24`），换全局量会外溢到其他用例；其二，夹具的 mediator 是 `new ExpressionMediator(null!, vEvaluator, null!)`（`TestBootstrap.cs:59`），原触发点所读的 `exm.Process` 恰为 null。`__LINE__` 与 `EXISTCSV` 折叠时都会解引用传入的 `exm`，但读的是不同成员（`exm.Process` 与 `exm.VEvaluator`）。故以普通模式可达、覆盖 `exm.VEvaluator` 那一支的 `EXISTCSV` 为代表：
   - `LogicalLineParserTests`：`#LOCALSIZE MAX(EXISTCSV(4), 1)` 解析成功且 `LocalLength == 1`（`#LOCALSSIZE` 与被修的是同一行，不另加用例）；
   - `LogicalLineParserTests`：`#DIM ARR, MAX(EXISTCSV(4), 1)` 解析成功且私有变量已登记；
   - `Data/UserDefinedVariableTests`（新增）：直接从 `UserDefinedVariableData.Create` 钉住 ERB/ERH 共用的折叠点（`isPrivate:false` 分支）；不经 `ErhLoader.analyzeSharpDimLines`，该入口「NRE 逃逸而非被吞」的后果差异不在覆盖范围。
   - 已知缺口：`exm.Process` 那一支（调试标识符）在本票内无测试覆盖；本票只保证两处站点不再把 null 当求值上下文传下去。

## 曾考虑的替代方案

1. 对需要上下文的表达式拒绝折叠，保留 `VariableTerm` 形态留待运行期求值：要在解析期判定「该表达式的取值是否触碰 `exm`」，就得让这一属性在整棵表达式树上完整传播，而 `FunctionMethod` 一侧约 190 个实现里已存在读 `exm.VEvaluator` 的可折叠者（`EXISTCSV`/`GETNUM`/`GETNUMB`）——只标注 token（`__LINE__` 一族）会漏掉它们，缺陷照旧；标注全部函数则要求逐个判定「是否读上下文」，代价与收益不匹配。且这些函数的值解析期可定，拒绝折叠会把本可用的写法（如 `#DIM ARR, EXISTCSV(1)`）一并挡掉。
2. 把三个 Debug token 的 `CanRestructure` 置 false：`CanRestructure` 是全局折叠判据，改动面远超本票；还会使 `#DIM X = __LINE__` 在 `Program.DebugMode` 为真时报「初值必须是常量」、为假时正常折叠，产生与开关相关的行为不对称。

## 影响

- 行为变更在解析期，与 `Program.DebugMode` 无关：元素数/初值中出现读上下文的折叠（`EXISTCSV`/`GETNUM`/`GETNUMB` 等）按设计折出值，不再 NRE——此前 ERB 路径把它吞成等级 2 警告并丢弃指令，ERH 路径逃逸到 `Process.Initialize`
- `Program.DebugMode` 为真时 `#DIM ARR, __LINE__` / `#LOCALSIZE __LINE__` 由 NRE 变为「折出解析期当前行（无当前行时 `-1`）→ 被既有范围检查拒绝」；该开关为假时的标识符语义不变
- 与 issues/22 的具名入口工作相邻但不重叠：本票处理两处真缺陷，issues/22 处理安全但契约不实的其余调用点
