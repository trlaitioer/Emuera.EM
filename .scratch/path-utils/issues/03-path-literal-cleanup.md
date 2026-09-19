# 路径:剩余硬编码分隔符与死代码清理

Status: needs-triage

Blocked by: 02

## 任务

S2 之后剩余的零散项。范围与完成标准：

- `Path.GetDirectoryName(...) + "\\"` 三处，一律改为 `PurePath.Directory` / `Combine`：
    - `AppContents.LoadContents`（`UI/Game/Image/AppContents.cs`）：`string directory = Path.GetDirectoryName(path) + "\\";` 之后用于拼动画帧名。
    - `ConstantData.loadDataTo`（`Runtime/Script/Data/ConstantData.cs`）：`Path.GetDirectoryName(csvPath) + "\\" + Path.GetFileNameWithoutExtension(csvPath) + ".als"` 推导别名文件路径。
    - `Runtime/Utils/Sys.cs` 的 `AssemblyData.ExeDir`（下一条）。
- 删除 `AssemblyData.ExeDir`（`Runtime/Utils/Sys.cs`）：声明为 `Path.GetDirectoryName(ExePath) + "\\"`， **全仓库零引用**，且与 `Program.ExeDir` 重复语义。同时处理其同源的 `data/`
  注释与文件/类名不一致问题（后者若已在 02 定案，此处只执行）。
- 删除 `Program.SoundDir`（`Program.cs`）：S2 把 `PLAYSOUND`/`PLAYBGM`/`EXISTSOUND` 全部切到 `Program.SoundRoot` 后，该字符串属性已无任何引用。
- 顺带核对：`Path.AltDirectorySeparatorChar` 全仓库零使用（无需动作，仅确认不再新增）；`Path.DirectorySeparatorChar` 的使用点收敛到根目录构造处。

完成标准：`Emuera/` 下不再出现 `Path.GetDirectoryName(...) + "\\"` 形式；`grep AssemblyData.ExeDir` 与 `grep Program.SoundDir` 均无结果（含注释）；`dotnet build` 0 错误、`Emuera.Tests`
全绿。

## 方案

1. `AppContents.LoadContents`：`PurePath.From(path).Directory` 取出父目录，帧名用 `Combine`；该处 `path` 来自 `Directory.EnumerateFiles(Program.ContentDir, ...)`，本身就在游戏目录内，不需要
   `GamePath`。
2. `ConstantData.loadDataTo`：`PurePath.From(csvPath)` 的 `Directory` + `NameWithoutExtension` + `".als"` 组成同级路径；`csvPath` 由调用方给出（当前是 `Program.CsvDir` 下的文件），不需要越界校验。
3. 两处删除都只需移除声明与 `SetDirPaths`/静态构造里的赋值；若 `Program.SoundDir` 的删除与 02 的类型化同时进行，以 02 的形态为准。

## 曾考虑的替代方案

1. 保留 `AssemblyData.ExeDir` 作为「exe 所在目录」语义的备用：`Program.ExeDir` 现在是 CWD（或 `--ExeDir`）派生，二者语义确实不同。放弃原因是该成员自声明起零引用，当前没有任何调用点需要 exe
   目录；若将来需要，应从 `Environment.ProcessPath` 重新引入并在 `Program` 中命名，而不是留一个同名的死常量。

## 影响

- 纯清理，无行为变化。
- 与 02 共享 `Program.cs`、`Runtime/Utils/Sys.cs`，因此以 02 为前置，避免同文件重复改动。
- 本票 **不含**单参 `Path.Combine` 的清理：S2 已消除 `Utils.GetValidPath` 中的那处，现存唯一一处位于 `EmueraConsole.OpenErrorFile`，属 `01-engine-path-prefix.md` 范围。
