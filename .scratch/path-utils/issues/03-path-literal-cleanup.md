# 路径:剩余硬编码分隔符与死代码清理

Status: ready-for-human

## 任务

S2 之后剩余的零散项。范围与完成标准：

- `Path.GetDirectoryName(...) + "\\"` 两处，一律改为 `PurePath.Directory` / `Combine`：
    - `AppContents.LoadContents`（`UI/Game/Image/AppContents.cs`）：`string directory = Path.GetDirectoryName(path) + "\\";` 之后用于拼动画帧名。
    - `ConstantData.loadDataTo`（`Runtime/Script/Data/ConstantData.cs`）：`Path.GetDirectoryName(csvPath) + "\\" + Path.GetFileNameWithoutExtension(csvPath) + ".als"` 推导别名文件路径。
- 顺带核对：`Path.AltDirectorySeparatorChar` 除 `PurePath`/`NamePattern` 外零使用；`Path.DirectorySeparatorChar` 的使用点收敛到根目录构造处。

## 方案

1. `AppContents.LoadContents`：`PurePath.From(path).Directory` 取出父目录传给 `CreateFromCsv`（参数由 `string` 改为 `PurePath`），帧名 `dir.Combine(arg2)`；该处 `path` 来自 `Directory.EnumerateFiles(Program.ContentRoot, ...)`，本身就在游戏目录内，不需要 `GamePath`。
2. `ConstantData.loadDataTo`：`PurePath.From(csvPath).ChangeExtension(".als")`；`csvPath` 由调用方给出（当前是 `Program.CsvRoot` 下的文件），不需要越界校验。

## 影响

- 纯清理，无行为变化。
- 原任务中的删除 `AssemblyData.ExeDir`、删除 `Program.SoundDir`、定案 `Sys.cs` 文件/类名三项已由 `02-root-typing.md` 吸收（它们与根类型化同文件、同一次改动）；本票自身只剩上述两处，并作为根类型化之后的独立提交落地。
- 本票 **不含**单参 `Path.Combine` 的清理：S2 已消除 `Utils.GetValidPath` 中的那处，现存唯一一处位于 `EmueraConsole.OpenErrorFile`，属 `01-engine-path-prefix.md` 范围。
