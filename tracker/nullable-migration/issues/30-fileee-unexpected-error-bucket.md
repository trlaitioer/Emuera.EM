# 存档坏输入抛出的 FileEE 落到「予期せぬエラー」+ 堆栈

id: nullable-migration-30
Status: ready-for-human
Type: bug

## 任务

`nullable-migration-28` 的站点 3 按方案把扩展数组块内的 EOF 从 `NullReferenceException` 改成 `FileEE`。复核该票完成标准（「坏输入分别得到回退值或**可读错误**」）时发现：新抛的 `FileEE` 在装载路径无人接，用户看到的仍是「予期せぬエラー」+ .NET 堆栈，只是其中多了 `FileEE` 的类型名与消息。

链路（均已核对）：

- `FileEE`（`Emuera/Runtime/Utils/EmueraException.cs:82`）继承 `EmueraException`，**既不是 `CodeEE` 也不是 `ExeEE`**
- `Process.handleException`（`Emuera/Runtime/Script/Process.cs:648` / `:688` / `:693`）只对 `CodeEE`/`ExeEE` 有专属分支，其余一律走 `else`：打 `HasUnexpectedError` 并逐行打印 `StackTrace`；它只被 `Process.cs:258` / `:413` 的 `catch (Exception)` 调用
- 两个 `LoadFrom` 调用点都没有 try/catch：`LOADDATA`（`Emuera/Runtime/Script/Process.ScriptProc.cs:1159`，其前置 `CheckData` 失败会转成 `CodeEE(LoadCorruptedData)`，是一条可读路径）与 `loadGameWaitInput`（`Emuera/Runtime/Script/Process.SystemProc.cs:1314`，前置的槽位可用性检查同样来自 `CheckData`）
- 前置检查读不到数据区：`VariableEvaluator.CheckDataByFilename`（`VariableEvaluator.cs:2695`）只读文件类型、unique code、版本与存档名，故**头部完好、数据区被截断**的记录能过检查，随后在 `LoadFrom` 里抛 `FileEE`

即：`CheckData` 能覆盖的损坏给出可读的「不正なデータをロードしようとしました」，它覆盖不到的损坏（数据区截断）给出 CLR 堆栈。范围不止 28 新增的那一处——`EraDataStream.cs`（72 处）、`EraBinaryDataReader.cs`（9 处）、`CharacterData.cs`/`VariableData.cs`（7 处）中既有 88 处同型 `FileEE` 走的是同一个出口（写入侧例外：这 72 处里的 18 处属 `EraDataWriter`，经 `SaveTo` 的 `catch (Exception)` 吞掉，见「实施结果」的存档侧一段）。

完成标准（待评估后细化）：

- 存档/装载类错误有明确的报错分级：或让 `FileEE` 归入 `ExeEE` 分支（`HasEmueraError` + 消息、不打堆栈），或为 `FileEE` 单开分支，或在两个 `LoadFrom` 调用点转成 `CodeEE(LoadCorruptedData)`
- 兜底分支保留：编程错误（索引越界、Pfc 一类）仍要连堆栈一起响亮报出
- `Emuera.Tests` 覆盖「数据区被截断的存档得到明确错误文本」

## 方案

待评估。三条候选的差别在错误文本与是否带位置：`CodeEE` 会带文件名行号（`LOADDATA` 现场）但语义是脚本错误；`ExeEE` 是 Emuera 自身错误、无堆栈；`FileEE` 单开分支可保留「存档损坏」语义并统一文案。

## 实施结果(2026-10-06)

取三条候选中的第一条:**为 `FileEE` 单开一档**,并用新增专用文案,而不是把它并进 `ExeEE`(`FileEE` 与 `Emuera` 自身错误是两回事,并档会丢掉「存档坏了」这一层语义)或只在两个 `LoadFrom` 调用点转 `CodeEE`(只覆盖两条路径、且丢掉具体损坏原因,而全库 88 处 `FileEE` 里装载侧的 70 处是同一个出口——写入侧 `EraDataWriter` 的 18 处另经 `SaveTo` 的 `catch (Exception)`,见文末存档侧一段)。做法:

