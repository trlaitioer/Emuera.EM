# 实参契约表只在解析期声明、到运行期靠 cast 兑现

Status: superseded

## 任务

发现于 2026-09-27 的只读架构审查（函数/指令层）。

`FunctionMethod` 的 `ArgTypeList`/`argumentTypeArrayEx` 是声明式实参契约：一个规则块声明整条实参的形状（类型、维度、可否省略、可否传常量、是否引用），全库 114 个块（其中 91 个设了 `OmitStart`）。方向是对的——184 个**活的** `FunctionMethod` 子类（`Creator.Method.cs` 内 186 处 `: FunctionMethod` 文本匹配，减去 `:3915` `GetRefMethod`、`:7560` `GraphicsRotateMethod` 两处注释掉的定义；其中 139 个是 `private sealed`）里只有 `MesSkipMethod` override 了 `CheckArgumentType`（`Creator.Method.cs:3652-3668`）。但声明出来的形状在三处兑现不了，depth 漏在运行期：

- **维度规则**：`FunctionMethod.cs:78` 的 `Array3D` 读错标志位，`RefInt3D` 槽位退化为 `ArrayDims == 0`、维度不校验（独立票见 issues/03；其可达后果是维度不符的**变量**被放行，不是 InvalidCastException）
- **省略规则**：`FunctionMethod.cs:168` 的判据在 `OmitStart` 未写时恒假，23 个规则块的「该槽不可省略」是死代码（独立票见 issues/04）
- **折叠与引用**：`AllowConstRef`（规则表 `:224-225`）× `FunctionMethodTerm.Restructure` 的常量折叠（`FunctionMethodTerm.cs:45-60`）× 函数体 `as VariableTerm` 三者隐式联动，契约只写在 `Creator.Method.cs:5000-5004` 的注释里；`HasUniqueRestructure` 实际承担的是「我的实参槽必须仍是变量」
- 兑现责任被推到消费端：`Creator.Method.cs` 内 39 处 cast（23 处 `as VariableTerm)!` + 16 处 `(VariableTerm)`，其中 6 处位于注释，⇒ **33 处活站点**）。逐点判决（2026-09-27 复核）：23 处类型可证且 null 安全、**10 处可达 null**、**0 处可达 InvalidCastException**——非变量实参由 `FunctionMethod.cs:239-242` 的 `else { error = true; }` 拦下，该分支与 `dims` 无关
- **访问形态普查（决定 (c) 的接口能不能小）**：`Int(i)` + `Str(i)` + `IsInt(i)/IsStr(i)` 可覆盖 **366/405 = 90.4%**（纯 int 值 214、纯 string 值 98、类型询问 54）。前提是 `Int(i)`/`Str(i)` 对 `Ref` 槽也返回变量当前值——今天成立（`VariableTerm` 本身是 `AExpression`）。**难点在剩下的 10%**：
  1. **可能为 null 的槽约 116 处**（口径见 issues/16；该票用全规则口径得 135，站点集合不同）。访问器必须显式选语义：抛异常（把 NRE 变成显式契约）还是返回「可能有」句柄
  2. **20 处 `Restructure` 站点是折叠期 API、不是函数体 API**：要求**可写**列表且写的是副本（`FunctionMethodTerm.cs:37`），**不可能**用只读访问器覆盖——必须留在另一接口上，或把折叠并入检查器
  3. **`VariableTerm` 实体槽**：405 中 11 处 + 索引器路径约 34 处 → 需独立 `Var(i)`（读写语义不同：`SetValue` 7 处、`GetFixedVariableTerm` 7 处）
  4. **54 处类型询问**说明函数体在运行期才知道槽的静态类型（`SameAsFirst` 规则）→ 访问器必须暴露 operand type
  5. 3 处把 `Required(i)` 原样传给别的 helper（`:147,2244,5147`）→ 需要「原始 term」逃生口
