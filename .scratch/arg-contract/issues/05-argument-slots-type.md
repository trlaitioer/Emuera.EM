# 实参契约深化：让声明兑现为槽位类型（阶段 B）

Status: ready-for-agent
Type: task

## 任务

承接 [arg-contract/issues/04](04-argument-slots.md) 的**阶段 B**（该票的阶段 A：片 0 重新基线 + 片 1 止血，已落地）。

形状选择、可行性核对与三个被撤销的原决策（折叠顺序、不可变别名、`[Obsolete(error: true)]` 门禁）的**证据都在 04 的「核对」节**；本票不重复记录，只承担阶段 B 的任务、完成标准与影响。

### 背景（一句话）

`ArgTypeList` 是声明式实参契约，但它只有三个独立解释者（检查器 / 折叠 / 函数体），彼此无同步机制，编译器又因「形状是数据不是类型」而无法介入。阶段 A 已把「可省略槽上的硬转型解引用」这一条缺陷车道清空；本票兑现剩下的结构性收益：**把规则表的解释收敛到检查期一次**，函数体不再自证非空、不再 cast。

### 完成标准

- 规则表在**检查期**产出「已解析槽位集合」新类型：每槽确定为值 / 显式留空 / 缺失三态（决策 5），且只能由检查器（规则表族）或三个具名 `Trusted` 调用点族构造
- 基类提供角色化访问器（`Int`/`Str`/`IsInt`/`IsStr`/`Var`/`Has`/…），覆盖 372/404 = 92.1% 的现有消费点（口径见 04）；迁移完成的函数体不再判空、不再 cast
- 旧签名 `GetIntValue(ExpressionMediator, List<AExpression?>)` 保留为「检查 + 解析 + 转发」适配器，183 个子类按切片迁移；**迁移完成后删除适配器**
- 迁移门禁三层齐备（编译期 / 测试期 / 脚本期，见「方案」）
- 折叠面不动：14 个 `UniqueRestructure` 保持旧签名与旧副本语义（04 核对 #2/#3）
- UDF 族的默认值语义不变（`CalledFunction.TryConvertArg` 一侧不在本票）

## 方案

形状：**类型是检查器产出的只读视图（元素数组为检查期快照），折叠仍发生在原位、不改 `Restructure` 的返回语义**。

```csharp
// FunctionMethod 内嵌：私有 ctor + 派生类可简单名引用
internal sealed class ArgumentSlots
{
    private readonly AExpression?[] terms; // 检查期快照；写回只经 FunctionMethod 内部方法（FunctionMethodTerm.Restructure 调用）
    private readonly SlotRole[] roles;     // 与命中的 ArgTypeList 对应；变长尾按 LastVariadics 取模

    internal static ArgumentSlots Checked(AExpression?[] terms, SlotRole[] roles); // 规则表族：唯一入口是 CheckArgumentTypeEx
    internal static ArgumentSlots Trusted(AExpression?[] terms);                   // 调用方担保族：运算符 / StrForm / TOSTR 包装（13 处）
}
```

「只能由检查器构造」的准确表述：规则表族由 `CheckArgumentTypeEx` 独占；运算符族（`OperatorMethod.cs`）、StrForm 族（`StrForm.cs`）、TOSTR 包装（`Process.CalledFunction.cs`）都由调用点担保、只走 `Trusted`，该入口的调用点集合由脚本计数锁死（全库 `new FunctionMethodTerm(` 共 14 处 + 1 处 target-typed `new(...)`，构造点共 15）。

访问器（`exm` 显式传入：term 的构造点在解析期只有 `GlobalStatic.EMediator`，且既有测试的 `GetIntValue(null!, [...])` 形态要继续可用）：

