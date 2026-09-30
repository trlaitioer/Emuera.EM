# 266 个内建函数的名字只存在于注册字典的键上，与实现分居两个文件且无一致性检查

Status: needs-triage

## 任务

发现于 2026-09-29 的内建函数注册机制讨论（只读核查）。

`FunctionMethodCreator` 在静态构造里手写一张 `Dictionary<string, FunctionMethod>(Config.StrComper)`（`Emuera/Runtime/Script/Statements/Function/Creator.cs` 的 `FunctionMethodCreator` 静态构造）：**实测 266 条活注册**。口径为行首 `["NAME"] = `；宽松 grep 得 270，差额 4 条是注释掉的注册——`CHKVARDATA`/`CHKGLOBALDATA`/`FIND_VARDATA`（`Creator.cs` 里被注释掉的那三条，其类仍是活的，只是这个枚举值未注册）与 `GROTATE`（`Creator.cs` 里被注释掉的那条；其类 `GraphicsRotateMethod` 本身也被注释）。266 个实现类集中在 `Creator.Method.cs`（约 1.01 万行，184 个活的具体子类）。

由此产生的三个现象（均已核实）：

1. **名字与实现分居两个文件**：类不知道自己叫什么，靠静态构造末尾回填（`Creator.cs` 静态构造末尾的 `SetMethodName` 回填循环；`FunctionMethod.Name` 为 `protected` 只读属性）。在 `Creator.Method.cs` 里 `grep "MIN"` 之类按名字找实现是无效的。
2. **`Name` 是行为的一部分，不只是诊断**：`Creator.Method.cs` 中 `Name` 有 276 行命中、649 次出现（含大量错误消息与静态 helper 的形参名），其中 **7 处是 `switch (Name)` 行为分派**（均在 `Creator.Method.cs` 内）。即**同一类注册为多个名字、每名字一个独立实例，实例靠运行期的 `Name` 分派**：`GraphicsStateMethod` 一个类占了 `GCREATED`/`GWIDTH`/`GHEIGHT`/`GGETFONTSIZE`/`GGETFONTSTYLE`/`GGETBRUSH`/`GGETPEN`/`GGETPENWIDTH` 等（见 `Creator.cs` 里的注册项）。`StrForm.cs` 与 `OperatorMethod.cs` 两族不碰 `Name`。
3. **重名会静默覆盖**：`["K"] = v` 是**索引器赋值**而非 `Add`（`{ {"K", v} }` 才抛异常），同名时后者覆盖前者且无任何提示。今天**没有**重名（已机械核对），所以这是潜在隐患而非现行缺陷——一旦发生，被覆盖的那个名字会成为死注册。

完成标准：

- **名字与实现同处一处声明**（候选见「方案」），`grep` 某个名字能在实现处命中
- **「恰好注册一次」可机械核对**：闸门用例断言「装配中非抽象 `FunctionMethod` 子类集 == 注册表键集」且「无重名」。今天 `Emuera.Tests/Runtime/Script/Statements/Function/CheckArgumentTypeEx.cs` 的 `AssertAccepted`（`Assert.NotNull(Get(name, args))`）只有「某个名字已注册」的单点断言，没有全域断言，也检不出死注册
- **逐字不变的不变量**：`GetMethodList()` 的形状（`Dictionary<string, FunctionMethod>`，`Config.StrComper` 比较器，21 处调用点含测试）、`SetMethodName` 的回填语义、`Name` 在 7 处 `switch` 下的取值、注册表仍在 `FunctionMethodCreator` 静态构造内建完（首次查找前）。旧脚本可观测行为不变
- 装载报告（`ErbLoader` 的 `OverWriteSystemFuncWarn` 列表，按 `methodDic` 迭代序打印）若因机制变化而改序，按**可选清理**处理（排序后打印），不作为验收项

## 方案

待评估。讨论过的候选方向（未细化，供评估时取舍）：

