# 路径:根目录类型化与「游戏目录」唯一定义

Status: implemented

## 任务

游戏目录此前有两个来源：S2 引入的 `Program.GameRoot`（`PurePath`）与它自己的字符串派生 `Program.ExeDir`/`CsvDir`/`ErbDir`/…；目录常量又以「必须以分隔符结尾」的隐式契约在字符串层面流转。本票把根目录类型化并定下唯一定义。范围与完成标准：

- **定下权威定义**：`Program.GameRoot`（`GamePath`，根自身）即游戏目录，`CsvRoot`/`ErbRoot`/`DebugRoot`/`DatRoot`/`ContentRoot`/`SoundRoot`/`FontRoot`（`GamePath`）由它派生；八个字符串成员 `ExeDir`/`CsvDir`/`ErbDir`/`DebugDir`/`DatDir`/`ContentDir`/`SoundDir`/`FontDir` 全部删除，`ExeDir` 的消费点改用 `GameRoot.FullName`。
- **处理引用面**：`Emuera/` 下 `Program.*Dir` 的限定引用为 **64 行 / 20 文件**（非注释、不含 `Program.cs` 自身），其中 **31 行是 `dir + name` 拼接**、**33 行只是把目录当参数**传给 `File.*`/`Directory.*`/`DirectoryInfo`/对话框 `InitialDirectory`/`SelectedPath`。
- **处理拼接**：透传取 `.FullName`；拼接改 `XxxRoot.CombineName("name").FullName`。只在单处使用的字面量（`Plugins`/`patch_versions`/`lang`/`macro.txt`/`HOTKEY.ERB`/`time.log`/`Analysis.log`）不进 `Program`，就地 `GameRoot.CombineName(...)`。
- **5 个静态路径常量改延迟求值**：`Lang.LangDir`、`JSONConfig.ConfigFilePath`、`ConfigData.ConfigPath`/`ConfigDebugPath`、`KeyMacro.MacroPath`。它们此前是 `static readonly` 字符串，在所属类型首次被触碰时求值。
- **删除 `Program` 静态构造与 `Data/` 探测**：`SetDirPaths` 成为唯一入口，`Main` 内联默认根本应为 `Directory.GetCurrentDirectory()`；`AppContext.BaseDirectory` 与 `Directory.GetCurrentDirectory()` 的其余引用随之归零。`GameRoot` 未初始化时抛 `InvalidOperationException`。
- `Config.SavDir` / `ForceSavDir` → `GamePath`（`UseSaveFolder == false` 时即游戏目录自身）。
- `Runtime/Utils/Sys.cs`：删除死常量 `AssemblyData.ExeDir`（全仓零引用）与 `WorkingDir`（唯一消费点是 `Main` 的默认根）；类只剩 assembly 事实，文件改名为 `AssemblyData.cs`。

完成标准：`Program` 上不再存在字符串目录成员；`Path.DirectorySeparatorChar` 只出现在 `PurePath`/`NamePattern` 内部；涉及被删符号的注释一并清除，使 grep 口径可判定；`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` 0 错误、警告数与基线（83）一致；`Emuera.Tests` 全绿。

## 方案

1. `Program`：`SetDirPaths` 用 `Path.GetFullPath(new DirectoryInfo(gameDir).FullName)` 归一化根（`--ExeDir` 的相对值仍按 CWD 解析，不用 `PurePath` 改写），`GameRoot = GamePath.FromRoot(...)`，其余 8 个根由 `GameRoot.CombineName(...)` 产出。原先的私有 `RootChild` 随之删除——它已退化为 `CombineName` 的一行转发，而「字面量名不可能解析失败」这条不变量属于 `GamePath`，见下条。
2. 新增 `GamePath.CombineName(string)`：单段名字面量拼接，失败即抛 `ArgumentException`。这是本票唯一新增的 API；非脚本侧的 31 处拼接、`Config.SavDir`、存档路径族与镜像/别名路径都靠它一行完成，脚本来源的段仍走 `TryResolveChildName`。
3. 未初始化守卫：9 个根是「可空字段 + `?? throw NotSetUp()`」的只读属性，静态构造删除后过早读取会明确抛错，而非拿到 `AppContext.BaseDirectory` 的值。
4. 静态常量改为表达式属性（PascalCase，`private static GamePath ConfigPath => Program.GameRoot.CombineName("emuera.config")`），在读取处取 `.FullName`；这 5 个常量此前都是 `static readonly`，是「谁先被触碰就固化谁」的来源。
5. `SavDir`/`ForceSavDir`：`ForceSavDir = GameRoot.CombineName("sav")`，`SavDir = UseSaveFolder ? ForceSavDir : GameRoot`；`Config.cs` 内 `Directory.Exists`/`CreateDirectory`/`File.Move` 取 `.FullName`。名字版两个解析器统一到 `TryResolveChild`（可指向存档/数据目录的子目录，不能离开该目录）：`TryResolveSaveFilePath` 由此前「只校验到游戏根」收紧，`TryResolveSaveDataPath` 则由 S2 的单段名口径放宽。
6. `Sys.cs`：内容迁到 `Runtime/Utils/AssemblyData.cs`（文件与类名一致），原文件留待删除。
7. 提交切分：①根目录类型化 + 全部引用替换 + 5 个静态常量延迟求值 + `Sys.cs` 收尾 + 04 的存档目录类型化与存档路径族统一；②03 的两处硬编码分隔符。原计划的三段切不开——删除 `Program.ExeDir`/`DebugDir` 后 5 个静态常量是仅存的引用（必须同 commit 改），删除 `Program.ExeDir`/`DatDir` 也强制存档族那几行同 commit 改；硬拆只能先写一批 `.FullName`/尾分隔符过渡代码，再被下一 commit 重写。

