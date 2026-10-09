---
id: arg-contract-05
status: implemented
---

# 实参契约深化：让声明兑现为槽位类型（阶段 B）

## 任务

承接 `arg-contract-04` 的**阶段 B**（该票的阶段 A：片 0 重新基线 + 片 1 止血，已落地）。

形状选择、可行性核对与三个被撤销的原决策（折叠顺序、不可变别名、`[Obsolete(error: true)]` 门禁）的**证据都在 04 的「核对」节**；本票不重复记录，只承担阶段 B 的任务、完成标准与影响。

### 背景（一句话）

`ArgTypeList` 是声明式实参契约，但它只有三个独立解释者（检查器 / 折叠 / 函数体），彼此无同步机制，编译器又因「形状是数据不是类型」而无法介入。阶段 A 已把「可省略槽上的硬转型解引用」这一条缺陷车道清空；本票兑现剩下的结构性收益：**把规则表的解释收敛到检查期一次**，函数体不再自证非空、不再 cast。

### 完成标准

- 规则表在**检查期**产出「已解析槽位集合」新类型 `ArgumentSlots`：每槽确定为值 / 显式留空 / 缺失三态（决策 5）
- 该类型只有四条构造路径，且每条都先过检查器或由调用点担保：① 规则表族解析（同一次检查的命中段 → 工厂的 `Checked`）；
  ② 三个具名 `Trusted` 调用点族（运算符 4 / StrForm 8 / TOSTR 包装 1，共 13 处，由调用点显式声明角色）；
  ③ 切片①–③ 期间的旧签名入口（转发，切片④ 后仍保留；**它按「检查器是不是由调用点担保」分两支**——运算符 / StrForm 族
  （`FunctionMethod.CallerOwnsCheck`，检查重载故意抛 `ExeEE`）与无规则表的类（`MesSkipMethod` 自检）直接按「调用点担保、
  无声明」解析，压根不问检查器；其余有规则表则跑同一次检查，失败当场抛 `CodeEE`（检查器的文案），不带着全 `Loose`
  的角色进函数体。**判据不能写成「有没有规则表」**：运算符族里 `PlusStrStr` 是唯一带 `argumentTypeArray` 的成员，
  按表分流会去问那个只抛 `ExeEE` 的检查器，门面当场炸——回归用例见
  [ArgumentSlotsTests.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/ArgumentSlotsTests.cs) 的
  `LegacyFacade_WorksForACallerCheckedClassThatHasARuleTable`）；④ 测试侧经 ③，另有 7 处直调 `Trusted(` 的访问器用例
  （`Emuera.Tests` 不在 `arg-slots.py` 的扫描根内，故由脚本单列计数并纳入自校验）。`roles` 的效力边界（**本轮 review 后更新**）：
  它仍**不做运行期断言**——与 `ArgumentListExtensions.Required` 的「调用方担保」同一约定——但不再是「只服务测试期」：
  `ArgumentSlots.Term` 在空槽 / 非变量实体上抛 `ExeEE` 时把该槽的 `RoleAt` 写进文案（`Var` 的硬转型也改成显式契约错误），
  故 `roles` 有了生产消费点（报错文案），投影断言仍由测试期用例覆盖；脚本侧的守卫判据（「体内有
  `Has`/`IsEmpty`/`IsMissing`」）与 `roles` 无关这一点不变
- 基类提供角色化访问器（`Int`/`Str`/`IsInteger`/`IsString`/`Var`/`Has`/…），覆盖**消费面** 369/379 的现有消费点
  （**阶段 A 的基线口径**：这两个数由 `arg-slots.py --phase-a` 在阶段 A 的 HEAD 上算出，见
  [slots-consumption.md](../slots-consumption.md)；迁移后 G 段只报当前值 0/0，故 97.4% 只是迁移前的覆盖声明，
  不要与 [spec.md](../spec.md) 的 372/404 = 92.1%（另一套分母）并列成两个比例）；
  迁移完成的函数体不再**自己写非空断言与硬转型**。**两处口径须按实读**：① 计数是「`arguments.Required(`/`arguments[...]` 文本锚定」
  口径，换成访问器后藏在 `slots.Term(i)` 或局部量之后的 `VariableTerm` 判别不再计入——消费面仍有 4 处这样的判别
  （`XmlGetMethod` 的 `outputArg as VariableTerm`、`ArrayMultiSortExMethod` 的 `slots.Term(0) is VariableTerm`、
  `DataTableColumnManagementMethod` 的 `slots.Term(1) is VariableTerm`，以及 `ReplaceMethod`
  里 `slots.Term(2) is not VariableTerm` 的**拒绝路径**（`BuiltinMethod/StringOperations.cs`，抛 `ArgIsNotNDStrArray`）；这四处都有规则表担保 `Ref` 且无 `AllowConstRef`，故安全，其中前三处可改成
  `Var(i)`，**第四处不能换**——它本意是抛 `CodeEE`(ArgIsNotNDStrArray)，换成 `Var(i)` 会变成 `ExeEE`）；② 「不再判空」指的是访问器不再替调用方判空，不是函数体里
  `Has`/`IsEmpty` 的消失——守卫判据见下条
