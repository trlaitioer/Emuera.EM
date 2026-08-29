# 性能:解析期 Span 化

Status: wontfix
Type: task

## 背景

原方案:解析期 `st.Substring()`(当时清点约 12 处)与 `ReadSingleIdentifier` 调用点逐步换 ROS,`st.Substring().Split(',')` 配套 span 辅助(已评估、未实施)。

评估结论(2026-08-29):原方案前提不成立,原样实施无可测收益。当前 `CharStream` 上的 `st.Substring` 调用点全部清点如下:

- **已 Span 化**(上游合并 7eb2e02 带入):词法热路径已在 span 上运行——标识符扫描 `LexicalAnalyzer.ReadSingleIdentifierROS`(`SearchValues` 实现)、整数核心 `readDigits`、浮点 `ReadDouble`、`LogicalLineParser.ParseLine` 的语句行切片、`ConstantData.loadDataTo`;`ReadSingleIdentifier` 本身只是 ROS 结果 `ToString()` 的封装。
- **错误路径 3 处,不构成开销**:`ReadInt64` 与 `NumericCheck` 的越界报错分支、`LexicalAnalyzer.Analyse` 中 rename `[[…]]` 报错分支(1810 起命中即抛错)。
- **AST 持久化 3 处,分配省不掉**:`ArgumentBuilder.STR_ArgumentBuilder.CreateArgument` 与 `Instraction.Child` 的两个 `CreateArgument` 覆写,`rowStr` 随即写入 `ConstStr`/`SingleStrTerm` 长期保留;换 ROS 只是把同一次分配后移。
- **启动期一次性 CSV 5 处,量级可忽略**:`GameBase.LoadGameBaseCsv` 与 `ConstantData.loadVariableSizeData` / `loadGlobalVarExSetting` / `loadCharacterDataFile` / `loadAliases` 的 `Split(',')`;文件小、仅启动执行一次,token 或走数值解析(可直接 span 化但收益微)或作为字符串存入字典(分配保留)。
- **运行期显示路径 7 处,不属于解析期**:`HtmlManager`(6 处)与 `ButtonStringCreator.lex`(token 存入 `List<string>` 供显示),为运行期印字/按钮渲染;若要优化应另立显示路径议题。

约束(若未来做任何 ROS 改造):`CharStream.Replace` / `AppendString` 会替换底层字符串,span 视图跨这两个调用失效,不得持有。

## 唯一有实际空间的方向

标识符/常量串驻留(interning):`IdentifierWord` 等词元以 `readonly string` 在 AST 中终身持有,ERB 语料中标识符高度重复(命令名、变量名等)。以 span 键查重(`Dictionary` 的 `AlternateLookup<ReadOnlySpan<char>>`)复用既有实例,可同时省分配并加速下游相等比较。这是与"Substring→ROS"不同的独立优化,收益需先以大型 ERB 语料的加载基准(耗时 + 分配计数)证实;且并行装载(issues/04)落地后驻留查重需并发安全设计(并发字典或分片合并)。

## 处置(triage 定案 2026-08-29)

原方案(Substring→ROS)不做,本票关闭。「唯一有实际空间的方向」的标识符驻留不在本票实施,延至 issues/04 实施时一并评估,已在 04 正文互链。

## Comments

### 2026-08-29

- 评估完成:逐点核实 `st.Substring` 调用点与 ROS 现状,原方案无可测收益(热路径已 Span 化,其余为错误路径/AST 持久化/一次性 CSV/运行期显示),详见背景;唯一实际方向为标识符驻留。处理方式(关闭 or 转向)待 triage,Status 保持 needs-triage。
- Triage 定案:原方案不做,本票以 wontfix 关闭;标识符驻留的评估延至 issues/04 实施时(04 正文已互链)。Status: needs-triage → wontfix。