## 曾考虑的替代方案

1. 停在 S2 的过渡态（`Program.*Dir` 保持字符串，只额外持有 `PurePath` 视图）：改动面最小，但「目录常量必须以分隔符结尾」的契约继续存在，同一个根在 `Program` 上长期有两套可漂移的表示。放弃原因是本票目标正是消除这两者。
2. 删除类型化根，只保留从 `GameRoot` 派生的字符串目录（`CsvDir => GameRoot.Combine("csv").ToString()`）：改动面降到 31 行，尾分隔符契约与双来源同样能消除，是本票唯一真正的竞争者。放弃原因是它保留了「一个根两种表示」的对外形状，后续新增脚本入口仍要就地 `PurePath.From`，与「根目录类型化」的目标不符；省下的只是 33 处一行调用。
3. 保留 `ExeDir` 作为 `GameRoot` 的派生字符串别名（`=> GameRoot.FullName`）：省约 24 行改动，但 `Program.ExeDir` 的内容本就是游戏目录而非 exe 目录，保留即留下名不副实的成员——与 03 删除 `AssemblyData.ExeDir` 的理由相同。
4. `GamePath` 持有进程级静态根（`Initialize` 单例）以缩短调用点：引入全局状态，并把风险从「静态字符串常量」搬成「静态路径常量」，与消除静态初始化顺序风险的目的一致相反。
5. 保留 `AssemblyData.ExeDir` 作为「exe 所在目录」语义的备用（它与 `Program.ExeDir` 的语义确实不同）：该成员自声明起零引用，当前没有任何调用点需要 exe 目录；若将来需要，应从 `Environment.ProcessPath` 重新引入并在 `Program` 中命名，而不是留一个同名的死常量。

## 影响

- 行为变化（脚本可见，1 处）：`TryResolveSaveFilePath` 从 `TryResolveAbsolute($"{SavDir}save{name}.sav")` 改为 `SavDir.TryResolveChild(...)`。存档目录内的子目录仍可达（`CHKDATA "sub/name"` 照旧找 `sav\savesub\name.sav`）；变的是此前用多级 `..` 逃出存档目录、落到游戏根其它位置的名字现在被拒绝——即该函数注释声明的「inside the save directory」由注释变为实际校验，此前实际只校验到游戏目录。
- 行为变化（配置可见，1 处）：`Config.RikaiFilename`（设置项说明即「Emuera本体との相対パス」）此前以字符串拼接到 `ExeDir`，现在由 `GameRoot.TryResolveChild` 解析并把解析结果（`GamePath`）交给 `RikaiDialog`；除逃出游戏目录的名字改为「未找到」外无变化，且索引文件仍按「字典名 + `.ind`」命名（不替换原有扩展名），生成失败不再可能落到游戏目录之外。
- 无其他行为变化预期：`PurePath` 已保证与现拼接结果同形，尾分隔符是唯一差异，而所有 31 处拼接点都改为显式 `CombineName`/`Combine`；正因为不再补尾分隔符，遗漏一处会立刻显形为错误路径（`<root>\debugwatchlist.csv` 一类），而不是靠人记住契约。
- 静态初始化顺序风险此前**不可达**（`Program` 静态构造必在 `Main` 体之前完成，而 `Main` 无条件调用 `SetDirPaths`，两次调用之间只构造/解析命令行，不触碰任何持有路径常量的类型）。删除静态构造后，该风险从「静默固化」变为「立即抛」，并顺带消灭 `Data/` 探测这条从未生效的分支。
- 新增测试 4 个：`GamePathTests` 覆盖 `CombineName` 的成功与拒绝；`ProgramDirectoriesTests` 覆盖「每个根都是根目录下的具名条目」与「先触碰 `JSONConfig`/`ConfigData`/`KeyMacro` 再换根，写出的文件落在新根下」。`Emuera.Tests` 257 通过、0 失败。
- 03 的两处 `Path.GetDirectoryName(...) + "\\"`（`AppContents.LoadContents`、`ConstantData.loadDataTo`）独立成一提交；03 其余条目（删 `AssemblyData.ExeDir`、删 `Program.SoundDir`、定案 `Sys.cs` 文件/类名）由本票吸收。
- `Program` 是 `internal static class`，不构成插件 API 契约（`EmueraPluginExample` 零引用 `Program`，`InternalsVisibleTo` 只有 `Emuera.Tests`），类型变更无需对外兼容。
