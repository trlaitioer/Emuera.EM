# 性能:解析并行化

Status: implemented

## 任务

ERB 加载解析目前串行(已评估、未实施):`Preload` 已并行(`Preload.Load` 的 `AsParallel().ForAll`);`LoadErbDir` 已按文件串行 `await Task.Run(() => loadErb(...))`(文件级并行外壳、文件内串行);`setLabelsArg` 与 `ParseScript` 整体仍是单线程。并行前需先处理 `Depth` 调用图发现顺序,以及下方「共享状态清点」列出的并发点。

原记「同名裁决需携带每个函数的来源文件及其文件列表顺序,该基础设施已存在」有误,见「同名裁决」——`FileIndex` 已实现该语义,无需额外携带。

准入条件(先于本票实施):在真实 ERA 语料上量出装载各阶段耗时占比,证实 `setLabelsArg` / `ParseScript` 确有可并行化的收益空间;否则本票按 issues/03 的先例不做。测量口径见「验证依据」。**该条件已满足,结论见「测量结论」——收益确认存在且集中在 `ParseScript`。**

完成标准:构建通过 + 全量测试套件 + 多份真实 ERA 语料上的装载耗时对比,以及「共享状态清点」各项的消除或保护。**实测结果**:构建 0 错误;测试 144/144(含本票新增的 `StBarTests` 4 例与 `StringMeasureTests` 中「非属主线程度量一致」2 例);4 份语料的并行/串行对比见「测量结论」——**`ParseScript` 单项在 4 份语料上均变快,但端到端并非每份都不劣于串行**(A 慢 75ms、C 基本持平,收益集中在 B/D),详见该节;共享状态逐项处置见表。同名裁决已定为「不新增开关、保留现有行为」(见「同名裁决」)。待人工验收后置 `implemented`。

## 方案

### 测量结论

4 份真实 ERA 语料、`Release-NAudio`、同一构建下测得。全部为 `LoadErbDir` 的 wall time 分解,单位为 ms。**串行基线**(并行化前的构建)与**并行结果**分列:

| 游戏 | ERB 文件数 | 阶段 | Enum | Files work | FileLoopWall | SetLabelsArg | **ParseScript** | 已知小计 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A | 2652 | 串行 | 41 | 230 | 1213 | 43 | **632** | 2159 |
| A | 2652 | 并行 | 51 | 259 | 1324 | 77 | **523** | 2234 |
| B | 822 | 串行 | 7 | 273 | 1303 | 13 | **2065** | 3661 |
| B | 822 | 并行 | 7 | 324 | 1465 | 13 | **1364** | 3173 |
| C | 1802 | 串行 | 28 | 143 | 1218 | 22 | **953** | 2364 |
| C | 1802 | 并行 | 21 | 140 | 1362 | 25 | **804** | 2352 |
| D | 3549 | 串行 | 43 | 958 | 5459 | 81 | **8875** | 15416 |
| D | 3549 | 并行 | 34 | 993 | 5459 | 83 | **3377** | 9946 |
| D(关 `DisplayReport`) | 3549 | 串行 | 35 | 1136 | 2215 | 82 | **8269** | 11737 |
| D(关 `DisplayReport`) | 3549 | 并行 | 42 | 1107 | 2215 | 82 | **3315** | 6761 |

「已知小计」为各阶段埋点之和,不含 `Preload` 等未埋点的零星等待,故略小于 `time.log` 的 `Proc:Init:ERB:End − Start`。