- 旧签名最终改为**非虚门面**（解析角色后转发到新入口）：全库任何残留 override 报 CS0115（三个取值入口**与检查入口的两参门面**同此，
  见 [FunctionMethod.cs](../../../Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs) 的 `CheckArgumentType`），且既有测试的
  `method.GetIntValue(null!, [...])` 形态仍可编译**且可用**（三族都验证过：规则表族、运算符族、调用点担保族——
  其中运算符族的「带规则表」那一半由 `LegacyFacade_WorksForACallerCheckedClassThatHasARuleTable` 钉住，见 ③ 的判据说明；
  测试侧按切片② 的约定改走 `BuiltinFunctionTestKit` 的 `Slots`/`Invoke`/`InvokeWith`，那批改动是入口收敛，不是门面失效）
- 迁移面 = 规则表族 183 个子类（旧签名 override **185** 处）+ Trusted 三族（`OperatorMethod.cs` 39 个类 /
  `StrForm.cs` 5 处 override = 3 个 `GetStrValue` + `FormattedStringMethod` 的 `GetIntValue` 与 `GetReturnValue`
  （票面旧稿只写「3 + 1」，漏了那个 `GetIntValue`）/ `Process.CalledFunction.cs` 的 TOSTR 构造点；合计旧签名 override **44** 处）
- 迁移门禁三层齐备（编译期 / 测试期 / 脚本期，见「方案」）；测试期这层现有 4 条：旧签名缺席、旧两参
  `CheckArgumentType` 不被子类覆写**且基类那份已非虚**（`IsVirtual` 也断言了——留着 virtual 同样会被
  `CheckAndResolveSlots` 的虚分派绕过，而 CS0115 才是编译期门禁）、槽位入口仍可覆写、**具体子类必须自己声明一个槽位入口**（删掉函数体不报 CS0115，
  只会静默回落基类抛 `ExeEE`，故这条不能省；判据收在「自己声明」上，因为**继承来的实现同样可能只是抛 `ExeEE` 的中转**
  ——`FormatPercent` 的 `GetIntValue` 继承自 `FormattedStringMethod`，若按「含继承」判，它删掉自己声明的 `GetStrValue`
  也照样通过）；口径与归零目标见 [slots-progress.md](../slots-progress.md)（阶段 B 的工件，由 `arg-slots.py` 默认产出）
- 折叠面不动：14 个 `UniqueRestructure` 保持旧签名与旧副本语义（04 核对 #2/#3）；折叠面 25 处 `arguments.Required(` 不在门禁内
- UDF 族的默认值语义不变（`CalledFunction.TryConvertArg` 一侧不在本票）

## 方案

形状：**类型是检查器产出的只读视图（元素数组为检查期快照），折叠仍发生在原位、不改 `Restructure` 的返回语义**。

```csharp
// FunctionMethod 内嵌：私有 ctor + 派生类可简单名引用
internal sealed class ArgumentSlots
{
    private readonly AExpression?[] terms; // 检查期快照（拷贝），不跟随检查之后的原地折叠
    private readonly SlotRole[]? roles;    // 命中段投影；变长尾按 LastVariadics 取模；null = 未声明（无表类/裸列表入口）

    // 活体列表：只给原地折叠（FunctionMethodTerm.Restructure）与整表转交，访问器一律不读它
    internal List<AExpression?> FoldTarget { get; }

    private ArgumentSlots(List<AExpression?> arguments, SlotRole[]? roles); // 校验 roles.Length == arguments.Count

    internal static ArgumentSlots Checked(List<AExpression?> arguments, SlotRole[] roles);   // 规则表族
    internal static ArgumentSlots Trusted(List<AExpression?> arguments, SlotRole[]? roles);  // 调用点担保族：13 处显式声明
}

// 构造点只走一个入口：命中段与错误文案来自同一次检查，构造点不再各写一份 Checked/Trusted 分流
string? errmes = method.CheckAndResolveSlots(name, arguments, out ArgumentSlots slots); // 内部：ResolveSlots(args, matchedIdx)
```

