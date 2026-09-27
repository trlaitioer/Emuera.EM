# 实参契约(Argument Contract)

Date: 2026-09-27
Status: in-progress

## 问题/背景

来源：2026-09-27 的只读架构审查（五处热点并行），结论落在 `.scratch/backlog/issues/02`（深化）与 `.scratch/backlog/issues/03/04/16`（缺陷）。本 effort 承接它们。

式中関数（`FunctionMethod`，`Creator.Method.cs` 内 184 个**活的**子类 / 270 个注册名）用**数据**声明实参形状：114 个 `ArgTypeList` 规则块（91 个设了 `OmitStart`），另有类只用旧的 `argumentTypeArray`（`Type[]`）。声明方向是对的——184 个类里只有 `MesSkipMethod` override 了检查（`Creator.Method.cs:3652`）——但**一份声明有三个独立解释者**：

1. **检查器**：`FunctionMethod.CheckArgumentTypeEx`（`FunctionMethod.cs:138-307`），另有 4 个实现分散在 `OperatorMethod.cs:22`、`StrForm.cs:296`、`MesSkipMethod:3652` 与指令侧的 `ArgumentBuilder`
2. **折叠**：`FunctionMethodTerm.Restructure`（`FunctionMethodTerm.cs:33-63`）会**就地改写**实参表（`:53`），并用 `SingleTerm` 替换整个 term
3. **函数体**：每个 `GetIntValue`/`GetStrValue` 自己重新回答规则表已经回答过的 5 个问题——有没有值 / 是值还是变量 / 哪个变量 / 几维 / 是否已被折叠

三者之间没有任何机制保持同步，而编译器无法介入，因为形状是**数据不是类型**：184 个子类的 C# 签名完全相同（`GetIntValue(ExpressionMediator, List<AExpression?>)`，`FunctionMethod.cs:360`）。

### 代价（均已核实）

- 函数体侧消费面约 **682 处取值 + 264 处计数**：`arguments.Required(` 405、`arguments[<字面量>]` 227、`arguments[i]` 47、`arguments[b…]` 3、`arguments.Count` 264；另有 `as VariableTerm` 24、`(VariableTerm)` 16、`is VariableTerm` 21（其中 6 处位于注释 ⇒ 33 处活 cast）
- 33 处活 cast 逐点判决：23 处类型可证且 null 安全、**10 处可达 null**、0 处可达 InvalidCastException
- **可能为 null 的槽约 116–135 处**（两种口径，见 issues/03）的「本槽位必已填充」无从证明
- 检查器里两处已核实的判据缺陷（issues/01、issues/02）

### 关键前提（已核实，决定方案形状）

- **默认配置下不存在「解析期」折叠**：`ReduceArgumentOnLoad` 默认 `NO`，只有 `YES`/`AnalysisMode`/`FORCE_SETARG` 才在装载期折叠（`ErbLoader.cs:1314`）；普通指令行是**首次执行时**构造（`Process.ScriptProc.cs:62-69`），结果由 `ArgumentParser.cs:65` 记忆
- **折叠不在稳态每调用热路径上**（每求值 0 次）。每调用成本 = 1 次虚派发 + N×(索引器 + 虚派发)，无装箱
- **仓库没有基准设施**（`BenchmarkDotNet`/`[Benchmark]` 零命中，50 个测试文件无一为性能测试）

## 整体方案

### 语义裁定（grilling 已定）

- **内建函数：逐槽可空性，由规则表声明**（2026-09-27 精化——原推荐「空槽一律按错误处理」被代码证据推翻）
  - **两个正交的问题压在同一字段上**：`OmitStart` 只回答「最少传几个」（**数量**省略 → 列表更短、不产生 null——`f(a,b,)` 得 `[a,b]`）；`null` 元素回答「这一槽被显式留空」（仅由显式空槽产生——`f(a,b,,)` 得 `[a,b,null]`、`f(a,,b)` 得 `[a,null,b]`）
  - **证据 1**：`DisallowVoid` 的用途正是「在可省略区间内单独标注某槽不可省略」（全库仅 `Creator.Method.cs:478-479` 一处）⇒ `i >= OmitStart` 的槽**默认允许为空**是既有设计意图
  - **证据 2**：约 34 处函数体**有意**把 null 当作「显式留空」处理：`GraphicsDrawGMethod:8373` 的 `arguments.Count == 10 || arguments[10] == null` 即「不带颜色矩阵」，同类还有 `GetcharaMethod:3031`、`CsvStrDataMethod:3124`、`MatchMethod:4646`、`FindElementMethod:5301`、`MoneyStrMethod:3976`、`ToStrMethod:6017`、`GetMethMethod:9933`
  - **反例**：`RAND(1,,)` 的 null 无意义——slot 1 的 `OmitStart = 1` 表达的是「可以只写 `RAND(5)`」（数量省略），而 max 没有默认值可代，故不能一律放行
  - **裁定**：函数体已处理 null 的槽在规则表**显式声明为可空**；其余可省略槽拒绝 null（`DisallowVoid` 一般化）。`DisallowVoid` 是这条路的半成品（只用了 1 次）
- **UDF 的空槽取已声明的默认值**：`@FOO(A = 1)` → `FunctionLabelLine.Def`，这是现有设计意图
- **两族语义不统一**：UDF 走完全另一套机制（`UserDefinedRefMethod.ArgTypeList` + `CalledFunction.TryConvertArg` + `UserDefinedFunctionArgument`），把任一族语义套到另一族都会破坏既有脚本

### 形状：新槽位类型 + 基类新入口 + 切片迁移（选项 (c)）

