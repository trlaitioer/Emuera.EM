# eraBasic 语法速查(csv / erb)

> 定位:阅读解释器代码、编写单元测试、编写翻译覆盖层(`.erbx`)时的语法参考。
> 以当前代码为准,代码定位采用「文件 + 函数」;所有指令/式中函数的完整清单不在此复述,
> 见注册表 `Statements/FunctionIdentifier.cs`(指令)与 `Statements/Function/Creator.Method.cs`(式中函数)。

## 1. 游戏数据构成

- 游戏目录 = `csv/`(数据)+ `erb/`(脚本),由 `Program.SetDirPaths` 设定
- `erb/` 下:`*.ERB` 函数脚本;`*.ERH` 头文件(`ErhLoader.LoadHeaderFiles`:`#DIM` 全局变量声明、`#DEFINE` 宏);`*.erd` 数据声明
- `csv/` 清单见 §8

## 2. 行的通用规则(所有文件)

由 `Runtime/Utils/EraStreamReader.cs` 的 `ReadEnabledLine` 逐行产出:

- 编码:`EncodingHandler.DetectEncoding` 自动检测(UTF-8 BOM → UTF-8 → Shift-JIS 回落)
- 空行跳过;**`;` 注释行在读取层即被过滤**——`LexicalAnalyzer.SkipWhiteSpace` 把 `;` 视为注释起点直接跳到行尾,随后 `st.EOS` 触发跳过
- 行中注释:词法层 `LexicalAnalyzer.Analyse` 的 `;` 分支(「行中コメント」),从 `;` 到行尾忽略
- 特殊注释标记(`SkipWhiteSpace` 内):`;#;` 仅 DebugMode 下当普通文本、`;!;`/`;^;` 为 EMEE 扩展注释
- 行连结:某行内容恰为 `{` 时开始连结,后续行依序拼接,直到内容恰为 `}` 的行结束(错误行号取 `{` 行);其余位置的孤立 `{`/`}` 报错
- `[[标识符]]` 替换:`_Rename.csv` 定义的文本替换(仅 `Config.UseRenameFile` 开启,替换发生在读取层)
- 全角空格:行中默认拒绝(`Config.SystemAllowFullSpace`,默认 false)

## 3. 预处理指令(仅 ERB)

`ErbLoader` 的 `PPState.AddKeyWord` 处理 `[KEYWORD]` / `[KEYWORD 参数]` 形式:

| 指令 | 作用 |
| --- | --- |
| `[SKIPSTART]` / `[SKIPEND]` | 无条件跳过区间 |
| `[IF_DEBUG]` / `[IF_NDEBUG]` | 按 `Program.DebugMode` 条件跳过 |
| `[IF 宏名]` / `[ELSEIF 宏名]` / `[ELSE]` / `[ENDIF]` | 按 `#DEFINE` 宏是否存在条件跳过 |

注意:其它 `[X Y]` 形式会告警「未识别的预处理指令」——`[X Y]` **不是**宏定义;宏定义在 ERH 中用 `#DEFINE`。

## 4. ERB 结构

`ErbLoader.loadErb` 逐行路由:`[X` → 预处理;`#` → `LogicalLineParser.ParseSharpLine`(必须紧跟函数标签);`@`/`$` → `ParseLabelLine`;其余 → `ParseLine`。

