# 路径:存档路径统一到 GamePath

Status: implemented

## 任务

存档路径族此前在字符串层面拼接：`VariableEvaluator.getSaveDataPath(int)` 用 `$"{SavDir}save{index:00}.sav"`、`getSaveDataPathV/C(int)` 用 `Program.DatDir + $"var_/chara_{index:00}.dat"`、`getSaveDataPathG()` 用 `SavDir + "global.sav"`、`Creator.Method.GetSaveDataPathText(int, string dir)` 用 `$"{dir}txt{index:00}.txt"`（`dir` 由调用方传 `Config.SavDir` 或 `Config.ForceSavDir`）、`GetSaveDataPathGraphics(int)` 用 `$"{Config.SavDir}img{index:0000}.png"`。

本票把这五处统一为 `GamePath` 产出，并让「存档目录」只有一个来源。完成标准：存档路径族（`save*.sav` / `var_*.dat` / `chara_*.dat` / `txt*.txt` / `img*.png` / `global.sav`）全部由 `GamePath` 工厂产出；`grep` 不再出现 `SavDir +`、`DatDir +`、`ForceSavDir +` 形式的字符串拼接；`dotnet build` 0 错误、`Emuera.Tests` 全绿。

## 方案

1. 依赖 02 把 `Config.SavDir` / `ForceSavDir` 变成 `GamePath`（`UseSaveFolder == false` 时为游戏目录自身，用 `GamePath.FromRoot` 表达）。删除 `Program.ExeDir`/`Program.DatDir` 会强制本票涉及的几行与 02 同 commit 改，故二者合并为一次提交；本票负责存档路径族本身的口径统一。
2. 索引版走 02 新增的 `GamePath.CombineName`（该形态调用点都是「字面量 + 已校验的整数索引」，解析失败即编程错误）：
    - `SavDir.CombineName($"save{index:00}.sav")` / `SavDir.CombineName("global.sav")`
    - `DatRoot.CombineName($"var_{index:00}.dat")` / `"chara_{index:00}.dat"`
    - `dir.CombineName($"txt{index:00}.txt")` / `SavDir.CombineName($"img{index:0000}.png")`
3. 名字版的两处（`TryResolveSaveFilePath`、`TryResolveSaveDataPath`）统一到 `SavDir`/`DatRoot` 上的 `TryResolveChild`：脚本给的名字可以是相对路径、可指向对应目录的子目录，但不能离开该目录。
4. `ForceSavDir` 与 `SavDir` 的差别只在基目录：不再为两者各写一套取文件逻辑，`GetSaveDataPathText` 的 `string dir` 参数改为 `GamePath dir`，调用方继续在 `Config.ForceSavDir`/`Config.SavDir` 之间选择。
5. `getSaveDataPathG()` 的基目录取自 `SavDir`，一并类型化。
6. 索引来自脚本的 `int` 且各入口已校验范围（`< 0 or > int.MaxValue` 直接失败），因此这些调用点不会失败；`CombineName` 的抛出只作为「不可能发生」的守卫。

## 曾考虑的替代方案

1. 索引版继续用字符串拼接：索引来自脚本 `int` 且已做范围校验，不存在越界风险，保持现状的收益是零改动。放弃原因是一致性——同一族路径一半由 `GamePath` 产出、一半是裸串，后人无法从类型判断哪个已校验；且 `GetSaveDataPathText` 的 `string dir` 参数正是「基目录靠字符串传递」的残留。
2. 在每个类里各放一个「按基目录取存档文件」的私有助手：02 已把该能力落在 `GamePath.CombineName` 上，再复制两份私有助手会让同一不变量出现在三处。

## 影响

- 行为变化（脚本可见）：名字版两个解析器统一为 `TryResolveChild`（可指向存档/数据目录的子目录，不能离开该目录）。`TryResolveSaveFilePath` 由此前「校验到游戏根」收紧为「校验到存档目录」；`TryResolveSaveDataPath` 则由 S2 的单段名口径放宽——带分隔符的名字重新可落到 `dat\` 的子目录，逃逸仍被拒绝。越界时各入口照旧静默返回（读取类先 `File.Exists`）；名字指向**不存在**的子目录时，写入会抛 `DirectoryNotFoundException`（`SaveChara` 的 catch 本就是注释掉的），与 S2 之前的字符串拼接行为一致。
- 其余无行为变化预期：索引版输入受控，产出路径与改动前同形（仅少了尾分隔符，而拼接点已全部改为显式 `Combine`）。
- 依赖 02 提供类型化的 `SavDir`（由 02 的 `GameRoot.CombineName("sav")` 产出，本票不再自行引入 `Program.SavRoot`），故设为前置；两者的改动因 `Program.ExeDir`/`DatDir` 的删除而必须在同一 commit 落地（见 02 的方案第 7 条）。
- 与 `02-root-typing.md` 共享 `Runtime/Config/Config.cs`、`Program.cs`。
- S2 已处理的 **字符串型**存档名中，`TryResolveSaveDataPath` 本不在本票范围，但因「入参不是明确单文件名时一律放宽到 `TryResolveChild`」的口径一并调整，使文件版与数据版同形。
- 未覆盖：`GetDatFiles`（`ENUMFILES`/`FIND_*`）仍只在 `dat\` 顶层按 `NamePattern` 枚举，落到子目录的数据文件不会被列出（S2 既有口径，本票不动）。