- 规则表在检查期产出**已解析槽位集合**（每槽确定为：值 or 变量实体），只有检查器能构造
- 基类提供**角色化访问器**（`Int(i)`/`Str(i)`/`IsInt(i)`/`IsStr(i)`/`Var(i)`…），函数体不判空、不 cast
- 旧签名保留为**转发适配器**，184 个类按切片迁移
- **决策 1（顺序）**：`check → fold → 建集合`。但实现上不是「把两个相邻调用换序」——检查在 `ExpressionParser.cs:320` 的嵌套构造里跑，折叠在同一 `CreateArgument` 里**稍后由 builder** 执行，所以必须把**折叠并入或前移到检查之内**
- **决策 2（别名）**：`IdentifierDictionary.cs:624` 不复制直接构造；CALLF/CALLFORMF 传 `SpCallArgment.RowArgs` 并被 `CallformCache` 跨调用复用。不可变集合会迫使 `Restructure` 返回**新 term**——须显式确认这些共享语义的新形态
- **决策 3（可机械核对）**：C# 对「参数列表不同的同名方法」只当重载、对不再被调用的私有方法默认不报警，故漏迁的函数体会**静默**走基类 `GetIntValue`（`FunctionMethod.cs:360-363` 抛 `ExeEE`），失败暴露在运行期。必须三选一：旧虚方法挂 `[Obsolete(error: true)]` / 引入分析器 / 以调用计数为门禁（`arguments.Required(` 从 405 核到 0）
- **决策 4（理由必须是契约安全，不是速度）**：本重构在 per-call 上拿不到可测收益；可省的成本在构造/折叠期（17 个 unique 方法每次折叠分配一次 `[.. Arguments]`、`VariableTerm.Restructure` 的重复 `CheckElement`）。任何性能主张**必须先补基准**
- **决策 5（槽位三态）**：槽位类型必须表达**缺失**（数量省略、列表更短）/ **显式留空**（null 元素）/ **有值**三态；「可空性」是与 `OmitStart` 正交的逐槽声明。这直接来自上面的精化——若只做「有值 / 无值」两态，`GraphicsDrawG` 的「不带颜色矩阵」就没有表达方式

### 接口可行性（访问形态普查）

`Int`/`Str`/`IsInt`/`IsStr` 覆盖 **366/405 = 90.4%**。难点在剩下的 10%，设计必须逐个消化：

1. 可能为 null 的槽 → 访问器须显式选语义（抛异常 / 返回「可能有」句柄）
2. **20 处 `Restructure` 站点是折叠期 API、不是函数体 API** → 不可能用只读访问器覆盖，必须留在另一接口上或并入检查器
3. `VariableTerm` 实体槽（405 中 11 处 + 索引器路径约 34 处）→ 独立 `Var(i)`；读写语义不同（`SetValue` 7、`GetFixedVariableTerm` 7）
4. 54 处类型询问（`SameAsFirst` 规则）→ 访问器必须暴露 operand type
5. 3 处把 `Required(i)` 原样传给别的 helper → 需要「原始 term」逃生口
6. `Int(i)`/`Str(i)` 必须对 `Ref` 槽继续返回变量当前值（今天成立，`VariableTerm` 本身是 `AExpression`）

## 曾考虑的替代方案

1. **(a) 只深化检查器、接口不动**：放弃——它消不掉 33 处 cast 与 682 处消费面（cast 正是缺陷的爆炸点），seam 停在函数体之前等于没修根因；且若 03/04 已按本 effort 的第一批独立落地，(a) 就退化成那两票加「`Required` 改成真检查」，不构成 deepening
2. **(b) 一次性换掉 184 个 override 的签名**：放弃——正确性本应由编译器保证，但 C# 对漏迁不报警（决策 3），一次做完的 review 面不可控；且没有中间态可供分批验收
3. **统一内建与 UDF 的空槽语义**：放弃——UDF 有逐参数默认值、内建没有，统一会破坏其中一族
4. **保持 682 处消费面不动，只加运行期真检查**：放弃——把 NRE 变成有文案的脚本错误是改善但非深化，作为 03 号票的一部分另做
5. **退回手写校验（给每个函数 override `CheckArgumentType`）**：放弃——`argumentTypeArrayEx` 的声明式方向是对的（184 个类只有 1 个 override），要兑现不要退回
6. **内建函数「任何 null 都报错」**：放弃——会破坏 `GraphicsDrawG` 的「不带颜色矩阵」等既有写法，并需删掉约 34 处**有意**的 null 分支；且与 `DisallowVoid` 的存在相矛盾（见「语义裁定」的精化）

## 影响

- 改动面：`FunctionMethod.cs`（规则表与检查器）、`Creator.Method.cs`（114 个规则块 + 约 682 处消费点）、`FunctionMethodTerm.cs`、`ArgumentBuilder.cs`、`IdentifierDictionary.cs`、`ArgumentListExtensions.cs`；`UserDefinedMethodTerm.cs`（UDF 侧同型缺陷）
- 交付顺序：01（Array3D 标志位）→ 02（OmitStart 判据）→ 03（可省略槽上的 `Required`，含 UDF）→ 04（深化）。01/02 是高优先、低风险的止血
- 测试：今天 `CheckArgumentType`/`ArgTypeList`/`argumentTypeArrayEx` **零命中**——114 个规则块是仓库里最大的一块未测行为。本 effort 的表驱动用例是该面的首个覆盖
- 与既有票的关系：`.scratch/nullable-migration/issues/08:49`（把「数量上可省略」写成「槽可以为 null」的出处）、`issues/13`（`Required` 的引入）、`issues/05`（省略实参以 null 承载的契约）——03 号票落地时须回填 issues/08
- 本 effort **不改**：`argumentTypeArrayEx` 的声明式方向、184 个子类的扁平结构、`ArgumentListExtensions.Required` 的存在理由（其注释的「调用方担保」措辞在 03 号票里更正）
