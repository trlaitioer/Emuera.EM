---
id: nullable-migration-38
status: implemented
type: bug
---

# 零串计数被截断成 int 后静默读短

## 任务

转出自 `nullable-migration-34` 第 3 组的复核：`EraBinaryDataReader1808` 的 12 处零串计数写成 `int cnt = (int)m_ReadInt();`。

- `m_ReadInt`（`Runtime/Utils/EraBinaryDataReader.cs`）按块标记返回 `long`：`Ebdb.Int64` 标记后的计数可以是任意 64 位值
- 先 `(int)` 截断再判上界，会把 `0x1_0000_0001` 读成 `1`：零串被读短、数组值静默出错，且**不落任何错误**——流位置已按 Int64 消费完，后续解析仍然同步，所以这份坏档会被当成一份值不同的正常档读进来
- `nullable-migration-34` 的完成标准是「坏输入不再以非 `FileEE` 异常逃逸」，本条不落任何异常，故不属其列，另立本票

完成标准：越界的零串计数（含 Int64 编码）报 `FileEE`；界内的 Int64 编码计数照常读入；`Emuera.Tests` 有能分辨旧行为的最小夹具。

## 方案

`long cnt = m_ReadInt();`——上界判据沿用 `cnt > 边界 - 游标`，在 long 域比较（`int` 回绕问题一并消失）；校验通过后 `游标 += (int)cnt`，此时 `cnt ≤ 边界 - 游标` 必在 int 域内，窄化安全。

## 影响

- 只有坏存档可观测：`Ebdb.Int64` 编码的越界计数由「静默读短」变为 `FileEE(AbnormalBinaryData)`
- 合法存档不受影响：写入器按最小值选编码，零串计数不超过数组长度（≤ `Array.MaxLength`），本项只加宽判据而不改判据本身

## 实施结果(2026-10-10)

落地即方案：12 处零串计数改为 `long cnt = m_ReadInt();`，上界判据沿用 `cnt > 边界 - 游标`（long 域比较，`int` 回绕问题一并消失），校验通过后 `游标 += (int)cnt`（此时 `cnt ≤ 边界 - 游标`，窄化安全）。约束写在 `m_ReadInt` 的 doc 上而不是重复 12 遍：该方法按块标记返回 `long`，调用方不得在判界之前窄化。

用例（`Emuera.Tests`，2 条）：越界的 Int64 计数（`0x1_0000_0001`）报 `FileEE(AbnormalBinaryData)`；界内的 Int64 计数（3）照常读入——判据是「越界」而不是「编码宽度」。旧行为复测：把 12 处改回 `int cnt = (int)m_ReadInt();` → **越界那条红**、正控制绿（截断后的 `1` 仍在界内，零串被读短却不报错，正是本票要消掉的静默路径）。

验证：`Debug-NAudio` 与 `Debug`(WMP，amd64 MSBuild) 两配置全量重建均 0 错误、CS86xx 0 条（警告仍 97）；`Emuera.Tests` 821 → **823** 全绿；`Emuera` 的后缀断言 798 / 残差 265 不变（本票未增删 `!`）；`Emuera.Tests` 因新用例由 159 涨到 161（该工程按 `nullable-migration-14` 豁免）。
