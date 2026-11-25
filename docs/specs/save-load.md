# 读档暂存与扩展数据

[`EMDataLoadScope`](../../Emuera/Runtime/Script/Statements/Variable/EMDataLoadScope.cs) 在 `LOADDATA` 和 `LOADGLOBAL` 读取期间暂存 MAP、XML 和 DataTable。读取失败时丢弃暂存值，恢复原有的扩展数据状态。

提交时合并新值，按目标键移除旧 XML，并原地清空旧 MAP 和 DataTable。清理范围包含存档省略的目标键和被新值替换的实例。插件持有的旧表引用仍能观察到原地清空及其回调。清表按实例去重，先处理存档省略的表。清表抛错时，提交中止并尝试恢复纳入备份的目标表。

暂存范围限于扩展数据。普通变量、角色和读档元数据可在提交前改变。插件回调及外键级联也可影响目标表之外的状态。[议题 #1](https://github.com/trlaitioer/Emuera.EM/issues/1) 跟踪这些读档副作用与兼容性问题。

[`VariableEvaluator`](../../Emuera/Runtime/Script/Statements/Variable/VariableEvaluator.cs) 的 `LoadFromFile` 通过异常报告读档失败，路径不存在时抛出 `ExeEE`，存档内容损坏时抛出 `FileEE`。`LoadGlobalFile` 通过 `false` 和 `errorReason` 报告失败。
