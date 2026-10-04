# 实参契约(Argument Contract)

Date: 2026-09-27
Status: in-progress

## 问题/背景

来源：2026-09-27 的只读架构审查（五处热点并行），结论落在 `.scratch/backlog/issues/02`（深化）与 `.scratch/backlog/issues/03/04/16`（缺陷）。本 effort 承接它们。

式中関数（`FunctionMethod`，`BuiltinMethod/*.cs` 内 183 个**活的**子类 / 266 个活注册名——注册名现以类体上的 `[BuiltinMethod("NAME")]` 工厂声明（issues/18），此前口径为 `Creator.cs` 内行首 `["NAME"] = ` 的字面量表，含 4 条被注释的注册）用**数据**声明实参形状：114 个 `ArgTypeList` 规则块（91 个设了 `OmitStart`），另有类只用旧的 `argumentTypeArray`（`Type[]`）。声明方向是对的——184 个类里只有 `MesSkipMethod` override 了检查（`BuiltinMethod/GeneralPurpose.cs`）——但**一份声明有三个独立解释者**：

1. **检查器**：`FunctionMethod.CheckArgumentTypeEx`（`FunctionMethod.cs`），另有 4 个实现分散在 `OperatorMethod.cs` 的 `CheckArgumentType`、`StrForm.cs` 的 `FormattedStringMethod.CheckArgumentType`、`BuiltinMethod/GeneralPurpose.cs` 的 `MesSkipMethod.CheckArgumentType` 与指令侧 `ArgumentBuilder.cs` 的 `CreateArgument`
2. **折叠**：`FunctionMethodTerm.Restructure`（`FunctionMethodTerm.cs`）会**就地改写**实参表，并用 `SingleTerm` 替换整个 term
3. **函数体**：每个 `GetIntValue`/`GetStrValue` 自己重新回答规则表已经回答过的 5 个问题——有没有值 / 是值还是变量 / 哪个变量 / 几维 / 是否已被折叠

三者之间没有任何机制保持同步，而编译器无法介入，因为形状是**数据不是类型**：183 个子类的 C# 签名完全相同（`GetIntValue(ExpressionMediator, List<AExpression?>)`，基类默认实现见 `FunctionMethod.GetIntValue`）。

### 代价（均已核实）

- 函数体侧消费面约 **682 处取值 + 264 处计数**：`arguments.Required(` 405、`arguments[<字面量>]` 227、`arguments[i]` 47、`arguments[b…]` 3、`arguments.Count` 264；另有 `as VariableTerm` 24、`(VariableTerm)` 16、`is VariableTerm` 21（其中 6 处位于注释 ⇒ 33 处活 cast）
- 33 处活 cast 逐点判决：23 处类型可证且 null 安全、**10 处可达 null**、0 处可达 InvalidCastException
- **可能为 null 的槽约 132–134 处**（口径见 arg-contract/issues/03）的「本槽位必已填充」无从证明
- 检查器里两处已核实的判据缺陷（arg-contract/issues/01、arg-contract/issues/02）

### 关键前提（已核实，决定方案形状）

- **默认配置下不存在「解析期」折叠**：`ReduceArgumentOnLoad` 默认 `NO`，只有 `YES`/`AnalysisMode`/`FORCE_SETARG` 才在装载期折叠（`ErbLoader.cs` 里那个 `NeedReduceArgumentOnLoad || AnalysisMode || IsForceSetArg` 的条件）；普通指令行是**首次执行时**构造（`Process.ScriptProc.cs` 里 `ArgumentParser.SetArgumentTo` 的调用点），结果由 `ArgumentParser.SetArgumentTo`（`ArgumentParser.cs`）记忆
- **折叠不在稳态每调用热路径上**（每求值 0 次）。每调用成本 = 1 次虚派发 + N×(索引器 + 虚派发)，无装箱
- **仓库没有基准设施**（`BenchmarkDotNet`/`[Benchmark]` 零命中，50 个测试文件无一为性能测试）

## 整体方案

### 语义裁定（grilling 已定）