- **收益已兑现**:`ParseScript` 并行加速比 A 约 1.2×、B 约 1.5×、C 约 1.2×、D 约 2.5–2.6×。D 关报告口径的 ERB 段已知小计从 11737ms 降到 6761ms;`Proc:Init:ERB:End` 从 16801ms 降到 11249ms(省约 5.6s),`Init:End` 从 20889ms 降到 15342ms(省约 5.5s)。加速比未达核数上限,符合 Amdahl 预期——`Enum`/`FileLoopWall`/`SetLabelsArg` 未并行,其中 `FileLoopWall` 本身即占 ERB 段的一半。
- **结果一致性**:并行后各语料的 `ErbStage:Labels`(A 3146 / B 1697 / C 3052 / D 5290)与串行一致,实机装载无错误。
- **「不劣于串行」的准确范围**:`ParseScript` 单项在 4 份语料上均变快;但**端到端已知小计并非每份都更优**——A 为 2234 vs 串行 2159(**慢 75ms**),C 仅快 12ms,收益集中在 B(488ms)与 D(5470ms)。原因是并行本身有调度开销,而 A/C 的 `ParseScript` 基数本就小(632/953ms)。故本票的收益应按语料规模表述,不能笼统写成「全部不劣于串行」。
- **`FileLoopWall` 未并行,是关掉报告口径下的次大项**:D 并行后 2215ms(关 `DisplayReport`),仍小于 `ParseScript` 的 3315ms;开启 `DisplayReport` 时为 5459ms(其中逐文件输出约 3244ms,即 5459−2215),此时才成为最大项。该段值得作为后续优化的独立目标。
- **解析期 UI 调用是主要风险源**:实机验证时 `CUSTOMDRAWLINE` 因解析期 GDI+ 文本测量崩溃(见「共享状态清点」)。今后排查同类问题应优先检查 `StringMeasure` / `FontFactory` / `TextRenderer` 等 GDI+ 入口是否在解析路径上被触达。
- **`Enum` / `SetLabelsArg` 可忽略**:分别 ≤51ms / ≤83ms,不构成目标。
- **`Files`(逐行词法)不是目标**:即使全并行,D 也只省约 0.5s,而其共享状态(`isOnlyEvent`、`output.Print*`)最密集。

### 三阶段拆分的收益条件

三阶段管线（解析签名 → 确定 label → 解析函数体）与并行化是**两个独立的问题**，本票只对前者作如下界定：

- **并行化本身不需要拆分**。`ParseScript` 内的 per-function 循环今天就能按 `Depth` 层并行（边界见下节）；`setLabelsArg` + `SortLabels` 已在 `ParseScript` 之前完成，label 确定先于 body 解析这一前提本就成立。
- **拆分的收益来自 `tl/` 的函数级覆盖**（见 `.scratch/multilingual-translation/spec.md` §2.2/§2.5），三条都指向同一结构：
  1. 覆盖表必须在加载原 `.erb` **之前**建成（§2.2 主机制为「跳过」）；
  2. 签名校验（§2.5）必须**解析被覆盖原函数的头部**（`@NAME,ARGS` + `#FUNCTION`/`#DIM` 等属性行）才能与覆盖版比对；
  3. 阶段 2 确定覆盖名单后，阶段 3 **无需解析被覆盖函数的函数体**——这是对 `ParseScript` 的直接节省，量级即本票测得的 39%–61%。
- 与本票测量的关系：上面第 3 条的节省只有在 `tl/` 生效时才存在；不装 `tl/` 时三阶段与现状等价。因此「拆分是否值得做」由 `tl/` 的需求驱动，而**拆分产出的 per-function 解析单元（含头部/函数体边界）恰好就是并行化需要的工作单元**——两者同批实施可避免把 `ParseScript` 重构两遍。

### 并行边界:按 `Depth` 分层并行,不能全局并行

`Depth` 与错误传播的耦合决定了并行的边界(详见「Depth 的发现顺序」):