| 访问器 | 语义 | 前置（`roles` 保证，可脚本核对） | 覆盖车道（live） |
| --- | --- | --- | --- |
| `Count` | 实参个数（折叠不改长度） | — | 119 处 `Count` + 18 个循环 |
| `Has(i)` / `IsEmpty(i)` / `IsMissing(i)` | 三态：有值 / 显式留空 / 数量省略 | — | 27 处判空 + 16 处列表模式 |
| `Int(exm,i)` / `Str(exm,i)` | 取值 | `role(i)=Required` ∧ 类型匹配 | 188 + 152 |
| `IsInt(i)` / `IsStr(i)` | operand type 询问 | `role(i)=Required` | 15 + 17 |
| `Var(i)` | 变量实体 | `role(i)=Ref` ∧ 非 const ∧（规则含 `AllowConstRef` ⇒ 类已设 `HasUniqueRestructure`） | 37 |
| `Term(i)` / `TermOrNull(i)` | 原始句柄逃生口 | 前者 Required | 2 + 1 |
| `Tail(from)` | 整段原样转交 | — | 2（`GETMETH`/`GETMETHS`，比今天的 `Skip().ToArray()` 少一次分配） |

迁移面与折叠面不相交（04 核对 #2/#3），故本票**不需要**可变逃生口。

**切片**：① 机制落地（基类新虚方法 + 旧签名保留为适配器，零迁移、全绿）→ ② pilot 2 个（`MaxMethod` 变长尾 + `DataTableRowSetMethod` 三态/转发/`AllowConstRef`）→ ③ 分批迁移（XML 族 → DT 族 → Graphics 族（含 `ReadPoint`/`ReadColormatrix` 等整表转发 helper 改签名）→ 数组/集合族（含 14 个 `UniqueRestructure` 类）→ 字符串/数值族 → 其余）→ ④ 删适配器。

**门禁**：
- 编译期：最后一步把旧虚方法改为**非虚** ⇒ 残留 override 报 CS0115（切片期间不能用，否则 183 个类必须一次改完）
- 测试期：反射门禁测试（枚举 `FunctionMethod` 子类，断言无旧签名 override；`InternalsVisibleTo Emuera.Tests` 已具备）+ 访问器契约与三态用例
- 脚本期：[arg-slots.py](../arg-slots.py) 扩成核对 `arguments.Required(`/`arguments[<字面量>]`/硬转型在消费面归零、`Int/Str/Var` 站点落在 Required 槽、`AllowConstRef ⇒ HasUniqueRestructure`、`Trusted` 调用点不增

## 影响

- **改动面**：`FunctionMethod.cs`、`Creator.Method.cs`（183 个子类 / 约 682 处消费点）、`FunctionMethodTerm.cs`、`ArgumentBuilder.cs`、`IdentifierDictionary.cs`、`ArgumentListExtensions.cs`；UDF 侧（`UserDefinedMethodTerm.cs`）不在内——`UserDefinedRefMethodTerm` 已确认为死代码（见 [issues/03](03-required-on-omittable-slot.md)）
- **性能**：per-call 拿不到可测收益（理由必须是契约安全，不是速度）。可省的成本在构造/折叠期；**仓库无基准设施** ⇒ 任何性能主张必须先补基准
- **共享语义**：不改（StrForm 的 5 个静态 term、`SpCallFArgment.RowArgs`、`UserDefinedRefMethodTerm.srcArgs` 均维持现状）；快照使 builtin 槽表不再与调用方共享数组
- **未决**：`UniqueRestructure` 收到副本导致写回被丢弃这一既有事实（04 核对 #3）本票不顺手改，若改须另立票并重新评估 13 个类的不变量
- **邻票**：[issues/04](04-argument-slots.md)（本票的输入：核对结论、片 0 工件 [slots-consumption.md](../slots-consumption.md)）、[nullable-migration/issues/18](../../nullable-migration/issues/18-unguarded-as-casts.md)（`AllowConstRef` 不变量）、[nullable-migration/issues/22](../../nullable-migration/issues/22-rationale-comments-and-contracts.md)（其余 `as`+`!` 站点的理由注释）、[`.scratch/backlog/issues/15`](../../backlog/issues/15-argument-folded-value-protocol.md)（`Argument` 三字段折叠协议，同一 seam 的另一侧，不并轨）
