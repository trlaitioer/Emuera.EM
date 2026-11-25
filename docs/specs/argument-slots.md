# 内建函数实参槽

脚本语法的省略规则见 [eraBasic 语法速查](../csv-erb-syntax.md)，各注册名的具体契约见[内建函数契约表](../builtin-functions.md)。

[`FunctionMethod.CheckAndResolveSlots`](../../Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs) 从实际命中的规则段投影逐槽角色。运算符、StrForm 和 TOSTR 包装由调用点声明角色。`ArgumentSlots` 通过 `IsMissing`、`IsEmpty` 和 `Has` 区分缺失、显式空槽与有值。`SlotRole.Required` 表示该槽若存在便拒绝显式空值，尾部实参数量由 `OmitStart` 决定。

`Int`、`Str`、`Var` 等访问器读取检查期快照。逐槽角色用于错误诊断，读取可能为空或缺失的槽之前，调用方按声明检查槽位。原地折叠改写 `FoldTarget` 中的实参，快照仍保留原来的表达式。外层调用无法整体折叠时，后续读取快照可能再次求值已经在折叠期求值过的内层调用。

`UniqueRestructure` 接收 `FoldTarget` 的副本，副本上的写回会被丢弃。若它返回 `true` 且 `CanRestructure` 成立，`FunctionMethodTerm.Restructure` 会调用 `GetValue` 生成常量结果。折叠期仍可在规则保证非空的槽上使用 `Required(i)`。

允许常量引用的规则段应启用 `HasUniqueRestructure`，否则变量实体可能被折叠成常量值。`CONST` 与 `CHARADATA` 互斥，因此常量角色数组无法进入该折叠路径。变更规则时可用 [实参槽审计脚本](../../scripts/arg-contract/arg-slots.py) 核对。
