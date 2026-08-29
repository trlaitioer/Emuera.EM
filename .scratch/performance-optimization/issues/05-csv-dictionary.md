# 性能:CSV 查询字典化

Status: ready-for-agent
Type: task

## 背景

角色模板 CSV 查询为线性扫描(已评估、未实施)。

评估修正:`ConstantData` 三个查询方法中,仅 `GetCharacterTemplate`(按 No)与 `GetCharacterTemplateFromCsvNo`(按 csvNo)是线性扫描,且均为冷路径(调用方分别为 `ADDCHARA`、`ADDCOPYCHARA` 与启动时 gamebase 默认角色);可能被脚本高频调用的 `GetCharacterTemplate_UseSp` 已是二分 O(log n),但实现靠 `List.BinarySearch(null, comparer)` 技巧,隐式依赖"列表已按 No 排序"且无保护,comparer 存在 long 差值强转 int 的溢出隐患,`sp` 参数被完全忽略。模板总量为 chara*.csv 条目数(典型几十到几百),字典化无可测量的性能收益。本 ticket 定位为等价性重构(消除上述坏味道),O(1) 为顺带收益。

## 方案

1. `ConstantData` 新增两个 `Dictionary<long, CharacterTemplate>` 字段,分别以 `No`、`csvNo` 为键。
2. 在 `loadCharacterData` 末尾(所有 chara*.csv 加载完成后)一次性填充,`TryAdd` 先到先得。字典须包含全部模板(含 SP 角色)。重复项不要求复刻旧行为:重复 No 时原查询取哪个模板由不稳定排序的内部实现决定,本属不可预测,正常游戏不应依赖;重复 csvNo 连告警都没有。新规则统一为首个定义胜出。
   - 现状补充:重复 No 加载时仅告警不去重(`DuplicateCharaDefine1/2`);CompatiSPChara 开启时普通与 SP 同号不告警,但查询层 `sp` 参数本就被忽略(调用处注释已承认),该场景现状同样是"取到哪个算哪个",字典化不改变它。
3. 三个 getter 统一改为 `TryGetValue`;`GetCharacterTemplate_UseSp` 保留签名,`sp` 继续忽略并注释说明。
4. `CharacterTmplList` 与每文件加载后的 Sort 保留(relationDic 构建、Callname 兜底、SetSpFlag 仍依赖它)。
5. 调用方零改动。

## 验收

构建通过 + `ADDCHARA`/`ADDCOPYCHARA`/`CSVNAME`/`EXISTCSV` 冒烟回归;不做基准测试(目标为行为等价)。

## Comments

- 2026-08-29: 评估完成。修正前提(线性扫描仅两个冷路径方法,`GetCharacterTemplate_UseSp` 已是二分);重新定位为等价性重构;重复项规则定为首个定义胜出,不复刻不可预测的旧行为。Status: needs-triage → ready-for-agent。
