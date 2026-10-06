# 存档坏输入抛出的 FileEE 落到「予期せぬエラー」+ 堆栈

id: nullable-migration-30
Status: needs-triage
Type: bug

## 任务

`nullable-migration-28` 的站点 3 按方案把扩展数组块内的 EOF 从 `NullReferenceException` 改成 `FileEE`。复核该票完成标准（「坏输入分别得到回退值或**可读错误**」）时发现：新抛的 `FileEE` 在装载路径无人接，用户看到的仍是「予期せぬエラー」+ .NET 堆栈，只是其中多了 `FileEE` 的类型名与消息。

链路（均已核对）：

- `FileEE`（`Emuera/Runtime/Utils/EmueraException.cs:82`）继承 `EmueraException`，**既不是 `CodeEE` 也不是 `ExeEE`**
- `Process.handleException`（`Emuera/Runtime/Script/Process.cs:648` / `:688` / `:693`）只对 `CodeEE`/`ExeEE` 有专属分支，其余一律走 `else`：打 `HasUnexpectedError` 并逐行打印 `StackTrace`；它只被 `Process.cs:258` / `:413` 的 `catch (Exception)` 调用
- 两个 `LoadFrom` 调用点都没有 try/catch：`LOADDATA`（`Emuera/Runtime/Script/Process.ScriptProc.cs:1159`，其前置 `CheckData` 失败会转成 `CodeEE(LoadCorruptedData)`，是一条可读路径）与 `loadGameWaitInput`（`Emuera/Runtime/Script/Process.SystemProc.cs:1314`，前置的槽位可用性检查同样来自 `CheckData`）
- 前置检查读不到数据区：`VariableEvaluator.CheckDataByFilename`（`VariableEvaluator.cs:2695`）只读文件类型、unique code、版本与存档名，故**头部完好、数据区被截断**的记录能过检查，随后在 `LoadFrom` 里抛 `FileEE`

即：`CheckData` 能覆盖的损坏给出可读的「不正なデータをロードしようとしました」，它覆盖不到的损坏（数据区截断）给出 CLR 堆栈。范围不止 28 新增的那一处——`EraDataStream.cs`（72 处）、`EraBinaryDataReader.cs`（9 处）、`CharacterData.cs`/`VariableData.cs`（7 处）中既有 88 处同型 `FileEE` 走的是同一个出口。

完成标准（待评估后细化）：

- 存档/装载类错误有明确的报错分级：或让 `FileEE` 归入 `ExeEE` 分支（`HasEmueraError` + 消息、不打堆栈），或为 `FileEE` 单开分支，或在两个 `LoadFrom` 调用点转成 `CodeEE(LoadCorruptedData)`
- 兜底分支保留：编程错误（索引越界、Pfc 一类）仍要连堆栈一起响亮报出
- `Emuera.Tests` 覆盖「数据区被截断的存档得到明确错误文本」

## 方案

待评估。三条候选的差别在错误文本与是否带位置：`CodeEE` 会带文件名行号（`LOADDATA` 现场）但语义是脚本错误；`ExeEE` 是 Emuera 自身错误、无堆栈；`FileEE` 单开分支可保留「存档损坏」语义并统一文案。

## 影响

- 触及 `handleException` 与两个 `LoadFrom` 调用点；正常读档路径不变
- 与 `nullable-migration-28` 分列：那张票的范围是「读取点解引用 null」的四处站点，本票是这些站点改抛的异常**如何被报出来**；28 的「影响」节已注明本票承接
- 本票为复核 28 完成标准时发现，缺陷本身早于该票存在（旧行为是 `NullReferenceException`，同样落兜底分支）
