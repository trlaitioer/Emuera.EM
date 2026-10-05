# UniqueRestructure 收到的是副本，13 个类的写回被静默丢弃

id: arg-contract-09
Status: wontfix

## 任务

承接 `arg-contract-04` 的「核对」#2/#3 与 `arg-contract-05` 的「未决」节（两票都明确不顺手改）。

现象：`FunctionMethodTerm.Restructure` 把活体实参表 `Slots.FoldTarget` 拷一份再交给 `UniqueRestructure`（`FunctionMethodTerm.cs` 的 `arguments = Slots.FoldTarget;` 后 `[.. arguments]`；阶段 B 前写作 `[.. Arguments]`），那 14 个 `UniqueRestructure` override 里的写回全部被丢弃；折叠期的写回实际只走基类的原地路径。全仓该副本分配仅此 1 处（issues/04 核对 #9）。

现状无害（折叠面与消费面不相交：`GetIntValue`/`GetStrValue` 零 `Restructure` 调用），但这是一处「语义与代码不符」的死契约。同源观察项：`ArrayMultiSortExMethod.UniqueRestructure` 从不被调用（该类未设 `HasUniqueRestructure`，且两条规则都不含 `AllowConstRef`，故常量数组进不到该槽），是静态可证的死 override。

完成标准（2026-10-05 裁定后落地）：

- 裁定**保留**（见「方案」）；「写回被丢弃」已写进 `docs/architecture.md` 的折叠面那一条（连同「全仓仅此 1 处 `[.. arguments]`」）
- 未采纳「消除」：传活体表或让 `UniqueRestructure` 返回新表都要逐一重估 13 个设了 `HasUniqueRestructure` 的类（17 处赋值）与 `AllowConstRef` 一面的不变量，而折叠期与消费面本不相交
- `ArrayMultiSortExMethod` 的死 override **已删除**（`BuiltinMethod/PrivateAdditions.cs`）：该类未设 `HasUniqueRestructure`、两条规则都不含 `AllowConstRef`，静态可证不被调用；删除后 `UniqueRestructure` override 由 14 个减为 13 个

## 方案

定为**保留**：`UniqueRestructure` 继续收活体表的副本，副本上的写回继续被丢弃——这是死契约中无害的一侧（折叠期与消费面不相交），代价已写进代码注释与 `docs/architecture.md`；改它的风险面（13 个类、`AllowConstRef` 不变量）远大于收益。

## 影响

- 触碰折叠面，是本 effort 里最需要回归网的一块；折叠期在稳态每调用 0 次（`ReduceArgumentOnLoad` 默认 `NO`），风险在装载/首次执行的语义而非热路径
- 与 `arg-contract-06` 同属折叠面，但改的是**写**的一侧，两者可各自独立裁定
