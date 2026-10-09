# Triage 标签

## 状态标签全集

可用的全部状态标签如下

| 标签 | issue 含义 | spec 含义 |
| ---- | ---- | ---- |
| `draft` | 尚未确定方案（需要更多信息 / 需要进一步评估） | 尚未确定方案（需要更多信息 / 需要进一步评估） |
| `ready` | 方案已初步确定，待实施 | 方案已初步确定，待拆 issue |
| `in-progress` | 方案实施中 | 实施中 |
| `need-check` | 已实施，待验收 | - |
| `implemented` | 已实施并验收通过 | issue 已全部到达终态 |
| `wontfix` | 不处理 | 不处理 |
| `superseded` | 被取代 | - |

标签写在票文件开头 front matter 的 `status` 键（见 `issue-tracker.md`）
本表与 `scripts/check_tracker_yaml.py` 的词表须一致。

## 状态流转

- 终态为 `implemented` / `wontfix` / `superseded`
- 允许在单次 commit 中推进多次

### issue

```mermaid
flowchart LR
    draft([draft]) -->|方案初步确定| ready([ready])
    ready -->|方案待定| draft
    ready -->|开始实施方案| in_progress([in-progress])
    in_progress -->|回退| ready
    in_progress -->|方案已实现| need_check([need-check])
    need_check -->|验收不通过，修订方案| in_progress
    need_check -->|验收通过| implemented([implemented])

    draft -.-> wontfix([wontfix])
    ready -.-> wontfix
    in_progress -.->wontfix

    draft -.-> superseded([superseded])
    ready -.-> superseded
    in_progress -.->superseded

    classDef terminal stroke:#2e7d32
    class implemented,wontfix,superseded terminal
```

### spec

```mermaid
flowchart LR
    draft([draft]) -->|方案定案| ready([ready])
    ready -->|方案待定| draft
    ready -->|开始| in_progress([in-progress])
    in_progress -->|issue 全部完成| implemented([implemented])

    draft -.-> wontfix([wontfix])
    ready -.-> wontfix
    in_progress -.-> wontfix

    classDef terminal stroke:#2e7d32
    class implemented,wontfix terminal
```