- **内建函数：逐槽可空性，由规则表声明**（2026-09-27 精化——原推荐「空槽一律按错误处理」被代码证据推翻）
  - **两个正交的问题压在同一字段上**：`OmitStart` 只回答「最少传几个」（**数量**省略 → 列表更短、不产生 null——`f(a,b,)` 得 `[a,b]`）；`null` 元素回答「这一槽被显式留空」（仅由显式空槽产生——`f(a,b,,)` 得 `[a,b,null]`、`f(a,,b)` 得 `[a,null,b]`）
  - **证据 1**：`DisallowVoid`（该标志今名 `ArgType.NotNull`，见下条）的用途正是「在可省略区间内单独标注某槽不可省略」（arg-contract/issues/03 片 1 前全库仅 `ArrayMultiSortExMethod` 的两段，片 1 又补了 `HtmlStringLenMethod`、`RegexpMatchMethod`、`RandMethod` 各一处）⇒ `i >= OmitStart` 的槽**默认允许为空**是既有设计意图
  - **证据 2**：函数体**有意**把 null 当作「显式留空」处理的槽，按 (类,槽) 去重有 34 个（工作表口径，见 arg-contract/issues/03 的 B 表；把未被 `Required` 读取的同类槽计入为 39——片 2-D 又补判 `DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]`——可省略 (类,槽) 全集 96）：`GraphicsDrawGMethod.GetIntValue` 的 `arguments.Count == 10 || arguments[10] == null` 即「不带颜色矩阵」，同类还有 `GetcharaMethod.GetIntValue`、`CsvStrDataMethod.GetStrValue`、`MatchMethod.GetIntValue`、`FindElementMethod.GetIntValue`、`MoneyStrMethod.GetStrValue`、`ToStrMethod.GetStrValue`、`GetMethMethod.GetIntValue`
  - **反例**：`RAND(1,,)` 的 null 无意义——slot 1 的 `OmitStart = 1` 表达的是「可以只写 `RAND(5)`」（数量省略），而 max 没有默认值可代，故不能一律放行
  - **第三类：转发槽**：函数体既不读也不解释，把实参原样交给下游（`GetMethMethod.GetIntValue` 的 `arguments.Skip(2)` → `GetFunctionMethod(…, methArgs, true)`）。这类槽的 null 由**被调方**解释——目标是 `#FUNCTION` 方法时取该 UDF 形参默认值，目标不存在时该槽不被读取——故规则表必须让它保持可空，不能用「本槽必已填充」的访问器
  - **第四类：消费型变长尾（arg-contract/issues/03 片 2-C 定案）**：函数体把尾部整条读完，没有默认值可代——`MaxMethod.GetIntValue` 的 `for (int i = 1; i < arguments.Count; i++) arguments.Required(i)`（`MAX`/`MIN`）、`ArrayMultiSortMethod.GetIntValue` 的 `foreach (VariableTerm term in arguments.Cast<VariableTerm>())`（`ARRAYMSORT`）——故整条标 `ArgType.NotNull`（`MAX(1,,2)`、`ARRAYMSORT(DA:0,,DA:1)` 由 NRE 变为 `CodeEE`）。与转发型的分别正是「谁解释这个 null」：转发型有下游，消费型没有；两型的数量省略（少写尾实参）都继续合法
  - **裁定**：函数体已处理 null 的槽在规则表**显式声明为可空**；其余可省略槽拒绝 null（`DisallowVoid` 一般化，今名 `ArgType.NotNull`）。该标志是这条路的半成品（片 1 前只用了 1 个类的 2 个段；片 3 已改名并在 A 表逐槽铺开）。**落地期不引入 `Nullable` 标志位**——放行本就是默认，故「显式声明为可空」由裁定表 + 规则段注释承载，只有拒绝侧逐槽标标志（见下条）
  - **落地顺序（arg-contract/issues/03 片 3，已落地）**：取 **B**——保持默认放行、把旧名 `DisallowVoid` 改名 `ArgType.NotNull` 并在 A 表逐槽铺开，理由是每次改动都可证明不回退（今天 NRE → `CodeEE`，被执行到的调用取值语义不变），而 A 要求先把 96 个可省略槽枚举完整、否则误拒现存写法。A（默认翻转、显式 `Nullable` 才放行）留作收尾条件：待 **96 个 (类,槽)** 的 `Nullable` 集合枚举完整后二者等价，届时可机械翻转；主流语言翻默认极性（TS `strictNullChecks`、C# NRT）都带非致命诊断作过渡，而 `CodeEE` 是硬错误（默认在首次执行该行时抛出，见 arg-contract/issues/03 风险 6），故不在枚举完整前翻。**枚举已由 arg-contract/issues/03 片 2-D 补齐**：补车道 20 项逐项过尺子并裁定（有意可空 5 / 转发 2 / 消费型变长尾 2 / 无意且可达 7 已标 `NotNull` / 硬转型 4 已由 [issues/04](issues/04-argument-slots.md) 阶段 A 标 `NotNull`），96 项全部有裁定；翻转动作归 [issues/05](issues/05-argument-slots-type.md)（阶段 B）——它要用槽位类型承载「显式声明为可空」，那 4 个 cast 槽的类型化移除也在 05
