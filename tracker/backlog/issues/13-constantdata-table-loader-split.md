---
id: backlog-13
status: draft
---

# ConstantData 的构造器 interface 吞下整个 csv/ 目录

## 任务

发现于 2026-09-27 的只读架构审查（装载/运行时数据）。

`Emuera/Runtime/Script/Data/ConstantData.cs`（2290 行，本领域最大单文件、churn 21 次）对外约 45 个公开方法，对内既是 csv 解析器又是 20 多张查找表的所有者——interface 与 implementation 同宽：

- 构造器（`:139-162`）：`ConstantData(string csvDir, EmueraConsole console, bool disp)` → `setDefaultArrayLength()` → `LoadData(csvDir, console, disp)`。**interface 里最重的输入（`csvDir`）恰是调用方最不该关心的东西**
- 装载段：`:164-273`、`:274-608`、`:760-889`、`:898-955`、`:1498-1802`、`:2050-2233`
- 查询段：`:127-130`、`:957-1006`、`:1012-1105`、`:1437-1466`、`:1468-1496`、`:2234-2290`
- 字段：`:42-140`（`MaxDataList`、10 个长度数组、12 个 `*SaveMaps`/`*StaticMaps`、`names`/`aliases`/`erdNameToIntDics`、`nameToIntDics`/`relationDic`、`CharacterTmplList`/`charaTmplByNo`/`charaTmplByCsvNo`）
- 反向写入：`ErhLoader.cs:367, 380, 394` 的 `GlobalStatic.ConstantData.UserDefineLoadData(...)`——表的所有者与表的填充者不是同一个 module
- 分区按「历史改动来源」而非职责：`#region EE_ERD` / `#region EM_私家版_セーブ拡張` / `#region EE_CSV拡張機能`

完成标准：

- 表的所有权与填充分开：一个 module 拥有名称/别名/关系/角色模板表并只提供查询；另一个 adapter 负责「遍历 `csv/` → 逐文件解析 → 把行喂给表」
- 装载选项（是否 ERD、是否打报告、`EmueraConsole` 或更小的报告 interface）只出现在 adapter 上，不出现在表的 interface 上
- `ErhLoader` 的反向 `UserDefineLoadData` 改为「adapter 把 ERD 数据交给表」，方向统一
- 用例：把已知的 csv 行直接喂给表，断言「关键词 → 整型」的升/降维查找、别名优先级、ERD 重复定义、越界与未知键的失败模式、角色模板合并（`NO`/`CFLAG` 组合）——无需临时目录、无需 `Preload`、无需 `null!` console
- 数据最终可见性不变（`docs/architecture.md` 的 Data 段落描述的表结构保持）

## 方案

待评估。**不要一次拆 2290 行**：先只把「表 + 查询」这一面独立出来（`GetCsvNameList` / `isDefined` / `isUserDefined` / `*KeywordTo*` / `IsDefinedCsvVar` / `GetCharacterTemplate*` 这批读方法所依赖的字段），让 `ConstantData` 变成「adapter + 内部持有表」。

另需单独确认：字段 `:84-93` 的长度数组与 `ArrayStrLength`/`ArrayLength` 看起来是**变量系统容量**（另一件事），被放在这里只是历史上先有 `ConstantData`——是否该跟变量系统走，不在本票范围内定。

## 影响

- **测试面**：`ConstantDataTests.cs` 只有 4 例，全部集中在角色模板查询，且必须写真实临时文件 + `Preload.Load` + 传 `null!` console（`:18-34`）——为一个纯查表断言付了「文件系统 + 预读缓存 + UI 依赖」三重装配。`isDefined`/`isUserDefined`/`TryKeywordToInteger`/`TryIntegerToKeyword`/`GetKeywordDictionary`/`IsDefinedCsvVar`/ERD 多维别名这一大片**零直接覆盖**
- **与既有票的关系**：
  - `GetKeywordDictionary` 的可空契约已由 `tracker/nullable-migration/issues/10-variable-data-chain.md:70` 与 `nullable-migration-29` 拆分定案（`TryGetKeywordDictionary` + 抛 `CodeEE` 的非空入口），本票不动该契约
  - `performance-optimization-05` 已就两个角色模板查询做了等价性重构并新增 4 例。本票与该票同域，但目标是构造器形状与表所有权，不是查询复杂度
  - `GetCharacterTemplate*` 的返回可空契约见 `nullable-migration-10`、消费点收尾见 `nullable-migration-03`
- **删除测试判决**：删掉 `ConstantData`，csv → 名称/别名/角色模板/数组长度这整套复杂度会回到每一个查询点（`ArgumentBuilder.cs:2485`、`Instraction.Child.cs:2082`、`CharacterData.cs:651`、`VariableData` 默认值设置、`ErhLoader`）→ 在挣饭钱且不可替代。真正的 pass-through 是它的**构造器**形状
- 改动量估计：大。建议排在 issues/07、issues/08 之后