- 新增 `Emuera/Runtime/Script/ErrorReport.cs`:`ErrorReportKind`(Code / Emuera / SaveData / Unexpected)+ `ErrorReport.Classify(Exception)`,以及抬头文案与堆栈策略 `Header`/`SystemProcHeader`(脚本行 / 函数终止两套文案)与 `PrintsStackTrace`/`Message`。这是**唯一**的分级判据——原先的档位隐含在 `Process.handleException` 与 `handleExceptionInSystemProc` 各自的 `if (exc is CodeEE) … else if (exc is ExeEE) … else` 里,两处各写一份,正是漏掉 `FileEE` 的根因。两处 handler 的「其余各档」因此收敛成同一段(先取抬头、再取消息行、按策略决定是否打堆栈),不再是两份四路级联。
- `SaveData` 档只打可读消息、**不打堆栈**;`Unexpected` 档保持原样(连 CLR 堆栈一起响亮报出),索引越界、Pfc 一类的编程错误仍走它。
- 新增 Lang 键 `Error.HasSaveDataError`、`Error.FuncEndSaveDataError`,三处同步:`Lang.cs`、`Emuera/Properties/lang/emuera-eng.xml`、`emuera-zhs.xml`(文案:セーブデータのエラー / Save data error / 存档数据错误)。

验证:

- 新增 `Emuera.Tests/Runtime/Script/ErrorReportTests.cs` 15 条:`Classify` 四档(含 `CodeEE` 的两个特化、五种编程错误)、`Header`/`SystemProcHeader` 四档不得互相借用文案、`PrintsStackTrace` 只对 `Unexpected` 为真、`Message` 只对 `Unexpected` 前置 CLR 类型名。分级、文案与堆栈策略都是纯函数,无需 `EmueraConsole`。
- `Emuera.Tests/Runtime/Utils/EraDataStreamTests.cs` 的截断存档用例补两条断言:抛出的 `FileEE` 文案是 `UnexpectedSaveDataEnd`,且分级为 `SaveData`——把「真实坏输入抛出的异常」与「它被报成哪一档」接上。
- 全量构建 0 错误、CS86xx 0 条;`Emuera.Tests` 784 全绿。

残留(须人工验收):两处 handler 里**剩下的只有打印调用本身**——`handleException` 的入口需要 `EmueraConsole`,其构造器要 `MainWindow`,测试工程构造不出来(与 `arg-contract-05` 记的「`DoInstruction` 需要 `EmueraConsole`」同一边界)。分级、文案与堆栈策略已由用例钉住,实机确认随 issues/19 的人工验收一起做(真实游戏目录 + 坏输入存档)。

存档侧(本票未覆盖):写入路径抛出的 `FileEE` 到不了 `ErrorReport`——`Emuera/Runtime/Script/Statements/Variable/VariableEvaluator.cs` 的 `SaveTo` 以 `catch (Exception) { return false; }` 收尾,失败由 `Emuera/Runtime/Script/Process.ScriptProc.cs` 的 `SAVEDATA` 分支打成 `UnexpectedErrorInSavedata`(「…予期しないエラーが発生しました」),这 18 处(`EraDataWriter`)因此仍是旧观感。故完成标准「存档/装载类错误有明确的报错分级」只落地装载半边;写入侧须另立票承接(判据与用例同装载侧:一个写不进去的目标或写入期异常,钉住 `SaveTo` 之后那一步的文案分级)。

## 影响

- 触及 `Process.handleException` 与 `Process.handleExceptionInSystemProc` 两处 handler（两个 `LoadFrom` 调用点一行未动）；正常读档路径不变
- 与 `nullable-migration-28` 分列：那张票的范围是「读取点解引用 null」的四处站点，本票是这些站点改抛的异常**如何被报出来**；28 的「影响」节已注明本票承接
- 本票为复核 28 完成标准时发现，缺陷本身早于该票存在（旧行为是 `NullReferenceException`，同样落兜底分支）
