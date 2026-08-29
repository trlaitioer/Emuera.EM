# 告警:重复角色 No 收敛为每组一条

Status: ready-for-agent
Type: task

## 背景

角色模板重复定义告警为每处多余定义各告警一次:同 No 出现 N 次输出 N-1 条(05 字典化重构后的 GroupBy 实现:CompatiSPChara 开启按 `(No, IsSpchara)` 分组、关闭按 `No` 分组)。告警文案仅两种——`DuplicateCharaDefine2`(番号{0}のキャラが複数回定義されています)与 `DuplicateCharaDefine1`(同句附加"SP 角色需开启兼容选项"提示),同一 No 的多条告警在绝大多数情况下逐字相同,无增量信息。`DisplayWarningLevel` 默认 1,这些告警默认可见。

评估来源:05 实施过程中的讨论。同 loader 其余逐次告警惯例(文件内第二个 `NO` 的 `CharaNoDefinedTwice`、`loadDataTo` 的 `VarKeyAreadyDefined`)不在本议题范围,保持现状。

本票是有意的行为变更,与 upstream 行为分叉一处(告警条数);实施基于 05 合入后的 develop。

## 方案

1. 每个重复组只告警一条:
   - CompatiSPChara 开启:按 `(No, IsSpchara)` 分组(维持"同号跨普通/SP 不互告警"),组内重复 → `DuplicateCharaDefine2`(组内必同属性)。
   - 关闭:按 `No` 分组,组内重复 → SP 属性混合则 `DuplicateCharaDefine1`,否则 `DuplicateCharaDefine2`。
2. 与现状的差异:
   - 告警条数 N-1 → 1。
   - 关闭模式混合组(如 [普通, SP, 普通])现状会先后输出 Define1 与 Define2 两种提示,新规则只输出 Define1;"两种重复并存"细节不再可见。
3. 实现为 05 告警块的就地改写(`group.Skip(1)` 循环 → `Count() > 1` 判断),不涉及其他方法。

## 验收

构建通过 + 代码 review。告警条数差异无法经现有单测断言(`ParserMediator.Warn` 在 console 为 null 时不落 warningList),仅能以重复 No 夹具人工观察;不做脚本冒烟。

## Comments