- **快照语义定案**：`FunctionMethodTerm.Restructure` 在检查**之后**原地改写 `FoldTarget[i]`，快照不跟随（票面旧稿的
  「写回只经 FunctionMethod 内部方法」作废）。两者读值等价：折叠只把元素换成 `SingleTerm`（可静态求值的标识符）
  或 `FixedVariableTerm`（下标全常量、仍是 `VariableTerm` 子类，`Var` 与 `as VariableTerm` 依旧成立），
  且 13 个 `HasUniqueRestructure` 类今天本就不走那条路径（`FunctionMethodTerm.Restructure` 的 unique 分支直接返回）。
  **等价只覆盖读值，不覆盖求值时机**：外层不可折叠时（`XML_GET`/`DT_*`/`GDRAW*` 等 `CanRestructure = false` 的类），
  访问器留着的是内层调用的原项，故每次调用都会重新求值那个已被折叠的内层调用——对纯表达式只是白算一次，对**状态依赖
  的可折叠调用**（如 `VARSIZE`：长度随 `DIM` 变）读到的就是调用时的值，而不是首次折叠时的常量。这是有意的语义
  （旧的活体读法会把首次执行时的常量一直用下去），票面旧稿只写「读值等价」不足以覆盖这一维。
  执行期证据 = [ArgumentSlotsTests.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/ArgumentSlotsTests.cs) 的
  `FoldedCall_StillReadsTheRightValueThroughTheAccessors`（真走解析 → `Restructure` → 访问器取值）、
  `UnfoldableCall_ReEvaluatesItsFoldedArgumentsAtCallTime`（不可折叠外层：活体表已折叠、快照仍读原项）与
  `Slots_DoNotFollowTheInPlaceFold`（快照不跟随，含 `Tail`）。`Tail` 也从快照取，故 `GETMETH`/`GETMETHS` 转交的是
  检查期的项而不是折叠后的活体项——下游（`UserDefinedMethodTerm.Restructure`）会自行再折叠，取值等价。
- **角色的三种形状**：检查器投影覆盖 ① `argumentTypeArrayEx`（`OmitStart` / `NotNull` / `SameAsFirst` 的
  `slot0Required` 特例 / 变长尾 `LastVariadics` 取模——投影与检查器共用 `RejectsEmptySlot`/`RuleForSlot`/`ExpectedType`
  三个判据，不另立副本；`ExpectedType` 是「该槽期望什么 operand type」，含 `SameAsFirst` 读 0 号槽、`Any` 表示两种都收
  这两种情形，检查器的 `typeNotMatch` 与投影的 `Int`/`String`/`Any` 角色都从它出）；② `argumentTypeArray`（定数表、
  非空：实测 **111** 个类设了它（115 个类文本上赋值，其中 4 个只赋 `null`——`CsvStrDataMethod`/`GraphicsClearMethod`/
  `StrfindMethod`/`MesSkipMethod`），其中 109 个没设 `argumentTypeArrayEx` ⇒ 走 `ProjectFixedRoles`；另 **2** 个两类都设
  （`DataTableColumnManagementMethod`、`MapGetStrMethod`），按 ① 优先。口径是「剥注释、按类体、非空定数表」，与
  `new ArgTypeList` 的**规则段数**（114）不是一个量，勿相减）；
  ③ 两者都没有的类（`MesSkipMethod`，0 参且自 override 检查）走 `Trusted(terms, null)`。
- **Trusted 族的角色由调用点声明**：`FormattedStringMethod`/`OperatorMethod` 的 `CheckArgumentType` 抛 `ExeEE`，
  角色无从推出，故 13 处显式给：StrForm 的 5 个静态 `formatPercent` term 为 `[Str|Required, Loose, Loose]`（槽 1/2
  就是显式 null）、`formatYenAt` 三槽 Required、`FromWordToken` 的 2 处为 `[Required, Loose, Loose]`、运算符族 4 处
  按方法取（`Increment`/`Decrement` 系声明 `Ref`）、TOSTR 为 `[Int|Required]`。

访问器（`exm` 显式传入：term 的构造点在解析期只有 `GlobalStatic.EMediator`，且既有测试的 `GetIntValue(null!, [...])` 形态要继续可用）：

| 访问器 | 语义 | 前置（可脚本核对） | 覆盖车道（live，口径见 [slots-progress.md](../slots-progress.md)） |
| --- | --- | --- | --- |
| `Count` | 实参个数（折叠不改长度） | — | 136 站点（`arguments.Count` 119 + 列表模式 16 + `Cast<>` 1；其中 18 处是 `for(… arguments.Count …)` 循环头） |
| `Has(i)` / `IsEmpty(i)` / `IsMissing(i)` | 三态：有值 / 显式留空 / 数量省略 | — | 45（判空 29 = `==/!= null` 27 + `?.` 2，列表模式 16） |
| `Int(exm,i)` / `Str(exm,i)` | 取值 | **两条**：① 该槽**存在**（`Count`/`IsMissing` 守卫，或下标小于命中段的最少实参数）∧ ② 非显式空槽（`role(i)=Required` ∨ 体内先 `Has`/`IsEmpty`/`IsMissing` 挡掉——`Count` **不算**②的守卫：数量省略与显式空槽正交）∧ 类型匹配 | 消费面 337（187 + 150） |
| `IsInteger(i)` / `IsString(i)` | operand type 询问 | 同 `Int`/`Str` | 消费面 32（15 + 17） |
| `Var(i)` | 变量实体 | 同 `Int`/`Str` 的①（引用位同样可数量省略）∧ `role(i)=Ref` ∧ 非 const ∧（规则含 `AllowConstRef` ⇒ 类已设 `HasUniqueRestructure`） | 消费面 32（全文件 37） |
| `Term(i)` / `TermOrNull(i)` | 原始句柄逃生口 | 前者同 `Int`/`Str`（`TermOrNull` 自己收边界） | 2（`XML_GET[2]` 一处、`DT_CELLSET[3]` 一处） |
| `Tail(from)` | 整段原样转交（自快照） | — | 2（`GETMETH`/`GETMETHS`，比今天的 `Skip().ToArray()` 少一次分配） |

