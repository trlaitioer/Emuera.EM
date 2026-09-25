# Nullable:非空断言的逐点复核

Status: in-progress

## 任务

issues/13 消掉的是「可以用机制替代」的 `!`；剩下的断言里有一批不能靠机制消除，`!` 是否成立只能逐点判断。本票逐个复核并给出结论（保留+理由注释 / 补判空 / 改契约），其中可能改动运行行为的部分单独成节，不与 issues/13 的元数据改动混在一起。

复核对象（处数为 issues/13「盘点口径」下的统计结果，分类以 issues/13 落地后的状态为准）：

1. **`Argument` 可选字段断言（83 处）**——`arg.Def!`/`arg.Mouse!`/`arg.Term!`/`tinputarg.Mouse!` 等，都是编译器在缺少断言时无法自行证明非空的位置。`Argument` 派生类字段的可空性由 `ArgumentBuilder` 各 `CreateArgument` 分支决定，判断某处是否会为 null 必须回到对应构造分支。
2. **`(x as T)!`（51 处）**：转成 `(T)x` 会把类型不符时的 `NullReferenceException` 变成 `InvalidCastException`，属可观测差异。
3. **`Emuera.Tests` 内的 `!`**：SKILL 允许测试代码更自由地断言。
4. **迁移期为「疑似断言错误」留下的 TODO 注释**：当时已识别但未决的点。
5. **`JumpTo`/`JumpToEndCatch`/`ParentLabelLine` 族断言（40 处）**。
6. **issues/13 未覆盖的可空元素容器（79 处）**：接收者类型不是 `AExpression?` 的 `list[i]!`，以及其余接收者尚未逐一核实的容器。
7. **延后初始化的 `= null!` 复核**：成员/属性声明处的 `= null!` 是 SKILL 推荐写法，局部变量与调用实参处需要判断初始化路径。
8. **「其他成员/局部 `!`」（issues/13 记为 401 处）**：这一类不在原 7 条复核对象的任何一条里，占剩余断言的约 39%。处数以 issues/21 的复测表为准（2026-09-21 实测：全库 1088 处，已被 1/2/5/6/7 覆盖的五类合计 675，减法残差 411 处，`Emuera` 内 356）。本项由 issues/21 承接。

完成标准：每处给出结论与依据；保留的断言补上说明为何编译器看不到该不变量；认定为真问题的另立修复票，不在本票顺手改。

## 方案

按「先不改行为的复核，后行为决策」两步：

1. 逐个断言点判断三选一——不变量成立（补理由注释）、不变量不成立但当前不可达（补理由注释 + 记入潜在缺陷清单）、不变量不成立且可达（另立修复票）
2. `(x as T)!` 与 `Emuera.Tests` 两项单独提问，确认后再改

复核时以实际调用路径为准，不能只看字段声明。

### 口径与清单（前置）

票面处数继承自 issues/13 的收尾表，不是独立枚举；同口径复测（剥注释与字符串、匹配后缀 `!`）得全库 1075 处，票面记 1040 处。两者的差别主要是**计数单位**：1040 是同一棵树上「匹配行数（不剥注释/字符串）」，1075 是「出现次数（剥注释/字符串）」；分类差异（`Argument` 可选字段按前缀数 40、按派生类可空字段名数 102，票面记 83；`(x as T)!` 因嵌套 `as` 重复计数得 63，票面记 51）是叠加其上的第二重差异。另需注意匹配式的 `\s*` 会把前缀逻辑非计入（HEAD 有 12 处会误计，`3dd766f1` 时为 0），复测时应收紧为 `[\w\)\]]!(?!=)`。因此本票的判定以「站点清单」为锚点：清单按 `<文件>#<方法>#<序号>` 给出稳定站点 ID（不含行号），排除项写明（成员/属性声明的 `= null!`、`ArgumentListExtensions` 内的 2 处、`Emuera.Tests`）。清单由 issues/21 产出，本票已完成的判定沿用当时的文件+方法定位。

### 决策（2026-09-17）

- `Emuera.Tests` 内的 `!` **暂时豁免**，不逐点判定。测试工程是 `Nullable enable` 但未设 `<WarningsAsErrors>nullable</WarningsAsErrors>`，SKILL 亦允许测试代码更自由地断言；`3dd766f1` 上实测该类断言 82 处 / 13 个文件，HEAD `1ed33b5c` 为 107 处 / 19 个文件（增量来自此后新增的测试）。
- `(x as T)!` **暂时搁置不改**，保留 `as`+`!` 形态并逐点补理由注释（issues/22）。改 `(T)x` 或 `is T t` 都会改动运行行为（异常类型或新增分支），与迁移期零行为变更的原则冲突，需另立显式标注行为变更的票。
- 判定为可达缺陷的位置不聚合进本票，按根因拆票（issues/15~20）。

## 复核结论

四类判定：**A** 不变量成立（保留 + 理由注释）／**B** 不变量不成立但当前不可达（记录潜在缺陷）／**C** 可达缺陷（另立修复票）。

