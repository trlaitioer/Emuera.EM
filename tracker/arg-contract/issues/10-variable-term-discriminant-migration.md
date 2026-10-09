---
id: arg-contract-10
status: implemented
---

# 迁移后残留的 4 处 VariableTerm 判别：2 处已改走 `Var(i)`，2 处不能换

## 任务

承接 `arg-contract-05` 的「完成标准」② 与第五轮 review 的实读结论：消费面迁到 `ArgumentSlots` 访问器后，计数口径（`arguments.Required(`/`arguments[...]` 文本锚定）看不到藏在 `slots.Term(i)` 或局部量之后的 `VariableTerm` 判别，今天仍有 4 处：

| # | 位置（类） | 形态 | 处置 |
| --- | --- | --- | --- |
| 1 | `XmlGetMethod`（`BuiltinMethod/PrivateAdditions.cs`） | `outputArg as VariableTerm` | **已改** `Var(2)`（原表记「可改」） |
| 2 | `ArrayMultiSortExMethod`（同文件） | `slots.Term(0) is VariableTerm` | **不能换**（原表记「可改」，实测更正——见下） |
| 3 | `DataTableColumnManagementMethod`（同文件） | `slots.Term(1) is VariableTerm` | **已改** `Var(1)`（原表记「可改」） |
| 4 | `ReplaceMethod`（`BuiltinMethod/StringOperations.cs`） | `slots.Term(2) is not VariableTerm` 的**拒绝路径** | **不能换**（已补注释） |

**#2 的更正（本票落地时实测）**：`ARRAYMSORTEX` 的 1 号规则段是 `{ ArgType.String, ArgType.RefString1D, … }`（`PrivateAdditions.cs` 的 ctor）——槽 0 **允许直接传变量名字符串**，`ArgType.String` 不含 `Ref`，该形态下槽里装的是字符串常量 term 而不是变量实体；`slots.Var(0)` 的实现是 `Term(index) as VariableTerm ?? throw new ExeEE(…)`（`FunctionMethod.cs` 的 `ArgumentSlots.Var`），**它不读角色**，只在槽内不是变量实体时抛 `ExeEE`，故 `ARRAYMSORTEX("DA:0", …)` 会被它拒掉——`GetConvertedTerm` 那条 `LexicalAnalyzer` 回退是有承载的。原判「前三处都有规则表担保 `Ref`」对 #2 不成立。

#1、#3 的规则表担保 `RefString1D` 且无 `AllowConstRef`，故可安全改走 `Var(i)`；#4 本意是抛 `CodeEE(ArgIsNotNDStrArray)`，换成 `Var(i)` 会降级成内部 `ExeEE`。

完成标准（2026-10-05 落地）：

- 2 处改走 `Var(i)`（#1、#3），2 处保持现状并在代码注释里写明「为什么不能换」（#2 的规则形态、#4 的拒绝路径）
- `BuiltinMethod/*.cs` 内不再有可迁移的类型判别；`arg-slots.py` 的「访问器前置核对」口径不受影响（它按字面下标与守卫判据统计）
- 全量测试绿（`Debug-NAudio` 689 通过，含 `Debug` 配置重建）

## 方案

机械替换 + 两处注释；#2 的更正同步回填 `arg-contract-07` 的「3 处可迁移」表述。

## 影响

- 与 `arg-contract-07` 同源但可独立完成：本票是纯替换，不牵动「访问器要不要做运行期断言」这个裁定
- 第 4 处若被当成同类改动一起替换，会把脚本级 `CodeEE` 降级为内部 `ExeEE`——上表单列它就是为了防这一手
