# 路径:根目录类型化与「游戏目录」唯一定义

Status: needs-triage

## 任务

代码里实际存在三个「游戏目录」概念，没有任何一处定义哪个是权威；目录常量又以「必须以分隔符结尾」的隐式契约在字符串层面流转，前缀判断与手工拼接都建立在这个前提上。本票把根目录类型化并定下唯一定义。范围与完成标准：

- **定下权威定义**：`Program.GameRoot`（`PurePath`）即游戏目录；`ExeDir` 是它的字符串别名，不得再有第二个来源。
- `Program.ExeDir` / `CsvDir` / `ErbDir` / `DebugDir` / `DatDir` / `ContentDir` / `SoundDir` / `FontDir`（`Program.cs`）由 `string`（带尾分隔符）改为 `PurePath`/`GamePath`。
  `SetDirPaths` 里 `Path.Combine(...) + Path.DirectorySeparatorChar` 的写法随之消失。
- **处理引用面**：`Emuera/` 下 `Program.*Dir` 的有效引用为 **97 行 / 23 文件**（不含 `Program.cs` 自身、不含注释行），集中在 `Runtime/Script/Process.cs`、`UI/Game/EmueraConsole.cs`、
  `Runtime/Config/Config.cs`、`UI/Game/EmueraConsole.Print.cs`、`VariableEvaluator.cs`、`UI/Framework/Forms/MainWindow.cs`、`ConfigData.cs` 等。
- `Config.SavDir` / `ForceSavDir`（`Runtime/Config/Config.cs` 的 `SetConfig`）→ `GamePath`；注意 `UseSaveFolder == false` 时 `SavDir` 等于游戏目录 **自身**，需用根自身的表示方式而非子目录。
- 静态路径常量改为延迟求值，消除静态初始化顺序风险：`Lang.langDir`（`Runtime/Utils/EvilMask/Lang.cs`）、`JSONConfig._configFilePath`（`Runtime/Config/JSON/JSONConfig.cs`）、
  `ConfigData.configPath` / `configdebugPath`（`Runtime/Config/ConfigData.cs`）。这些是 `static readonly` 字符串，在类型首次被触碰时求值；若触碰发生在 `Program` 静态构造与 `Main` 的
  `SetDirPaths` 之间，就会被固化成 `AppContext.BaseDirectory`/`Data` 的值。
- `Runtime/Utils/Sys.cs` 的 `AssemblyData.WorkingDir` 类型化；同时修正其 XML 注释（现照抄了 `ExeDir` 的「実行ファイルのディレクトリ」，实际是 CWD），并定案文件 `Sys.cs` 与类名
  `AssemblyData` 不一致是否改名。
- **定案 `Data/` 分支**：`Program` 静态构造里的 `AppContext.BaseDirectory` + `Data/erb` 探测会被 `Main` 的 `SetDirPaths` 无条件覆盖，仓库内也没有任何
  launcher/csproj/文档支持该布局。二者只能留一个，并写明理由。

完成标准：`Program.*Dir` 不再有「必须以分隔符结尾」的约定（无任何调用点依赖它）；`Path.DirectorySeparatorChar` 只出现在根目录构造处或不再出现；静态路径常量不再有初始化顺序敏感的风险；
`Emuera.Tests` 全绿。

## 方案

1. `Program` 保留 `GameRoot`（`PurePath`，根自身）+ `ContentRoot`/`SoundRoot`/`DatRoot`（`GamePath`，S2 已建），补 `ErbRoot`/`CsvRoot`/`DebugRoot`/`FontRoot`（`GamePath`），统一由现成的私有
   `RootChild` 构造。
2. 逐个消费方替换：`File.*`/`Directory.*` 调用取 `.FullName`；拼子路径改用 `Combine`/`TryResolveChild`；`Program.XxxDir + "name"` → `XxxRoot.Combine("name")`。这一步是纯机械替换，建议按文件分批提交以便单独
   review。
3. `SavDir` 在 `UseSaveFolder == false` 时用 `GamePath.FromRoot(GameRoot)` 表达根自身；`UseSaveFolder == true` 时用 `RootChild(GameRoot, "sav")`。
4. 静态常量改为属性（`private static PurePath ConfigPath => Program.GameRoot.Combine("setting.json")`），把求值推迟到首次使用；`Lang.langDir`
   同理。若认为每次访问都重新构造不可接受，再引入显式的一次性初始化，并保留「未初始化即抛」。
5. `Data/` 分支：优先删除（它从未生效且无配套布局），确需保留则在 `SetDirPaths` 里显式表达，而不是留在静态构造中。

## 曾考虑的替代方案

1. 停在 S2 的过渡态（`Program.*Dir` 保持字符串，只额外持有 `PurePath` 视图）：改动面从 97 行降到 0，但「目录常量必须以分隔符结尾」的契约继续存在，前缀判断与手工拼接的前提不消失，静态路径常量的顺序风险也不受影响。放弃原因是本票的目标正是消除这两者。
2. 让 `GamePath` 持有进程级静态根（`Initialize` 单例）以缩短调用点：引入全局状态，并把风险从「静态字符串常量」搬成「静态路径常量」，与消除静态初始化顺序风险的目的一致相反。

## 影响

- 这是整个 spec 中改动面最大的一票（97 行 / 23 文件）。它与 03、04 共享 `Program.cs`/`Sys.cs`/`Config.cs`，因此 03、04 均以本票为前置。
- `Program` 是 `internal static class`，不构成插件 API 契约，类型变更无需对外兼容。
- 无行为变化预期：`PurePath` 已保证与现拼接结果同形（尾分隔符是唯一差异，而所有消费方都在拼子路径，不依赖它）。
- 完成后「路径常量必须是带尾分隔符的字符串」这条前提消失，01 的前缀判断改造会更直接。
