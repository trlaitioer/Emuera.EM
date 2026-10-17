---
id: nullable-migration-36
status: implemented
type: bug
---

# 读档失败之前先删掉了当前存档

## 任务

转出自 `nullable-migration-34`（第 1 组的登记项，该票未处置）：`VariableEvaluator.LoadFrom`（`LOADDATA`）与 `VariableEvaluator.LoadGlobalFile`（`LOADGLOBAL`）都在**读取之前**调 `RemoveEMSaveData()`/`RemoveEMGlobalData()`。

- `LoadFrom`（`LOADDATA`）的删除排在 `CreateReader` **之后**：二进制档读不出来时 `CreateReader` 先抛 `FileEE`，这条路径上的删除尚未发生
- `LoadGlobalFile`（`LOADGLOBAL`）的 `RemoveEMGlobalData()` 排在读取**之前**（同一方法内先删、后打开并读取），故 `LOADGLOBAL` 遇到坏二进制档时全局数据**已经被删掉**——两处的破坏程度不同，修复要一并覆盖
- 文本档读损坏仍会走到删除：当前存档/全局数据先被清掉，随后读取失败，玩家既读不到旧档也拿不到新档（`nullable-migration-34` 第 1 组把「读不出来」报出来之后，这一点才成为可观测的失败）

完成标准（细化后）：MAP/XML/DT 这三类私有版存档扩展数据在读取失败时保持原样（实例与内容都不变），成功读档的终态与旧「先删后读」逐键一致，且目标键的旧值实例都被清空——含文件提供了的那些键（该键最终取文件值，但旧 `Dictionary`/`DataTable` 实例仍在原处被清空，经 `PluginManager.GetDataTable` 持表者看到同一实例且为空）；`Emuera.Tests` 有能分辨旧行为的最小夹具。普通变量与角色的失败保全不在本票，见 `nullable-migration-39`。

## 方案

已按「暂存 + 成功后合成」实施：`LOADDATA`/`LOADGLOBAL` 开始读取时由 `VariableData.BeginEMSaveDataLoad`/`BeginEMGlobalDataLoad` 返回一个 `EMDataLoadScope`，把变量数据当前那份 `EMDataState`（`DataStringMaps`/`DataXmlDocument`/`DataDataTables` 三只字典合一的持有者）换成一份空的暂存状态；读取成功后 `EMDataLoadScope.Commit` 在副本上合并暂存、清空目标键的旧实例，最后整体换入；未 `Commit` 就 `Dispose`（含所有失败路径）直接丢弃暂存、换回原状态。

判据：

- 失败路径不需要回滚代码：读取期间 `VariableData` 的三个属性指向暂存，当前那三只字典对象及其内容不被修改，暂存整体丢弃即可；`LoadGlobalFile` 提前 `return false`（类型/唯一码/版本不符）同样走 `Dispose`，此时暂存尚未写入，等价 no-op
- `Commit` 先构建新状态再整体换入：可能抛异常的步骤（复制外层字典、合并暂存）全部排在清空之前，中途失败时当前状态未被改动，`Dispose` 交回的仍是完整旧状态；清空只做 `Clear()`/`Remove()`（BCL 不抛；与旧实现读取前那一次 `Clear()` 同一假设），其后是三个字段的整体换入
- 成功终态与旧实现逐键一致：目标键（`LOADDATA` 为 `SaveMaps`/`SaveXmls`/`SaveDTs`，`LOADGLOBAL` 为对应 `GlobalSave*`）由文件提供的取文件值，未提供的按旧 `Remove*` 语义清空（地图/DataTable 原地 `Clear`，Xml 移除）；目标键的旧值实例——含被文件替换的那些——同样原地清空，与旧「先删后读」的可见效果一致；非目标键保持原值，读取写入的非目标键照旧合并
- 清空逻辑只有一处：`EMDataState.ClearEMData`（`EMDataLoadScope.Commit` 传入文件那份暂存状态以跳过它已覆盖的键，另对旧状态整体清一次；RESET 只传目标键集合），`RemoveEMSaveData`/`RemoveEMGlobalData`/`RemoveEMStaticData` 与 `Commit` 同一判据，避免新旧两套语义漂移；`ResetData`/`ResetGlobalData` 仍即时清理
- `LoadFrom` 拆出可测入口 `LoadFromFile(string)`：返回 `bool`（成功恒 `true`），失败抛 `ExeEE`/`FileEE`，沿用 `LoadFrom` 的抛契约而非 `LoadGlobalFile`/`SaveToFile` 的 `out errorReason`（后者服务要打原因的调用点）；路径取自 `Config.SavDir`，测试指不到别处，文件不存在与 `LoadFrom` 同样抛 `ExeEE(NotExistPath)`
- 两处原先的删除时点差异（`CreateReader` 之后 vs 打开文件之前）随删除动作一起消失

## 曾考虑的替代方案

1. 逐键引用快照回滚（票面候选 1 字面做法）：读取前记下三只字典的引用，失败后恢复。可行，但还要回答「哪些键被文件改写过」才能做成功路径的清理；暂存字典方案让读取完全不碰当前字典，这两件事同时消失
2. 只把 `Remove*` 挪到读取之后（票面候选 2 字面做法）：会把刚读进来的 MAP/XML/DT 一起清掉，不可行；必须只清理「文件未提供的键」，故仍需暂存或对照
3. 把整次读取做成事务（普通变量与角色一起快照/回滚）：唯一能兑现「坏档不毁当前进度」的做法，但需要全状态快照（系统数组、用户自定义变量 token 私有数组、`CharacterList`、`LastLoad*`）或两阶段解析，粒度与性能都要单独裁定；转 `nullable-migration-39`
4. 副本 + `Clone()` 替换实例（第一版实现）：清空时用新建字典与 `table.Clone()` 取代原地 `Clear()`，不用碰当前实例；放弃原因是换实例后插件经 `GetDataTable` 持有的旧表保留旧行，而游戏侧已换成空表，违反「成功读档的终态不变」

## 影响

- 对外可见：坏档（类型/唯一码/版本不符、正文截断、内容级损坏）不再清掉当前 SAVEDATA 扩展数据；`LOADGLOBAL` 不再在打开文件之前就删全局 MAP/XML/DT；成功读档的终态不变（目标键的旧实例都被原地清空，含被文件替换的那些；经 `GetDataTable` 持表者看到同一实例且为空）
- `LoadFromFile` 是新增公开入口；`LoadFrom` 的返回值/异常契约不变（读失败仍抛 `FileEE`，由 `ErrorReport` 分档打印，属 `nullable-migration-34`）
- 残留：读取失败时普通变量与角色仍会被 `SetDefaultValue`/`CharacterList.Clear` 复位或部分覆盖；本票只覆盖 MAP/XML/DT，见 `nullable-migration-39`
