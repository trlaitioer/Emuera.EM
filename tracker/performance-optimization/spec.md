# 迁移方案:性能优化

Date: 2026-08-22
Status: in-progress

## 问题/背景

七项已评估的性能优化,每项一个 ticket(见 issues/)。

## 整体方案

1. 字符串拼接(issues/01):`PlusStrStr` 链展平 + `AExpression.AppendStrValue` 写入器通道
2. 动态调用(issues/02):`CALLFORM` 函数名槽值缓存,方案按大型 ERB 语料普查修订为 long 键/内容键双模式 + 负缓存
3. 解析期 Span(issues/03):评估结论为原方案无可测收益;标识符驻留评估延至 issues/04 实施时
4. 解析并行(issues/04):`loadErb` / `ParseScript` 并行化(函数级三阶段管线方案在票内)
5. CSV 查询(issues/05):模板查询线性扫描改字典 O(1)
6. CS8981(issues/06):小写 alias 暂不重命名,决策记录
7. 重复角色告警(issues/07):每组一条收敛

每项按各自 ticket 内的衡量方式验证(基准测试或代码审查)。

## 影响

优化不改变现有功能行为;issues/03 的标识符驻留评估并入 issues/04 实施时处理。