- **只能在单个 `Depth` 层内并行**,层与层之间保持原有顺序。
- **`useCallForm` 采认后的最后一次 pass 必须串行**,且在所有深度 pass 完成之后——它依赖全量 `Depth` 判定,并沿用同一错误传播链。
- 跨层必须串行,理由有二:其一,`setJumpTo` 会把新发现的被调函数标为 `Depth = 当前层 + 1`,若把多层一起投出去,未采番(`Depth < 0`)的函数会被 `setJumpTo` 的 `-2` 归一化处理,导致其后所有层号错位;其二,`ParseFunctionWithCatch` 在 `setJumpTo` 中写/读 `call.TopLabel.IsError`,跨层并行会让浅层函数在被调函数错误就位前就读到 `false`,错误不传播。
- **层内并行的确定性已补回**:同层互调(`x`、`y` 同为第 `k` 层),串行的解析顺序来自 `labelList` 的枚举顺序(`labelDic.GetAllLabels` 遍历 `labelAtDic`,即**函数名首次被定义的位置**顺序),而非 `Depth`;因此存在「调用者先于被调者解析」的情形,此时调用点读到的被调者 `IsError` 仍为 `false`——串行与并行在此都不确定。现已在 CALL 系指令的 `SetJumpTo` 中把已解析的静态调用边(含调用实参)交给 `ParserMediator.CallEdgeRecorder`,并在**每层 `Parallel.ForEach` 结束后、下一层解析之前**重判这些边:该层 `IsError` 已全部定稿,读取安全,而写入落在调用点行、其属主 worker 已结束,故可在并行区内进行。重判**不照抄结果、而是复刻串行分支**:先看被调者 `IsError`,判定为错误时**重跑 `ConvertArg`**——参数本身有问题时按串行同一分支 `Warn`(因此告警照旧进入 `warningList`),只有「函数体解析失败」这类被调者自身出错的情形才让调用点携带被调者的 `ErrMes`。这样调用点的诊断文案与是否进告警列表都与解析顺序无关。`CallFunc` 不回填:`Process.ScriptProc` 遇错误行即在进入指令前抛 `CodeEE`,解析出的调用不会被读到。归约放在层循环内而非最后统一处理,是因为下一层的 `SetJumpTo` 必须看到修正后的 `IsError`。

### 并行方案

在 `ParseScript` 内按 `Depth` 层并行化解函数体解析。若同期实施三阶段拆分（见上「三阶段拆分的收益条件」），则改为在「解析函数体」阶段并行，届时被 `tl/` 覆盖的函数本就不进入该阶段。`GetFunctionMethod` 走 `labelDic.Initialized` 分支解析 `#FUNCTION` 函数是可行的:`LoadErbDir`/`LoadErbList` 均在 `ParseScript` 之前置 `labelDic.Initialized = true`。

### 与其他票的衔接

- `dotnet-modernization`:装载循环内泵消息的 `Application.DoEvents()`(`ErhLoader.analyzeSharpDimLines` / `ErbLoader.ParseFunctionWithCatch`)允许界面操作在装载中途重入。该调用位于 `ParseScript` 的 catch 块内,而 `ParseScript` 本身已由 `Task.Run` 执行,即当前已存在「后台线程调用 UI 组件」的问题;并行后更是硬阻断,**必须先从并行区域移除**(或改为明确的取消机制),不能与其他共享状态一并处理。
- `multilingual-translation`:该 spec §2.5 的覆盖裁决与签名校验挂在「阶段 2」,并承诺「先做串行等价实现、迁移时搬入管线」。三阶段拆分的主要驱动力来自该票(见「三阶段拆分的收益条件」),故两票的先后应协调:`tl/` 若先落地,先按串行等价实现,后续搬入管线;本票若先落地,应把「签名/函数体边界」这一 per-function 解析单元一并抽出,`tl/` 直接复用,避免重构两遍。
- `issues/03`:解析期标识符驻留(span 键查重复用 AST 常驻字符串)的评估延至本票实施时一并处理,并发安全约束见其「唯一有实际空间的方向」一节。

### 共享状态清点

并行化实施后各项的处置如下。标 **硬阻断** 的项无法在线程池线程上成立,必须先改掉。

