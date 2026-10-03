# 实参契约表（阶段 A）：可省略槽的硬转型止血

Status: implemented

## 任务

本票原为 arg-contract effort 的深化票（方案形状、语义裁定与可行性结论见 [spec.md](../spec.md)）。范围收窄为**片 0 + 片 1（止血）**；深化本体（新槽位类型 + 183 个子类迁移 + 删旧签名适配器）已转出为 [arg-contract/issues/05](05-argument-slots-type.md)（`ready-for-agent`），本票只承担阶段 A。核对证据见「核对」节，落地结论并入「方案」与「影响」节。

本次（阶段 A）的完成标准：

1. **片 0：在当前 HEAD 重新基线。** 机械产出「类 × 槽 × 消费形态」清单（脚本 + 工件，口径与既有的 [slots-worklist.py](../slots-worklist.py) 一致，可复跑、自校验），并与 [arg-contract/issues/03](03-required-on-omittable-slot.md) 当时已铺的 50 个拒绝槽（本片追加 4 个后为 54）、[nullable-migration/issues/18](../../nullable-migration/issues/18-unguarded-as-casts.md) 的已落地面逐点对齐。
   票面原有的消费面数字（682 / 33 cast / 132–134 未证 / 366 覆盖）采集于 03 与 18 落地之前，**不再作为工作面依据**。
2. **片 1：可达 null 站点止血。** 按片 0 的清单逐点处置；已确证的最小集合是 **4 个硬转型槽**（issues/03 片 2-D 已裁定「显式空槽应拒绝」，并明确留给本片实现，以免与槽位类型两套改法）：
   - `EnumNameMethod[1]`：规则 `{String, RefString1D}`、`OmitStart = 1`；`GetIntValue` 在 `Count == 2` 下 `(arguments[1] as VariableTerm)!`
   - `EnumFilesMethod[3]`：规则 `{String, String|NotNull, Int|NotNull, RefString1D}`、`OmitStart = 1`；`Count == 4` 下同形
   - `DataTableSelectMethod[3]`：规则 `{String, String, String, RefInt1D}`、`OmitStart = 1`；`Count != 4` 走 RESULTS，故只有 `Count == 4` 时同形
   - `DataTableToXmlMethod[1]`：规则 `{String, RefString}`、`OmitStart = 1`；`Count > 1` 下同形
   处置：在**每一处可省略段**的该槽位加 `ArgType.NotNull`（`FunctionMethod.CheckArgumentTypeEx` 是「任一段通过即通过」，只标一段无效），显式空槽改由 `CodeEE(ArgCanNotBeNull)` 拒绝，**数量省略（少写尾实参）继续合法**。用例落在既有 seam [CheckArgumentTypeEx.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/CheckArgumentTypeEx.cs)（拒 + 正控制）。
   片 0 若发现其余可达站点（尤其 23 处活 `as VariableTerm` 与 10 处活 `(VariableTerm)` 里未被 03 的裁定覆盖的），按同一把尺子逐点处置并补用例。
3. **不在本次范围**：槽位类型、183 个子类的签名迁移、删除旧签名适配器 → 已转出为 [arg-contract/issues/05](05-argument-slots-type.md)。

## 背景（一句话）

`ArgTypeList` 是声明式实参契约，但它只有三个独立解释者（检查器 / 折叠 / 函数体），彼此无同步机制，编译器又因「形状是数据不是类型」而无法介入；存量后果是函数体侧的消费面（`arguments.Required(` 全仓 live 491、`arguments[<字面量>]` live 54、`arguments[i]` live 18、`arguments.Count` live 119）各自重签规矩，其中 33 处硬转型与「本槽位必已填充」的未证假设是缺陷爆发点。阶段 B 的靶子即这条 seam，阶段 A 只做可证明不回退的止血。

## 核对：票面事实与基线 HEAD 的差异

基线 HEAD `ddcf0f9f`。数字口径：**live = 剥注释后的活代码**（口径与 `slots-worklist.py` 一致）。

