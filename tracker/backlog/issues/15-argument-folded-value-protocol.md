---
id: backlog-15
status: draft
---

# Argument 基类的 IsConst/ConstStr/ConstInt 是一个没有 interface 的三字段协议

## 任务

发现于 2026-09-27 的只读架构审查（声明/指令执行层）。

`Emuera/Runtime/Script/Statements/Argument.cs:112-120` 声明了 `IsConst`/`ConstStr`/`ConstInt` 三个字段：写入 30 处（`ArgumentBuilder.cs:526-527, 753, 845, 873-885, 1207-1213, 1297-1299, 1395, 1443, 1459-1473, 1500-1502, 1532-1534, 1567, 1764-1765, 1810-1823, 1870, 1899, 2554-2565`），读取 43 处（`Process.ScriptProc.cs:576, 633, 651-653, 685-739, 781-795, 1114`；`Instraction.Child.cs:201-203, 693-695, 879-881, 1432, 1467-1474, 1517-1531, 1580, 1602-1609, 2112-2114, 2371-2373, 3586, 3612, 3846-3848, 4340, 4453, 4496-4524`）。

问题在于 `IsConst` 有**四种含义**：

| 写入方 | 含义 | 锚点 |
| --- | --- | --- |
| `ExpressionArgument` / `SpSetArgument` | Term 是可折叠常量 | `ArgumentBuilder.cs:845, 1764` |
| `SpCallArgment` / `SpCallFArgment` | 函数名是字面量 | `:1207-1213` |
| `SpSetArrayArgument` | 整张字面量表已折叠 | `:1443` |
| `SpHtmlPrint` | 字符串与整数都折了 | `:524-528` |

读者在基类上读三个无类型字段，靠「我的 builder 一定会填」这一口头契约。协议不完整处已留下疤痕：

- `ConstStr` 在 `Process.ScriptProc.cs:576/633/699` 被无条件读，而只有 `STR_ArgumentBuilder`（`:845`）无条件填它，`FORM_STR_ArgumentBuilder`（`:879-885`）只在 `term is SingleTerm` 时填
- `SP_COLOR_ArgumentBuilder` 算了 `ConstInt`/`IsConst`（`:2552-2566`），`SETBGCOLOR` 读了（`:597-603`），`SETCOLOR` 完全不读（`:547-568`）——同一 builder 的一种预计算被一个分支忽略
- `CUSTOMDRAWLINE` 故意置 `IsConst = false` 以把 GDI+ 文本测量推迟到 UI 线程（`Instraction.Child.cs:651-653`）——说明该协议还兼着「能否在解析期折叠」的火线职责（解析跑在 `ErbLoader` 的 `Parallel.ForEach` 上）

完成标准：

- 「可静态折叠的值」成为少数具备 interface 的实参形态：能折叠者才暴露取值，读者向实参要值，而不是读三个未类型化字段并相信自己的 builder
- seam 落在**实参类型家族**上，不落在基类上
- 用例：对每个 `ArgumentBuilder` 断言「产物若被某读取点读 `IsConst`，则必已填 `ConstStr`/`ConstInt`」

## 方案

待评估。**诊断强、改法待评估**：删掉三字段会让 43 个读取点各自转型、各自重建约定（复杂度重现，故字段本身在挣饭钱）；它今天是 shallow——只提供共享名字，不提供任何保证。

与 issues/02 是同一 seam 的两侧（02 管解析期产出槽位，本票管折叠结果的协议），建议合并评估。

## 影响

- **与本分支主题重合**：`Argument.cs:112-120` 现有的 `null!` 注释只说明「读取点永不为 null」，没有说明「哪个 builder 填哪个字段」——正是 `nullable-migration` 断言审计（`nullable-migration-14`、`nullable-migration-22`）留白的形状。该契约的属性登记见 `tracker/nullable-migration/issues/09-argument-instruction-chain.md:117`（`Argument.ConstStr` 以声明处 `null!` 表达）
- **测试面**：今天「哪些 builder 填哪些字段」无任何测试；`OmittedArgumentTests` 只钉形状
- 改动量估计：中（30 个写入点 + 43 个读取点 + `Argument` 家族的形态调整）
- 相关票：issues/02（实参契约表）、issues/15 之外的 issues/06（命令表一处声明）