- **UDF 的空槽取已声明的默认值**：默认值来自函数标签 `@NAME(ARG, ARG:1 = 5)` 的形参默认表达式（`ErbLoader.parseLabel` 建立 `FunctionLabelLine.Def`；未写 `=` 的 ARG/ARGS/私有形参填 0/空串），代入点是 `CalledFunction.TryConvertArg`（`Process.CalledFunction.cs`）。这是现有设计意图
- **两族语义不统一**：UDF 走完全另一套机制（`UserDefinedRefMethod.ArgTypeList` + `CalledFunction.TryConvertArg` + `UserDefinedFunctionArgument`），把任一族语义套到另一族都会破坏既有脚本

### 形状：新槽位类型 + 基类新入口 + 切片迁移（选项 (c)；阶段 B，落地票 [issues/05](issues/05-argument-slots-type.md)）

- 规则表在检查期产出**已解析槽位集合**（每槽确定为：值 or 变量实体）；构造路径只有四条，都先过检查器或由调用点担保（规则表族的命中段投影、运算符 / StrForm / TOSTR 包装三族的调用点声明、旧签名门面、测试直调工厂，见 [issues/05](issues/05-argument-slots-type.md)）
- 基类提供**角色化访问器**（`Int(i)`/`Str(i)`/`IsInteger(i)`/`IsString(i)`/`Var(i)`…），逐槽角色只写进契约失败的报错文案、**不做运行期断言**（与旧 `Required(i)` 的「调用方担保」同强度：`Var(i)` 仍是转型、空槽仍须函数体先判），故函数体的判空与转型只是从 `arguments.Required(i)` + `(VariableTerm)` 收敛到访问器，**不是消失**
- 旧签名保留为**转发适配器**，183 个子类按切片迁移（**检查器由调用点担保**的族（运算符 / StrForm，其检查重载故意抛 `ExeEE`；在代码里由 `FunctionMethod.CallerOwnsCheck` 声明）与无规则表的类在适配器里**不问检查器**、按「调用点担保、无声明」解析；其余有规则表而检查失败则抛 `CodeEE`，见 [issues/05](issues/05-argument-slots-type.md)）
- **决策 1（顺序）——撤销**（理由与证据见 [arg-contract/issues/04](issues/04-argument-slots.md) 的「核对」#2/#3）：原定 `check → fold → 建集合`，且须把折叠并入/前移到检查之内。核对发现 `FunctionMethodTerm.Restructure` 传给 `UniqueRestructure` 的是 `[.. Arguments]` **副本**，那 14 个方法里的写回全部被丢弃；20 处折叠期站点 20/20 落在 `UniqueRestructure` 内，而消费面（`GetIntValue`/`GetStrValue`）零 `Restructure` 调用——两面不相交，折叠不必动
- **决策 2（别名）——撤销**：原担心不可变集合迫使 `Restructure` 返回新 term、须重新确认 `RowArgs`/`CallformCache`/StrForm 静态 term 的共享语义。核对发现 builtin 槽表**不进** `RowArgs`/`CallformCache`（`GetFunctionMethod` 的 `userDefinedOnly` 分支在查 `methodDic` 前即返回），类外只读 `FunctionMethodTerm.Arguments`，跨实例共享只有 StrForm 的 5 个静态 term 且其重复折叠写恒等值。⇒ 阶段 B 改为**检查期快照 + 只读访问器**，不引入新的共享语义（切片④ 后活体表经 `ArgumentSlots.FoldTarget` 暴露，只服务原地折叠与整表转交；访问器读快照。**等价性只覆盖读值、不覆盖求值时机**：外层不可折叠时访问器会重新求值已折叠的内层实参，见 [issues/05](issues/05-argument-slots-type.md) 的「快照语义定案」）
- **决策 3（可机械核对）**：C# 对「参数列表不同的同名方法」只当重载、对不再被调用的私有方法默认不报警，故漏迁的函数体会**静默**走基类 `GetIntValue`（`FunctionMethod.GetIntValue` 抛 `ExeEE`），失败暴露在运行期。必须三选一：旧虚方法挂 `[Obsolete(error: true)]` / 引入分析器 / 以调用计数为门禁（`arguments.Required(` 从 405 核到 0）。**实测**：`[Obsolete(error: true)]` 对 override 只报 warning CS0672、编译通过，项目也只把 `nullable` 提为错误且无 CI ⇒ 该候选作废，改用「最终非虚化（编译期 CS0115）+ 反射门禁测试 + 脚本期」三层，见 [issues/05](issues/05-argument-slots-type.md)；调用计数门禁的口径也要按 live 计数改（本文件 404、全仓 491），不是 405（阶段 B 落地后 **31 = 折叠面 25 + 检查器 3 + `FunctionMethodTerm` 1 + 指令侧 `ArgumentBuilder` 2**（检查器侧有 2 处 `SameAsFirst` 的期望类型读取收敛进新抽的 `ExpectedType`，故比迁移前少 2）；口径只覆盖内建函数实参契约相关的文件，UDF 侧另有 `Process.CalledFunction.cs` 6 处、`Process.State.cs` 1 处不在本门禁内，门禁只对**消费面**归零，见 [issues/05](issues/05-argument-slots-type.md)）
- **决策 4（理由必须是契约安全，不是速度）**：本重构在 per-call 上拿不到可测收益；可省的成本在构造/折叠期（`FunctionMethodTerm.Restructure` 传给 `UniqueRestructure` 的 `[.. Arguments]` 副本——全仓仅此 1 处该分配，见 issues/04 核对 #9——与 `VariableTerm.Restructure` 的重复 `CheckElement`）。任何性能主张**必须先补基准**
- **决策 5（槽位三态）**：槽位类型必须表达**缺失**（数量省略、列表更短）/ **显式留空**（null 元素）/ **有值**三态；「可空性」是与 `OmitStart` 正交的逐槽声明。这直接来自上面的精化——若只做「有值 / 无值」两态，`GraphicsDrawG` 的「不带颜色矩阵」就没有表达方式。三态落到 `Has(i)`/`IsEmpty(i)`/`IsMissing(i)`；[issues/04](issues/04-argument-slots.md) 只做止血，三态随 [issues/05](issues/05-argument-slots-type.md) 的类型落地

