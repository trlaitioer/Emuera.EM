# 游戏路径与文件名

`Program.GameRoot` 是游戏目录的唯一定义。启动时使用 `--ExeDir` 指定的目录，默认使用进程当前目录。`Program.SetDirPaths` 从游戏根派生资源、声音及数据目录。脚本路径按相应入口指定的根解析，调用方显式将根传入 `GamePath` 的解析方法。

- [`PurePath`](../../Emuera/Runtime/Utils/PathUtils/PurePath.cs) 表示经过词法规范化的相对或绝对路径。比较采用 `OrdinalIgnoreCase`，解析过程中保持与进程当前工作目录无关。
- [`GamePath`](../../Emuera/Runtime/Utils/PathUtils/GamePath.cs) 表示经词法校验、位于指定根内的绝对路径。通过这一类型的 `FullName` 访问文件系统。调用者需要区分任意相对路径、单段名称和已确定为绝对路径的输入。
- [`NamePattern`](../../Emuera/Runtime/Utils/PathUtils/NamePattern.cs) 允许文件枚举通配符 `*` 和 `?`，拒绝空模式、目录分隔符以及 `.` 和 `..`。

`GCREATEFROMFILE` 的绝对路径限于资源目录。第三个实参省略或为 `0` 时，相对路径以资源目录为基准。该实参非零时，相对路径以游戏根为基准。`EXISTSOUND` 以声音目录为基准。

`ENUMFILES` 将相对于游戏根的路径写入输出数组，例如 `erb\a.erb`。成功时返回实际写入的条数。路径被拒绝时，沿用入口自己的失败结果。`SAVETEXT` 或 `LOADTEXT` 的输入解析到游戏根本身时，分别返回 `0` 或空字符串。

`GamePath` 的包含性检查基于规范化后的路径文本。符号链接和 junction 的实际目标由文件系统解析。
