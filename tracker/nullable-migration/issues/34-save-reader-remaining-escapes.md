# 坏存档仍能以非 FileEE 异常逃逸的其余出口

id: nullable-migration-34
Status: needs-triage
Type: bug

## 任务

`nullable-migration-30`(装载侧 `FileEE` 分级 + 二进制档读取出口收敛)之后,复核该票「坏存档不会以 BCL 异常的形式绕过分级」这一层还剩哪些出口。以下三组都有各自的修法与裁定,故未在 30 内就地修:

1. **读不出来的存档不产生任何归档错误** —— 两处 catch-all 各吞一半:`EraBinaryDataReader.CreateReader` 以 `catch { return null; }` 收尾,把包括 30 新增的 `FileEE` 在内的异常一律吞成 `null`(头部级);`VariableEvaluator.LoadGlobal` 自己的 `catch { return false; }` 则吞掉新包装在数据区抛出的 `FileEE`(故 `LOADGLOBAL` 永不报存档错误)。于是调用方各自分化:`VariableEvaluator.LoadFrom` 回退文本档读取(`LOADDATA` 此前还过 `CheckData`,故可见结果是 `CodeEE(LoadCorruptedData)`)、`VariableEvaluator.LoadGlobal` 返回 `false`、`VariableEvaluator.LoadChara`(由 `LOADCHARA` 直呼,无预检)直接 `return`——后两条用户什么都看不到。需裁定 `CreateReader` 的失败是否该带出原因(如返回结果类型而非 `null`),以及 `LOADCHARA`/`LOADGLOBAL` 的失败该报什么。
2. **值级损坏** —— `EraBinaryDataReader1808.ReadXml` 的 `XmlDocument.LoadXml` 与 `ReadDataTable` 的 `DataTable.ReadXmlSchema`/`ReadXml` 会抛 XML/DataTable 侧的 BCL 异常,`ReadMap` 的 `dict.Add` 遇重复键抛 `ArgumentException`(均在 `Runtime/Utils/EraBinaryDataReader.cs`)。它们不是**读**失败而是**内容**失败,故不在 `SaveDataReader.Guarded` 的漏斗里;是否与解码失败同档需裁定——这些异常也可能指向脚本侧数据本身的问题,一律归入「存档已损坏」会盖掉那层信息。
3. **读取器健壮性** —— `EraBinaryDataReader1808.ReadIntArray`/`ReadIntArray2D`/`ReadIntArray3D`(及对应的 Str 版)按存档内容推进的 `x`/`y`/`z` 没有上界检查:元素写入 `refArray[x] = …` 与 `Zero` 标记的 `refArray[x + i] = …` 都与 `needInit` 无关(`needInit` 只门控清零循环),可越界抛 `IndexOutOfRangeException`;数组分配取存档声明的长度——非 null 路径在 `length0 < saveLength0` 时 `new long[Math.Max(length0, saveLength0)]`、丢弃路径(`refArray == null`)直接按 `saveLength0` `new`——可 `OverflowException`/`OutOfMemoryException`。这组是读侧边界检查缺失,与上面两组的修法不同。

完成标准(待评估后细化):上述任一路径的坏输入不再以非 `FileEE` 异常落到 `Process.handleException` 的兜底分支;`Emuera.Tests` 为各自的最小夹具留用例。

## 方案

待评估。

## 曾考虑的替代方案

1. 就地在 `nullable-migration-30` 内一并修:三组的裁定与修法各不相同(1 要动 `CreateReader` 与两个 `Load*` 的失败契约,2 要先区分「存档坏」与「脚本数据坏」,3 是读侧边界检查),混进 30 会让该票的完成标准与用例失真

## 影响

- 缺陷本身早于 30 存在(旧行为同样是 BCL 异常或静默返回);30 已就地收敛「读取入口的 BCL 异常」这一类(越界、不可解码的值、长度前缀畸形/为负、介质级 I/O 故障),本票只收 30 未覆盖的**内容级**失败与**静默**路径
- 与 30 的存档侧残留并列:那张票承接写入路径(`EraDataWriter` 的 18 处经 `SaveTo` 转 `UnexpectedErrorInSavedata`),本票承接读取侧剩余出口
