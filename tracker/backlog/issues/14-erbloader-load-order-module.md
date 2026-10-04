# ErbLoader 的装载顺序策略内联，且只在两个入口中的一个实现

id: backlog-14
Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（装载/运行时数据）。

`#` 目录优先这条 EE 扩展策略**没有任何 interface**：它由 `ErbLoader.LoadErbDir` 内部 41 行内联代码实现（`Emuera/Runtime/Script/Loader/ErbLoader.cs:54-60` 的 `Directory.GetDirectories(erbDir, "*#*", …)` + `:72-92` 先加载含 `#` 目录内的文件 + `:96-114` 再加载其余），靠局部 `List<string>` 做去重（`:100` 的 `loadedFiles.Contains(file)` 是线性扫描 → 与文件数成平方关系，`SortWithFilename` + 子目录递归时更明显）。

两个装载入口行为不对称：

- `LoadErbDir`（`:47-163`，用户运行游戏用）有顺序策略
- `LoadErbList`（`:169-218`，`--analysis` / `RunERBFromMemory` 用）**没有任何顺序策略**
- 相位切换（`Initialized = false` → `setLabelsArg` → `Initialized = true` → `ParseScript`）在两个入口里被抄了两遍（`:52/127` 与 `:175/210`）——相位本身就是 interface 的一部分（见 `.scratch/nullable-migration/issues/11-process-loader-chain.md` 涉及的 `LabelDictionary.Initialized`），却只由方法的语句顺序维持

结果：同一份「装载」语义有两个 implementation，只有一个实现了 EE 扩展；修一个顺序 bug 时不知道要改几处。想验证「`#` 目录真的先加载」只有端到端跑真实目录或读代码——**leverage 为零**，没有调用方或测试能复用「给我一个按 EE 规则排好序的文件列表」。

另注：`async Task<bool>` 在 interface 上，但 implementation 是 `await Task.Run(() => loadErb(...))` **逐个串行**（`:90`、`:113`）——没有并行收益，交出的是每文件一次线程池往返 + 「工作线程不得碰 UI」的全套纪律，而该代价（每个告警汇聚点都必须线程安全）没有出现在 interface 上。

完成标准：

- 装载前插一个纯 module：输入 = 根目录 + 枚举选项（递归、排序、`#` 优先规则），输出 = 有序的 (显示名, 完整路径) 列表；不碰 `GlobalStatic`、不碰 console、不碰 `labelDic`
- 两个入口都退化为「拿到有序列表 → 逐文件 `loadErb` → 相位切换」的 adapter，相位切换只写一份
- 去重从线性扫描改为集合
- `useCallForm`（`:862`）、`enabledLineCount`、`noError`、`warningDic` 等跨阶段可变字段随相位收进「本次装载的会话」对象——这是让 `LoadErbDir` 可被调用两次（重载 ERB）的前提
- 用例：顺序 module 的纯函数用例（`#` 优先、排序开关、去重、跨子目录 key 前缀）不需任何 fixture；`LoadErbDir` 至少可在单目录 + 少量文件下断言装载后的标签顺序 / `useCallForm` 效果

## 方案

待评估。**建议先只做顺序 module 那一刀**——「会话对象」那一步会牵动 `Process`/`GlobalStatic`，与 issues/12 同批更合适。

顺带确认：`:161` 的 `isOnlyEvent.Clear()` 是 return 前一行的死代码；`:158` 的 `parentProcess.scaningLine = null!` 的用途。

## 影响

- **删除测试判决**：删掉 `:54-114` 的顺序策略，`#` 目录优先会回到调用方（`Process`/`Program` 启动链必须自己排好文件）或回到用例里（`ErbLoaderNestCheckTests` 已在自造单文件 fixture）→ 这份行为在挣饭钱，但它是**内联代码而不是 module**：没有可被调用或被测试的 interface，也就没办法判断它什么时候坏了。删掉 `LoadErbList` 则是纯亏损——它承载 analysis 模式这条真实入口
- **测试面**：本领域只有 `nestCheck` 配对不变量（4 例，走 `LoadErbList` + 单文件 fixture）有覆盖。**顺序策略、`#` 目录规则、`LoadErbDir` 的相位切换、`useCallForm`/`IgnoreUncalledFunction` 的输出全部零覆盖**。根因是可测面太大：真实目录树 + `Preload` 缓存 + `GlobalStatic` 全套 + `null!` console，且 `Program` 目录静态是进程级（`GameRootScope.cs:5-8`）
- **这块别动**：`ErbLoader.cs:882-946`（`ReconcileLayerErrors`）与 `:948-991`（Depth 分层并行）是本仓库**深度最好**的一段——分层屏障、并发写同值的论证、同层 read-before-write 边的重决、`CallFunc` 不回填的理由都写成了可验证的注释，且 `ErbLoaderNestCheckTests` 用「读 `JumpTo` 的行必须带 `IsError`」这条不变量把它网住。`.scratch/performance-optimization/issues/04-parse-parallel.md` 是它的定案记录，本票不改并行边界
- 改动量估计：顺序 module 那一刀为小到中；含会话对象为中到大
- 相关票：issues/08（枚举 module，本票消费它）、issues/11（诊断返回值化，同一文件）、issues/12（装载会话对象）