- **载体**：attribute 挂在**静态工厂方法**上，而非类上——因为注册单元是**实例**而非类型，同名一类多实例且构造参数不同（`MIN`/`MAX` 是 `new MaxMethod(false/true)`，`CSVNAME`/`CSVCALLNAME` 是 `new CsvStrDataMethod(...)`）：
  `[BuiltinMethod("GCREATED")] static GraphicsStateMethod Create() => new();`
  第一版**不要**把名字传进构造函数，仍由注册层回填，与今天逐字等价；`Name` 只读化留作后续单独清理（它会牵动 7 处 `switch`）
- **consumer 二选一，可先反射后换生成器**（attribute 不变）：反射＝启动期扫 `Assembly.GetTypes()` 找带 attribute 的静态方法；生成器＝构建期产出注册表，重名在编译期报错，且可顺带产出 `arg-contract` 需要的角色化访问器
- **迁移三步，各自可证**：① 纯新增 attribute（266 处），用例断言「反射名字集 == 字典键集」→ 跑绿即证明等价，同时把「attribute 字符串会静默腐烂」变成可检测；② 一次性把字典替换为反射建表（一处改动，diff 可 review）；③ 按类别拆 `partial` 文件分散 `Creator.Method.cs`（纯搬家）
- 不使用「分批搬工厂 + 混用两套注册来源」：混合期必须合并两个来源，验证复杂度超过收益

## 曾考虑的替代方案

1. **方法名即内建名**（`static GraphicsStateMethod GCREATED()`，连 attribute 都省掉）：放弃——技术上可行（266 个名字全是合法 C# 标识符、无关键字冲突，已核），但 266 个全大写方法名与本仓库 C# 约定冲突，且 attribute 位置是唯一能承载后续注册元数据的地方
2. **类级 attribute**（`[BuiltinFunction("MIN","MAX")]`）：放弃——注册单元是实例，类级 attribute 表达不了「两个实例、两套构造参数」
3. **只拆文件、不动注册机制**：放弃——文件拆分只改善导航，名字与实现的分居、静默覆盖、无全域闸门三条都不动；拆文件降级为本方案的步骤 ③
4. **把入参规则也写进 attribute**：放弃——`ArgTypeList` 是构造函数里组合出的对象图（`OmitStart` + `ArgType` 标志位），attribute 只能带常量/`typeof`/数组；且多条规则的顺序无法靠重复 attribute 可靠表达。本票只做「命名 + 注册」

## 影响

- **与 backlog/issues/06 是同一病的两张表**：06 是指令表（`FunctionIdentifier` 的两条 `addFunction` 路径 + `Process.ScriptProc.cs` 的两个 switch），本票是内建函数表（`methodDic`）。两者都缺「一处声明」，但 seam 不同，宜各自独立评估
- **与 `arg-contract` effort 正交**：该 effort 明确不改 `argumentTypeArrayEx` 的声明式方向与 184 个子类的扁平结构（`.scratch/arg-contract/spec.md` 的「影响」节末条）；本票只动注册与命名，不动规则表。若将来上源生成器，可与该 effort 的「角色化访问器」合并为同一笔投资
- **顺带发现（归属 `arg-contract`，已回填）**：`.scratch/arg-contract/spec.md` 的「问题/背景」节原写「184 个**活的**子类 / 270 个注册名」——前半句口径正确（188 个 `: FunctionMethod` 命中减去 2 处注释掉的类声明与 2 个抽象基类），后半句是宽松口径（含 4 条注释注册），活注册名实为 266；spec.md 已按 266 改写。该偏差不影响 `slots-worklist.py` 的计数：它按**类**计数，而 4 条注释注册里 3 条的类（`CheckdataStrMethod`/`CheckdataMethod`/`FindFilesMethod`）另有活注册名（`CHKCHARADATA`/`CHKDATA`/`FIND_CHARADATA`），余下 1 条的类（`GraphicsRotateMethod`）整段被注释、不进解析
- **不在本票范围**：`PluginManager` 的 `methods` 表（`PluginManager.GetMethod`，仅由 `CALLSHARP` 经 `Instraction.Child.cs` 调用）与内建、UDF 均无交集，无优先级问题
- 改动量估计：中（266 处工厂 + 一次注册层替换 + 文件拆分）；风险集中在 `Name` 语义与注册时机，不在反射本身
