# 路径:引擎与 UI 侧的前缀判断、相对化与剥前缀收敛

Status: needs-triage

## 任务

脚本可达入口已切到 `GamePath`（`../spec.md` 的 S2）。本票收敛 **非脚本来源**的同类写法：把「字符串前缀当路径前缀」「用 `Contains`/`Replace` 剥前缀」与「手工补分隔符再拼接」改为 `PurePath`
的段比较与相对化。范围与完成标准：

- `Config.getFiles`（`Runtime/Config/Config.cs`）
    - `dir.StartsWith(rootdir, StringComparison.OrdinalIgnoreCase)` + `dir[rootdir.Length..]` 切片 → `IsRelativeTo` / `RelativeTo`。
      现状之所以没出事，只是因为当前调用点传入的 `rootdir` 恰好带尾分隔符；一旦传入不带尾分隔符的目录，`C:\game2` 会被 `C:\game` 命中并截错。
    - `RelativePath += "\\"` 与 `!RelativePath.EndsWith('\\') && !RelativePath.EndsWith('/')` 的双查 → `PurePath.Combine`；`Path.Combine(RelativePath, Path.GetFileName(filepath))` →
      `PurePath.Combine`；`Path.GetFileName` → `Name`。
- `ErbLoader.LoadErbList`（`Runtime/Script/Loader/ErbLoader.cs`）：`fpath.StartsWith(Program.ErbDir, Config.Config.SCIgnoreCase)` → `IsRelativeTo`；
  `Path.GetRelativePath(Program.ErbDir, fpath)` → `RelativeTo`。顺带把比较口径从「脚本标识符大小写设置」回归路径语义的 `OrdinalIgnoreCase`。
- `EmueraConsole.OpenErrorFile`（`UI/Game/EmueraConsole.cs`）：`fname.Contains(Program.CsvDir)` + `fname.Replace(Program.CsvDir, "", ignoreCaseCmp)` / 同一模式对 `Program.ErbDir` →
  `IsRelativeTo` + `Combine`；同时清掉该函数里的单参 `Path.Combine(Program.ErbDir + fname)`（该重载不做拼接，只做 null 检查，纯误导）。
- `Process.getRawTextFormFilewithLine`（`Runtime/Script/Process.cs`）：`Program.ErbDir + position.Value.Filename` / `Program.CsvDir + position.Value.Filename` → `Combine`；需同时定义
  `ScriptPosition.Filename` 在 **分析模式**下是游戏目录外的绝对路径时如何取值（现有写法在该模式下本就拼错，靠 `OpenErrorFile` 的剥前缀掩盖）。
- `MainWindow`（`UI/Framework/Forms/MainWindow.cs`）两处等价于无操作的剥前缀：`ProcessCmdKey` 的 `string.Concat(Program.ErbDir, fname.AsSpan(Program.ErbDir.Length))` 与「ファイルを読み直す」菜单项的
  `Program.ErbDir + fname[Program.ErbDir.Length..]` —— 两者都原样返回 `fname`，删除分支。

完成标准：`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` 0 错误；`Emuera.Tests` 全绿；`Emuera/` 下不再存在把 `StartsWith`/`Contains` 用作路径前缀或包含性判断的调用点。

## 方案

1. 每处先把「根」表达成 `PurePath`：`Program.GameRoot` 已存在；`Config.getFiles` 的 `dir`/`rootdir` 由调用方保证为同一根下的绝对路径，直接 `PurePath.From` 或改用已类型化的
   `Program.ErbRoot`/`CsvRoot`（若 02 已完成，优先用它，避免重复解析）。
2. 前缀判断一律换成 `IsRelativeTo(root)`；需要相对串时用 `RelativeTo(root).ToString()`。`ENUMFILES` 已经在 S2 走过这条路，形态（`erb\a.erb`）保持不变。
3. `getRawTextFormFilewithLine` 单独定案：`ScriptPosition.Filename` 在分析模式携带的是命令行传入的原始路径，可能是游戏目录之外的绝对路径。倾向按 `PurePath` 的「可相对可绝对」表达它，取值时若
   `IsRooted` 则直接使用、否则 `Combine(ErbRoot)`；不要引入新的类型。
4. 无操作剥前缀的两处直接删分支，不保留「为了对称」的写法。

## 曾考虑的替代方案

1. 为 `ScriptPosition.Filename` 引入独立的「任意路径」类型：分析模式下它携带游戏目录之外的绝对路径，看似需要与 `GamePath` 区分的第二个类型。放弃原因是该字段只用于显示与定位、不参与写入，
   `PurePath` 的「可相对可绝对」已经覆盖它，引入类型只会增加转换面。

## 影响

- 行为变化：`Config.getFiles` 的相对路径在「`rootdir` 不带尾分隔符」时由错误截断变为正确结果；`ErbLoader` 的相对化不再受脚本大小写配置影响。
- 与 03 共享 `EmueraConsole.cs`（本票动 `OpenErrorFile`，03 不动该函数），不构成阻塞。
- 若 02 先完成，本票可直接使用 `Program.ErbRoot`/`CsvRoot`；否则先用 `PurePath.From(Program.ErbDir)`，02 完成后再收敛。
