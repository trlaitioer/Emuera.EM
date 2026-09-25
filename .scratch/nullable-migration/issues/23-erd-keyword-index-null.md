# Nullable:ERD 变量码的键索引在 KeywordToInteger 处解引用 null

Status: wontfix

## 任务

issues/21 的清单判定曾把 `ConstantData.KeywordToInteger`（`Runtime/Script/Data/ConstantData.cs`）的 `dic!` 判为 **C 类可达缺陷**：该函数把 `GetKeywordDictionary` 的 `varname` 实参硬编码为 `null`，而 `GetKeywordDictionary` 在 `ret == null && UseERD && string.IsNullOrEmpty(varname)` 时直接 `return ret`（null）；ERD-only 变量码（`CVAR`/`CVARS`/`VAR`/`VARS`，`__LOWERCASE__` 为 0xFC/0xFE ≥ 0xF0，不在主 switch 内）因而必然拿到 null。

复核后该判定**不成立**，本票关闭：该处 `dic` 的非空性由**调用方**的前置门禁保证，不变量成立（A 类），见下。

## 方案

不实施。以下为判定不成立的依据：

`KeywordToInteger` 只有两处调用点，都在 `Instraction.Child.cs` 的同一指令实现内，且都以 `ConstantData.isDefined(code, key)` 为前置门禁（`CvarsetArgIsNotCharaVar` 一段），不成立即抛 `CodeEE(NotDefinedKey)`：

- `isDefined` 内部以 `GetKeywordDictionary(out _, varCode, -1, null)` 取值（index = -1、varname = null）。对 ERD-only 码，该调用同样命中 `string.IsNullOrEmpty(varname)` 的提前返回而得到 null → `isDefined` 返回 **false** → 门禁抛异常。
- 因此 ERD-only 码**到不了** `KeywordToInteger`；能到达的变量码必然在主 switch 内（`ret != null`），再经 `GetKeywordDictionary` 在 `index >= 0` 时的 `if (ret == null) throw CodeEE` 与 `index == allowIndex` 判定，要么返回非空字典、要么抛 `CodeEE`。
- `Config.UseERD` 默认为 `true`（`ConfigData.cs` 的 `ConfigItem<bool>(ConfigCode.UseERD, "ERD機能を利用する", "Use ERD", true)`），不改变上述结论——决定因素是 `isDefined` 传的 `varname = null`，与配置无关。

保留 `!`；其理由注释（不变量来自调用方门禁 + 被调方 null→CodeEE 的转换）归 issues/22。

## 影响

- 无代码改动
- 该 `!` 的不变量是跨模块的（调用方 `isDefined` 门禁 + 被调方把 null 转成 `CodeEE`），属 issues/22 需要注明理由的 A 类
- 附带记录一个与本票无关的既有行为（不在本迁移范围）：`CVARS:キー` 这类「字符用户定义变量 + 字符串键索引」在**赋值路径**（`SetValueAllEachChara`）会因 `isDefined` 返回 false 而报 `NotDefinedKey`；**读取路径**（`VariableStrArgTerm`，会传真实变量名）不受影响。是否为预期行为由 ERD 功能的负责人判断