| # | 票面/原审查 | 核对结果 |
| --- | --- | --- |
| 1 | 「`FunctionMethodTerm` 只有 `IdentifierDictionary` 与 `METHOD_ArgumentBuilder` 两个构造点」 | **15 个构造点 / 4 族**（字面 `new FunctionMethodTerm(` 全库 14 处——`ArgumentBuilder.cs` 的 `METHOD_ArgumentBuilder.CreateArgument` 是 target-typed `new(...)`）：规则表 2 处（`IdentifierDictionary.GetFunctionMethod`、`METHOD_ArgumentBuilder.CreateArgument`）、运算符 4 处（`OperatorMethod.cs` 的 `OperatorMethodManager.ReduceUnaryTerm`/`ReduceUnaryAfterTerm`/`ReduceBinaryTerm`/`ReduceTernaryTerm`）、StrForm 8 处（`StrForm.cs` 的静态构造 5 处 + `StrForm.FromWordToken` 3 处：`FormatYenAt`/`FormatCurlyBrace`/`FormatPercent`）、TOSTR 包装 1 处（`Process.CalledFunction.cs` 的 `CalledFunction.TryConvertArg`）。后三族绕过规则表（`OperatorMethod.CheckArgumentType` 与 `FormattedStringMethod.CheckArgumentType` 直接抛 `ExeEE`）⇒「只能由检查器构造」只对规则表族成立 |
| 2 | 「20 处折叠期专属站点留在类型化访问器之外」 | ✔ 且更强：20 处 `.Restructure(exm)` **20/20 落在 14 个 `UniqueRestructure` 里**；全部 `arguments[x] = …` 写回也都在那 14 个方法内。**消费面（`GetIntValue`/`GetStrValue`）零 `Restructure` 调用**，两面不相交 |
| 3 | 决策 1「折叠必须并入或前移到检查之内」 | ✘ **前提不成立，已撤销**。`FunctionMethodTerm.Restructure` 传给 `UniqueRestructure` 的是 `[.. Arguments]` **副本**（全仓仅此 1 处该形态分配），那 14 个方法里的写回**全部被丢弃**；折叠对槽表唯一的实际影响是 `FunctionMethodTerm.Restructure` 自身的就地写，它本来就在检查之后 |
| 4 | 决策 2「不可变集合会迫使 `Restructure` 返回新 term」 | ✘ **不需要付这个代价，已撤销**。builtin 槽表不进 `RowArgs`/`CallformCache`——`GetFunctionMethod(..., userDefinedOnly: true)` 在查 `methodDic` 之前就 `return null`（6 个生产调用者逐一核过），`RowArgs` 只喂 UDF 面；`FunctionMethodTerm.Arguments` 类外只有读（`StrForm.cs` 的 `SingleIntFormTerm`）。真正的跨实例共享只有 StrForm 的 5 个 `static readonly` term，其重复折叠写的是**恒等值**（`NAME`/`TARGET` 均 `CanRestructure = false`，`VariableTerm.Restructure` 最终 `return this`） |
| 5 | 决策 3 候选门禁 `[Obsolete(error: true)]` | ✘ **拦不住**：实测（net10.0）对 override 只报 **warning CS0672** 并编译通过；`Emuera.csproj` 的 `WarningsAsErrors` 只覆盖 `nullable`；仓库无 CI |
| 6 | 片②「33 处活 cast」 | ⚠ 结论对、算式要改：33 = 23 处活 `as VariableTerm)!` + 10 处活 `(VariableTerm)`（`is VariableTerm` 不参与；原式 24+16+21−6 混了 raw 与 live）。且 [nullable-migration/issues/18](../../nullable-migration/issues/18-unguarded-as-casts.md) 已落地 4 处（XML_GET 分流、CMATCH 收紧规则、INRANGEARRAY/MAXARRAY 补 `HasUniqueRestructure`），故「可达 null」的集合必须以 HEAD 重新基线 |
| 7 | 片①「10 处可达 null」 | ⚠ 同上，需重新基线。机制上可达 null 只可能来自「可省略槽的显式空槽」或「引用型实参被折叠成 `SingleTerm`」两条路；后者由 issues/18 立的**不变量**（规则含 `AllowConstRef` 的引用位 ⇒ 类必须设 `HasUniqueRestructure`）覆盖 |
| 8 | 「184 个活的子类」 | 差 1：活类声明 184 = 外层 `FunctionMethodCreator` 1 + 子类 **183**；另有 3 条在注释里 |
| 9 | 「17 个 unique 方法每次折叠分配一次 `[.. Arguments]`」 | ✘ **错**：全仓只有 `FunctionMethodTerm.Restructure` 一处该分配。17 是 `HasUniqueRestructure = true;` 的**语句数**（13 个类；`UniqueRestructure` override 14 个） |
| 10 | 消费面规模（682 / 264 处 `arguments.Count` / 366 覆盖） | ⚠ 重新基线后的 live 口径：`arguments.Required(` 404（本文件）/ 491（全仓）、字面量下标 54、`arguments[i]` 18、`arguments[b+N]` 3、列表模式 16、`arguments.Count` 119、`.Cast<`/`foreach` 各 1、`Skip().ToArray()` 2、整表转发 helper 58+2+2+1+1。访问器覆盖（live）：`Int` 188、`Str` 152、`IsInt` 15、`IsStr` 17 = **372/404 = 92.1%**；`Var` 37 |