| 项 | 处置 |
| --- | --- |
| `VariableLocal.localVarTokens` | 已加 `localVarTokensLock`;`Add`/`Resize`/`Clear` 均在锁内,并把「存在性检查 + 生成」合并为 `GetNewVariableTokenIfAbsent` 的单一临界区(`IdentifierDictionary.GetVariableToken` 改调它),避免同名二次生成。 |
| `Application.DoEvents` | 已从 `ParseFunctionWithCatch` 的 catch 块移除。该调用本就运行在 `Task.Run` 包装的线程池线程上(既存缺陷),移除无实际回归;`ErhLoader.analyzeSharpDimLines` 中的三处未动(不在本票并行区内)。 |
| `SystemSounds.Hand.Play` | 已从并行区移除。`ParserMediator.NotifyParseError()` 在运行期立即发声、解析期只计数(`DeferParseErrorSound` 由 `ParseScript` 用 try/finally 包住整个方法体),全部 pass 结束后 `TakeParseErrorCount()` 鸣一次。注意该调用仍在 `ParseScript` 所在线程池线程上——这是保持原行为(原本也在池线程上)的选择。 |
| `StringMeasure.GetDisplayLength`(GDI+ 文本测量) | **硬阻断**。由 `CUSTOMDRAWLINE` 暴露:`CUSTOMDRAWLINE_Instruction.CreateArgument` 原在解析期调用 `EmueraConsole.getStBar()`,后者用 `StringMeasure.GetDisplayLength` → `TextRenderer.MeasureTextInternal` 做 GDI+ 测量,并行后抛 `InvalidOperationException: Object is currently in use elsewhere`。已改为把测量延后到打印期:实参保留原串并置 `IsConst = false`,`DoInstruction` 经 `Term.GetStrValue` 取串后由 `printCustomBar(..., false)` 在 UI 线程上调用 `getStBar`。`CALLSHARP_Instruction.CreateArgument` 中同样的调用属死代码(构造函数已设 `ArgBuilder`,不会走到该覆写),一并移除以免日后 `ArgBuilder` 被置空时重现。 |
| `GETLINESTR` | 保持**可折叠**(`GetLineStrMethod.CanRestructure = true`),且必须保持:「`#DIM` 的初值须为常量」由 `UserDefinedVariableData` 判定(初值 `Restructure` 后不是 `SingleTerm` 即抛 `InitialValueOnlyConst`),而 `STRLENSU(GETLINESTR("─"))` 之所以是常量,正是靠本函数折叠。曾置 `CanRestructure = false`(理由:其 `GetStrValue` 调用 `getStBar`(GDI+),与上一行同类),实测导致该写法直接**装载失败**(初值报错 → 变量未定义 → 后续 `for`/`NEXT` 结构失配),故该处置作废。改为让测量本身对线程安全:`StringMeasure` 记录构造它的线程,**在非属主线程上把共享 `Graphics` 换成 null**,由 GDI 自建设备上下文——测量值不变(多上下文、多字体逐值相同,并由 `StringMeasureTests.Measurement_agrees_on_a_thread_that_does_not_own_the_measure` 覆盖 `GRAPHICS`/`TEXTRENDERER` 两档钉住),且解析期不再跨线程触碰那个 `Graphics`。注意 `TextRenderer.MeasureText` 的 `Graphics` 参数**不接受 null**(传 null 抛 `ArgumentNullException(Parameter 'dc')`,实测在 `TEXTRENDERER` 默认档下由 header 装载的进度打印触发),故两档都必须显式分支到「无 `Graphics` 重载」,不能靠三元表达式选择重载。折叠本身发生在 header 装载(`ErhLoader.analyzeSharpDimLines` ← `LoadHeaderFiles`,由 `Process.Initialize` 的 `Task.Run` 承载)**这条串行池线程**上,与并行 `ParseScript` 不重叠。 |
| 解析期 `output.PrintError` / `PrintSystemLine`(**既存问题**) | `ParseScriptCore` 的 `AnalysisMode` 报告块、`LoadErbDir` 的逐文件报告、`ErhLoader.LoadHeaderFiles` 均在 `Task.Run` 池线程上调用 `output.PrintError`/`PrintSystemLine`,而 `PrintError` 会经 `RefreshStrings` 触发重绘。这是并行化之前就存在的形态(`Process.Initialize` 早已用 `await Task.Run(...)` 包住 header 装载),本票未引入也未修复;修复方向是把这些报告集中到调用方的 UI 线程上输出。 |
| `ParserMediator` 警告列表 | `Warn(string, LogicalLine, ...)` 的 `warningList.Add` 与 `line.IsError`/`ErrMes` 写入取 `warningListLock`;`HasWarning`/`ClearWarningList` 加锁;`FlushWarningList` 改为锁内取快照、锁外打印,避免 UI 线程 flush 与 worker 写入并发。未纳入该纪律的三处写入点各有理由:`ErbLoader.setLabelsArg` 在并行区之前的单线程阶段;`ErbLoader.ParseFunctionWithCatch` 的 `Warn` 与随后的显式 `label.ErrMes`/`label.IsError` 由该 label 的属主 worker 写;`ErbLoader.printFunctionNotFoundWarning` 仅由 `setJumpTo` 调用、处于解析该函数体的同一 worker 内,故行同样只有单一写者。`ReconcileLayerErrors` 虽替非属主写调用点行,但发生在该层 join 之后、属主 worker 已结束之时。`IsError` 是普通字段,读原子、只从 false 变 true(见「并行边界」),故这几处读写无需加锁。 |
| `scaningLine` | 解析路径改为 `SetScaningLineForThread`:线程本地值优先、回落到原实例字段,使不参与并行解析的线程行为不变。`Creator.Method.cs` / `LogicalLineParser.cs` 中对裸字段的读取暂不改——它们不在本票的并行区内(该处读取发生在并行区之外的单线程阶段)。 |
| `useCallForm` | 未加锁,已在字段处写明合并规则:所有写入都是 `true`(单调),读发生在并行区 join 之后。 |
| 同层互调的 `IsError` 读取顺序 | 见「并行边界」:以 `ParserMediator.CallEdgeRecorder` 收集静态 CALL 边及其实参,每层 join 后重判这些边——被调者为错误时先重跑 `ConvertArg`,再按串行同一分支 `Warn` 或标 `IsError`/`ErrMes`,使调用点的结果与解析顺序无关。 |
| `getStBar` 的 GDI+ 测量量 | 顺带优化:原实现按「整段 `barStr`」反复追加,每追加一次就重测整个前缀,故测量次数与重复次数同阶(整行宽度下约几十次)。改为先用一次整重复测量做整数除法估算重复次数,再按整重复逼近、最后逐字符裁剪定界;结果与逐字符逼近一致(见 `Emuera.Tests/UI/Game/StBarTests.cs`,以原算法为参照实现做等价性比对)。**该等价性以 `targetWidth > 0` 且一次重复的显示宽度 > 0 为前提**,两项各有一处已记录的残留(本票均不修):其一,`BuildStBar` 的两个宽度循环都靠宽度前进,空串/null 会死循环,已由上游 `printCustomBar`(`CodeEE`)与 `GETLINESTR`(`ArgIsEmptyString`)拦掉,`Config.DrawLineString` 亦有可打印默认值,但**非空且显示宽度为 0 的串**(如 `\u200B`)会绕过这些 `IsNullOrEmpty` 守卫,使 `GETLINESTR` 死循环;其二,`targetWidth == 0` 时新实现返回空串、原实现返回一次重复(原实现先播种一次重复再首次测量),而 `getStBar` 传入的 `Config.DrawableWidth` 是真实渲染宽度、不为零,故产品路径上不可达。两项前提已分别由 `StBarTests.GetDisplayLength_is_zero_for_text_that_cannot_advance_the_loops` 与 `BuildStBar_seeds_nothing_at_a_zero_target_where_the_original_seeded_a_repetition` 钉住。 |
| `GlobalStatic.tempDic` | 新增 `GlobalStatic.TempDicLock`;`ExpressionParser.reduceIdentifier` 的写入与 `ErbLoader` 的读回快照均在锁内。未在锁内的两处为:`GlobalStatic.Reset` 的 `Clear`(关闭期、无并发)、`ParseScriptCore` 的 `Count` 读取(在并行区 join 之后),均不构成并发访问。 |
| `isOnlyEvent` / `output.PrintSystemLine` 等 | 按设计未触发:`loadErb` 保持按文件串行,未并行化该段(见「曾考虑的替代方案」)。 |
| `ParserMediator.RenameDic` | 确认只读,无需处理。 |
| `ParseFunctionWithCatch` 只写 `ErrMes` 的既存形态 | **已修正**。该 catch 原先只写 `label.ErrMes` 而未写 `label.IsError`(`IsError` 仅由其中 `Warn(..., isError: true, ...)` 顺带置位)。这意味着「函数体解析失败」不会经 `ReconcileLayerErrors` 的 `!edge.Callee.IsError` 判定传给同层调用点,该路径的错误传播与解析顺序相关。现补上显式 `label.IsError = true`,使该错误也纳入 `CallEdgeRecorder` 的传播链。 |

