---
id: nullable-migration-34
status: in-progress
type: bug
---

# 坏存档仍能以非 FileEE 异常逃逸的其余出口

## 任务

`nullable-migration-30`(装载侧 `FileEE` 分级 + 二进制档读取出口收敛)之后,复核该票「坏存档不会以 BCL 异常的形式绕过分级」这一层还剩哪些出口。以下三组都有各自的修法与裁定,故未在 30 内就地修:

1. **读不出来的存档不产生任何归档错误** —— 两处 catch-all 各吞一半:`EraBinaryDataReader.CreateReader` 以 `catch { return null; }` 收尾,把包括 30 新增的 `FileEE` 在内的异常一律吞成 `null`(头部级);`VariableEvaluator.LoadGlobal` 自己的 `catch { return false; }` 则吞掉新包装在数据区抛出的 `FileEE`(故 `LOADGLOBAL` 永不报存档错误)。于是调用方各自分化:`VariableEvaluator.LoadFrom` 回退文本档读取(`LOADDATA` 此前还过 `CheckData`,故可见结果是 `CodeEE(LoadCorruptedData)`)、`VariableEvaluator.LoadGlobal` 返回 `false`、`VariableEvaluator.LoadChara`(由 `LOADCHARA` 直呼,无预检)直接 `return`——后两条用户什么都看不到。需裁定 `CreateReader` 的失败是否该带出原因(如返回结果类型而非 `null`),以及 `LOADCHARA`/`LOADGLOBAL` 的失败该报什么。
2. **值级损坏** —— `EraBinaryDataReader1808.ReadXml` 的 `XmlDocument.LoadXml` 与 `ReadDataTable` 的 `DataTable.ReadXmlSchema`/`ReadXml` 会抛 XML/DataTable 侧的 BCL 异常,`ReadMap` 的 `dict.Add` 遇重复键抛 `ArgumentException`(均在 `Runtime/Utils/EraBinaryDataReader.cs`)。它们不是**读**失败而是**内容**失败,故不在 `SaveDataReader.Guarded` 的漏斗里;是否与解码失败同档需裁定——这些异常也可能指向脚本侧数据本身的问题,一律归入「存档已损坏」会盖掉那层信息。
3. **读取器健壮性** —— `EraBinaryDataReader1808.ReadIntArray`/`ReadIntArray2D`/`ReadIntArray3D`(及对应的 Str 版)按存档内容推进的 `x`/`y`/`z` 没有上界检查:元素写入 `refArray[x] = …` 与 `Zero` 标记的 `refArray[x + i] = …` 都与 `needInit` 无关(`needInit` 只门控清零循环),可越界抛 `IndexOutOfRangeException`;数组分配取存档声明的长度——非 null 路径在 `length0 < saveLength0` 时 `new long[Math.Max(length0, saveLength0)]`、丢弃路径(`refArray == null`)直接按 `saveLength0` `new`——可 `OverflowException`/`OutOfMemoryException`。这组是读侧边界检查缺失,与上面两组的修法不同。

完成标准(待评估后细化):上述任一路径的坏输入不再以非 `FileEE` 异常落到 `Process.handleException` 的兜底分支;`Emuera.Tests` 为各自的最小夹具留用例。

## 方案

第 1 组已落地:裁定取「保留返回值契约 + 打出可读错误」——失败不升级成异常(`LoadGlobal`/`LoadChara` 的脚本通道是 `RESULT`),改由回传原因、调用点打印;`EraBinaryDataReader.CreateReader` 只对「不是二进制档」返回 null,头认出来之后一律抛 `FileEE`。做法与判据见「实施结果」第 1 组。

第 2、3 组待评估:前者要先区分「存档坏」与「脚本数据坏」,后者是读侧边界检查(索引推进与分配长度上界),裁定不与第 1 组共用,范围见「任务」第 2、3 组。

## 实施结果(2026-10-07)

第 1 组(读不出来的存档不产生任何归档错误)已落地,第 2、3 组保持原判未动。失败语义按本票的裁定取「保留返回值契约 + 打出可读错误」:`LOADGLOBAL`/`LOADCHARA` 不把失败升级成异常(脚本的通道是 `RESULT`),只把原因带到调用点打印。