## 方案

### 阶段 A（本次范围）

#### 片 0：重新基线（脚本 + 工件）

生成器 [arg-slots.py](../arg-slots.py) → 工件 [slots-consumption.md](../slots-consumption.md)，沿用 `slots-worklist.py` 的形态（生成器 + 生成物同目录、头注释写口径、可复跑、缺项/多项即报警）。扫描范围是规则表族（`Creator.Method.cs`）——另三族绕过规则表、由调用点担保，已在工件头部写明。产出三张表：

1. **类 × 槽 × 消费形态**：逐 `FunctionMethod` 子类列出其规则段与每个槽位的读取形态（`Int/Str/IsInt/IsStr/Var/Count/判空/转发/折叠`），并给出「该槽位当前的判据是否保证非空」（无 `OmitStart` / `i < OmitStart` / `ArgType.NotNull` / `SameAsFirst` 的 0 号槽）。
2. **可达 null 候选**：把形态为「硬转型 / 判空后解引用」而判据不保证非空的 (类,槽) 逐条列出，附「数量省略是否越界」这把正交尺子的结论（口径同 issues/03 的「附」）。
3. **与邻票对齐**：与 issues/03 已铺的 `ArgType.NotNull` 槽、issues/18 的 4 处修法逐点比对，标出「已覆盖 / 归本片 / 归阶段 B」。

落地口径（live = 剥注释后的活代码）：`arguments.Required(` **404**（本文件）/ **491**（全仓）、字面量 `Required(<n>)` 371、非字面量 33、`arguments[<字面量>]` 54、`arguments[<变量>]` 18、`arguments.Count` 119、`as VariableTerm` 24、`(VariableTerm)` 10、`is VariableTerm` 10、`.Restructure(exm)` 20、列表模式 16、`Skip(` 2、`Cast<` 1、`HasUniqueRestructure = true;` 17 条语句 / 13 个类、`UniqueRestructure` override 14、全仓 `[.. Arguments]` 1、可省略 (类,槽) 全集 **96**。

96 项按「是否已在全部可省略段带 `ArgType.NotNull`」+「读该槽时类内有无判空写法」四分：**54** 已带标志（= issues/03 的 50 个拒绝侧 + 本片 4）、A 段（可空 ∧ 无判空 ∧ 硬解引用）**2**、B 段（可空 ∧ 有判空）**37**、A2 段（可空 ∧ 无判空 ∧ 机械未见硬解引用，读取走非字面量下标或整表转发）**3**；四段合计 96，脚本逐项自校验。

与 issues/18 对齐：该票立的**不变量**（规则含 `AllowConstRef` 的引用位 ⇒ 类必须设 `HasUniqueRestructure`）已做成机械核对，18 个引用位里 17 个受保护、1 个（`SumArrayMethod[0]`，角色数据段）按「`CONST` 与 `CHARADATA` 互斥、折叠路径不可达」豁免。

自校验项（脚本复跑即报警）：与 `slots-worklist.py` **同函数同口径**地数 `ArgType.NotNull`（口径与现行预期值见 [_scan.py](../_scan.py) 的 `NOT_NULL_EXPECTED`，数字不在此复述——阶段 B 起步后含注释口径由 87/76 上调为 88/77，剥注释口径仍 81 处 / 70 行）；`HasUniqueRestructure` 语句数 = 17 / 类数 13；`[.. Arguments]` 全仓 1 处；`arguments.Required(` 的本文件 live 计数 = 404。

