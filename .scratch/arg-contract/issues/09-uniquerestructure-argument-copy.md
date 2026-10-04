# UniqueRestructure 收到的是副本，14 个类的写回被静默丢弃

Status: needs-triage

## 任务

承接 [issues/04](04-argument-slots.md) 的「核对」#2/#3 与 [issues/05](05-argument-slots-type.md) 的「未决」节（两票都明确不顺手改）。

现象：`FunctionMethodTerm.Restructure` 把活体实参表 `Slots.FoldTarget` 拷一份再交给 `UniqueRestructure`（`FunctionMethodTerm.cs` 的 `arguments = Slots.FoldTarget;` 后 `[.. arguments]`；阶段 B 前写作 `[.. Arguments]`），那 14 个 `UniqueRestructure` override 里的写回全部被丢弃；折叠期的写回实际只走基类的原地路径。全仓该副本分配仅此 1 处（issues/04 核对 #9）。

现状无害（折叠面与消费面不相交：`GetIntValue`/`GetStrValue` 零 `Restructure` 调用），但这是一处「语义与代码不符」的死契约。同源观察项：`ArrayMultiSortExMethod.UniqueRestructure` 从不被调用（该类未设 `HasUniqueRestructure`，且两条规则都不含 `AllowConstRef`，故常量数组进不到该槽），是静态可证的死 override。

完成标准（待评估后细化）：

- 裁定保留还是消除：保留 ⇒ 把「写回被丢弃」写进基类注释与 `docs/architecture.md`；消除 ⇒ 传活体表或让 `UniqueRestructure` 返回新表
- 若改，逐一重估 13 个设了 `HasUniqueRestructure` 的类（17 处赋值，变长尾规则段各一处）与 `AllowConstRef` 一面的不变量
- `ArrayMultiSortExMethod` 的死 override 一并处理（删掉，或补上 `HasUniqueRestructure` 并说明理由）

## 方案

待评估。

## 影响

- 触碰折叠面，是本 effort 里最需要回归网的一块；折叠期在稳态每调用 0 次（`ReduceArgumentOnLoad` 默认 `NO`），风险在装载/首次执行的语义而非热路径
- 与 [issues/06](06-slot-snapshot-eval-timing.md) 同属折叠面，但改的是**写**的一侧，两者可各自独立裁定
