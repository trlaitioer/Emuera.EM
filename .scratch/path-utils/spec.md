# 路径处理统一：PurePath / GamePath

Date: 2026-09-20
Status: implemented

## 问题/背景

项目里"路径"以裸 `string` 流动，构造与校验各自为政：

- **目录根两套并存**：`Program.ExeDir/CsvDir/ErbDir/DatDir/DebugDir/ContentDir/SoundDir/FontDir`（`AppContext.BaseDirectory` 派生、带尾分隔符）与 `Runtime/Utils/Sys.cs` 的
  `AssemblyData.WorkingDir`/`ExeDir`（CWD / `Environment.ProcessPath` 派生，硬编码 `"\\"`）。`AssemblyData.ExeDir` 零引用。代码里实际存在三个"游戏目录"概念，没有任何一处定义哪个是权威。
- **构造写法至少 6 套**：`Path.Combine` + `Path.DirectorySeparatorChar`（3 文件）、单参 `Path.Combine` 包手工拼接（2 处，该重载不做拼接）、目录常量 + 硬编码 `"\\"`、目录常量 + 文件名（主力，约
  44 处）、`$"{dir}..."` 插值、直接交给 `DirectoryInfo`/`EnumerateFiles`。`Path.AltDirectorySeparatorChar` 全仓库零使用；`Path.GetDirectoryName` 的 3 处调用全部手工补 `"\\"`。
- **越界校验 5 套互不相关手法**：`Utils.GetValidPath`（删 `"..\\"` 的删除式过滤）、`Contains("../")`（只认正斜杠）、`StartsWith(Dir)`（字符串前缀）、`Path.IsPathRooted`（只决定加不加前缀）、
  `Path.GetInvalidFileNameChars`（返回值被丢弃）。13~15 个脚本可达入口中有 6 个零校验。

已实测确认的缺陷：

