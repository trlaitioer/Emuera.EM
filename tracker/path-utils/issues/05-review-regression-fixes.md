---
id: path-utils-05
status: implemented
---

# 路径:审查发现的三处入口回归修复

## 任务

`path-utils` 四票落地后,对 `83a959af..401f5b99` 做 bug 审查(只查 bug,不设 spec 轴),发现三处中危回归:

| 入口 | 问题 |
| --- | --- |
| `SAVETEXT` | 名字词法折叠成游戏根(`"."`、`"erb\.."`、`"..\<游戏目录名>"`)时,`GamePath.ChangeExtension(".txt")` 的越界校验抛 `ArgumentException`;抛点在 `try` 之外,异常直达 `Process.Run` 顶层 catch → 按「予期しないエラー」中断脚本,而入口契约是返回 0 |
| resources 下 csv 的图片名 | `dir + arg2` 改写成 `dir.Combine(arg2)` 后,`PurePath.Combine` 沿用 `Path.Combine`「第二参为根路径则直接返回第二参」→ csv 文本可让 `Utils.LoadImage` 读到游戏目录外(含 UNC)的图片,并以该绝对路径作 `resourceDic` 键 |
| `Rikaichan` 词典设置 | 旧 `Path.Combine(Program.ExeDir, Config.RikaiFilename)` 对 rooted 参数原样生效;改用 `TryResolveChild` 后一律拒 rooted → 既有绝对路径配置弹 not found 并禁用 Rikaichan |

完成标准:三处修复;新增单测能在修复前的实现上失败(经伪变异实测);构建 0 错误、`Emuera.Tests` 全绿。

## 方案

1. `SAVETEXT`:按 `GamePath` 既有的 Try/throw 成对风格补 `TryChangeExtension`(与 `CombineName`/`TryResolveChildName` 一致),`ChangeExtension` 覆写改为委托它并在失败时抛原异常;SaveText 名字分支改用 Try 版,失败返回 0。
2. resources 图片名:新增 `AppContents.ResolveResourcePath(PurePath resourceCsvDirectory, string imageName)`,以 `Program.ContentRoot` 为界解析(rooted 与越出 resources 都告警跳过);`sub\x.png`、`..\b.png`(仍在 resources 内)保持可用。
3. `Rikaichan`:新增 `TryResolveRikaiFile`,rooted 走 `GamePath.TryResolveAbsolute(Program.GameRoot, …)`,否则 `GameRoot.TryResolveChild`;目录内绝对路径恢复,目录外仍拒(`.ind` 因此不会写到游戏目录外)。

### 实施现状

- 构建 0 错误、83 个既有警告;`Emuera.Tests` 278 通过 / 0 失败(262 + 新增 16)。
- 新增 16 例中 7 例经伪变异实测为真回归测试(回退实现即失败:SaveText 1、`TryChangeExtension` 1、resources 图片名 4、Rikaichan 根内绝对路径 1)。
- 两轮独立 review(对抗性 review + `.temp/` 隔离工程边界穷举):未发现修复引入的新 bug;同类根因全仓排查无漏改点(`Path.Combine` 30 处第二参均为字面量、`PurePath.Combine` 3 处均已守、`CombineName` 9 处无脚本/磁盘来源);路径型配置仅 `RikaiFilename` 一处。

## 曾考虑的替代方案

1. `SAVETEXT` 就地加 `textFile.Equals(Program.GameRoot)` 守卫:只堵当前触发路径,且调用点要知道 `ChangeExtension` 的失败模式;改为把不变量放进类型(Try 版 API)。
2. 让 `TryResolveCore` 的非单段分支也拒绝「等于基准目录」:会连带砍掉 `ENUMFILES "."`(`TryResolve` 允许返回根是它的既有语义),放弃。
3. resources 图片名只拒 rooted、不收严 `..`:放弃原因是「图片资源以 `resources\` 为界」这条不变量应一处定义。
4. `Rikaichan` 完全恢复旧语义(rooted 不限目录、他盘/UNC 也读):兼容最好,但会重新打开「引擎在游戏目录外读文件、并在词典旁写 `.ind`」;取「目录内允许、目录外拒绝」。
5. 删掉 `ResolveResourcePath` 的空名守卫(唯一调用点已保证非空):保留守卫作 fail-closed,配套删掉 2 例测不可达分支的用例。

## 影响

- 行为变化(脚本可见):`SAVETEXT "x","."`/`"erb\.."`/`"..\<游戏目录名>"` 由「中断脚本」变为返回 0(迁移前经 `Utils.GetValidPath` 的删除式过滤落成游戏目录内的怪名并返回 1,如 `"."` → `<游戏目录>\.txt`);resources csv 的 rooted 图片名与越出 resources 的名字由「可加载」变为失败告警(受影响的是 `\a.png`、`..\..\x.png` 这类此前靠 Win32 路径归一化生效的写法)。
- 行为变化(配置可见):`Rikai- Dictionary Filename` 恢复接受游戏目录内的绝对路径;目录外绝对路径仍不可用——该条是迁移期引入的既有回归,本票按「不得离开游戏目录」的口径保留。
- 审查同时发现,当时留待后续拆票(均已由 06 处理,下列描述为处理前状态):`ENUMFILES("")` 在本次改动前后都是 -1(`"."` 仍可用)——原记「由枚举游戏根变为 -1」不成立,`Path.GetPathRoot("")` 在 .NET 上返回 `null`,空名没进旧 `GetValidPath` 的拼接分支;`PurePath.Segments` 返回内部数组(`internal`,同程序集可强转改写);`GamePath.VerifyRoot` 用 `IsRooted` 使 drive-relative 根通过校验但子路径必失败(生产路径经 `GetFullPath` 归一,不可达);`Process.getRawTextFormFilewithLine` 与 `EmueraConsole.OpenErrorFile` 对分析模式相对路径的口径不一致(非本次引入,仅影响报错时取源码行);`ConfigData` 的 `TextEditor` 配置只认反斜杠路径(既有,非本次引入)。