### 接口可行性（访问形态普查）

`Int`/`Str`/`IsInt`/`IsStr` 覆盖 **372/404 = 92.1%**（重新基线后的 live 口径，见 [issues/04](issues/04-argument-slots.md) 的「核对」#10）。难点在剩下的 7.9%，设计必须逐个消化：

1. 可能为 null 的槽 → 访问器须显式选语义（抛异常 / 返回「可能有」句柄）
2. **20 处 `Restructure` 站点是折叠期 API、不是函数体 API** → 不可能用只读访问器覆盖，必须留在另一接口上或并入检查器
3. `VariableTerm` 实体槽（405 中 11 处 + 索引器路径约 34 处）→ 独立 `Var(i)`；读写语义不同（`SetValue` 7、`GetFixedVariableTerm` 7）
4. 54 处类型询问（`SameAsFirst` 规则）→ 访问器必须暴露 operand type
5. 3 处把 `Required(i)` 原样传给别的 helper → 需要「原始 term」逃生口
6. **转发槽**（`GetMethMethod.GetIntValue` 的 `arguments.Skip(2)` 交给下游函数调用约定）→ 不属于本集合的访问器，需要「整段原样转交」语义
7. `Int(i)`/`Str(i)` 必须对 `Ref` 槽继续返回变量当前值（今天成立，`VariableTerm` 本身是 `AExpression`）

## 曾考虑的替代方案

