# Triage 标签

这些技能用五个规范化的分流（triage）角色来沟通。本文件将这些角色映射为本仓库 issue tracker 中实际使用的标签字符串，并定义 tracker 原生的状态标签。

## 状态标签全集

`Status:` 行可用的全部标签如下

| 标签 | 作用域 | 含义 |
| ---- | ------ | ---- |
| `needs-triage` | issue | 维护者需要评估 |
| `needs-info` | issue | 等待报告者补充信息 |
| `ready-for-agent` | issue | 已完整描述，可供 agent 处理 |
| `ready-for-human` | issue | 需要人类处理（实现，或验收已实现内容） |
| `in-progress` | issue、spec | 进行中（已开工/推进中） |
| `implemented` | issue、spec | 终态：实现且验收通过 |
| `wontfix` | issue、spec | 终态：不处理/放弃 |
| `superseded` | issue | 终态：被其他票承接，「影响」节注明承接方（文件不移动） |
| `draft` | spec | 提出/评估中，尚未拆票开工 |