- `EraBinaryDataReader.CreateReader`:只对「长度不足 8 字节」「头不是本格式」「头本身读不出来(介质故障)」返回 null(调用方据此回退文本档);头认出来之后的一切失败都抛 `FileEE`——越界与不可解码走既有的 `SaveDataReader.Guarded` 文案(`UnexpectedSaveDataEnd` / `CorruptedSaveData (类型名: 细节)`),gzip 解压失败与未知版本另行包成 `CorruptedSaveData (类型名: 细节)` / `CorruptedSaveData (version: N)`。此前一律返回 null,而 null 在调用方只意味着「不是二进制档」,于是损坏被回退文本读取掩盖成静默失败
- `LoadGlobal` 改签名为 `bool LoadGlobal(out string? errorReason)`(`RESULT` 仍由调用点设置),并抽出可测入口 `LoadGlobalFile(string filepath, out string? errorReason)`:原路径取自 `Config.SavDir`,测试指不到别处。`LoadChara` 改返回 `string?`(`RESULT` 仍由方法内设置,既有契约与既有用例保持)
- 两侧的静默 `return`/`return false` 都改成回传原因:文件类型/唯一码/版本不符分别给 `CorruptedSaveData`/`DifferentGame`/`DifferentVersion`,正文读不出来给读取器的文案;`LoadChara` 只吞**非 `CodeEE`** 的 `EmueraException`(读者的诊断;判据与 `SaveChara`/`SaveVariable` 统一,`CodeEE` 按分档策略逃逸——复核确认该路径今天不产生它,见 `nullable-migration-35` 的评审修订 c1),其余异常保留 CLR 堆栈;`LoadGlobal` 维持原有的宽 catch,但把原因带回。`LOADGLOBAL`/`LOADCHARA` 指令用 `exm.Console.PrintError(原因)` 打出;「文件不存在」「名字被拒」仍是既有的安静 0
- 2026-10-09 追加:`LoadGlobal` 的 `getSaveDataPathG()` 原先在任何 try 之外(`Config.SavDir` 未设时 NRE 逃逸 → `Unexpected` 档 + CLR 堆栈),已移进自己的守卫并走 `FailWith`,与 `SaveTo` 同形(同批 `SaveGlobal` 一并处理,见 `nullable-migration-35` 的评审修订 a1)
- 用例:`EraBinaryDataReaderTests` 补 4 条(块表截断、未知版本、gzip 正文损坏、非本格式仍返 null 的正控制),`VariableEvaluatorLoadTests` 补 4 条(chara 两处原因、global 两处原因,含 `RESULT` 归零)。旧行为复测:`CreateReader` 临时换回 `catch { return null; }` → **3 条红**;`LoadChara` 临时换回静默 `return` 并去掉 catch → **2 条红**;`LoadGlobal` 的 catch 临时换回丢弃原因 → **1 条红**
- 验证:`Debug-NAudio` 与 `Debug`(WMP) 均 0 错误、CS86xx 0 条;`Emuera.Tests` 789 → **803** 全绿(本票 8 条,同批 35 的 6 条;评审修订再追加 1 条后为 **804**);后缀断言计数不变(`Emuera` 799 / 残差 266)

仍未做,与第 1 组内另登记的两处:

- 第 2 组(`ReadXml`/`ReadDataTable` 的 XML 侧 BCL 异常、`ReadMap` 重复键的 `ArgumentException`)与第 3 组(`ReadIntArray*` 按存档内容推进的索引与分配上界)保持原判
- `LoadFrom`(`LOADDATA`)与 `LoadGlobalFile` 都在**读取之前**调 `RemoveEMSaveData()`/`RemoveEMGlobalData()`:读取失败时当前存档/全局数据已被删除(二进制档读不出来时 `CreateReader` 会先抛 `FileEE`,该路径的删除尚未发生;文本档读损坏仍如此)。`Remove*` 必须排在加载之前(否则会删掉刚读进来的内容),故改动要么是快照/回滚,要么另立裁定,未在本票处置
- 手动/自动存档路径的 XML 侧与写入侧不在本票(写入侧见 `nullable-migration-35`)

### 复核补记(2026-10-07)

本次修改复核时另判两处,均保留现状,理由在此:

- 头部门槛由 `fs.Length < 16` 改为 `fs.Length < sizeof(ulong)`(8):8~15 字节且前 8 字节为 magic 的文件,由「返回 null → 调用方回退文本档」变为 `FileEE(UnexpectedSaveDataEnd)`。这是本票任务未点名的一处行为变更,保留——头已声明自己是本格式,就按存档的读法报错,与 `Guarded` 的漏斗(见 `nullable-migration-30`)同一条判据
- 头部那段 `catch (Exception) { return null; }` 会连带吞掉 `Guarded` 已包好的 `FileEE(CorruptedSaveData (类型名: 细节))`:`Length >= 8` 下 `ReadUInt64` 只可能因介质故障失败,故该 catch 实际等价于本节记的第三个出口「头本身读不出来」。代价不止「诊断不进玩家可见文本」:返回 null 等于告诉调用方「不是二进制档」,调用方随即回退文本读取,而 `EraDataReader` 对 I/O 故障没有收敛——同一次介质故障仍可能以裸 I/O 异常落 `Unexpected` 档,正是本票要关掉的那类出口。保留的理由是「头认不出来 ⇒ 不是二进制档」这条裁定本身;把介质故障也报出来需要先区分「读不到」与「不是本格式」,未在本票处置

## 曾考虑的替代方案

1. 就地在 `nullable-migration-30` 内一并修:三组的裁定与修法各不相同(1 要动 `CreateReader` 与两个 `Load*` 的失败契约,2 要先区分「存档坏」与「脚本数据坏」,3 是读侧边界检查),混进 30 会让该票的完成标准与用例失真

## 影响

- 缺陷本身早于 30 存在(旧行为同样是 BCL 异常或静默返回);30 已就地收敛「读取入口的 BCL 异常」这一类(越界、不可解码的值、长度前缀畸形/为负、介质级 I/O 故障),本票只收 30 未覆盖的**内容级**失败与**静默**路径
- 与 30 的存档侧残留并列:写入路径已由 `nullable-migration-35` 承接并落地(2026-10-07,含「写失败毁旧档」与失败原因的分级),本票承接读取侧剩余出口
- 2026-10-07 行为变更(对外可见,实施结果第 1 组的直接后果):`LOADGLOBAL`/`LOADCHARA` 在「文件在、读不出来」时开始打一行(此前 `RESULT`/`false` 归零即止,玩家与脚本都分不清「没有这个存档」与「这个存档坏了」);`LOADCHARA` 对「在、但不是 chara 存档(非二进制档或文件类型不符)」的文件同样报 `CorruptedSaveData`,不再是安静 0;`LOADDATA` 读损坏的二进制档时不再落进文本回退(旧结局按损坏形状而异:静默、或裸 BCL 异常连 CLR 堆栈),改为 `FileEE`——存档档、只打消息