下标越界（`i >= Count`）**不是脚本错误**而是函数体自身的 bug：访问器与 `ArgumentListExtensions.Required` 一样只做
非空断言，越界抛 `IndexOutOfRangeException`（`IsMissing`/`Count` 就是给调用方自己挡这条路用的）。

消费面 `arguments.Required(` 共 379：四访问器覆盖 369（= 97.4%），其余 10 = `Term`/`TermOrNull` 2 + `Var` 承接的硬转型 8。
迁移面与折叠面不相交（04 核对 #2/#3），故本票**不需要**可变逃生口。

**切片**：① 机制落地（`ArgumentSlots` + 基类新虚入口 + 旧签名保留为转发入口 + 检查器产出角色，零迁移、全绿）→ ② pilot 2 个（`MaxMethod` 变长尾；`DataTableRowSetMethod` 双规则段 + `MatchVariadicGroup` + 动态基址 `b` + 三态 DBNull + 裸句柄转发——它的规则段**不含** `AllowConstRef`，故它还顺带钉住不变量的另一面：无 `AllowConstRef` 的引用位由检查器拒常量数组，`Var` 因此安全）→ ③ 分批迁移（XML 族 → DT 族 → Graphics 族（含 `ReadPoint`/`ReadColormatrix` 等整表转发 helper 改签名）→ 数组/集合族（含 14 个 `UniqueRestructure` 类，只迁消费面、折叠面保持旧签名）→ 字符串/数值族 → 其余规则表族 → **Trusted 三族**（`OperatorMethod.cs` 39 个类、`StrForm.cs` 3 个 `GetStrValue` + `FormattedStringMethod` 的 `GetIntValue`/`GetReturnValue`、TOSTR 构造点））→ ④ 旧入口非虚化（门面）。

**门禁**：
- 编译期：最后一步把旧虚入口改为**非虚门面** ⇒ 全库残留 `override …(ExpressionMediator, List<AExpression?>)` 报 CS0115（切片期间不能用；Trusted 三族也纳入迁移，故无需豁免）。**它拦不住「函数体被删掉」**：签名没了就是没 override，只会静默回落基类那三个抛 `ExeEE` 的实现 ⇒ 由下面测试期的第 4 条兜
- 测试期：反射门禁测试（枚举全部 `FunctionMethod` 子类，断言 ① 无旧签名 override、② **无子类 override 旧的两参 `CheckArgumentType` 门面**（否则 `CheckAndResolveSlots` 的虚分派会绕过它）、③ 槽位入口仍可覆写、④ **每个具体子类必须自己声明一个槽位入口**（判据收在「自己声明」上——继承来的实现可能是抛 `ExeEE` 的中转，如 `FormattedStringMethod.GetIntValue`，故不能按「含继承」；基类那三个抛 `ExeEE` 的默认实现作前提对照，`FormatPercent` 的继承实现作负对照），族数按 183/39 反空跑；`InternalsVisibleTo Emuera.Tests` 已具备）+ 访问器契约与三态用例 + 快照求值时机用例（`FoldedCall_…` / `UnfoldableCall_ReEvaluatesItsFoldedArgumentsAtCallTime` / `Slots_DoNotFollowTheInPlaceFold`）+ 旧签名门面按族分流的用例（规则表族失败抛 `CodeEE`、运算符族——含带规则表的 `CallerCheckedWithTableMethod`——与调用点担保族可用）
- 脚本期：[arg-slots.py](../arg-slots.py) 的默认产物 [slots-progress.md](../slots-progress.md)：**消费面**（`BuiltinMethod/*.cs` 的 `GetIntValue`/`GetStrValue`/`GetReturnValue`）`arguments.Required(` 379 → 0、消费面硬转型 29 → 0、消费面 `Var` 家族 32 → 0、列表模式 16 → 0、`arguments.Count` 119 → 0、`Skip(`/`Cast<` 2/1 → 0、旧签名 override 229 → 0（规则表族 185 + Trusted 44），同时锁死 Trusted 三族构造点 13 / 全库字面 14 / target-typed 1（不增）、`ArgumentSlots.Trusted(`/`Checked(` 调用点 15/2（不增；切片④ 后都收敛到 `FunctionMethod.ResolveSlots` 这一个工厂）、折叠面不变量（`HasUniqueRestructure` 17/13、override 14、unique 分支整表副本 1）、**槽位类型自己 ctor 的快照复制 == 1** 与 `ArgType.NotNull` 计数（含注释 88 处/77 行、剥注释 81 处/70 行；本轮因 `DataTableRowSetMethod` 的规则段注释提到该标志，含注释口径从 87/76 上调 1，剥注释口径未动——`_scan.NOT_NULL_EXPECTED` 与 [slots-worklist.py](../slots-worklist.py) 同源同步，实质不变量没松）；`Int`/`Str`/`Var` 站点落在 Required 槽改为按「体内有 `Has`/`IsEmpty`/`IsMissing` 守卫」机械核对（`Count` 不算守卫），`AllowConstRef ⇒ HasUniqueRestructure` 违例进自校验报警（不再只写在阶段 A 快照的 F 段）。**判据边界据实写在产物里**：守卫判据是「同体内出现过该下标的一次判空」（不限先后），且只覆盖**字面下标**；非字面下标（变量/偏移）单列一张弱判据表（是否出现 `Count`/`Has`/`IsEmpty`/`IsMissing` 之一）+ 一张 helper 明细表（`GraphicsDrawG` 族传常量 `argNo`，由调用点担保，不计入判定）。
  注意口径：这里的「归零」全部限定在**上列文件与面**内——指令侧 `ArgumentBuilder.cs` 仍用 `arguments.Required(`（两族语义定案为「不对齐」，见 `arg-contract-04` 片 6），检查器自身与折叠面也继续用它（迁移+本轮 review 修正后全仓**仍 31 处** = `BuiltinMethod/*.cs` 25（全在折叠面）+ 检查器 3 + `FunctionMethodTerm.cs` 1 + `ArgumentBuilder.cs` 2；检查器侧少掉的 2 处是 `SameAsFirst` 的期望类型读取收敛进 `ExpectedType` 所致）