### Depth 的发现顺序

`Depth` 有两层含义,并行的边界由第 2 点决定(结论见「并行边界:按 `Depth` 分层并行,不能全局并行」):

1. **数值的发现顺序依赖**:`SetJumpTo` 里 `if (call.TopLabel.Depth < 0) call.TopLabel.Depth = currentDepth + 1`——先到达的调用者决定值。因每个目标只被赋值一次、后续不覆盖,`Depth` 实质是按层推进的调度器,数值本身不进运行期语义(只作为 `depth` 局部量传回 `SetJumpTo`)。并行下需保证分层调度不退化。
2. **错误传播的确定性**:`ParseFunctionWithCatch` 在 `setJumpTo` 中写/读 `call.TopLabel.IsError`,而 `ConvertArg` 对 `TopLabel.IsError` 提前返回 `null`。跨层串行保证层 `k+1` 的 `SetJumpTo` 看到层 `k` 已定稿的错误状态;但**层内**串行并不保证「被调函数先解析」——同层互调的先后由枚举顺序决定,故层内并行后该结果与解析顺序无关(见「并行边界」的 `CallEdgeRecorder` 机制)。此项与「同名裁决」是两个不同的问题。

### 同名裁决

- 「先定义生效」**已经**由 `LabelDictionary.AddLabel` 的静态 `FileIndex` + `SortLabels()` 内 `list.Sort()` 后取 `list[0]` 实现,不依赖解析循环顺序,故无需让 label 处理额外携带来源文件与文件列表顺序;只需保证文件枚举阶段(`FileIndex` 分配)串行确定——该阶段本就是串行的。
- `Config.Config.GetFiles` 经 `Directory.GetFiles` + `Array.Sort`(受 `SortWithFilename` 影响)返回"文件名序 + 目录序"的确定列表,递归展开顺序稳定,文件枚举顺序在并行下依然确定。
- **不新增同名兼容开关**(已定)。原方案为「并行装载无可靠加载顺序」预设了 `CompatiFuncDup` 之类的开关,但如上所述,「先定义生效」已由 `FileIndex` 独立实现、不依赖加载顺序,并行化不带来额外开销,故保留现有行为即可,不引入新的配置面与其默认值 triage。
- 现状语义(维持不变):重复定义非事件函数时 `WarnNormalFunctionOverloading` 默认 `false`(`ConfigData.cs`),即**静默**、后定义被忽略;需要提示时由玩家自行打开该既有开关。
- 事件函数不受影响:保持多定义并列执行（eraBasic 核心语义）。同桶执行顺序无需重新设计:`SortLabels()` 已按 `FunctionLabelLine.CompareTo`(`FileIndex` → `LineNo` → `Index`)给出全序,并行下沿用该全序即确定性规则。

