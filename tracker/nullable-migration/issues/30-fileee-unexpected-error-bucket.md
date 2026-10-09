# 存档坏输入抛出的 FileEE 落到「予期せぬエラー」+ 堆栈

id: nullable-migration-30
Status: implemented
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

残留:两处 handler 里**剩下的只有打印调用本身**——`handleException` 的入口需要 `EmueraConsole`,其构造器要 `MainWindow`,测试工程构造不出来(与 `arg-contract-05` 记的「`DoInstruction` 需要 `EmueraConsole`」同一边界)。分级、文案与堆栈策略已由用例钉住;二进制档与压缩档的文案另有 `EraBinaryDataReaderTests` 覆盖到读取器一级(以 `SystemSaveInBinary=true` 存出一份再截断数据区的实机样本不另列)。

存档侧(本票未覆盖):写入路径抛出的 `FileEE` 当时到不了 `ErrorReport`——`Emuera/Runtime/Script/Statements/Variable/VariableEvaluator.cs` 的 `SaveTo` 以 `catch (Exception) { return false; }` 收尾,失败由 `Emuera/Runtime/Script/Process.ScriptProc.cs` 的 `SAVEDATA` 分支打成 `UnexpectedErrorInSavedata`(「…予期しないエラーが発生しました」),这 18 处(`EraDataWriter`)因此仍是旧观感。故完成标准「存档/装载类错误有明确的报错分级」当时只落地装载半边;写入侧须另立票承接(判据与用例同装载侧:一个写不进去的目标或写入期异常,钉住 `SaveTo` 之后那一步的文案分级)。**已由 `nullable-migration-35` 承接并落地(2026-10-07)**:三条写入路径的抬头合并为 `Error.FailedToSaveData` + 原因行(`UnexpectedErrorInSavedata`/`AutoSaveError1`/`UnexpectedSaveError` 三个键随之删除),`SaveChara`/`SaveVariable` 的失败归入存档档,四条写入路径改走「先写临时文件再替换」。

### 二进制格式的截断(2026-10-07 补充)

上面的分级与用例只覆盖**文本档**:`EraDataStream` 的 `EraDataReader` 在每个 `ReadLine()` 为 null 的 EOF 检查处抛 `FileEE(UnexpectedSaveDataEnd)`,二进制档不是——`EraBinaryDataReader` 只用裸 `BinaryReader` 的 `ReadByte/ReadUInt32/ReadUInt64/ReadInt16/ReadInt32/ReadInt64/ReadString/PeekChar` 取值,读越界抛 BCL 的 `EndOfStreamException`(继承 `IOException`,与 `EmueraException` 无亲缘),按 `Classify` 的 `_` 分支落 `Unexpected`:玩家拿到 `System.IO.EndOfStreamException: Unable to read beyond the end of the stream.` + CLR 堆栈,本票要消掉的观感原样复现。原 `SaveTextReader` 只包 `DecoderFallbackException`,而 `BinaryReader` 的整型读不是 virtual、一个 override 拦不住,故「每次读存档流都从同一个出口过」当时并不成立。触发条件:`Config.SystemSaveInBinary` 默认 `false`,但 `CreateReader` 按文件头自动判别,存档目录里存在一份二进制存档即会走到;压缩档(`ZipHeader`)当时例外——gzip 解压失败被 `CreateReader` 的 catch-all 吞成 `null`、回退文本读取,预检即给出可读结果(该出口已由 `nullable-migration-34` 收敛为 `FileEE(CorruptedSaveData (类型名: 细节))`)。

修法:`SaveTextReader : BinaryReader` 换成持有 `BinaryReader` 的 `SaveDataReader`(组合而非继承),全部读取入口经同一个 `Guarded`:越界转 `FileEE(予期しないセーブデータの終端です)`(与文本档同文案);**值级**损坏——解码失败(`DecoderFallbackException`)、长度前缀畸形或负长度(`FormatException`,以及 `BinaryReader.ReadString` 对负长度抛的 `IOException`)——转 `FileEE(セーブデータが壊れています (类型名: 细节))`,类型名留在文案里是因为该档不打堆栈,否则介质级 I/O 故障与 framing 损坏无从区分。读取调用点一行未动;`CreateReader` 的 3 个构造点与基类 ctor 形参只换了类型名,`reader` 字段与基类 ctor 的可见性因可访问性规则(ctor 为 `CS0051`,字段同规则是 `CS0052`)一并收回 `private`(唯一子类嵌套在类内)。包装只暴露本文件用到的 10 个成员(8 个读 + `BaseStream` + `Close`;无人调用的 `ReadChar` 随之删除)。

用例:`EraBinaryDataReaderTests` 补 5 条——扩展数组块截断、文本段正文截断、头部定长字段截断(这三条钉 `FileEE` + `UnexpectedSaveDataEnd` 文案 + `Classify == SaveData`)、长度前缀畸形与长度前缀为负(这两条钉 `FileEE` + `CorruptedSaveData` 前缀 + `SaveData`)。

验证:`Emuera` 构建 0 错误、97 条既有分析器警告(无 CS86xx);`Emuera.Tests` 784 → **789** 全绿。

仍未覆盖(`nullable-migration-34` 承接):`CreateReader` 的 catch-all 与 `LoadGlobal` 自己的 `catch { return false; }` 把存档错误吞成 `null`/`false`,故读不出来的存档不产生任何存档错误(`LOADDATA` 此前还过预检,`LOADCHARA` 无预检则静默返回);`ReadXml`/`ReadDataTable` 的 XML 侧 BCL 异常、`ReadMap` 重复键的 `ArgumentException`、`ReadIntArray*` 按存档内容推进的索引与分配长度造成的越界/OOM 属内容级与健壮性缺陷,各有各的裁定。

## 曾考虑的替代方案

1. 二进制档的修法改为在 `ErrorReport.Classify` 里把 `EndOfStreamException`(或 `IOException` 整个分支)并入 `SaveData` 档:一行改动,但只改分级不改消息——用户看到的仍是 BCL 的英文文本,离完成标准「可读错误」还差一步;且「IO 异常即存档错误」会把非存档来源的 IO 异常写进唯一判据。故取在读取器出口收敛(见「二进制格式的截断」)
2. 只在两个 `LoadFrom` 调用点转 `CodeEE(LoadCorruptedData)`:只覆盖两条路径且丢掉具体损坏原因,放弃(同上,见「实施结果」)

## 影响

- 触及 `Process.handleException` 与 `Process.handleExceptionInSystemProc` 两处 handler（两个 `LoadFrom` 调用点一行未动）；正常读档路径不变
- 2026-10-07 补充：触及 `Runtime/Utils/EraBinaryDataReader.cs` 的读取出口（包装类型、字段与基类 ctor 的可见性）；各 `LoadFrom` 调用点仍未动
- 与 `nullable-migration-28` 分列：那张票的范围是「读取点解引用 null」的四处站点，本票是这些站点改抛的异常**如何被报出来**；28 的「影响」节已注明本票承接
- 本票为复核 28 完成标准时发现，缺陷本身早于该票存在（旧行为是 `NullReferenceException`，同样落兜底分支）
