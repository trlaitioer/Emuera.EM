---
id: nullable-migration-39
status: draft
type: bug
---

# 读档失败后普通变量与角色已被复位

## 任务

`nullable-migration-36` 只把 SAVEDATA 扩展数据（MAP/XML/DT）从「读失败即被删」里救出来。读失败对普通变量与角色仍未保全：二进制档在读完头部（类型/唯一码/版本）之后、读正文之前就执行 `SetDefaultValue`/`SetDefaultLocalValue` 并 `CharacterList.Clear()`（`VariableEvaluator.LoadFromStreamBinary`），文本档同样（`VariableEvaluator.LoadFromStream`）；随后逐块读取时才可能失败，于是当前进度已被复位或部分覆盖。

完成标准（待评估后细化）：`LOADDATA`/`LOADGLOBAL` 读取失败后，进程内变量状态与读取前一致，或明确裁定「载入失败即视为当前进度作废」并把该裁定写进文档；`Emuera.Tests` 有能分辨旧行为的最小夹具。

## 方案

待定。状态清点：

- 系统变量：`VariableData` 的标量与各维数组共 8 组
- 用户自定义静态/全局变量：数组存在各自的 `UserDefinedVariableToken` 私有字段里，不在上述系统数组内
- 角色：`CharacterList` 是对象列表，`CharacterData` 自己持有角色变量数组
- 读档还会改写 `LastLoadVersion`/`LastLoadText`/`LastLoadNo`

候选：

1. 内存存档快照 + 失败回读：读取前用现有写入器把当前状态写进 `MemoryStream`（需给 `EraBinaryDataWriter` 加 `Stream` 入口），失败时按同一格式回读恢复。覆盖已保存变量、角色与 EM 数据；但快照只含已保存状态，未保存变量与 `LastLoad*` 仍会被复位，恢复是重放而不是精确回滚
2. 显式深拷贝全状态快照：把上述四类状态全部纳入快照，精确恢复。工作量大且容易漏字段（用户自定义变量分散在 token 里），需要为每个变量族写快照/恢复
3. 两阶段读取：先完整解析/校验到暂存结构，再一次性应用；改动面覆盖 `LoadVariableBinary` 与 `CharacterData.LoadFromStream*`，远超一张缺陷票的比例

## 影响

- 与 `nullable-migration-36` 同源：36 的「坏档不毁当前进度」只兑现了 MAP/XML/DT 一层，普通变量与角色仍可能被毁
- 若选方案 1，每次 `LOADDATA` 会多一次全量序列化（当前状态大小），需要评估读档延迟