### 验证依据

测量已完成,以下为口径记录(埋点测得即撤,不作为交付物):

- `Config.DisplayReport = true` 时 `EmueraConsole.Initialize` 与 `Process.Initialize` 会写 `time.log`(`Program.ExeDir` 下),其中 `Proc:Init:ERB:Start` / `Proc:Init:ERB:End` 夹住整个 `LoadErbDir`。该文件依赖既存修复:原以 `FileMode.OpenOrCreate` + 默认缓冲的 `StreamWriter` 打开,而 `logWriter` 只在 `EmueraConsole.Initialize` 返回(即游戏退出)时才落盘,故运行期间始终为空;已改为 `FileMode.Create` + `AutoFlush = true`。
- `LoadErbDir` 内临时补埋点,分解为 `Enum`(目录/文件枚举)、`Files work`(per-file `loadErb` 实体之和)、`FileLoopWall`(文件循环 wall time)、`SetLabelsArg`、`ParseScript`。其中 `FileLoopWall − Files work` 即循环框架开销。**测量须关掉 `DisplayReport`**,否则该差额被逐文件输出放大数倍(见「测量结论」)。埋点测得即撤,未随本票交付。
- 必须用 Release 系配置(`Release-NAudio` 可用 `dotnet build`);Debug 配置的解析开销占比不具代表性。
- 每份语料多次运行取稳定值。`time.log` 与阶段埋点须在**同一构建的同一轮运行**中产出——分次构建会导致口径不可对齐(早期测量即因此返工)。
- 埋点的引入与撤除、以及「实测结果」中的复测结论一并见 git 历史;`Config.DisplayReport = false` 时不写 `time.log`,故该口径下只有阶段小计可用。

