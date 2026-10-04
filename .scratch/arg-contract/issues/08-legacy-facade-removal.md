# 旧签名的四个入口作为非虚门面长期保留，测试是它唯一的消费者

Status: needs-triage

## 任务

承接 [issues/05](05-argument-slots-type.md) 的「未决」节。该票把原完成标准的「迁移完成后删除适配器」降级为**非虚门面**并留在代码里：

- `FunctionMethod.GetIntValue`/`GetStrValue`/`GetReturnValue` 的 `(ExpressionMediator, List<AExpression?>)` 三个取值入口（`FunctionMethod.cs`，非虚）
- 两参 `FunctionMethod.CheckArgumentType(string, List<AExpression?>)`（同文件，非虚）

门面今天的唯一消费者是 `Emuera.Tests`（`InternalsVisibleTo`）里那批测门面自己的用例；生产侧已全部走槽位入口。

完成标准（待评估后细化）：

- 测试全部改走 `BuiltinFunctionTestKit` 的 `Slots`/`Invoke`/`InvokeWith`（需要时直接构造槽位）
- 删掉上述 4 个门面入口，并同步删掉 `SlotMigrationGateTests` 中「无旧签名 override」与「两参检查入口非虚」两条断言、以及 `ArgumentSlotsTests` 的 `LegacyFacade_*` 用例
- 论证门禁三层（编译期 CS0115 / 测试期反射 / 脚本期）在门面消失后的等价性——门面没了，CS0115 这一层就不再是「防回归」而是「本就不该存在」

## 方案

待评估。也可选择**有意保留**：那就把「为什么留」写进 `FunctionMethod` 的注释与 `docs/architecture.md`，否则每轮 review 都会重新问一遍。

## 影响

- 删门面后「旧签名残留」这一整类隐患消失，槽位入口成为唯一入口
- 门面是 `internal`，不影响插件 API 与脚本期行为
