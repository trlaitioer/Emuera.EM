# 迁移后残留的 4 处 VariableTerm 判别：3 处可改走 `Var(i)`，1 处不能换

Status: needs-triage

## 任务

承接 [issues/05](05-argument-slots-type.md) 的「完成标准」② 与第五轮 review 的实读结论：消费面迁到 `ArgumentSlots` 访问器后，计数口径（`arguments.Required(`/`arguments[...]` 文本锚定）看不到藏在 `slots.Term(i)` 或局部量之后的 `VariableTerm` 判别，今天仍有 4 处：

| # | 位置（类） | 形态 | 处置 |
| --- | --- | --- | --- |
| 1 | `XmlGetMethod`（`BuiltinMethod/PrivateAdditions.cs`） | `outputArg as VariableTerm` | 可改 `Var(i)` |
| 2 | `ArrayMultiSortExMethod`（同文件） | `slots.Term(0) is VariableTerm` | 可改 `Var(i)` |
| 3 | `DataTableColumnManagementMethod`（同文件） | `slots.Term(1) is VariableTerm` | 可改 `Var(i)` |
| 4 | `ReplaceMethod`（`BuiltinMethod/StringOperations.cs`） | `slots.Term(2) is not VariableTerm` 的**拒绝路径** | **不能换** |

前三处都有规则表担保 `Ref` 且无 `AllowConstRef`，故安全（issues/05 的实读结论）。第 4 处本意是抛 `CodeEE(ArgIsNotNDStrArray)`，换成 `Var(i)` 会降级成内部 `ExeEE`。

完成标准：

- 前三处改走 `Var(i)`，第 4 处保持现状并在代码注释里写明「为什么不能换」
- 迁移后 `BuiltinMethod/*.cs` 内不再有这类可迁移的类型判别；`arg-slots.py` 的「访问器前置核对」口径不受影响（它按字面下标与守卫判据统计）
- 全量测试绿

## 方案

待评估（机械替换 + 一处注释）。

## 影响

- 与 [issues/07](07-accessor-runtime-assertions.md) 同源但可独立完成：本票是纯替换，不牵动「访问器要不要做运行期断言」这个裁定
- 第 4 处若被当成同类改动一起替换，会把脚本级 `CodeEE` 降级为内部 `ExeEE`——上表单列它就是为了防这一手
