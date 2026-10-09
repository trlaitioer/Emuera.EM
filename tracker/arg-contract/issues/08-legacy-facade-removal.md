---
id: arg-contract-08
status: implemented
---

# 旧签名的四个入口曾作为非虚门面长期保留，测试是它唯一的消费者（已移除）

## 任务

承接 `arg-contract-05` 的「未决」节。该票把原完成标准的「迁移完成后删除适配器」降级为**非虚门面**并留在代码里：

- `FunctionMethod.GetIntValue`/`GetStrValue`/`GetReturnValue` 的 `(ExpressionMediator, List<AExpression?>)` 三个取值入口（`FunctionMethod.cs`，非虚）
- 两参 `FunctionMethod.CheckArgumentType(string, List<AExpression?>)`（同文件，非虚）

门面今天的唯一消费者是 `Emuera.Tests`（`InternalsVisibleTo`）里那批测门面自己的用例；生产侧已全部走槽位入口。

完成标准（2026-10-05 落地，取「移除」）：

- 删掉那 4 个旧签名入口，并删掉 `FunctionMethod.CallerOwnsCheck`——核实后它全仓唯一的**读取**点就在只服务门面的私有 `ResolveSlots(List<AExpression?>)`（其余为 `OperatorMethod`/`StrForm` 的 override），门面一去它即成死声明
- `SlotMigrationGateTests` 的两条断言改为**反向断言**（旧取值签名在基类与全部子类上都不存在、两参检查入口不存在），机械守卫不丢，且能拦住把兼容层加回来的改动；`ArgumentSlotsTests` 的 4 条 `LegacyFacade_*` 用例与只服务它们的 2 个嵌套类删除
- 测试侧其余用例本就经 `BuiltinFunctionTestKit` 的 `Slots`/`Invoke`/`InvokeWith` 走构造点，无需改写
- `docs/architecture.md` 与 `tracker/arg-contract/spec.md` 的门面段落同步改写
- 三层门禁的等价性：门面消失后 CS0115 不再是「防回归」；反射层（反向断言）与 `arg-slots.py` 的消费面计数是仅有的两层，见「影响」

## 方案

取**移除**（2026-10-05 裁定）：留着门面时，每轮 review 都要重新解释「它为什么在、谁在用它」，而它的唯一消费者是测它自己的用例。移除后槽位入口成为唯一取值入口。

## 影响

- 删门面后「旧签名残留」这一整类隐患消失，槽位入口成为唯一入口；`List<AExpression?>` 只剩折叠面在用（`UniqueRestructure` 与 `arg-contract-09` 的副本转交）
- 门面与 `CallerOwnsCheck` 都是 `internal`，不影响插件 API 与脚本期行为；运算符 / StrForm 的「检查器由调用点担保」仍由它们故意抛 `ExeEE` 的那一个检查重载承载
- 门禁从「门面必须存在且非虚」翻转为「旧签名必须不存在」，仍能拦住兼容层回归
