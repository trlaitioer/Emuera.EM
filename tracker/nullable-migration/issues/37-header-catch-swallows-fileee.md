---
id: nullable-migration-37
status: draft
type: bug
---

# 头部 catch 把介质故障当成「不是本格式」

## 任务

转出自 `nullable-migration-34`（「复核补记」登记，该票未处置）：`EraBinaryDataReader.CreateReader` 头部那段 `catch (Exception) { return null; }` 会连带吞掉 `SaveDataReader.Guarded` 已包好的 `FileEE(CorruptedSaveData (类型名: 细节))`。

- `Length >= 8` 下 `ReadUInt64` 只可能因介质故障失败，故该 catch 实际等价于「头本身读不出来」
- 代价不止「诊断不进玩家可见文本」：返回 `null` 等于告诉调用方「不是二进制档」，调用方随即回退文本读取，而 `EraDataReader` 对 I/O 故障没有收敛——同一次介质故障仍可能以裸 I/O 异常落 `Unexpected` 档，正是 `nullable-migration-34` 要关掉的那类出口
- `nullable-migration-34` 保留现状的理由是「头认不出来 ⇒ 不是二进制档」这条裁定本身；要改就得先区分「读不到」与「不是本格式」

完成标准（待评估后细化）：介质故障不再被当成「不是本格式」而触发文本回退；`Emuera.Tests` 有能分辨两种情形的最小夹具。

## 方案

待定：让头部读取只在「头认出来但不是本格式」时返 `null`，其余失败抛 `FileEE`（判据与 `nullable-migration-34` 第 1 组的 `CreateReader` 收口同源）；需要先定「读不到」的判据与文案。

## 影响

- 介质故障的诊断与分档（不再以裸 I/O 异常落 `Unexpected` 档）
- `LOADDATA` 的文本回退路径不再被 I/O 故障触发
