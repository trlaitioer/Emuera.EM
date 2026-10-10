---
id: nullable-migration-36
status: draft
type: bug
---

# 读档失败之前先删掉了当前存档

## 任务

转出自 `nullable-migration-34`（第 1 组的登记项，该票未处置）：`VariableEvaluator.LoadFrom`（`LOADDATA`）与 `VariableEvaluator.LoadGlobalFile`（`LOADGLOBAL`）都在**读取之前**调 `RemoveEMSaveData()`/`RemoveEMGlobalData()`。

- `LoadFrom`（`LOADDATA`）的删除排在 `CreateReader` **之后**：二进制档读不出来时 `CreateReader` 先抛 `FileEE`，这条路径上的删除尚未发生
- `LoadGlobalFile`（`LOADGLOBAL`）的 `RemoveEMGlobalData()` 排在读取**之前**（`VariableEvaluator.cs`：3258 先删、3265 才读），故 `LOADGLOBAL` 遇到坏二进制档时全局数据**已经被删掉**——两处的破坏程度不同，修复要一并覆盖
- 文本档读损坏仍会走到删除：当前存档/全局数据先被清掉，随后读取失败，玩家既读不到旧档也拿不到新档（`nullable-migration-34` 第 1 组把「读不出来」报出来之后，这一点才成为可观测的失败）

完成标准（待评估后细化）：读取失败时当前存档/全局数据保持原样；`Emuera.Tests` 有能分辨旧行为的最小夹具。

## 方案

待定，两条路各自要给出判据：

1. 快照/回滚：读取前先记下待删数据的来源，读取成功后才真正删除
2. 重排：把 `Remove*` 挪到读取成功之后——但它必须排在加载之前（否则会删掉刚读进来的内容），故需要等价的改写形式

## 影响

- 读失败时的数据保全行为变更（玩家可观测：坏档不再连带毁掉当前进度）
- 与 `nullable-migration-34` 的失败语义（保留返回值契约 + 打出可读错误）同源，不改变那部分