## 曾考虑的替代方案

1. **只并行 `loadErb` 的词法层**:放弃——`Files work` 最差 1.1s(D),即使全并行也只省约 0.5s,而该段共享状态最密集(`isOnlyEvent`、`output.Print*`)。
2. **`loadedFiles` 改用 `HashSet` 去重**:一度以为 `List.Contains` 的 O(n²) 扫描是瓶颈(曾据一次错误测量估计 D 为约 1.0s)。更正:该扫描实测为 0ms(`Files` 只含短相对路径,3549 条线性扫描不足 1ms),无优化价值,放弃。

## 影响

- 装载期语义变化:同名函数裁决规则由兼容配置决定(默认值待定,需语料数据支撑);`Depth` 分层的错误传播须按「并行边界:按 `Depth` 分层并行,不能全局并行」保持层内并行、跨层串行。
- **解析错误提示音的行为变化**:由「每个错误响一次」改为「整轮解析结束后响一次」(运行期路径仍即时发声)。这是为把 UI 操作移出并行区所做的取舍。
- **`BuildStBar` 的两处退化输入残留**:空串/null 由上游守卫拦下,但非空且显示宽度为 0 的串(如 `\u200B`)会让 `GETLINESTR` 死循环;`targetWidth == 0` 时新实现返回空串而原实现返回一次重复,该值在产品路径不可达(`Config.DrawableWidth` 不为零)。两者本票均不修,已在「共享状态清点」`getStBar` 行记录。
- 装载循环 `Application.DoEvents()` 泵已从并行区移除;`ErhLoader.analyzeSharpDimLines` 中的三处不在本票范围。
- 标识符驻留评估随本票实施一并处理(见 issues/03)。
- 附带发现(独立于本票,可先行):`LoadErbDir` 逐文件 `await Task.Run(() => loadErb(...))` 的调度往返约 0.30ms/文件(D 的 3549 文件约 1.1s),批量化投递即可消除;`Config.DisplayReport` 开启时逐文件 `PrintSystemLine` 另加约 0.91ms/文件(D 实测 3244ms),属该选项自身开销,生产默认为关。并行化后这两项在 ERB 段中的相对占比上升。