观察项（不属本片，记此备查）：`Creator.Method.cs` 的 `ArrayMultiSortExMethod.UniqueRestructure` 从不被调用——该类未设 `HasUniqueRestructure`，是静态可证的**死 override**；因它的两条规则都不含 `AllowConstRef`，常量数组进不到该槽，故**无缺陷**，只是死代码（本票不改，避免与阶段 B 的类型化改造冲突）。

#### 片 1：止血

片 0 的 A 段只有 6 个候选，逐点裁定后**只有 4 个需要改代码**，即 issues/03 片 2-D 留给本片的四条「读输出数组」硬转型槽：

| (类,槽) | 规则段（改后） | 修前形态 | 数量省略尺子 |
| --- | --- | --- | --- |
| `EnumNameMethod[1]` | `{String, RefString1D \| NotNull}` `OmitStart = 1` | `ENUMFUNCBEGINSWITH("a",,)` 放行 null → `(arguments[1] as VariableTerm)!` | 过（`Count == 2` 才读） |
| `EnumFilesMethod[3]` | `{String, String\|NotNull, Int\|NotNull, RefString1D \| NotNull}` `OmitStart = 1` | `ENUMFILES("a","b",0,,)` 同上 | 过（`Count == 4` 才读） |
| `DataTableSelectMethod[3]` | `{String, String, String, RefInt1D \| NotNull}` `OmitStart = 1` | `DT_SELECT("t","f","s",,)` 同上 | 过（`Count == 4` 才读） |
| `DataTableToXmlMethod[1]` | `{String, RefString \| NotNull}` `OmitStart = 1` | `DT_TOXML("t",,)` 同上 | 过（`Count > 1` 才读） |

处置：在**每一处可省略段**的该槽位加 `ArgType.NotNull`（`FunctionMethod.CheckArgumentTypeEx` 是「任一段通过即通过」，只标一段无效），显式空槽改由 `CodeEE(ArgCanNotBeNull)` 拒绝，**数量省略（少写尾实参）继续合法**。四个类各只有一段规则块，故标志一处即覆盖全部可省略段。用例落在既有 seam [CheckArgumentTypeEx.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/CheckArgumentTypeEx.cs)（4 条拒绝 + 3 条数量省略 + 1 条正控制）。

片 0 表 2 的其余候选按同一把尺子逐点处置：能用规则表表达的（显式空槽是唯一入口）标 `NotNull`；不能表达的（引用位可被折叠）按 issues/18 的不变量补 `HasUniqueRestructure` 并写注释说明不变量，而不是只补判空。另两条 A 段候选经逐点核查为**有意可空**，不标：`DataTableCellSetMethod[3]`（`AExpression? v = arguments.Count > 3 ? arguments[3] : null` 把 null 原样交给 DBNull 分支）与 `DataTableColumnManagementMethod[1]`（跨构造器假阳性：`Required(1).GetStrValue` 在 `op == Names` 早退之后，Names 段只读 `arguments[1] is VariableTerm`）。片 0 未再发现其余可达站点（23 处活 `as VariableTerm` 与 10 处活 `(VariableTerm)` 里未被 03 的裁定覆盖的都在本片之外）。

### 阶段 B（已转出）

原写在本节的「视图 + 快照」设计预案（类型草案、访问器表、切片、三层门禁）已转出为 [arg-contract/issues/05](05-argument-slots-type.md)（`ready-for-agent`）；本票只承担阶段 A。

## 曾考虑的替代方案