## 影响

- **改动面**：`FunctionMethod.cs`、`BuiltinMethod/*.cs`（183 个子类 / 消费面 379 站点 / 旧签名 override 185）、`FunctionMethodTerm.cs`、`ArgumentBuilder.cs`、`IdentifierDictionary.cs`，以及 Trusted 三族 `OperatorMethod.cs`（39 个类 / 旧签名 override 39 / 硬转型 4）、`StrForm.cs`（3 个 `GetStrValue` + `FormattedStringMethod` 的 `GetIntValue`/`GetReturnValue`，旧签名 override 5）、`Process.CalledFunction.cs`（TOSTR 构造点 1）。
  `ArgumentListExtensions.cs` **只在语义上被承接**（`ArgumentSlots` 的访问器取代了它在消费面的用法，`docs/architecture.md` 随之改写），文件本身未改：它仍是折叠面与检查器在用的 `Required(i)`。UDF 侧（`UserDefinedMethodTerm.cs`）不在内——`UserDefinedRefMethodTerm` 已确认为死代码（见 `arg-contract-03`）
- **性能**：per-call 拿不到可测收益（理由必须是契约安全，不是速度）。可省的成本在构造/折叠期；**仓库无基准设施** ⇒ 任何性能主张必须先补基准
- **共享语义**：不改（StrForm 的 5 个静态 term、`SpCallFArgment.RowArgs`、`UserDefinedRefMethodTerm.srcArgs` 均维持现状）；快照使 builtin 槽表不再与调用方共享数组
- **验收口径**：[slots-progress.md](../slots-progress.md)（`arg-slots.py` 的默认产物）是机器可复跑的起点与归零目标（基线：消费面 `Required(` 379 / 四访问器 369；硬转型 29；`Var` 家族 32；旧签名 override 229；Trusted 构造点 13）。G 段除原表外现有三张核对表：字面下标（39 处）、非字面下标（弱判据 + helper 明细）、以及归零目标里新增的「ctor 快照复制 == 1」一行；阶段 A 的快照 [slots-consumption.md](../slots-consumption.md) 与 [slots-worklist.md](../slots-worklist.md) 随之**冻结**——阶段 B 重写消费面后那批表的「读取形态」扫描不到访问器调用、必然漂移，重生成要显式 `arg-slots.py --phase-a`（只应在阶段 A 的 HEAD 上跑）
- **进度**：四个切片都已落地。
  - 切片①：`FunctionMethod.SlotRole` + `ArgumentSlots`（`Checked`/`Trusted` 工厂与全部访问器）、基类槽位入口、检查器角色投影（`CheckArgumentTypeEx(out matchedIdx)` + `ProjectRoles`，判据与检查器共用）、构造点接线（规则表族走 `Checked`、无表类与担保族走 `Trusted`）、[ArgumentSlotsTests.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/ArgumentSlotsTests.cs)。
  - 切片②：pilot `MaxMethod`（变长尾）与 `DataTableRowSetMethod`（数组形态 `Var`、对形态 `Str`/`TermOrNull`、动态基址 `b`、`SetValue` 形参放宽为 `AExpression?`），新增 [DataTableRowSetMethodTests.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/DataTableRowSetMethodTests.cs) 7 条；测试侧「直接调旧签名」的入口收进 [BuiltinFunctionTestKit.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/BuiltinFunctionTestKit.cs) 的 `Slots`/`Invoke`（走检查期路径），后续切片不再逐个改测试。
  - 切片③：183 个规则表族子类 + Trusted 三族（`OperatorMethod.cs` 39 个类、`StrForm.cs` 3 个方法、TOSTR 构造点）全部迁到访问器——**619 个访问器调用点**（口径：剥 `//` 注释后按 `slots.<访问器>` 机械计数 `Emuera/` 全树；`Int` 244 / `Str` 173 / `Count` 85 / `Has` 42 / `Var` 33 / `IsString` 17 / `IsInteger` 16 / `Term` 4 / `Tail` 2 / `TermOrNull` 2 / `IsEmpty` 1；票面旧稿写 637，分解已按此口径重算），消费面 `arguments.Required(` 379 → **0**、旧签名 override 229 → **0**、消费面硬转型 29 → **0**、列表模式 16 → **0**；迁移中两个只在用例里暴露的边界被修掉（`ArgumentSlots.Tail` 对「转发尾起点超过实参个数」要返回空表；`DataTableCellSetMethod` 一族把 0 号列写成 DBNull 时返回语句抛 `InvalidCastException` 这条既有行为用用例钉住）。`IsMissing` 目前无生产调用点（`!Has(i)` 更顺），保留为三态的完整接口，由访问器契约用例覆盖。
  - 切片④：旧入口改为**非虚门面**（`GetIntValue`/`GetStrValue`/`GetReturnValue` 的 `(ExpressionMediator, List<AExpression?>)` 三个入口已不可覆写），新增 [SlotMigrationGateTests.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/SlotMigrationGateTests.cs)（反射枚举全部子类断言无旧签名声明、无旧两参 `CheckArgumentType` override，且族数按 183/39 反空跑）、[slots-progress.md](../slots-progress.md) 增加「访问器前置核对」（可空槽上的 39 处字面下标取值站点全部有体内 `Has`/`IsEmpty`/`IsMissing` 守卫，未证明 0 处）与 `arguments.Count` 的按面拆分（消费面 0 / 折叠面 13 保留）
  - review 修正（本票收尾）：构造点收敛为**单次检查 + 一个工厂**（`CheckAndResolveSlots` → `ResolveSlots(arguments, matchedIdx)`，去掉构造点各写一份的 `Checked`/`Trusted` 分流与「检查器跑两遍」）；检查器与角色投影共用 `RejectsEmptySlot`/`RuleForSlot`（不再各写一份判据）；`ArgumentSlots.Source` 更名 `FoldTarget`（只服务原地折叠与整表转交，访问器不读它），`FunctionMethodTerm.Arguments` 随之取消；访问器 `IsInt`/`IsStr` 更名 `IsInteger`/`IsString`；去掉 13 处 `Count > n && Has(n)` 冗余守卫；脚本的守卫判据收紧为「`Count` 不算守卫」并新增「测试侧构造点 == 3」「issues/18 不变量无违例」两条自校验；补执行期折叠等价用例与 `Tail` 快照断言。
  - review 修正（第二轮，代码审查发现，已落地）：**门面按有无规则表分流**（`ResolveSlots(List)` 无表时走 `ResolveSlots(args, -1)`，不问检查器——运算符 / StrForm 族的检查重载故意抛 `ExeEE`，继续问它等于把「直接传 List 调函数体」这条既有写法禁用；有表时检查失败抛 `CodeEE`，不再带着全 `Loose` 的角色进函数体），补 `LegacyFacade_SplitsOnWhetherACheckExists` 与 `LegacyFacade_WorksForTheOperatorFamily` 两条用例；**门禁补第 4 条**「每个具体子类至少有一个槽位入口实现」（带正对照与反空跑；CS0115 拦不住「函数体被删掉」）；检查器与投影再抽一份共用判据 `ExpectedType`（`SameAsFirst` 读 0 号槽、`Any` 两种都收）；5 处 `Count < k || IsEmpty(k)` 统一成 `!Has(k)`（`:8430` 那处是反极性，按原样保留）；访问器前置核对补**非字面下标弱判据表**与 helper 明细表，并把「守卫判据只到同体内出现一次判空」这条边界写进产物；新增「ctor 快照复制 == 1」不变量（与折叠面那条理由不同，不再共用一个标签）；票面数字按可复算口径重算（637 → 619）、StrForm override 分解改为 5、「硬转型 29 → 0」标注锚定口径并点出消费面剩余 3 处 `VariableTerm` 判别。（**该轮的门面分流判据与门禁④ 判据已被第三轮替换/收紧，见下一条**）
  - review 修正（第三轮，代码审查发现，已落地）：**门面分流判据改为「检查器由调用点担保」**——新增 `FunctionMethod.CallerOwnsCheck`（`OperatorMethod` 与 `StrForm.FormattedStringMethod` 各自声明为真），`ResolveSlots(List)` 变成「`CallerOwnsCheck` ∨ 无规则表 ⇒ 按调用点担保、无声明解析」。第二轮按「有没有规则表」分流时，运算符族里**唯一带 `argumentTypeArray`** 的 `PlusStrStr` 会去问那个只抛 `ExeEE` 的检查器，门面当场炸（"运算符族可用"这条断言因此只对不带表的成员成立）；回归用例 `LegacyFacade_WorksForACallerCheckedClassThatHasARuleTable`（真 `PlusStrStr` 的角色锚点 + 同形合成类，变异验证过：还原旧判据该用例必失败）。**门禁④ 收紧为「具体子类自己声明一个槽位入口」**（`DeclaredOnly`）——继承来的入口可能只是抛 `ExeEE` 的中转，`FormatPercent` 的 `GetIntValue`（继承自 `FormattedStringMethod`）就是负对照，删掉它自己声明的 `GetStrValue` 会被该用例抓到（变异验证过）。**快照等价性补上「求值时机」这一维**（旧稿只写「读值等价」）：外层不可折叠时访问器会重新求值已折叠的内层实参，新增 `UnfoldableCall_ReEvaluatesItsFoldedArgumentsAtCallTime` 钉住这条语义，票面相应段落随之改写。`ArgumentSlots` 的 ctor 增加 `roles.Length == arguments.Count` 校验（角色表写短会静默把尾槽降级成 `Loose`）。口径修正：消费面 369/379 的来源标注为阶段 A 基线（不再与 [spec.md](../spec.md) 的 372/404 并列成两个比例）。
  - review 修正（第四轮，代码审查发现，已落地）：**检查入口的两参门面也改成非虚**（`CheckArgumentType(string, List<AExpression?>)`；此前留着 `virtual`，覆写它会被 `CheckAndResolveSlots` 的三参虚分派静默绕过，`SlotMigrationGateTests` 相应补 `IsVirtual == false` 断言）——与三个取值入口同一处理。**访问器报契约而不是 NRE**：`ArgumentSlots.Term` 越界仍抛 `IndexOutOfRangeException`（与旧 `Required(i)` 同契约），空槽与 `Var` 拿到非变量实体时抛 `ExeEE` 并把 `RoleAt` 写进文案，故角色的生产消费点是报错文案；三态访问器的负下标统一按「缺失」处理（`Has`/`IsEmpty` 为 false、`IsMissing` 为 true、`TermOrNull` 为 null）。**6 处 `Has(i)` 补回旧列表模式的个数上界**（`Csvcstr`/`CsvData`/`ExistCsv`/`Varsize`/`SumArray`/`Join`：旧写法是 `[_, _, not null]` 这类**精确长度**模式，`slots.Has(2)` 只约束非空，等价的唯一理由是规则表把上限压在模式长度，改成 `slots.Count == N && slots.Has(k)` 后不再依赖该耦合；其余带 `..` 的模式本来就只约束「至少 N 个」，`Has` 已等价）。**测试夹具拆成两入口**（`Invoke` 用 `GlobalStatic.EMediator`、`InvokeWith` 原样传参），修掉 `exm ?? GlobalStatic.EMediator` 让「显式传 null」不可能却写在注释里的问题（`FunctionMethodTests` 那批「函数体不碰 exm」的用例恢复原强度）。**文档口径**：`docs/architecture.md` 那条 1252 字符的单行压回架构级陈述；票号引用补 `tracker/` 前缀（`FunctionMethodTerm.cs`、`FunctionMethod.cs`）；`AllowConstRef` 注释补上 `SumArrayMethod` 的既有豁免（`arg-slots.py` 一直开着这条豁免，注释却写成无条件不变量）；`spec.md` 的「只有检查器能构造」「函数体不判空、不 cast」按实读改写。第二/三轮记的「消费面剩余 3 处 `VariableTerm` 判别」按实读是 4 处（第四处是拒绝路径，**不该**换成 `Var(i)`）；测试侧构造点自校验随之 3 → **7**（新增 4 条访问器契约用例）。
  - review 修正（第五轮，代码审查发现，已落地）：**只改文档与注释，无行为改动**（构建 0 错误、全量重编译下 86 条既有分析器警告均落在未改动文件、测试 684 全绿、`arg-slots.py` 自校验通过且重生成 `slots-progress.md` 逐行不变）：① `docs/architecture.md` 那条上一轮声称「压回架构级陈述」却仍有 945 字符的单行，按语义拆成 4 条（表达式 / `ArgumentListExtensions` / `FunctionMethod` 的槽位两面 / 折叠面与门面），内容不删；② 注释里的 eraBasic 名改 C# 类型名（`IdentifierDictionary.cs`、`FunctionMethod.cs` 的 `MESSKIP` → `MesSkipMethod`，`OperatorMethod.cs`、`FunctionMethod.cs` 的 "PLUS's string overload" → `PlusStrStr`）；③ 代码注释去掉重构过程叙述（`SlotRole` 摘要的 "as `ArgumentListExtensions.Required` had it" 与 "asserted by the projection tests"、`ArgumentSlots` 类摘要的 "a migrated body … an unmigrated one"、两处门面文档的 "migration gate" / "used to carry" / "rather than leaving every role Loose"），票号引用与全部约束句保留（按裁定：留票号、去过程）。**四条经机械核对推翻的误报（留档，下轮不必重报）**：① `ArgumentSlots.Trusted(` 活体 **15** 处（`StrForm.cs` 8 / `OperatorMethod.cs` 4 / `Process.CalledFunction.cs` 1 / 工厂 2），与脚本断言、`slots-progress.md` 及本文件口径一致，不是 17、`StrForm.cs` 也不是 9；② `Has(i)` 与 `Term(i)` 不会不一致——`Term` 只在 `terms[i] == null` 时抛 `ExeEE`，而 `Has` 正是 `terms[i] != null`；③ `PlusStrStr` 的定数表是 `[typeof(string), typeof(string)]`，故 `ProjectFixedRoles` 给出的 `Required | String` 与表相符，不存在与 `long` 的矛盾；④ 门面对无规则表类（`MesSkipMethod`）走 `ResolveSlots(arguments, -1)` 直接返回、**不经过**检查器，故不会每次旧签名调用重发 `ParserMediator.Warn`（真正成立的只是「规则表类走门面会重跑一次检查」，这条第三轮已定案并写进完成标准）。另核 `_scan.py` 的 `NOT_NULL_EXPECTED` 87/76 → 88/77：剥注释口径 81/70 未动，属本次新增注释所致，非门禁松动。**仍成立但本票未处理**：`SlotRole` 的类型位在生产侧仍无读取点（只进报错文案）；`BuiltinMethod/*.cs` 那 3 处可改 `Var(i)` 的判别未迁移（后由 `arg-contract-10` 实测更正为 2 处并完成迁移）；`FunctionMethod.cs` 新增 354 行里 139 行为注释（39%，本轮只去过程叙述、未整体精简；`Emuera/` 全 diff 为 12.9%）。
