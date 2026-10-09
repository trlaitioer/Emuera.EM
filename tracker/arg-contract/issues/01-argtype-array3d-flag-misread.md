---
id: arg-contract-01
status: implemented
type: bug
---

# _ArgType.Array3D 读错标志位，三维数组实参槽不校验维度

## 任务

`Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs:78`：

```csharp
public bool Array3D => (type & ArgType.Array2D) != 0;
```

`ArgType.Array3D` 是独立位（`:32`，`1 << 7`），与 `Array2D`（`:31`，`1 << 6`）不同。故 `Array3D` 属性实际回答的是「是不是二维」。

可达链：`RefInt3D = RefInt | Array3D`（`:52`）经 `ArrayDims`（`:84-88`）——`Array` 未置位（`RefInt3D` 不含 `Array` 标志），`Array1D`/`Array2D` 为假，`Array3D` 因该缺陷也为假，故 `ArrayDims` 得 `0`；落到 `:195` 的 `switch (dims)` 的 `case 0:`（`:197-204`），该分支只按 `rule.Int`/`rule.String` 出错误文案，`:230` 的 `case 0: error = typeNotMatch;` 也只看类型。

**已核实的后果**（纠正初版结论「抛 InvalidCastException」——不成立）：`:224-242` 的 `if (rule.Ref)` 块中，实参不是 `VariableTerm` 时走 `else { error = true; }`（`:239-242`），该分支与 `dims` 无关 ⇒ 非变量实参被拒，**可达的 InvalidCastException 为 0**。真正被放行的是**维度不符的变量**：

- **标量或一维整型变量** → `ReadColormatrix`（`:6963-7040`）的 `IsArray2D` 与 `IsArray3D` 两个分支都不命中 → 返回的 `cm` 是 `new float[5][]`（`:6968`）且 **5 行全为 null** → 下游 `GDrawG`/`GDrawCImg` 解引用空矩阵
- **二维整型变量** → 命中 `IsArray2D` 分支，按二维处理（`AllowConstRef` 下这可能是期望行为，需确认规则意图）

活体调用点共两个，规则均为 `RefInt3D | AllowConstRef`（`RefInt3D` 全库仅此两处）：

| 规则 | 类（注册名） | `ReadColormatrix` 调用 | 空槽守卫 |
| --- | --- | --- | --- |
| `:8323` | `GraphicsDrawGMethod`（GDRAWG） | `:8379` 传 `argNo = 10` | 有（`:8373` 的 `arguments.Count == 10 \|\| arguments[10] == null`） |
| `:8503`（`OmitStart = 6`） | `GraphicsDrawSpriteMethod`（GDRAWSPRITE） | `:8581` 传 `argNo = 6` | **无** |

后者的无守卫空槽是 issues/03 的缺陷类；`GDRAWSPRITE(0,"s",1,2,3,4,,)` 在 `:6966` 的 `arguments.Required(6)` 处 NRE。

复现写法（未执行，供确认）：向 GDRAWG 的 colormatrix 实参传**标量整型变量**，预期不再被 `CodeEE` 拒绝，而是在 `cm` 全 null 行的下游抛 NRE。

完成标准：

- `:78` 改为 `(type & ArgType.Array3D) != 0`
- 补表驱动用例：`RefInt3D` 槽传 {三维整型变量 → 通过 / 标量整型变量 → 报维度错（文案 `ArgIsNotNDIntArray`）}，断言得到脚本级 `CodeEE` 而非下游空矩阵 NRE
- 二维实参的裁定见「方案」（已裁定：规则块不改）
- `RefInt3D` 的出现处已核实为恰好两处（`:8323`、`:8503`），无需再查

## 方案

只改标志位（1 行）。

**二维实参的裁定**：二维是**合法**的。`GraphicsDrawGMethod` 构造器之后那段被注释掉的旧手工检查要求 `!(IsArray2D || IsArray3D)` 即拒绝（`!(varToken.Identifier.IsArray2D && !varToken.Identifier.IsArray3D)` 的等价写法见该处），即原意是「二维或三维」；而末两段规则（`RefInt2D` 段与 `RefInt3D` 段，均 `OmitStart = 10`）合起来正是这个条件——二维命中前段、三维命中后段、一维与标量两段都拒。故**规则块无需改动**。`RefInt3D` 的出现处已核实为恰好两处。

> 补充（同一构造器内被注释掉的旧检查）：`arguments.Count == 10 → return null` 即「省略颜色矩阵」是原意，与本票的正控制一致。

## 影响

- 与本票同型的一处已在 `tracker/nullable-migration/issues/17-container-member-guards.md:20` 记录（`ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments` 第 2 实参 `IsArray1D/IsArray2D/IsArray3D` 检查缺 `return null`）——同属「维度校验写漏」，站点不同
- 相关票：issues/02（省略判据）、issues/03（可省略槽上的 `Required`）、issues/04（阶段 A 止血）、issues/05（槽位类型深化）
- 改动量估计：1 行 + 用例。但若不先裁定二维实参的合法形态，可能把「本就错误的规则块」当成「写错的属性」修
- 原始审查记录：`backlog-03`
