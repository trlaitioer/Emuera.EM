# 路径:审查遗留问题(四处修复 + 一处伪回归核实)

id: path-utils-06
Status: implemented

## 任务

`05-review-regression-fixes` 结尾按「供后续拆票」挂起的 5 项,逐项核实后处理:

- 行为(脚本/配置可见):`ENUMFILES("")` 经核实为**伪回归**,不改代码(见方案 5);分析模式下「取报错源码行」与「打开编辑器」对相对路径口径不一致;`TextEditor` 配置只认反斜杠盘符。
- 类型不变量加固:`PurePath.Segments` 把内部数组暴露给同程序集,可强转改写;`GamePath.VerifyRoot` 用 `IsRooted` 使 drive-relative 根通过校验。

完成标准:四处修复;构建 0 错误、`Emuera.Tests` 全绿。伪变异覆盖范围:回退 `VerifyRoot` 判据、回退 `TextEditor` 旧解析(只有 `C:/…` 那一行)、回退 `TryResolvePositionFile` 的分析模式分支各有用例失败;`EnumFilesMethodTests` 两例是钉住既有契约的守卫(修复前后都通过),`Segments` 的返回类型改动无真值测试。

## 方案

1. 报错口径:`Program` 新增 `TryResolvePositionFile(ScriptPosition, out string)` 把「分析模式且非 `.csv` 则保留命令行原样,否则 `TryResolveScriptFile`」收在一处;`EmueraConsole.OpenErrorFile` 与 `Process.getRawTextFormFilewithLine` 改用它(后者保留 `.erb`/`.csv` 过滤与 `File.Exists`)。两个文件随之不再引用 `PathUtils`,删除其 `using`。
2. `TextEditor`:配置文件解析改为「整行第一个 `:` 之后全是值」,替掉「`tokens[2]` 以 `\` 开头才拼回」的 token 拼装;`loadConfig` 放宽为 `internal` 以便直接测(不经 `LoadConfig()`,避免 `SetConfig`/`CheckUpdate` 改写其它用例依赖的静态)。
3. `PurePath.Segments` 改 `internal ReadOnlySpan<string>`:唯一消费点写法不变;旧 `IReadOnlyList<string>` 包的就是同一数组,本就零分配,收益是**无法再强转回 `string[]`**。
4. `VerifyRoot` 判据由 `IsRooted` 改为 `IsPathFullyQualified`(新增 `PurePath.IsFullyQualified`),drive-relative 与 `\foo` 这类根在构造点即被拒,两例都有用例。
5. `ENUMFILES` 的记载修正:旧 `Utils.GetValidPath`(83a959af)用 `Path.GetPathRoot(path) != string.Empty` 判据,而 `Path.GetPathRoot("")` 在 .NET 上返回 `null`(不是 `""`),空名直接 `return null` —— 旧 `ENUMFILES("")` 本就是 `-1`,`"."` 经 `Path.Combine(ExeDir + ".")` 仍枚举游戏根。故不改代码,只修正 05 与 spec 的记载(见影响)。

## 曾考虑的替代方案

1. 空名当 `"."` 解析:会让 `ENUMFILES("")` 由 `-1` 变成枚举游戏根——这是新增行为而非恢复兼容(前提是伪回归),且与 spec 已记录的契约相反;放弃。
2. 空串在 `GamePath.TryResolveCore` 里当 `.`:一条规则覆盖全部 `TryResolve` 调用点,但 `GCREATEFROMFILE ""` 会从「拒绝」变成「解析到游戏目录自身」,后续 IO 可能抛未捕获异常;放弃。
3. 只把 `OpenErrorFile` 的 `keepAsIs` 复制进 `getRawTextFormFilewithLine`:同一规则留两份,下次改动会再次分叉;改为收进 `Program`。
4. `TextEditor` 只把拼回条件由 `\` 扩成 `\` 或 `/`:多冒号与 UNC 仍要靠 token 拼装的怪逻辑维持;改为按首个 `:` 切值。
5. `Segments` 收回 `private`、校验方法移入 `PurePath`:暴露面更小,但把「段必须是可用文件名」的判据从 `GamePath` 挪进词法类型,改动面更大;保留判据在 `GamePath`,只收窄返回类型。
6. `VerifyRoot` 改用 `Path.GetPathRoot(...) != ""`:与 `IsRooted` 同源,drive-relative 仍通过;放弃。

## 影响

- 行为变化(脚本可见):无。`ENUMFILES("")` 保持 `-1`,`"."` 仍枚举游戏根,二者都是迁移前的行为。
- 行为变化(报错显示):分析模式下以相对路径分析的文件(如 `--analyze sub\a.erb`),报错行改为按命令行原样读该文件,不再按 `erb\` 解析(此前会读到 `erb\` 下同名文件的行);非分析模式与 csv 位置不变。
- 行为变化(配置):旧实现对「第一个 `:` 之后不是 `\` 开头、且后面还有 `:`」的值一律截断(`C:/tools/ed.exe`→`C`、`C:/a:b`→`C:b`、`abc\def:ghi`→`abc\def`),新解析按首个 `:` 切值,这些形态都拿到完整值;此前就正确的形态(`C:\…`、UNC、`C:\tools\ed:itor.exe`、首尾空白)不变——`ConfigItem.TryParse` 自带 `Trim()`,旧实现的 `tokens[3..]` 循环已覆盖后者。同族的 `EditorArgument` 仍按 `tokens[1]` 取值,含 `:` 的参数照样被截断(既有缺陷,不在本票范围)。
- 不变量:非完全限定根(drive-relative、`\foo`)在 `GamePath` 构造点即抛 `ArgumentException`,文案同步改为 fully qualified;生产唯一构造点 `SetDirPaths` 走 `GetFullPath`,不受影响。
- 插件可见面:`PurePath`/`GamePath` 是 `public` 类型,`EmueraPluginExample` 引用了 `Emuera`,故插件可达;本次只新增一个 `public` 属性 `PurePath.IsFullyQualified`,不改动任何插件 API 签名,可见差异仅是 `VerifyRoot` 判据收紧。
- 记载修正:`05` 与 `spec.md` 的「`ENUMFILES("")` 由枚举游戏根变为 -1」前提不成立,已按实测改写。
