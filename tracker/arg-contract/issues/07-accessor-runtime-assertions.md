---
id: arg-contract-07
status: wontfix
---

# 槽位访问器与旧 Required 同强度：非空没有被类型兜住，守卫纪律是唯一防线

## 任务

承接 `arg-contract-05` 的「未决」节：`Int`/`Str`/`Var` 等访问器**不做运行期断言**，与旧 `ArgumentListExtensions.Required(i)` 的「调用方担保」同强度（可省略槽为空时仍由函数体先判、`Var` 仍是转型）。于是「函数体不再自证非空、不再 cast」这句话只由**守卫纪律 + 脚本期核对 + 访问器的契约报错**兜住，**不是由类型兜住**。

同源的零散残留（issues/05 第五轮实测，均仍成立）：

- `SlotRole` 的类型位在生产侧仍无读取点（只进 `Term`/`Var` 的报错文案）——保留：它是契约的投影结果，删掉只会让 `RoleAt` 无从解释
- `BuiltinMethod/*.cs` 内可改 `Var(i)` 的判别**实为 2 处**（原写 3 处，见下）：`XmlGetMethod`、`DataTableColumnManagementMethod`；`ArrayMultiSortExMethod` **不能换**（槽 0 的规则段允许 `ArgType.String`——变量名字符串形态，`Var(0)` 会让 `ARRAYMSORTEX("DA:0", …)` 抛 `ExeEE`）；第 4 处 `ReplaceMethod` 的拒绝路径**不能**换（它本意抛 `CodeEE(ArgIsNotNDStrArray)`，换成 `Var(i)` 会变成 `ExeEE`）
- `IsMissing` 无生产调用点（保留为三态的完整接口，只由访问器契约用例覆盖）

完成标准（2026-10-05 裁定后落地）：

- 二选一：① 访问器改断言式（契约被破坏时抛带 `RoleAt` 的文案），并在 183 个类上补/校验守卫；② 明确「类型不兜非空」是长期约定，写进 `docs/architecture.md` 后关掉这个方向——**取 ②**
- ② 已落 `docs/architecture.md` 的 `Statements/Function/FunctionMethod.cs` 两条：访问器不做运行期断言（角色只进报错文案，「槽位存在」与「非显式空槽」两个证明仍由函数体给出），以及 `SlotRole` 的 `Required` 只声明「非显式空槽」
- 判别迁移拆到 `arg-contract-10`（已落地 2 处 + 2 处注释），与本票的裁定解耦
- 门禁脚本（`arg-slots.py` 的「访问器前置核对」）口径未变：② 不改变守卫判据，`slots-progress.md` 无需重跑

## 方案

定为 **②**：把「类型不兜非空」写成长期约定。① 要为 183 个类补/校验守卫纪律并自建回归网，收益是把一类内部错误提前暴露；而 `Term`/`Var` 已经在破坏契约时抛带角色名的 `ExeEE`，脚本期核对与 `Emuera.Tests` 的表驱动用例是现有防线。

## 影响

- 与 `backlog-19` 无耦合，但同样落在 `BuiltinMethod/*.cs`
- `IsMissing` 与 `SlotRole` 的类型位一并裁定为**保留**（② 之下它们是契约的完整表达，不是待消费的接口）
