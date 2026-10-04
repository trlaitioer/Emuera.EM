# 槽位访问器与旧 Required 同强度：非空没有被类型兜住，守卫纪律是唯一防线

Status: needs-triage

## 任务

承接 [issues/05](05-argument-slots-type.md) 的「未决」节：`Int`/`Str`/`Var` 等访问器**不做运行期断言**，与旧 `ArgumentListExtensions.Required(i)` 的「调用方担保」同强度（可省略槽为空时仍由函数体先判、`Var` 仍是转型）。于是「函数体不再自证非空、不再 cast」这句话只由**守卫纪律 + 脚本期核对 + 访问器的契约报错**兜住，**不是由类型兜住**。

同源的零散残留（issues/05 第五轮实测，均仍成立）：

- `SlotRole` 的类型位在生产侧仍无读取点（只进 `Term`/`Var` 的报错文案）
- `BuiltinMethod/*.cs` 内 3 处可改 `Var(i)` 的 `VariableTerm` 判别未迁移：`XmlGetMethod`、`ArrayMultiSortExMethod`、`DataTableColumnManagementMethod`；第 4 处 `ReplaceMethod` 的拒绝路径**不能**换（它本意抛 `CodeEE(ArgIsNotNDStrArray)`，换成 `Var(i)` 会变成 `ExeEE`）
- `IsMissing` 无生产调用点（保留为三态的完整接口，只由访问器契约用例覆盖）

完成标准（待评估后细化）：

- 二选一并落地：① 访问器改断言式（契约被破坏时抛带 `RoleAt` 的文案），并在 183 个类上补/校验守卫；② 明确「类型不兜非空」是长期约定，写进 `docs/architecture.md` 后关掉这个方向
- 3 处可迁移的判别拆到 [issues/10](10-variable-term-discriminant-migration.md)（纯替换，与本票的裁定解耦）
- 门禁脚本（`arg-slots.py` 的「访问器前置核对」）与新判据保持一致，`slots-progress.md` 自校验随之更新

## 方案

待评估。① 等于改 183 个类的守卫纪律，需要回归网；② 的代价是每个新函数都得自己记住守卫。

## 影响

- 与 [backlog/issues/19](../../backlog/issues/19-name-retirement.md) 无耦合，但同样落在 `BuiltinMethod/*.cs`
- 走 ① 时，`IsMissing` 与 `SlotRole` 的类型位要么被消费、要么一并裁定去留