1. **(a) 只深化检查器、接口不动**：放弃——它消不掉 33 处 cast 与 682 处消费面（cast 正是缺陷的爆炸点），seam 停在函数体之前等于没修根因；且若 03/04 已按本 effort 的第一批独立落地，(a) 就退化成那两票加「`Required` 改成真检查」，不构成 deepening
2. **(b) 一次性换掉 184 个 override 的签名**：放弃——正确性本应由编译器保证，但 C# 对漏迁不报警（决策 3），一次做完的 review 面不可控；且没有中间态可供分批验收
3. **统一内建与 UDF 的空槽语义**：放弃——UDF 有逐参数默认值、内建没有，统一会破坏其中一族
4. **保持 682 处消费面不动，只加运行期真检查**：放弃——把 NRE 变成有文案的脚本错误是改善但非深化，作为 03 号票的一部分另做
5. **退回手写校验（给每个函数 override `CheckArgumentType`）**：放弃——`argumentTypeArrayEx` 的声明式方向是对的（184 个类只有 1 个 override），要兑现不要退回
6. **内建函数「任何 null 都报错」**：放弃——会破坏 `GraphicsDrawG` 的「不带颜色矩阵」等既有写法，并需删掉约 34 处**有意**的 null 分支；且与 `DisallowVoid`（今 `ArgType.NotNull`）的存在相矛盾（见「语义裁定」的精化）

## 影响

- 改动面：`FunctionMethod.cs`（规则表与检查器）、`BuiltinMethod/*.cs`（114 个规则块 + 约 682 处消费点）、`FunctionMethodTerm.cs`、`ArgumentBuilder.cs`、`IdentifierDictionary.cs`、`ArgumentListExtensions.cs`；`UserDefinedMethodTerm.cs`（UDF 侧同型站点在 ref 分支上，已核实为死代码，见 arg-contract/issues/03）
- 交付顺序：01（Array3D 标志位）→ 02（OmitStart 判据）→ 03（可省略槽上的 `Required`；UDF 侧已核实为死代码，不在其中）→ 04（阶段 A 止血：重新基线 + 4 个硬转型槽标 `NotNull`，已落地）→ 05（阶段 B 类型深化：槽位类型 + 183 个子类迁移）。01/02 已落地；03 的片 1 是高优先、低风险的止血，片 2/3/4（A 表 41 槽铺 `NotNull`、B 表 34 槽裁定为「有意可空」、A/B 用例网 76 槽 + UDF 默认值 1 条）也已落地，片 2-C 把变长尾部按转发型/消费型逐块定案（`MAX`/`MIN`、`ARRAYMSORT` 标 `NotNull`），片 2-D 把工作表从 76 补到全集 96：补车道 20 项逐项过「数量省略越界」尺子并裁定（7 处「无意且可达」补标 `NotNull` + 用例、2 处「有意可空」补注释、4 处硬转型归 04），变量下标 `Required(i)` 与 helper 传参路径随之并入。03 的余项已全部落地——最后的余项 7（`docs/` 侧内建函数契约表）产出为 [docs/builtin-functions.md](../../docs/builtin-functions.md)（266 个注册名的逐槽形状 + 空槽语义 + 说明，生成器 [builtin-contract.py](builtin-contract.py) 可复跑），03 随之转为 `implemented`。原列的三条里，B 表执行期语义抽查（片 5，46 条用例，覆盖 27/34 槽）与指令侧 `ArgumentBuilder` 的两轴模型定案（片 6，**定案为「不对齐」**——指令侧即函数侧被否决的 A 极性，工件为 `instruction-args.py`/`.md`）已落地；那 4 处硬转型已由 04 的阶段 A 标 `NotNull` 收口（cast 本身的类型化移除归 05）。同批复核发现的另两条（未被 `Required` 读取的 3 个有意可空槽补注释、两处代码注释不再指向票据）已就地落地
- 测试：`CheckArgumentType`/`ArgTypeList`/`argumentTypeArrayEx` 原先**零命中**——114 个规则块是仓库里最大的一块未测行为。本 effort 的表驱动用例是该面的首个覆盖，落在 `Emuera.Tests/Runtime/Script/Statements/Function/CheckArgumentTypeEx.cs`
- 与既有票的关系：`.scratch/nullable-migration/issues/08-expression-method-chain.md`（把「数量上可省略」写成「槽可以为 null」的出处）、`nullable-migration/issues/13`（`Required` 的引入）、`nullable-migration/issues/05`（省略实参以 null 承载的契约）——`nullable-migration/issues/08` 与 `nullable-migration/issues/13` 已随 `arg-contract/issues/03` 回填
- 本 effort **不改**：`argumentTypeArrayEx` 的声明式方向、183 个子类的扁平结构、`ArgumentListExtensions.Required` 的存在理由（其注释的「调用方担保」措辞在 03 号票里更正）