- **消费面比预想的大**：除 405 处 `Required` 外，还有 `arguments[<字面量>]` **227 处**、`arguments[i]` 47 处、`arguments[b…]` 3 处、`arguments.Count` **264 处**；`as VariableTerm` 24、`(VariableTerm)` 16、`is VariableTerm` 21。⇒ 真正的函数体侧消费面约 **682 处取值 + 264 处计数**，本票的改动量估计应按此修正
- `Required` 自称的不变量写在 `ArgumentListExtensions.cs:11-13`（「调用方担保」），而把该担保当作「解析器担保」的书面出处是 `.scratch/nullable-migration/issues/08-expression-method-chain.md:49`

完成标准：

- 规则表在解析期返回**已解析的槽位**——每槽要么是已折叠的值、要么是仍可写的变量实体，由规则自己声明要哪种；函数体只消费结果，不再判空、不再转型
- `CanRestructure`/`HasUniqueRestructure`/`AllowConstRef` 的语义能在规则表上一处读出，不再依赖注释
- `CheckArgumentTypeEx` 的返回从 `string?` 错误文本改为「错误或已解析槽位」
- 表驱动用例：每个函数 × {正常 / 少一参 / 中间空参 / 错类型 / 错维度 / Ref 槽传字面量 / AllowConstRef 传常量数组}，能钉死 03、04、16 三票的缺陷
- 「`Required` 只能在 `i < OmitStart` 的槽上使用」这条约束应能机械校验，而不是靠注释与人工复核（这是 16 号票里 135 处未证站点会反复再生的根因）

## 方案

待评估。**形状方向（倾向上游未定案）**：(c) —— 规则表产出「已解析槽位集合」新类型 + 基类新读取入口，旧签名保留为**转发适配器**，186 个子类按切片迁移（① 10 处可达 null ② 33 处 cast ③ 135 处未证站点裁定 ④ 其余机械迁移）。两个硬性设计输入：

1. **空槽语义**：内建函数按错误处理（无默认值可声明）；UDF 保留 `@FOO(A = 1)` 的默认值语义。两族不统一
2. **迁移完成度必须可机械核对**：C# 对「参数列表不同的同名方法」只当重载、对不再被调用的私有方法默认不报警，所以漏迁的函数体会**静默**走基类 `GetIntValue`（`FunctionMethod.cs:360-363` 抛 `ExeEE`），失败暴露在运行期而非编译期。手段三选一：旧虚方法挂 `[Obsolete(error: true)]` / 引入分析器 / 以调用计数为门禁（`arguments.Required(` 从 405 核到 0）

第一步不做签名设计——先把 03、04 两处改对并配表驱动用例，再谈槽位解析期返回。

## 影响

- **测试面**：全工程 `CheckArgumentType` / `ArgTypeList` / `argumentTypeArrayEx` 零命中——114 个规则块是仓库里最大的一块未测行为。今天只能经 `FunctionMethodCreator.GetMethodList()["名字"]`（`Emuera.Tests/Runtime/Script/Statements/Function/FunctionMethodTests.cs:19-103`）或经 `IdentifierDictionary.GetFunctionMethod` 断言抛 `CodeEE`（`UnguardedAsCastTests.cs:116-117`）间接碰到
- **与既有票的关系**：
  - `ArgumentListExtensions.Required` 是 `.scratch/nullable-migration/issues/13-null-forgiving-cleanup.md` 的既定产物（该票把散落的 `!` 收成具名入口）。本票**不动它**——按该票的判据它仍是净收益；要做的是在产出端让槽位有值，使该入口自然失去存在理由
  - 省略实参以 `null` 承载是 `.scratch/nullable-migration/issues/05-omitted-args-nullable.md` 的既定契约，本票不改该契约
  - 契约测试须与求值测试分开：`Creator.Method.cs` 有 68 处 `exm.VEvaluator`、46 处 `GlobalStatic.`，只有纯函数侧才能用 `exm: null!`