1. **不可变槽位集合 + `Restructure` 返回新 term（原决策 2 的方向）**：放弃——它要求同步确认 StrForm 5 个静态 term、`SpCallFArgment.RowArgs`、`UserDefinedRefMethodTerm.srcArgs`（唯一持有调用方 list 本体的类）等共享语义的新形态，而核对 #4 证明 builtin 面本就没有跨 owner 共享；换成检查期快照即可拿到同一份「函数体不再自证非空」的收益，风险面小一个量级。
2. **把折叠并入/前移到检查之内（原决策 1）**：放弃——前提是「折叠会改变槽表判据」，而核对 #3 证明 `UniqueRestructure` 收到的是副本、写回被丢弃；折叠与消费两面不相交，动它只增加行为变更面。
3. **`[Obsolete(error: true)]` 作迁移门禁（原决策 3 的首选）**：放弃——实测对 override 只报 warning CS0672，`WarningsAsErrors` 也只覆盖 `nullable`，且仓库无 CI；改用「最终非虚化（编译期）+ 反射门禁测试 + 脚本期」三层。
4. **本次直接开工阶段 B（全量迁移 183 个类）**：放弃——682 处机械改写与高风险止血同批做，review 面不可控，且止血的价值不依赖类型落地；本次先取可证明不回退的部分。
5. **按 4 个 cast 槽的既有裁定，等槽位类型一并解决（issues/03 片 2-D 的原安排）**：放弃——那会把 4 处可达 NRE 一直留到阶段 B；同形的 7 处已在 issues/03 就地标 `NotNull`，本片照此办理。

## 影响

- **本次（阶段 A）改动面**：新增 `.scratch/arg-contract/arg-slots.py` + `slots-consumption.md`；`Creator.Method.cs` 的 4 个规则段各加一个 `ArgType.NotNull`（`EnumNameMethod[1]`、`EnumFilesMethod[3]`、`DataTableSelectMethod[3]`、`DataTableToXmlMethod[1]`）；`Emuera.Tests/.../CheckArgumentTypeEx.cs` 补 4 条拒绝 + 3 条数量省略 + 1 条正控制，并把解析夹具（原 `Shape`）与「按名取已注册方法」（原 `AssertMethodExists`）收编进新增的 `Emuera.Tests/.../BuiltinFunctionTestKit.cs`（检查器与执行期两处 seam 共用，`CheckArgumentTypeEx.cs` 内 13 处调用点随之改名：12 处 `Shape` + 1 处 `AssertMethodExists`；与本票的止血无逻辑耦合，只是同批工作里的夹具归并）；`.gitignore` 增 `__pycache__/`、`*.pyc`（`.scratch/` 下生成器运行产物）。片 0 的候选集未再扩大（另两条经核查为有意可空，不改代码）。
- **行为变更**：4 个槽的**显式空槽**由运行期 NRE 变为检查器判据拒绝的 `CodeEE(ArgCanNotBeNull)`（`FunctionMethod.CheckArgumentTypeEx`；默认在首次执行该行时抛出，时机见 issues/03 风险 6）；数量省略与合法写法不变。涉及的注册名：`ENUMFUNCBEGINSWITH`/`ENUMFUNCENDSWITH`/`ENUMFUNCWITH`/`ENUMVARBEGINSWITH`/`ENUMVARENDSWITH`/`ENUMVARWITH`/`ENUMMACROBEGINSWITH`/`ENUMMACROENDSWITH`/`ENUMMACROWITH`（9 个名字共用同一规则形状）、`ENUMFILES`、`DT_SELECT`、`DT_TOXML`。
- **阶段 B**：已转出为 [arg-contract/issues/05](05-argument-slots-type.md)（`ready-for-agent`），改动面与性能说明见该票。
- **不在本票范围**：issues/03 的片 5（B 表执行期语义用例）与片 6（指令侧 `ArgumentBuilder` 两轴定案）是与本票同批落地的工作，记录在 [issues/03](03-required-on-omittable-slot.md) 的对应小节，不属本票的完成标准。
- **邻票**：`arg-contract/issues/01/02/03`（01/02 已落地；03 余项 7「`docs/` 侧内建函数契约表」已落地为 [docs/builtin-functions.md](../../../docs/builtin-functions.md)，03 随之收尾）、`nullable-migration/issues/18`（已落地，本票按它的 `AllowConstRef ⇒ HasUniqueRestructure` 不变量收口）、`nullable-migration/issues/22`（其余 `as`+`!` 站点的理由注释，`ready-for-agent`）、`.scratch/backlog/issues/15`（`Argument` 三字段折叠协议，同一 seam 的另一侧，不并轨）。
- **原始审查记录**：`.scratch/backlog/issues/02`。
