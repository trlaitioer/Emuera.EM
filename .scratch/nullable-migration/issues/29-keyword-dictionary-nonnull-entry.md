# Nullable:`GetKeywordDictionary` 的 Try 拆分与非空入口

Status: implemented

## 任务

issues/21 的 A 类判定里有 21 处 `dict![key]`（`VariableDataWrappers` 18、`PluginManager` 2、`ConstantData.KeywordToInteger` 1）：不变量成立（审计已逐点取证），但每处都要写 `!`，而理由只是「该变量码必命中主 switch」——一条写在注释里、编译器与调用点都看不到的前提。根因是 `ConstantData.GetKeywordDictionary` 用一个可空返回同时表达三件事：

- `index < 0` 的存在性探测（`isDefined`、`TryKeywordToInteger`，null 是正常答案）
- ERD 表缺失（`varname` 为空或 `erdNameToIntDics` 未命中，`index >= 0` 时同样返回 null；issues/19 行 8 登记的潜在缺陷）
- `index >= 0` 且 `ret == null`（此时已经抛 `CodeEE(CanNotSpecifiedByString)`）

消费点拿不到「失败信号」，只能对返回值写 `!`。

完成标准：求值点不再写 `!`，探测点仍能如实问「有没有字典」，`Emuera`/`Emuera.Tests` 的 CS86xx 保持 0、测试通过。

## 方案

拆成「如实探测」与「非空求值」两个入口，让不变量由签名承担：

| 入口 | 契约 |
| --- | --- |
| `bool TryGetKeywordDictionary([NotNullWhen(true)] out Dictionary<string, int>? dic, [NotNullWhen(true)] out string? errPos, VariableCode code, int index, string? varname)` | `false` 只回答两个可以合法为「没有」的问题：`index < 0` 的存在性探测、`varname` 的 ERD 表缺失；`dic` 留空，`errPos` 非空 ⇔ 命中。用法错误仍抛 `CodeEE`（无法用字符串指定的码、该码不允许的索引——与原方法末段的抛出一致） |
| `Dictionary<string, int> GetKeywordDictionary([NotNull] out string? errPos, VariableCode code, int index, string? varname)` | 求值入口（`index >= 0`），没有字典即抛 `CodeEE(CanNotSpecifiedByString)`（与原先 `ret == null` 分支同文案）；`errPos` 只与命中同时赋值，故恒非空 |

调用点迁移（27 处）：

- 21 处走非空入口并去掉 `!`：`VariableDataWrappers` 18（16 个方法）、`PluginManager.SetCharVar`/`GetCharVar` 2、`ConstantData.KeywordToInteger` 1；插件侧的 `string? errPos = "";` 死局部一并去掉，实参写 `out _`。
- 6 处保留探测语义：`isDefined` 3（`index = -1` 问存在性）、`TryKeywordToInteger`（`-1` 探测）、`VariableStrArgTerm` 2（`GetIntValue` 用真 `varname` 解析、`Restructure` 用 `varname = null` 预取，两者都必须允许「没有」）。
- `KeywordToInteger` 的 `if (errPos == null) throw CanNotSpecifiedByString` 删除：`dic != null` ⇔ 该次求值命中 ⇔ `errPos` 非空，该分支在 `[NotNull]` 下不可达。
- `VariableStrArgTerm` 的守卫由 `dic == null && key != ""` 收窄为 `dic == null`（`key` 在上方 `IsNullOrEmpty` 处已抛，后半句恒真），`dic!` 随之删除；`errPos == null` 分支保留——该处 Try 的返回值被丢弃，字段未收窄，编译器看不到「命中即有 errPos」，故保留并依赖运行期事实。
- 非空保证的落点（实施中补充）：`nameToIntDics` 的元素只由 `LoadData` 建立，故 `LoadData` 收为 `private` 并只由 `ConstantData` 的构造函数调用（`Process.cs` 的 `new(...)` 与 `Emuera.Tests` 的两处夹具同步）——「构造即已加载」使求值入口的非空返回、Try 的 `[NotNullWhen(true)]`/`[NotNull]` 与 CDFLAG 分支的 `return true` 不再依赖 `Process.Initialize` 里「先 `new` 后 `LoadData`」的调用顺序。

## 曾考虑的替代方案

1. 只加非空入口、保留现有可空方法：放弃——`index < 0` 的探测点会留在可空方法上，同一函数继续承担两种语义；改动更小但契约依旧混杂，`VariableStrArgTerm` 的 `dic!` 也仍需另找理由（可退化为删掉恒真的 `&& key != ""`）。
2. 把现有方法直接改成抛异常、不提供 Try：放弃——`isDefined` 的三处 `-1` 探测与 `VariableStrArgTerm.Restructure` 的 `varname = null` 预取会被误伤，二者都需要「可以是没有」的答案。
3. 在 21 个断言点就地补判空/抛错、不加新入口：放弃——把同一条不变量抄 21 遍，`VariableDataWrappers` 的每个包装方法都要多一段错误分支。

## 影响

- `Emuera/` 后缀断言 943 → 921（−22；同一收紧式 `[\w\)\]]!(?!=)`、剥注释与字符串字面量），`dict!`/`dic!` 家族清零
- 行为变化只在审计判 A 的不可达路径上：原先 `dict![key]` 的 `NullReferenceException` 变为 `CodeEE(CanNotSpecifiedByString)`；`VariableStrArgTerm` 的 `&& key != ""` 恒真（`key` 在其上方的 `IsNullOrEmpty` 处已抛），删除它不改变任何可达行为
- issues/19 行 8 登记的「ERD 早退在 `index >= 0` 外泄 null」随之结构性关闭：探测版如实返回 `false`，求值版抛 `CodeEE`
- 装载入口收归构造函数（`LoadData` 转 `private`）：实例一生只装载一次，原先「同实例二次 `LoadData` 会沿用并累积旧名字表」的路径随之消失；代价是 `ConstantData` 的构造可能抛装载期异常——调用点仍在 `Process.Initialize` 的既有 `try` 内，异常面不变
- issue/21 的该组 21 处 A 类断言不再需要保留与理由注释，[bang-sites.md](../bang-sites.md) 的对应站点移入「已失效站点」
- 覆盖空白：`Emuera.Tests` 目前没有触及这两个入口与插件包装方法的用例（`PluginManagerTests` 只覆盖 `ExecuteLine`/`GetStackTrace`，`VariableDataWrappers` 无引用），本次唯一的行为差异尚无用例钉住；宜补一条最小用例（夹具可复用 `ConstantDataTests` 的临时目录 + 最小 CSV + 真实 `LoadData`），覆盖「关键字命中」「`index < 0` 探测为 false 且 `dic == null`」与「缺字典时 `CodeEE`」
