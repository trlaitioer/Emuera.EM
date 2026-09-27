# 文件枚举策略藏在 Config 静态里，读取入口的缓存路径没有失败模式

Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（装载/运行时数据）。本票只承担**架构面**：把隐藏输入搬到 interface 上。其中「缓存缺失时的失败模式」已由既有票记录（见「影响」），不重复立题。

### 一、枚举策略是隐藏输入

`Config.GetFiles`（`Emuera/Runtime/Config/Config.cs:509-569`）的签名是 `(dir, rootdir, pattern)`，行为却由 `Config.SearchSubdirectory` / `Config.SortWithFilename` 决定（`:516` 是唯一读点）。调用方 `ErbLoader.LoadErbDir` 完全看不出自己要的递归与排序策略是什么。

关键事实：`private getFiles(dir, rootdir, pattern, toponly, sort)`（`:520`）**已经把两个开关当实参接收**——即 seam 的实参形态已存在，只是没被提升为 interface。

### 二、读取入口要求隐式前序

`EraStreamReader.OpenOnCache`（`Emuera/Runtime/Utils/EraStreamReader.cs:55-70`）要求 `Preload.Load`（`Preload.cs:54-96`）先把内容塞进进程级静态字典（`:14`）。`EraStreamReader.cs:90` 另有一条跨 3 个 module 的顺序约束写在注释里：「LexicalAnalyzer 経由で Config を参照するので Config 完成までつかわないこと」。

调用方：`ErbLoader.cs:403-406`、`ErhLoader.cs:104-108`、`ConstantData.cs:1710`；对照 `ConfigData.cs:841-842` 走 `Open`（不碰缓存）。

完成标准：

- 新增独立的枚举 module（与 `PurePath`/`GamePath` 同层，放 `Runtime/Utils/PathUtils/` 侧）：输入 = 根目录 + 枚举选项（递归、排序、`#` 优先规则），输出 = 有序的 (显示名, 完整路径) 列表；`Config.GetFiles` 退化为一行 adapter，`getFiles` 的 private 实现随之搬走
- 缓存 adapter 改为**由调用方把已读到的行集合交给 reader**（调用方本来就已经持有这些行），失败模式变为明确错误而非解引用 null
- 枚举 module 的用例：递归开/关 × 排序开/关 的 2×2、key 前缀、`erB2` 兄弟目录、大小写差异——不需要 `GlobalStatic`、不需要 `TestBootstrap`

## 方案

待评估。

## 影响

- **删除测试判决**：删 `Preload` → 三处 `OpenOnCache` 都要自己同步读盘（并行预读 + 编码探测 + BOM 分支约 85 行/份），复杂度回到 3 个调用方，**在挣饭钱**；但删 `OpenOnCache` 这层**入口**、改成调用方交行集合时复杂度**不会重现**——真正的 pass-through 是这个入口，不是 `Preload`。删 `Config.GetFiles` → 枚举 + key 前缀 + 排序 + 递归会回到 `ErbLoader`/`ErhLoader`/`ConstantData` 三处，**在挣饭钱**，问题是 interface 藏了一半输入
- **失败模式已记录，不重复立题**：`.scratch/nullable-migration/issues/28-bad-input-nulls.md:14` 已记 `OpenOnCache` 恒返回 true（`_fileLines = Preload.GetFileLines(path)!`）致死 `FailedOpenFile` 分支，并给出「缓存值可为 null」的成因（`readAllLinesDetectEncoding` 的内层 catch）。本票的「交行集合」改法会同时消掉该缺陷，落地时应回填该票
- **与既有票的关系**：`Config.getFiles` 的路径前缀逻辑已由 `.scratch/path-utils/issues/01-engine-path-prefix.md` 改为经 `PurePath.TryRelativeTo`；`.scratch/path-utils/spec.md:51` 也记了这条。本票只搬选项，不重复路径处理
- **测试面**：`Preload`/`EraStreamReader` 零直接用例；`ErbLoaderNestCheckTests.cs:35-36` 与 `ConstantDataTests.cs:26-27` 只在注释里解释「必须 Preload」——测试被迫复刻生产启动顺序。`ConfigGetFilesTests` 的 4 例只能覆盖「不递归、不排序」一种组合
- 改动量估计：小到中（提升一个已存在的 private 方法 + 换一次 reader 的喂食方式）
- 相关票：issues/07（解析期开关）、issues/14（`ErbLoader` 装载顺序，消费本票的枚举 module）