- `@函数名` — 函数标签;事件函数(`EVENTFIRST` 等)自动标记 `IsEvent`/`IsSystem`
- `$标签名` — 跳转标签(`GotoLabelLine`)
- `#` 属性行(`ParseSharpLine`):`SINGLE`/`LATER`/`PRI`/`ONLY`(事件函数分桶)、`#FUNCTION`/`#FUNCTIONS`(函数可作式中函数调用,返回 long/string)、`LOCALSIZE n`/`LOCALSSIZE n`、`#DIM`/`#DIMS`(函数内私有变量声明)
- 指令行:首标识符命中 `IdentifierDictionary.GetFunctionIdentifier` → `InstructionLine`
- 代入行:`变量 = 表达式` → `SET` 指令;写 `==` 也被容忍为赋值(带互兼容警告);行首前置 `++`/`--` 同样归为 `SET`
- `VARI`/`VARS` 指令(#DIM 的指令形态)仅在 `JSONConfig.Data.UseScopedVariableInstruction` 开启时注册
- 解析失败的行 → `InvalidLine`/`InvalidLabelLine`(带 `ErrMes`),装载期整体仍可继续,`noError` 置 false

## 5. 表达式(`ExpressionParser`)

- 字面量:整数;字符串 `"…"`;FORM 字符串 `@"…"`(§6);`'…'` 语义依上下文(§7)
- 变量引用:`变量:下标1[:下标2[:下标3]]`(冒号分隔,对应 1D/2D/3D);字符变量第一个下标是角色索引(`CALLNAME:MASTER`、`CSTR:角色:索引`);跨函数引用私有变量用 `变量@函数名`(`IdentifierDictionary.GetVariableToken` 的 subKey)
- 式中函数:`函数名(参数,…)`;经 `IdentifierDictionary.GetFunctionMethod` 解析——用户 `#FUNCTION` 同名定义优先于内建
- **三元运算符分隔符是 `?` 与 `#`**(`Ternary_a`/`Ternary_b`;`:` 保留给变量下标,`OperatorCode` 注释:「三項演算子区切り":"が使えないのでかわり」)
- 运算符优先级(`OperatorCode.cs`,`OperatorManager.GetPriority`,「優先順は本家に準拠」):

  单项(`+` `-` `!` `~` `++` `--`)→ `*` `/` `%` → `+` `-` → `<<` `>>` → `>` `<` `>=` `<=` → `==` `!=` → `&` `|` `^` → `&&` `||` `^^` `!&` `!^` → 三元 `?` `#` → 赋值 `=`(字符串赋值 `'=`)

- 宏:ERH 中 `#DEFINE 名 替换文本` 或 `#DEFINE 名(a,b) 替换文本`;词法期 `LexicalAnalyzer.expandMacro` 对裸标识符命中即展开,有单句展开次数上限
- 未知标识符 → `IdentifierNotFoundCodeEE`

## 6. FORM 字符串 `@"…"`(`StrForm.FromWordToken`)

静态文本与内插段交替构成;`PRINTFORM` 系指令的参数由 `LexicalAnalyzer.AnalyseFormattedString` 解析成 `StrFormWord`。

- `{数值表达式[,宽度[,LEFT|RIGHT]]}` — 数值格式化;`%字符串表达式[,宽度[,LEFT|RIGHT]]` — 字符串格式化;省略第三参 = 右对齐(PadLeft),写 `LEFT` = 左对齐(PadRight)
- 对齐宽度按所选语言字节宽计算(`LangManager.GetStrlenLang`;CP932 中假名/全角符号 2 字节;**CP932 不可映射的汉字按 1 字节计**)
- 三连符号(`StrForm.Initialize` 绑定到系统变量):`***`=NAME:TARGET、`+++`=CALLNAME:MASTER、`===`=CALLNAME:PLAYER、`///`=NAME:ASSI、`$$$`=CALLNAME:TARGET
- 三元内插:`\@ 条件 ? FORM串 # FORM串 \@`(两个分支都是嵌套 FORM;分隔符同样不是 `:`)
- 错误路径:空 `{}`/`%%` → `CodeEE`;对齐第三参非 `LEFT`/`RIGHT` → `CodeEE`
- `GetAExpression()`:整体恰好是单一内插段(如 `@"{…}"`)时还原为表达式;`Restructure(exm)` 做常量折叠,全常量可折叠为纯字面量

## 7. 字符串字面量的引号语义(`LexicalAnalyzer`)

引号行为由 `LexAnalyzeFlag` 按「指令」区分,不是全局统一:

| 引号 | 语义 | 适用 |
| --- | --- | --- |
| `"…"` | 标准字符串 | 全局 |
| `@"…"` | FORM 字符串(§6) | 全局 |
| `'…` | `'` 后直到逗号或行尾都是字符串文本,**无闭合引号概念**(`LexAnalyzeFlag.AnalyzePrintV`) | `PRINTV`/`PRINTVL`/`PRINTVW` 参数(`ArgumentBuilder`) |
| `'…'` | 成对引号字符串(`LexAnalyzeFlag.AllowSingleQuotationStr`) | `HTML_PRINT` 系 |

## 8. csv/ 数据文件

装载入口 `ConstantData.LoadData`(名字表/角色)与 `Process.Initialize`(GAMEBASE);**文件缺失一律跳过、非致命**;`;` 注释规则与 ERB 相同。

- `VariableSize.CSV`:`变量名,尺寸`,覆盖变量默认数组长度(`loadVariableSizeData`)
- 名字表 CSV(行格式 `索引,名称`):`ABL` `EXP` `TALENT` `PALAM` `TRAIN` `MARK` `ITEM` `BASE` `SOURCE` `EX` `STR` `EQUIP` `TEQUIP` `FLAG` `TFLAG` `CFLAG` `TCVAR` `CSTR` `STAIN` `CDFLAG1` `CDFLAG2`,及 `STRNAME` `TSTR` `SAVESTR` `GLOBAL` `GLOBALS`(字符串表名)、`DAY` `TIME` `MONEY`(EE 扩展);`ITEM.CSV` 第三列为价格(`ItemPrice`)
- `CHARA*.CSV`:角色模板(`Config.GetFiles(csvDir, "CHARA*.CSV")` 收集,`loadCharacterDataFile` 解析);键 → 模板字段映射见 `CharacterTemplate`;`AddCharacter(模板番号)` 按此取用,`AddPseudoCharacter` 提供无 CSV 时的伪角色
- `GAMEBASE.CSV`:键为日文(「コード」「バージョン」「タイトル」「作者」「製作年」「追加情報」「ウィンドウタイトル」「最初からいるキャラ」「アイテムなし」「バージョン違い認める」「動作に必要なEmueraのバージョン」「バージョン情報URL」「バージョン名」);缺失时全部取默认值(`GameBase.LoadGameBaseCsv`)
- `_Rename.csv`:行格式 `替换文本,标识符`;脚本中写 `[[标识符]]`,读取层替换为「替换文本」(`ParserMediator.LoadEraExRenameFile`)
- `_Replace.csv`:覆盖系统初值(PALAMLV/EXPLV/金钱标签等;`ConfigData.LoadReplaceFile` → `Config.SetReplace`——`VariableData.SetDefaultValue` 依赖其结果)

## 9. 怪癖与陷阱(写测试/读代码时易踩)

1. `@`/`$`/`#` 行**不经** `LogicalLineParser.ParseLine`——由 `ErbLoader.loadErb` 路由到 `ParseLabelLine`/`ParseSharpLine`;对 `ParseLine` 直接传这些行会得到 `InvalidLine`
2. 表达式三元分隔符是 `?` 与 `#`;`:` 永远是变量下标
3. `;` 注释有读取层(`ReadEnabledLine` + `SkipWhiteSpace`)与词法层(`Analyse` 行中注释)两道处理,自写解析循环时二选一即可,不要都不做
4. 指令名后必须跟空白/行尾/`;`;紧跟其它字符(含全角空格默认态)报错
5. CP932 不可映射字符(如简体专用字)宽度按 1 计,做对齐类断言时选假名等可映射字符
6. `IdentifierDictionary` 构造依赖 `VariableData`(→ `GameBase` + `ConstantData`),而 `FunctionIdentifier` 静态构造读取 `JSONConfig.Data`——单元测试的最小初始化序列见 `Emuera.Tests/TestBootstrap.cs` 及 `Emuera.Tests/README.md`