- **边界：UDF 族不在本票范围内**。用户定义函数（`@FOO`）**不经过** `argumentTypeArrayEx`/`CheckArgumentTypeEx`，是另一套机制：形状声明在 `UserDefinedRefMethod.ArgTypeList`（枚举 `UserDifinedFunctionDataArgType`），检查在 `CalledFunction.TryConvertArg`，产物是 `UserDefinedFunctionArgument`。且两族的空槽语义**必须分开**——UDF 有逐参数默认值（`@FOO(A = 1)` → `FunctionLabelLine.Def`），内建函数没有任何声明默认值的地方。本票的槽位类型只覆盖内建函数；UDF 侧的同型缺陷见 issues/16
- **头号可行性风险（(c) 方案）——已核实，比我原先的描述更硬**：不可变槽位集合与现状不兼容，但阻塞点不是「懒加载」，而是两个：

  1. **检查与折叠的先后**：检查在 `ExpressionParser.cs:320` 的嵌套构造里执行，折叠在同一个 `CreateArgument` 里**稍后由 builder 执行**（`ArgumentBuilder.cs:126`），且折叠会原地改写槽位（`FunctionMethodTerm.cs:53`）、还可能用 `SingleTerm` **替换整个 term**（`:39,59`）。要不可变，必须把折叠**并入或前移到检查之内**，而不是把两个相邻调用换个顺序
  2. **共享与别名**：`IdentifierDictionary.cs:624` 直接 `new FunctionMethodTerm(method, arguments)` **不复制**；CALLF/CALLFORMF 传入的是 `SpCallArgment.RowArgs`，且结果被 `CallformCache` 按调用点缓存复用（`Argument.cs:528-538`）⇒ `Arguments` 与 `RowArgs` 是**同一个 List 且跨调用复用**。不可变集合会迫使 `Restructure` 返回**新的** term，从而改变这些共享语义

  另需纠正一处口径：默认配置下**不存在「解析期」折叠**——`ReduceArgumentOnLoad` 默认 `NO`，只有 `YES`/`AnalysisMode`/`FORCE_SETARG` 才在装载期折叠（`ErbLoader.cs:1314`），普通指令行是**首次执行时**构造（`Process.ScriptProc.cs:62-69`），结果由 `ArgumentParser.cs:65` 记忆
- **并发现象（非缺陷，但深化时必须处理）**：`StrForm.cs:59-63,87-91` 的 5 个 `static readonly FunctionMethodTerm` 被塞进每个含 `***`/`+++`/`===`/`///`/`$$$` 的 StrForm，外层 `StrForm.Restructure`（`:235`）会对**同一对象**重复调 `Restructure`；在 `ErbLoader.cs:986` 的 `Parallel.ForEach` 下即对同一 `List` 的并发写。写入的是同值（引用写入原子），与 `.scratch/performance-optimization/issues/04-parse-parallel.md` 对 `Depth` 的论证同型
- **性能结论（已核实，纠正原先的「可能变快」）**：`Restructure` **不在稳态每调用热路径上**（每求值 0 次——折叠只在 Argument 构造时发生，而 Argument 被记忆）。当前每调用成本 = 1 次虚派发 + N×(索引器 + 虚派发)，无装箱，`Required` 是 3 条指令级静态方法。⇒ 本重构**在 per-call 上拿不到可测收益**，理由必须是**契约安全**（未证站点 + 33 处转型），不是速度。真实可省的成本在**构造/折叠期**（17 个 unique 方法每次折叠分配一次 `[.. Arguments]`、`VariableTerm.Restructure` 的重复 `CheckElement`）。且仓库**没有基准设施**（`BenchmarkDotNet`/`[Benchmark]` 零命中，50 个测试文件无一为性能测试，perf/04 的埋点是临时且未交付）——**任何性能主张必须先补基准**
- 改动量估计：大（`FunctionMethod.cs` + `Creator.Method.cs` 的 114 个规则块 + 消费端）。建议按票拆：先 03/04 止血，再动槽位形状
- 相关票：issues/03（维度标志位）、issues/04（省略判据）、issues/15（`Argument` 的三字段折叠协议，同一 seam 的另一侧）
- **已转出（2026-09-27）**：承接方 `.scratch/arg-contract/issues/04-argument-slots.md`，方案裁定见 `.scratch/arg-contract/spec.md`。本票作为审查记录保留（文件不移动）