- **未决**：`UniqueRestructure` 收到副本导致写回被丢弃这一既有事实（04 核对 #3）本票不顺手改，若改须另立票并重新评估 13 个类的不变量；无规则表的类（`MesSkipMethod` 等）不产出角色，按「调用点担保、无声明」处理；**快照取代活体表带来的语义变更（不可折叠外层重新求值已折叠的内层实参，状态相关的可折叠调用取值随之为「调用时的值」而不再是「首次执行时的常量」）本票不动**，第四轮 review 只把这条差异写进 `ArgumentSlots` 的类注释与票面：要不要保留新语义须单独立项裁定（改回等价只需让访问器与折叠共用一处存储），保留则还需补 `GETMETH`/`GETMETHS` 转交路径的用例与不可折叠外层的每执行成本基准；**门面长期保留**（后由 `arg-contract-08` 裁定移除，`CallerOwnsCheck` 一并删除）——本票把 HEAD 版完成标准的「迁移完成后删除适配器」降级成「非虚门面」（第三轮 review 指出，见下），`FunctionMethod` 是 `internal`，门面今天的唯一消费者是 `Emuera.Tests`（`InternalsVisibleTo`）里那批测门面自己的用例；若要兑现原标准，须另立收尾票：把测试全部改走 `Slots`/`Invoke` 后删掉三个门面入口，并同步删掉门禁的第 1/2 条与 `LegacyFacade_*` 用例；`IsMissing` 仍无生产调用点（保留为三态的完整接口，只由访问器契约用例覆盖）；**角色（`roles`/`SlotRole`）的效力边界（第四轮已修订）**：它仍不做运行期断言，但已不是「只服务测试期」——`Term`/`Var` 的失败文案会报出该槽的角色，投影断言另由 `ArgumentSlotsTests` 覆盖；`Int`/`Str`/`Var` 与 `ArgumentListExtensions.Required(i)` 仍同强度（可省略槽为空时由函数体先判、`Var` 仍是转型），故「函数体不再自证非空、不再 cast」由「守卫纪律 + 脚本期核对 + 访问器的契约报错」兜住，**不是由类型兜住**；若要让类型真兜，须另立票把访问器改成断言式（并在 183 个类上补/校验守卫），本票不做。**本票收尾时把「未决」各项各自转出为独立票**：`arg-contract-06`（快照求值时机）、`arg-contract-07`（访问器断言式 + `SlotRole`/`IsMissing` 去留）、`arg-contract-08`（门面去留）、`arg-contract-09`（`UniqueRestructure` 副本写回）、`arg-contract-10`（3 处可改 `Var(i)` 的判别），本票随之转 `implemented`
- **邻票**：`arg-contract-04`（本票的输入：核对结论、片 0 工件 [slots-consumption.md](../slots-consumption.md)）、`nullable-migration-18`（`AllowConstRef` 不变量）、`nullable-migration-22`（其余 `as`+`!` 站点的理由注释）、`backlog-15`（`Argument` 三字段折叠协议，同一 seam 的另一侧，不并轨）
- **转出项的收尾（2026-10-05）**：`arg-contract-06`–`arg-contract-10` 五票均已结清，正文中「门面长期保留」「3 处可改 `Var(i)`」等表述已按各票裁定改写（08 移除门面与 `CallerOwnsCheck`；10 实测只有 2 处可迁移）。本节其余内容是本票当时的记录，不再当作待办。
