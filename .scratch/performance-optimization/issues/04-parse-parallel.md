# 性能:解析并行化

Status: needs-triage
Type: task

## 背景

ERB 加载解析目前串行(已评估、未实施)。

## 方案

- `Preload` 已并行读文件;`loadErb` / `ParseScript` 仍串行
- 并行前需先处理 `Depth` 调用图发现顺序,以及 `scaningLine` / `ParserMediator` 等共享状态

### 函数级三阶段管线

- 解析粒度可拆到**函数级**，管线改为三阶段——
  1. **解析签名**（并行，per-function）：`@NAME,ARGS` 头行 + `#FUNCTION`/`#DIM` 等属性行；
  2. **确定 label**：全量签名到齐后统一裁决；
  3. **解析函数体**（并行，per-function）：label 已确定，CALL 目标可即时解析，函数体解析、`nestCheck`、CALL 跳转处理同一遍完成。
- **同名裁决**：并行装载无可靠的加载顺序，非事件函数重复定义的处理加**同名函数兼容配置**（沿用 `Compati*` 系命名惯例，如 `CompatiFuncDup`）：
  - 关闭：重复定义**报错**；
  - 启用：保留「先定义生效」语义——label 处理需携带每个函数的来源文件及其文件列表顺序。文件枚举顺序先于解析确定、并行下依然确定（需固定枚举顺序基准，现行受 `SortWithFilename` 影响）；`FunctionLabelLine.Position.Filename` 与 per-file 序号的基础设施已存在，开销很小。
  - 事件函数不受此开关影响：保持多定义并列执行（eraBasic 核心语义），但同桶执行顺序在并行装载下失去「定义顺序」依据，需定义确定性规则。
- **待定**：兼容开关默认值。默认启用可保持现状（EE `*#*`/`#patch` 目录以重复定义打补丁的用法不受影响）；默认报错则沿 `Compati*` 系「兼容需显式开启」的方向、语义更严格，但默认即破坏现有打补丁用法。
- 来自 `multilingual-translation` 的衔接：覆盖裁决与签名校验挂在阶段 2——被覆盖原函数的签名在阶段 1 已解析，校验零额外成本；`tl/` 树并行装载并复用同一管线（见 `.scratch/multilingual-translation/spec.md` §2.5）。
- 来自 issues/03 的衔接：解析期标识符驻留（span 键查重复用 AST 常驻字符串）的评估延至本票实施时一并处理，评估结论与并发安全约束见 issues/03 背景与「唯一有实际空间的方向」一节。

## Comments

- 2026-08-29: 函数级三阶段管线方案、同名裁决兼容配置与跨 spec 衔接写入正文;兼容开关默认值待定。
- 2026-08-29: 增加 issues/03 的衔接条目，标识符驻留评估延至本票实施时。
