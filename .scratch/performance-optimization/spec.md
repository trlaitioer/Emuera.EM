# 迁移方案:性能优化

状态:滚动推进中(2026-08-30)。02/05 已完成,03/06 wontfix 关闭,07 ready-for-human,01/04 已评估未实施。

## 目标

七项已评估的性能优化,每项一个 ticket(见 issues/)。

## 条目

1. 字符串拼接(issues/01):`PlusStrStr` 链展平 + `AExpression.AppendStrValue` 写入器通道 — 已评估未实施
2. 动态调用(issues/02):`CALLFORM` 函数名槽值缓存 — 已完成(2026-08-30),方案按大型 ERB 语料普查修订为 long 键/内容键双模式 + 负缓存
3. 解析期 Span(issues/03):评估定案,原方案无收益关闭;标识符驻留评估延至 issues/04 实施时 — wontfix
4. 解析并行(issues/04):`loadErb` / `ParseScript` 并行化 — 已评估未实施(函数级三阶段管线方案在票内)
5. CSV 查询(issues/05):模板查询线性扫描改字典 O(1) — 已完成(2026-08-29)
6. CS8981(issues/06):小写 alias 暂不重命名,决策记录 — wontfix
7. 重复角色告警(issues/07):每组一条收敛 — ready-for-human

## 验收

每项按各自 ticket 内的衡量方式验证(基准测试或代码审查);不改变现有功能行为。