| 入口                                            | 问题                                                                                                                                               |
|-------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------|
| `OUTPUTLOG`                                     | `Contains("../")` 只认正斜杠；`StartsWith(Program.ExeDir)` 因 filename 恒被前置拼接而**永远为真**。`..\..\evil.log` 可任意深度写文件               |
| `SAVECHARA` / `SAVEVAR`                         | `CheckDatFilename` 的值被当独立语句丢弃；`LOADCHARA` / `LOADVAR` / `CHKCHARADATA` 连调用都没有。`..` 可逃逸至 `FileStream(FileMode.Create)` 覆盖写 |
| `Utils.GetValidPath`                            | 末尾裸 `..` 在 `Replace("..\\", "")` 后残留一个 `..` 段，落到 `ExeDir` 的父目录                                                                    |
| `ENUMFILES` / `FIND_CHARADATA` / `FIND_VARDATA` | 目录参数走 `GetValidPath`，但 `pattern` 完全不过滤；.NET 的 searchPattern 允许分隔符与多级 `..` → 任意深度枚举                                     |
| `EXISTSOUND`                                    | 基准是进程当前目录；`PLAYSOUND`/`PLAYBGM` 用 `Program.SoundDir`。`--ExeDir` 或异地启动时二者必然分叉                                               |
| `GCREATEFROMFILE`                               | 绝对路径直接使用；`isRelative != 0` 按 CWD 解析；否则拼 `ContentDir`——后两者可逃逸                                                                 |
| 存档名                                          | `save{s}.sav` 中 `s` 含 `..\` 可逃逸出游戏目录                                                                                                     |

两个前提事实：Windows 路径为 **词法解析**（`File.Exists(@"C:\g\nope\..\..\x")` 成立，不要求中间目录存在）；`Program` 静态构造里的 `Data/` 探测会被 `Main` 的 `SetDirPaths` 无条件覆盖，而
`ConfigData.configPath`、`JSONConfig._configFilePath`、`Lang.langDir` 都是 `static readonly` 字符串，谁先被触碰就固化谁看到的值。

## 整体方案

核心判断：问题不是"拼接风格不统一"，而是 **没有唯一的"解析 + 越界校验"出口**，且路径类型上无法区分"未验证的裸串"与"已证明在游戏目录内的路径"。

1. 新增 `Runtime/Utils/PathUtils/`，两个类型加两个辅助：
    - **`PurePath`** —— 词法路径值：统一分隔符、折叠 `.`/`..`， **不调用 `GetFullPath`、不碰 CWD**，因此可相对可绝对（`ENUMFILES` 返回值、`ScriptPosition.Filename` 这类相对形态可表示）。成员：
      `From`/`TryFrom`、`Name`/`NameWithoutExtension`/`Extension`/`Directory`、`Combine`、`ChangeExtension`、`IsRelativeTo`/`RelativeTo`/`TryRelativeTo`、基于 `OrdinalIgnoreCase` 的值语义相等（可直接作字典键）。
    - **`GamePath : PurePath`** —— 额外保证位于游戏目录内。 **构造器私有**，只能由 `TryResolve`（相对根解析）、`TryResolveName`（单段名）、`TryResolveAbsolute`（证明已有绝对路径）、
      `TryResolveChild`/`TryResolveChildName`（相对本实例）、`FromRoot`（受信 setup）产出，`ChangeExtension` 覆写后同样保持该保证。`FullName` 只挂在此类型上，使"做 IO 必须先拿到 `GamePath`"落在 API 形状上，而不是只靠签名约定。
    - **`PathReject`** —— 失败原因枚举（`Empty`/`InvalidChar`/`Rooted`/`NotRooted`/`MultiSegment`/`OutsideRoot`）。`bool + out + out reason` 的形状没有"可被静默丢弃的错误消息"，
      `CheckDatFilename` 那类失效模式无法复发。
    - **`NamePattern`** —— 搜索模式校验：允许 `*`/`?`，拒绝分隔符与 `.`/`..`；模式不是路径，不套 `GamePath`。
2. **游戏根单点**：`Program.GameRoot`（`PurePath`，根自身）+ `ContentRoot`/`SoundRoot`/`DatRoot`/`DefaultLogFile`（`GamePath`），在 `SetDirPaths` 构造。`GamePath` **不持有进程级静态根**
   ——避免全局状态与静态初始化顺序风险，根由调用方传入。
3. **脚本入口一律先解析再触碰文件系统**，拒绝时沿用各入口原有失败语义（返回 `0`/空串/既有对话框），不新增 `Lang` 文案。
4. 根目录的字符串形态暂时保留（`Program.*Dir` 另有 97 处 / 23 文件引用），逐步收敛。

待拆票：

- **非脚本来源的同类写法**：`Config.getFiles` 的字符串前缀切片、`ErbLoader.LoadErbList` 借用 `Config.Config.SCIgnoreCase` 做路径比较、`MainWindow` 两处等价于无操作的剥前缀、
  `EmueraConsole.OpenErrorFile` 的 `Contains`/`Replace` 剥前缀、`Process.getRawTextFormFilewithLine` 的 `ErbDir + Filename`。
- **根目录类型化**：`Program.*Dir`、`Config.SavDir`/`ForceSavDir`、`Lang.langDir`、`JSONConfig`/`ConfigData` 的静态路径常量；一并处理「游戏目录」唯一定义、`static readonly` 顺序风险、
  `Program` 静态构造里 `Data/` 分支的去留。
- **零散收敛**：`Path.GetDirectoryName(...) + "\\"` 3 处、`AssemblyData.ExeDir` 死常量、现已无引用的 `Program.SoundDir`。（单参 `Path.Combine` 在 S2 后仅剩 `EmueraConsole.OpenErrorFile`
  一处，归入上面第一项。）
- **索引版存档路径**（`$"{SavDir}save{n:00}.sav"`、`GetSaveDataPathText`/`GetSaveDataPathGraphics`）仍在字符串层面，未统一。

## 曾考虑的替代方案

1. 静态工具类（`TryResolve(string) → string?`）：改动最小，但路径仍是裸 `string`，校验靠"记得调用"，无法阻止"校验函数存在却被绕过/丢弃"的失效模式复发，只适合作为过渡。
2. 全量类型化替换所有 `string`：`ScriptPosition.Filename`（分析模式下就是游戏目录外的原始绝对路径）、文件对话框取值、`--ExeDir` 都属于 **合法的任意路径**，全量类型化需要再造 2~3
   个类型或放宽不变量，类型域大于不变量域。
3. 把 `Dir + name` 全量替换为 `Path.Combine`：`Path.Combine` 在第二参为根路径时直接返回第二参，会把现有"拼错即报错"静默放宽为"直接使用该绝对路径"。
4. 根目录保持 `string`：每次使用都要重新解析，且「目录常量必须以分隔符结尾」这条隐式契约无法消除——而该契约正是前缀判断与手工拼接能成立的前提。
5. `GamePath` 持有进程级静态根（`Initialize` 单例）：调用点更短，代价是全局状态与静态初始化顺序风险。
6. 目录与命名空间取名 `Runtime/Utils/Path`：实测在 `namespace MinorShift.Emuera.Runtime.Utils` 下会形成嵌套命名空间 `Path`，同命名空间文件里的 `Path.GetExtension` 报 CS0234；当下影响面仅
   `Sys.cs` 2 行，但会给该命名空间未来的新文件埋雷。
7. 用 `IsAbsolute` 表达"是否已限定"：`Path.IsPathRooted(@"C:foo")` 为真而 `Path.IsPathFullyQualified(@"C:foo")` 为假，拒绝输入必须用前者，故采用 `IsRooted`。

## 影响

- **行为变化（脚本可见）**：所有脚本可达路径不再能离开游戏目录；`GCREATEFROMFILE` 的绝对路径改为须落在 `resources\` 内、`isRelative` 基准由 CWD 改为游戏根；`EXISTSOUND` 基准由 CWD 改为 sound 目录；`ENUMFILES` / `FIND_*` 的
  `pattern` 含分隔符时不再枚举。越界输入按各入口原有失败语义处理。
- **兼容契约未动**：`ENUMFILES` 返回的 `erb\a.erb` 形态不变；`SAVETEXT`/`LOADTEXT` 的扩展名判断保持原有 `ToLower()`。
- **无插件 API 影响**：`Program` 是 `internal static class`。
- **验证**：`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` 0 错误、警告数与基线一致；`Emuera.Tests` 262 通过（基线 144 + 新增 118）。
- **未覆盖**：词法包含性判断无法拦截符号链接 / junction；`Utils.GetValidPath` 保留原签名，其"返回值可被丢弃"的形状仍在。
