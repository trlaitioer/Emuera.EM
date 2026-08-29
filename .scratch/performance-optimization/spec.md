# 迁移方案:性能优化(已评估、未实施)

状态:评估完成,未实施。

## 目标

六项已评估的性能优化,每项一个 ticket(见 issues/)。全部未实施。

## 条目

1. 字符串拼接(issues/01):`PlusStrStr` 链展平 + `AExpression.AppendStrValue` 写入器通道
2. 动态调用(issues/02):`CALLFORM` 函数名槽值缓存
3. 解析期 Span(issues/03):评估定案,原方案无收益关闭;标识符驻留评估延至 issues/04 实施时
4. 解析并行(issues/04):`loadErb` / `ParseScript` 并行化
5. CSV 查询(issues/05):模板查询线性扫描改字典 O(1)
6. CS8981(issues/06):小写 alias 暂不重命名,决策记录

## 验收

每项按各自 ticket 内的衡量方式验证(基准测试或代码审查);不改变现有功能行为。
