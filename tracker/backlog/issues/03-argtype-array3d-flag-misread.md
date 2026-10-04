# _ArgType.Array3D 读错标志位，三维数组实参槽不校验维度

id: backlog-03
Status: superseded
Type: bug

## 任务

发现于 2026-09-27 的只读架构审查（静态推导，未在运行期复现）。

`Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs:78`：

```csharp
public bool Array3D => (type & ArgType.Array2D) != 0;
```

`ArgType.Array3D` 是独立位（同文件 `:32`，`1 << 7`），与 `Array2D`（`:31`，`1 << 6`）不同。故 `Array3D` 属性实际回答的是「是不是二维」。

可达链：`RefInt3D = RefInt | Array3D`（`:52`）经 `ArrayDims`（`:84-88`）——`Array` 未置位（`RefInt3D` 不含 `Array` 标志），`Array1D`/`Array2D` 为假，`Array3D` 因该缺陷也为假，故 `ArrayDims` 得 `0`；落到 `:195` 的 `switch (dims)` 的 `case 0:`（`:197-204`），该分支只按 `rule.Int`/`rule.String` 出错误文案，`:230` 的 `case 0: error = typeNotMatch;` 也只看类型——整型规则下 `typeNotMatch` 恒假。结论：**声明「三维整型数组」的槽接受任意整型表达式**。

### 已核实的后果（2026-09-27 复核，**纠正初版结论**）

初版判断「最终在 `:6966` 的 `(VariableTerm)` 硬转抛 InvalidCastException」**不成立**：`:224-242` 的 `if (rule.Ref)` 块中，实参不是 `VariableTerm` 时走 `else { error = true; }`（`:239-242`），该分支与 `dims` 无关——非变量实参被拒，**可达的 InvalidCastException 为 0**。

真正被放行的是**维度不符的变量**（实参确实是 `VariableTerm`，硬转成功）：

- **标量或一维整型变量** → `ReadColormatrix`（`:6963-7040`）的 `IsArray2D` 与 `IsArray3D` 两个分支都不命中 → 返回的 `cm` 是 `new float[5][]`（`:6968`）且 **5 行全为 null** → 下游 `GDrawG`/`GDrawCImg` 解引用空矩阵
- **二维整型变量** → 命中 `IsArray2D` 分支，按二维处理（`AllowConstRef` 下这可能是期望行为，需确认规则意图）

活体调用点共两个，规则均为 `RefInt3D | AllowConstRef`（`RefInt3D` 全库仅此两处）：

| 规则 | 类（注册名） | `ReadColormatrix` 调用 | 空槽守卫 |
| --- | --- | --- | --- |
| `:8323` | `GraphicsDrawGMethod`（GDRAWG） | `:8379` 传 `argNo = 10` | 有（`:8373` 的 `arguments.Count == 10 \|\| arguments[10] == null`） |
| `:8503`（`OmitStart = 6`） | `GraphicsDrawSpriteMethod`（GDRAWSPRITE） | `:8581` 传 `argNo = 6` | **无** |

（初版把 `:8503` 记为 GDRAWGWITHMASK 有误，实为 GDRAWSPRITE。）

后者的无守卫空槽是另一类缺陷（函数体在「设计上可省略」的槽上解引用 null），归 issues/16；`GDRAWSPRITE(0,"s",1,2,3,4,,)` 在 `:6966` 的 `arguments.Required(6)` 处 NRE。

复现写法（未执行，供确认）：向 GDRAWG 的 colormatrix 实参传**标量整型变量**，预期不再被 `CodeEE` 拒绝，而是在 `cm` 全 null 行的下游抛 NRE。

完成标准：

- `:78` 改为 `(type & ArgType.Array3D) != 0`
- 补表驱动用例：`RefInt3D` 槽传 {三维整型变量 → 通过 / 标量整型变量 → 报维度错（文案 `ArgIsNotNDIntArray`）}，断言得到脚本级 `CodeEE` 而非下游空矩阵 NRE
- 裁定二维整型变量在 `RefInt3D` 槽上的预期：`GDRAWG` 的 `ReadColormatrix` 有 `IsArray2D` 分支，若该形态是有意支持，则规则不应声明为 `RefInt3D`（或该槽应允许 2D）；这决定 `:8323`/`:8503` 是否也要改，而不只是改 `:78`
- `RefInt3D` 的出现处已核实为恰好两处（`:8323`、`:8503`），无需再查

## 方案

待评估。候选：只改标志位（1 行）；或改标志位 + 复核 `RefInt3D`/`RefAny3D`/`RefString3D` 三者的实际使用（`RefInt3D` 是唯一被 `case 3` 正确分支依赖的）。

## 影响

- 与本票同型的一处已在 `tracker/nullable-migration/issues/17-container-member-guards.md:20` 记录（`ArgumentBuilder.cs` 的 `SP_COPY_ARRAY_Arguments` 第 2 实参 `IsArray1D/IsArray2D/IsArray3D` 检查缺 `return null`）——同属「维度校验写漏」，但站点不同
- 相关票：issues/02（实参契约表兑现）、issues/04（省略判据）
- 改动量估计：1 行 + 用例。但若不先确认 GDRAWG 三维实参的合法形态，可能把「本就错误的规则块」当成「写错的属性」修
- **已转出（2026-09-27）**：承接方 `arg-contract-01`。本票作为审查记录保留（文件不移动）