| 复核对象 | 结论 | 去向 |
| --- | --- | --- |
| 1 `Argument` 可选字段 | 多数成立；`INPUT`/`ONEINPUT`/`BINPUT`/`ONEBINPUT` 的 `CanSkip` 分支可达 null（C）；`VARS` 的 `arg.Value` 可绑定 null（B）；`RETURNF` 的 `Term!` 是假的断言但紧随判空 | issues/15 |
| 2 `(x as T)!` | 53 处中 49 处有前置类型检查；`Lang.cs` 那处类型有保证但反射值可空；`XML_GET` 第三实参为 0、`CMATCH` 的 `ArgType.Any`、`IN_RANGE_ARRAY` 的 `Restructure` 三处无保护（C） | issues/18 |
| 3 `Emuera.Tests` | 豁免（决策见上） | 无 |
| 4 TODO 注释 | 列了 7 处：`StrConv` 两处（**判定已更正，见下**）；`UnicodeMethod` 的 `Position`（B）；`UserDefinedRefMethodTerm.Restructure` 是死代码（B）；`CharacterData.temp_SortKey`（C）；`CALLSHARP` 的 `ConvertTerm`（C） | issues/15、17、20 |
| 5 `JumpTo` 族 | 46 处断言：10 成立、11 仅靠 `IsError` 门禁成立、23 可达。GOTO 的 `JumpTo = null` 复位路径喂不到任何断言点，原票的担心不成立；真因是 `ErbLoader.nestCheck` 的恢复分支与未复位的 `pairLine` | issues/16 |
| 6 未覆盖容器 | 85 处断言：`XmlNodeList` 15 处全部有界；`VariableToken?[]` 46 处中 1 处无保护；`DataColumnCollection[name]` 9 处中 1 处无保护；`LinkedList` 的判空写成了节点而非值；`string?[,]`（`temp_SortKey`）1 处无保护 | issues/17 |
| 7 `null!` | 成员声明类约 187 处出局；局部声明 66、赋值 71、调用实参 65。实参中占多数的常量折叠 `GetIntValue(null!)`/`GetStrValue(null!)`（其中 18 处，另有 `ToStrFormTerm` 的 1 处 `strf.GetString(null!)`）均被 `is SingleTerm`/`IsConst` 支配（`VariableEvaluator.FindChara` 两重载例外，靠 `ArgType.CharacterData` 保证），安全但契约不实；`LogicalLineParser` 的 `#LOCALSIZE` 与 `UserDefinedVariable` 的 `#DIM` 两处真缺陷（C） | issues/15、20、22 |
| 8 其他成员/局部 `!` | 未判定 | issues/21 |

### 已删除的判定歧义

- `GetCharacterTemplate*` 不是问题：其 6 个调用方全部判空。TODO 指向的位置实为 `ConstantData.GetKeywordDictionary`，该函数在 `index >= 0` 时把 null 转成 `CodeEE`，只在插件传空 key 时外泄 null（B，随 issues/19 记录潜在缺陷）。
- `Strings.StrConv` **前面这条判定已更正**：`Microsoft.VisualBasic.Core` 的该成员是有可空注解的（`string? StrConv(string? str, VbStrConv, int LocaleID = 0)`），调用点的 `!` 抑制的是 CS8603，删掉会报错；issues/08 迁移该文件时已记录「返回 `string?`」并就地补了 TODO。可删的只有「无 null 返回路径」这一半（不支持的转换抛 `ArgumentException`），故两处 `!` 保留、由 issues/22 复核 TODO，不构成可清理的噪音。
- `INPUTS`/`ONEINPUTS`/`BINPUTS`/`ONEBINPUTS` 不在缺陷范围内：`SP_INPUTS_ArgumentBuilder` 在把第二实参用作 `CanSkip` 之前已要求首实参非空且为整数。
- `TINPUT`/`TINPUTS` 不在缺陷范围内：`checkArgumentType` 配合 `minArg = 2` 使省略实参在解析期即报错。

## 缺陷票

| 票 | 范围 |
| --- | --- |
| issues/15 | 省略参数以 null 到达消费点（`INPUT` 族、`CALLSHARP`、`PRINT_SPACE`/`PRINT_RECT`、`VARS`） |
| issues/16 | `nestCheck` 配对被破坏时未标记错误行，空 `JumpTo` 进入执行期（含 `pairLine` 未复位） |
| issues/17 | 容器/成员断言的零散守卫缺失 |
| issues/18 | 无保护的 `as` 转换三处 |
| issues/19 | 插件与错误报告路径的空引用 |
| issues/20 | `Restructure(null!)` 在调试模式下解引用求值上下文 |

## 影响

- 本票不产出机械变换以外的代码改动；结论写入正文，需要改代码的部分按结论另开票（issues/15~20）
- 保留的断言由 issues/22 补理由注释，冗余断言同票清理
- issues/21 承接第 8 项复核对象；其清单产出前，「剩余断言的每一处都有结论」无法核对

## 曾考虑的替代方案

1. 把 400 处「其他成员/局部 `!`」并入现有家族票：边界模糊且量级不减，判定标准也不好统一，故单列 issues/21
2. 缺陷票聚合为一张：15 条缺陷分布在不同根因（省略参数契约、解析期配对不变量、断言守卫缺失、无保护转换、插件路径、求值上下文），一张票无法给出统一的修复判据
3. `Emuera.Tests` 一并逐点判定：测试工程的断言不进入产品契约，判定收益低于成本
