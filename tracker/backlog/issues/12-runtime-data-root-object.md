# GlobalStatic 的装配顺序只写在注释里

id: backlog-12
Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（装载/运行时数据）。

`Emuera/GlobalStatic.cs:21-94` 的 interface 是「9 个可变静态字段 + 一条只存在于注释里的拓扑顺序」：

- `:23-24` 的注释「これは生成される順序で並んでいる。下から上を参照した場合、nullを返されることがある」——**装配顺序就是这里的 interface**
- `:27-44` 九个 `null!` 可变静态字段（`Console`/`Process`/`GameBaseData`/`ConstantData`/`VariableData`/`VEvaluator`/`IdentifierDictionary`/`EMediator`/`LabelDictionary`）
- 反向写入：`VariableEvaluator.cs:38` 的 `GlobalStatic.VariableData = varData;`
- 构造期反向读取：`StrForm.cs:65-92` 的 static 构造器读 `GlobalStatic.VariableData`，`:65-68` 的注释自陈「早于此时触碰本类型就是初始化顺序 bug（static 构造器不会重跑）」
- 并发契约放错位置：`tempDic` + `TempDicLock`（`:49-55`）是 `ErbLoader.ParseScriptCore` 那段 `Parallel.ForEach` 的私有协议（写入方 `ExpressionParser.cs:323-339`、读取方 `ErbLoader.cs:1118`），却作为全局成员公开——宣布「这两个东西必须一起用同一把锁」的地方离产生该约束的并行区有 3 个文件远
- 全库 `GlobalStatic.` 引用 **337 处**

完成标准：

- 引入「一次装载的运行时数据」根 module，拥有上列 7 份非进程单例数据，并在自身构造路径里按依赖顺序建好；装配顺序从注释变成类型
- `GlobalStatic` 收缩为「持有当前根对象」的一个字段；`Console`/`Process`/`Pfc`/`ctrlZ`/`ExistPlugin` 这类真·进程单例留在原处（它们不参与顺序）
- `VariableEvaluator.cs:38` 的反写、`StrForm.cs:65-92` 的构造期读取改为「根对象建好后把依赖交给需要它的 module」
- `tempDic` + `TempDicLock` 收进装载会话对象（与 issues/14 的同一对象）
- 用例：每个用例构造自己的运行时数据（不同角色表、不同 csv、不同 `IgnoreCase`），从而 `AssemblyInfo.cs:6` 的并行禁用有望只保留给真正共享 `Program` 目录静态的用例

## 方案

待评估。**建议只吃「根对象 + 顺序进构造函数」这一步**，不要顺手重命名 337 个读点——这是本领域收益最大、也最容易做过头的一刀。

与 issues/15 同批做：`StrForm` 的符号表是「运行时数据根对象」的第一个消费者。

## 影响

- **对审查结论的一处更正**：审查曾把 `GlobalStatic.Reset()`（`:80-93`）记为「不可达的死代码 ⇒ 进程内无法构造第二份运行时」。已核实：`Reset()` 是 `public static`，所在类 `internal` 而 `Emuera.csproj` 有 `InternalsVisibleTo("Emuera.Tests")`，故**测试侧可调用**；准确的说法是「产品内无调用点」——唯一调用点 `Program.cs:339` 位于 `:308-345` 的 `/* VVII版マージ前の起動処理 */` 注释块内，现役重启走 `MainWindow.cs:1262 Reboot()`（不调 `Reset`）。`tracker/test-infrastructure/issues/03-phase2-beyond.md:33` 本就计划「`GlobalStatic.Reset()` + 重建做隔离」，实际 `TestBootstrap.cs:19` 改用了 `static bool initialized`。故本票的前置不是「让 Reset 可达」，而是「顺序有类型」；`Reset()` 该删还是该成为新根对象的正常生命周期终点，随本票定
- **与既有决策的关系（非冲突）**：`tracker/nullable-migration/issues/06-nullability-contracts.md` 定了「初始化前不可达即保持非空 + 声明处 `null!`」，`tracker/nullable-migration/issues/11-process-loader-chain.md:77` 据此为这 9 个字段保留了非空声明。本票**不要求**把这些字段改为可空——它要求把**顺序**搬进构造函数，字段的不可空声明可以原样保留，故与那两张票的结论不冲突
- **测试面**：`TestBootstrapTests.cs:31-41` 专门断言 `GlobalStatics_AreWired`——在测试里验证装配顺序，这本身就是顺序没有类型的代价。`TestBootstrap.cs` 只能一次性初始化（`:19`），`Program` 的目录静态是进程级（`GameRootScope.cs:5-8` 明确写下「用例不得假设另一个游戏根」）
- 改动量估计：中到大（装配链 + 3 处反向写入/读取）。**建议先只做根对象，不动 `Reset` 与读点命名**
- 相关票：issues/15（`StrForm` 符号绑定）、issues/07（解析期开关）、issues/14（装载会话对象）
