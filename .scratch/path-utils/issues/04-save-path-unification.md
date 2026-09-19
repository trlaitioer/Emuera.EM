# 路径:存档路径统一到 GamePath

Status: needs-triage

Blocked by: 02

## 任务

存档路径族仍在字符串层面拼接。当前形态：

| 位置（文件 · 成员）                                                 | 现状                                                                                  |
|---------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| `VariableEvaluator.getSaveDataPath(int)`                            | `$"{Config.Config.SavDir}save{index:00}.sav"`                                         |
| `VariableEvaluator.getSaveDataPathV(int)` / `getSaveDataPathC(int)` | `Program.DatDir + $"var_{index:00}.dat"` / `"chara_{index:00}.dat"`                   |
| `VariableEvaluator.getSaveDataPathG()`                              | 全局存档（同样基于 `SavDir`/`ExeDir`）                                                |
| `Creator.Method.GetSaveDataPathText(int, string dir)`               | `$"{dir}txt{index:00}.txt"`，`dir` 由调用方传 `Config.SavDir` 或 `Config.ForceSavDir` |
| `Creator.Method.GetSaveDataPathGraphics(int)`                       | `$"{Config.SavDir}img{index:0000}.png"`                                               |

本票把这五处统一为 `GamePath` 产出，并让「存档目录」只有一个来源。完成标准：存档路径族（`save*.sav` / `var_*.dat` / `chara_*.dat` / `txt*.txt` / `img*.png` / `global.sav`）全部由
`GamePath` 工厂产出；`grep` 不再出现 `SavDir +`、`DatDir +`、`ForceSavDir +` 形式的字符串拼接；`dotnet build` 0 错误、`Emuera.Tests` 全绿。

## 方案

1. 依赖 02 把 `Config.SavDir` / `ForceSavDir` 变成 `GamePath`（`UseSaveFolder == false` 时为游戏目录自身，用 `GamePath.FromRoot` 表达）。
2. 索引版改为单段名解析，与 S2 已落地的 `TryResolveSaveDataPath` 同形：
    - `SavDir.TryResolveChildName($"save{index:00}.sav")`
    - `DatRoot.TryResolveChildName($"var_{index:00}.dat")` / `"chara_{index:00}.dat"`
    - `SavDir.TryResolveChildName($"txt{index:00}.txt")` / `"img{index:0000}.png"`
3. `ForceSavDir` 与 `SavDir` 的差别只在基目录，收敛成一个「按基目录取存档文件」的私有助手，`GetSaveDataPathText` 的 `string dir` 参数随之消失。
4. `getSaveDataPathG()` 的基目录同样取自 `SavDir`/游戏目录，一并类型化。
5. 索引来自脚本的 `int` 且已校验范围（`< 0 or > int.MaxValue` 直接失败），因此这些调用点不会失败；仍按 `Try*` 处理，失败时沿用各入口原有失败语义。

## 曾考虑的替代方案

1. 索引版继续用字符串拼接：索引来自脚本 `int` 且已做范围校验，不存在越界风险，保持现状的收益是零改动。放弃原因是一致性——同一族路径一半由 `GamePath` 产出、一半是裸串，后人无法从类型判断哪个已校验；且
   `GetSaveDataPathText` 的 `string dir` 参数正是「基目录靠字符串传递」的残留。

## 影响

- 无行为变化预期：索引版输入受控，产出路径与现状同形。
- 依赖 02 提供类型化的 `SavDir`/`DatRoot`；若 02 未完成，本票需要自行引入 `Program.SavRoot`，会造成重复，故设为前置。
- 与 `02-root-typing.md` 共享 `Runtime/Config/Config.cs`、`Program.cs`。
- S2 已处理的 **字符串型**存档名（`TryResolveSaveFilePath`、`TryResolveSaveDataPath`）不在本票范围内。
