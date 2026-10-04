# arg-contract/issues/03 逐槽清单（机械预筛 + 片 2 裁定结果）

本文件由 [slots-worklist.py](slots-worklist.py) 生成，口径见脚本头注释；票见 [arg-contract/issues/03](issues/03-required-on-omittable-slot.md)。

> **冻结声明（2026-10 补）**：本文件是**阶段 A 的冻结快照**（口径见 [slots-worklist.py](slots-worklist.py) 头注释的「冻结提示」）。
> 在当前 HEAD 上重跑该脚本会**覆盖**本文件、且数字必然漂移——已知漂移：`arguments.Required(` 405→26、
> 「被 `Required`(字面量) 读取」76→2、E 段补车道 20→94、`ArgType.NotNull` 含注释口径 87/76→88/77。
> 阶段 B 的验收口径改用 [slots-progress.md](slots-progress.md)。

## 计数

| 量 | 数 |
| --- | --- |
| 规则块 / 含可省略槽的块 / 可省略槽总数 | 114 / 88 / 139 |
| `ArgType.NotNull` 处数（含注释 / 剥注释） | 87 / 81 |
| └ 含 `ArgType.NotNull` 的行数（含注释 / 剥注释） | 76 / 70 |
| 规则段旁的空槽语义注释行数（`empty slot`/`omitted slot`，大小写不敏感） | 37 |
| `arguments.Required(` 总处数（含注释） | 405 |
| 可省略 (类,槽) 全集（不论是否被读取） | 96 |
| └ 被 `Required`(字面量) 读取——本表口径 | 76 |
| 　├ 剥注释后该槽无 null 处理 ⇒ 需标 `NotNull` | 42（已带标志 41、裁定可空 1，见 D 段） |
| 　├ 剥注释后该槽有 null 处理 ⇒ 有意候选 | 34（片 2-B 裁定：全部为「显式留空」） |
| 　└ 其中仅注释里有 `== null`（旧手工检查器残留，仍归 `NotNull`） | 2 |
| └ 未被 `Required`(字面量) 读取——E 段补车道 | 20 |
| 　├ 有意可空（E 段逐槽裁定） | 5 |
| 　├ 转发槽（E 段逐槽裁定） | 2 |
| 　├ 消费型变长尾（E 段逐槽裁定） | 2 |
| 　├ 无意且可达（E 段逐槽裁定） | 7 |
| 　└ 硬转型解引用（E 段逐槽裁定） | 4 |

A/B 两段的 76 项 + E 段的 20 项 = 可省略 (类,槽) 全集 96 项。E 段的读取痕迹由脚本机械列出，类别与处置逐槽人工核查（见票内片 2-D）。

`null 处理` 识别三种写法（均在剥注释后的文本上统计）：`arguments[i] == null` / `arguments[i]?.` / 列表模式 `arguments is [… not null …]`。

`必填段` 非空的槽出现在多个段里：某段要求它必填、另一段允许省略。检查器「任一段通过即通过」（`FunctionMethod.CheckArgumentTypeEx`），故**可省略段仍会放行 null**，`NotNull` 必须加在可省略段上。

## A. 需标 `NotNull` 的槽

`可省略段` 列出该槽出现的**全部**可省略段（`OmitStart/ArgTypes`）——标志必须铺满这些段。

`已带标志` = 该槽的全部可省略段都已带 `ArgType.NotNull`（只标一段仍会从别的段放行 null）。

| 类[槽] | 站点（类内方法） | 可省略段 | 必填段 | 类内 helper | 仅注释命中 | 已带标志 |
| --- | --- | --- | --- | --- | --- | --- |
| `ArrayMultiSortExMethod[3]` | GetIntValue | 2/4、2/4 | — | — | — | 是 |
| `CBGSETButtonSpriteMethod[6]` | GetIntValue | 6/7 | — | ReadPoint | — | 是 |
| `DataTableCellGetMethod[3]` | GetIntValue、GetStrValue | 3/4 | — | — | — | 是 |
| `DataTableCellSetMethod[4]` | GetIntValue | 3/5 | — | — | — | 是 |
| `DataTableColumnManagementMethod[1]` | GetIntValue | 1/2 | 2/4 | — | — | — |
| `DataTableColumnManagementMethod[2]` | GetIntValue×3 | 2/4 | — | — | — | 是 |
| `DataTableColumnManagementMethod[3]` | GetIntValue | 2/4 | — | — | — | 是 |
| `DataTableRowSetMethod[1]` | GetIntValue | 1/3 | — | — | — | 是 |
| `EnumFilesMethod[1]` | GetIntValue | 1/4 | — | — | — | 是 |
| `EnumFilesMethod[2]` | GetIntValue | 1/4 | — | — | — | 是 |
| `ErdNameMethod[2]` | GetStrValue | 2/3 | — | — | — | 是 |
| `ExistFunctionMethod[1]` | GetIntValue | 1/2 | — | — | — | 是 |
| `FlowInputMethod[1]` | GetIntValue | 1/4 | — | — | — | 是 |
| `FlowInputMethod[2]` | GetIntValue | 1/4 | — | — | — | 是 |
| `FlowInputMethod[3]` | GetIntValue | 1/4 | — | — | — | 是 |
| `FlowInputsMethod[1]` | GetIntValue | 1/2 | — | — | — | 是 |
| `GetnumMethod[2]` | GetIntValue | 2/3 | — | — | — | 是 |
| `GraphicsCreateFromFileMethod[2]` | GetIntValue | 2/3 | — | ReadGraphics | — | 是 |
| `GraphicsGetTextSizeMethod[3]` | GetIntValue | 3/4 | — | — | — | 是 |
| `GraphicsSetFontMethod[3]` | GetIntValue | 3/4 | — | ReadGraphics | — | 是 |
| `HtmlGetPrintedStrMethod[0]` | GetStrValue | 0/1 | — | — | 是 | 是 |
| `HtmlStringLenMethod[1]` | GetIntValue | 1/2 | — | — | — | 是 |
| `LoadTextMethod[1]` | GetStrValue | 1/3 | — | — | — | 是 |
| `LoadTextMethod[2]` | GetStrValue | 1/3 | — | — | — | 是 |
| `MapGetStrMethod[1]` | GetStrValue×2 | 1/2 | — | — | — | 是 |
| `OutputlogMethod[0]` | GetIntValue | 0/2 | — | — | — | 是 |
| `OutputlogMethod[1]` | GetIntValue | 0/2 | — | — | — | 是 |
| `RandMethod[1]` | GetIntValue | 1/2 | — | — | 是 | 是 |
| `RegexpMatchMethod[2]` | GetIntValue | 2/3 | — | — | — | 是 |
| `ReplaceMethod[3]` | UniqueRestructure、GetStrValue | 3/4 | — | — | — | 是 |
| `SaveTextMethod[2]` | GetIntValue | 2/4 | — | — | — | 是 |
| `SaveTextMethod[3]` | GetIntValue | 2/4 | — | — | — | 是 |
| `VarSetExMethod[2]` | GetIntValue | 2/5 | — | — | — | 是 |
| `VarSetExMethod[3]` | GetIntValue | 2/5 | — | — | — | 是 |
| `VarSetExMethod[4]` | GetIntValue | 2/5 | — | — | — | 是 |
| `XmlAddNodeMethod[3]` | GetIntValue | 3/5、3/5、3/6、3/6、3/5、3/6 | — | — | — | 是 |
| `XmlGetMethod[2]` | GetIntValue | 2/4、2/4 | 3/4、3/4 | — | — | 是 |
| `XmlGetMethod[3]` | GetIntValue | 2/4、3/4、2/4、3/4 | — | — | — | 是 |
| `XmlRemoveNodeMethod[2]` | GetIntValue | 2/3、2/3、2/3 | — | — | — | 是 |
| `XmlReplaceMethod[3]` | GetIntValue | 3/4、3/4、3/4 | — | — | — | 是 |
| `XmlSetMethod[3]` | GetIntValue | 3/5、3/5、3/5 | — | — | — | 是 |
| `XmlSetMethod[4]` | GetIntValue | 3/5、3/5、3/5 | — | — | — | 是 |

## B. 有意候选（片 2-B 裁定：34 项全部为「显式留空」）

判空若是防御性写法或注释残留，归 A 表；片 2-B 逐槽取证后**无一项**归 A——34 项的函数体都把 null 与「不传该实参」读成同一个默认值（或走明确分支，如 `GETMETH` 无兜底值时抛 `CodeEE`）。

各槽的空槽语义写在 `BuiltinMethod/*.cs` 对应规则段旁，由 `CheckArgumentTypeEx.IntentionalEmptySlot_IsAccepted` 逐槽钉住；A 表里的 `DataTableColumnManagementMethod[1]` 同判（NAMES 的空槽 = 写入 RESULTS 数组），故它是 A 表唯一不带 `NotNull` 的行。

| 类[槽] | 站点（类内方法） | 可省略段 | 必填段 | 类内 null 处理（方法） |
| --- | --- | --- | --- | --- |
| `ArrayMultiSortExMethod[2]` | GetIntValue | 2/4、2/4 | — | GetIntValue |
| `CsvDataMethod[2]` | GetIntValue | 2/3、2/3 | — | GetIntValue |
| `CsvStrDataMethod[1]` | GetStrValue | 1/2、1/2 | — | GetStrValue |
| `CsvcstrMethod[2]` | GetStrValue | 2/3 | — | GetStrValue |
| `EncodeToUniMethod[1]` | GetIntValue | 1/2 | — | GetIntValue |
| `ExistCsvMethod[1]` | GetIntValue | 1/2 | — | GetIntValue |
| `FindElementMethod[2]` | GetIntValue | 2/5 | — | GetIntValue |
| `FindElementMethod[3]` | GetIntValue | 2/5 | — | GetIntValue |
| `FindElementMethod[4]` | GetIntValue | 2/5 | — | GetIntValue |
| `FindFilesMethod[0]` | GetIntValue | 0/1 | — | GetIntValue |
| `FindcharaMethod[2]` | GetIntValue | 2/4 | — | GetIntValue |
| `FindcharaMethod[3]` | GetIntValue | 2/4 | — | GetIntValue |
| `GetMethMethod[1]` | GetIntValue | 1/3 | — | GetIntValue |
| `GetMethsMethod[1]` | GetStrValue | 1/3 | — | GetStrValue |
| `GetcharaMethod[1]` | GetIntValue | 1/2 | — | GetIntValue |
| `InRangeArrayMethod[3]` | GetIntValue | 3/5、3/5 | — | GetIntValue |
| `InRangeArrayMethod[4]` | GetIntValue | 3/5、3/5 | — | GetIntValue |
| `JoinMethod[1]` | GetStrValue | 1/4 | — | GetStrValue |
| `JoinMethod[2]` | GetStrValue | 1/4 | — | GetStrValue |
| `JoinMethod[3]` | GetStrValue | 1/4 | — | GetStrValue |
| `MatchMethod[2]` | GetIntValue | 2/4、2/4 | — | GetIntValue |
| `MatchMethod[3]` | GetIntValue | 2/4、2/4 | — | GetIntValue |
| `MaxArrayMethod[1]` | GetIntValue | 1/3、1/3、1/3、1/3 | — | GetIntValue |
| `MaxArrayMethod[2]` | GetIntValue | 1/3、1/3、1/3、1/3 | — | GetIntValue |
| `MoneyStrMethod[1]` | GetStrValue | 1/2 | — | GetStrValue |
| `StrfindMethod[2]` | GetIntValue×2 | 2/3 | — | GetIntValue |
| `SubstringMethod[1]` | GetStrValue | 1/3 | — | GetStrValue |
| `SubstringMethod[2]` | GetStrValue | 1/3 | — | GetStrValue |
| `SubstringuMethod[1]` | GetStrValue | 1/3 | — | GetStrValue |
| `SubstringuMethod[2]` | GetStrValue | 1/3 | — | GetStrValue |
| `SumArrayMethod[1]` | GetIntValue | 1/3、1/3 | — | GetIntValue |
| `SumArrayMethod[2]` | GetIntValue | 1/3、1/3 | — | GetIntValue |
| `ToStrMethod[1]` | GetStrValue | 1/2 | — | GetStrValue |
| `VarsizeMethod[1]` | GetIntValue、UniqueRestructure | 1/2 | — | GetIntValue |

## C. 多段类（标注时勿漏段）

| 类 | 可省略段数 | 段（OmitStart/ArgTypes） |
| --- | --- | --- |
| `ArrayMultiSortExMethod` | 2 | 2/4、2/4 |
| `CsvDataMethod` | 2 | 2/3、2/3 |
| `CsvStrDataMethod` | 2 | 1/2、1/2 |
| `DataTableColumnManagementMethod` | 2 | 2/4、1/2 |
| `GraphicsDrawGMethod` | 2 | 10/11、10/11 |
| `GraphicsDrawSpriteMethod` | 2 | 6/7、6/7 |
| `InRangeArrayMethod` | 2 | 3/5、3/5 |
| `MatchMethod` | 2 | 2/4、2/4 |
| `MaxArrayMethod` | 4 | 1/3、1/3、1/3、1/3 |
| `MaxMethod` | 2 | 1/2、1/2 |
| `SumArrayMethod` | 2 | 1/3、1/3 |
| `XmlAddNodeMethod` | 6 | 3/5、3/5、3/6、3/6、3/5、3/6 |
| `XmlGetMethod` | 4 | 2/4、3/4、2/4、3/4 |
| `XmlRemoveNodeMethod` | 3 | 2/3、2/3、2/3 |
| `XmlReplaceMethod` | 3 | 3/4、3/4、3/4 |
| `XmlSetMethod` | 3 | 3/5、3/5、3/5 |

## E. 补车道：未被 `Required`(字面量) 读取的可省略槽（全集补齐 20 项）

这些槽由别的方式读取：helper 传参（`ReadPoint(Name, exm, arguments, argNo)` 之类）、变长下标 `Required(i)`、
`arguments[i]` 硬转型、`arguments.Skip(k)` 转发，或函数体根本不读。「读取痕迹」列由脚本机械扫描，
「类别 / 处置 / 尺子」三列是逐槽人工核查结论（票内片 2-D）；尺子是「读该槽时是否保证 `Count >= i + 1`」。

`已带标志` = 该槽的全部可省略段都在该槽位带 `ArgType.NotNull`；裁定为可空的槽若带标志，脚本会报警。

| 类[槽] | 可省略段 | 读取痕迹（机械） | 已带标志 | 类别 | 处置 | 尺子 |
| --- | --- | --- | --- | --- | --- | --- |
| `ArrayMultiSortMethod[1]` | 1/2 | Required(非字面量) | 是 | 消费型变长尾 | 片 2-C 已标 NotNull | 过（Cast 整表） |
| `DataTableCellSetMethod[3]` | 3/5 | arguments[3] @ GetIntValue | — | 有意可空 | 本次补注释 + 用例（空槽 = 写 DBNull） | 过（Count > 3） |
| `DataTableRowSetMethod[2]` | 1/3 | Required(非字面量) | — | 有意可空 | 本次补注释 + 用例（变长对 value 位 = DBNull；4 实参数组形态共用该槽，但由第二段 OmitStart == -1 拒空） | 过（Count == b + 4 / 变长对） |
| `DataTableSelectMethod[1]` | 1/4 | arguments[1] @ GetIntValue | — | 有意可空 | 规则段注释已写（空槽 = 不筛） | 过（Count > 1） |
| `DataTableSelectMethod[2]` | 1/4 | arguments[2] @ GetIntValue | — | 有意可空 | 规则段注释已写（空槽 = 不排序） | 过（Count > 2） |
| `DataTableSelectMethod[3]` | 1/4 | arguments[3] @ GetIntValue | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（Count == 4） |
| `DataTableToXmlMethod[1]` | 1/2 | arguments[1] @ GetStrValue | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（Count > 1） |
| `EnumFilesMethod[3]` | 1/4 | arguments[3] @ GetIntValue | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（Count == 4） |
| `EnumNameMethod[1]` | 1/2 | arguments[1] @ GetIntValue | 是 | 硬转型解引用 | 已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05） | 过（Count == 2） |
| `GetMethMethod[2]` | 1/3 | arguments.Skip(...) | — | 转发槽 | 规则段注释已写（交给下游解释） | 过（Skip(2) 原样转发） |
| `GetMethsMethod[2]` | 1/3 | arguments.Skip(...) | — | 转发槽 | 规则段注释已写（交给下游解释） | 过（Skip(2) 原样转发） |
| `GraphicsDrawGMethod[10]` | 10/11、10/11 | arguments[10] @ GetIntValue、Required(非字面量)、ReadColormatrix(argNo=10) | — | 有意可空 | 规则段注释已写（空槽 = 不带颜色矩阵） | 过（Count != 10 时判空短路） |
| `GraphicsDrawGWithRotateMethod[3]` | 3/5 | ReadPoint(argNo=3) | 是 | 无意且可达 | 本次标 NotNull + 用例（ReadPoint 无判空） | 过（Count != 3 的否支） |
| `GraphicsDrawGWithRotateMethod[4]` | 3/5 | ReadPoint(argNo=3+1) | 是 | 无意且可达 | 本次标 NotNull + 用例（ReadPoint 无判空） | 过（Count != 3 的否支） |
| `GraphicsDrawSpriteMethod[6]` | 6/7、6/7 | Required(非字面量)、ReadColormatrix(argNo=6) | 是 | 无意且可达 | 本次标 NotNull + 用例（ReadColormatrix 无判空） | 过（2/4/6 分支返回后即 Count == 7） |
| `GraphicsDrawStringMethod[2]` | 2/4 | ReadPoint(argNo=2) | 是 | 无意且可达 | 本次标 NotNull + 用例（ReadPoint 无判空） | 过（Count == 4） |
| `GraphicsDrawStringMethod[3]` | 2/4 | ReadPoint(argNo=2+1) | 是 | 无意且可达 | 本次标 NotNull + 用例（ReadPoint 无判空） | 过（Count == 4） |
| `MaxMethod[1]` | 1/2、1/2 | Required(非字面量) | 是 | 消费型变长尾 | 片 2-C 已标 NotNull | 过（循环 i < Count） |
| `XmlAddNodeMethod[4]` | 3/5、3/5、3/6、3/6、3/5、3/6 | Required(非字面量) | 是 | 无意且可达 | 本次标 NotNull + 用例（Node 段 setAllPos、Attribute 段 methodPos 都读它） | 过（Count == 5 / Count >= 5） |
| `XmlAddNodeMethod[5]` | 3/6、3/6、3/6 | Required(非字面量) | 是 | 无意且可达 | 本次标 NotNull + 用例（Attribute 段 setAllPos） | 过（Count == 6） |

## D. 状态与余项

- [x] **A 表逐槽铺标志（片 2-A + 片 3，已落地）**：`ArgType.DisallowVoid` → `ArgType.NotNull`（改名面 5 处；片 1 新增标注的 3 处写的就是新名，不属改名面），A 表除 `DataTableColumnManagementMethod[1]` 外全部在**每一段**可省略段的该槽位带上标志（只标一段会被别的段放行 null，见机制事实 1）
- [x] **B 表 34 项裁定（片 2-B，已落地）**：全部「有意可空」，不标 `NotNull`；规则段旁写明空槽语义，另有逐槽放行用例
- [x] 片 1（3 处可达 NRE + 两处数量省略越界 + 用例）已落地
- [x] **另一把尺子（数量省略越界）**已对 A 表逐点核过：只有 `GraphicsSetFontMethod`、`HotkeyStateMethod` 两处属「少写尾实参即越界」且标 `NotNull` 修不掉（检查器循环只走 `i < Count`）——已抬 `OmitStart` 修掉并补用例；其余站点读 `Required(i)` 时都有保证 `Count >= i+1` 的守卫（`Count > i` / `== i+1` / `>= i+1`，含 `Count < k || …`、`Count == 1 || …` 的短路形态）。本表为 42 项 / 47 个站点（站点数按上表「站点」列合计）
- [x] **同一把尺子对 E 段 20 项也逐项核过**：无一处「数量省略即越界」，故没有第三处 `OmitStart` 附修；逐项结论见 E 段「尺子」列
- [x] 片 2 的三处更正（票面「片 2-A」有记）：① `ReplaceMethod[3]` 被预筛漏在批次外——它同样可达（`REPLACE("a","b","c",,)` 由段 1 的 `OmitStart = 3` 放行 null），已补标 + 补用例；② `DataTableRowSetMethod[1]`（脚本按类内可省略段与站点做笛卡尔匹配时多出的那条）的站点确属 Set 分支（跨构造器假阳性），但 `DT_ROW_ADD` 的槽 1 经 `arguments.Required(pos)`（变量下标，不在本表车道）可达、修前 NRE，已标 `NotNull`；③ `MapGetStrMethod[1]` 只有 `MAP_GETKEYS` 那一段可省略（`MAP_GET`/`MAP_TOXML` 走定长 `argumentTypeArray`），已按名绑定标注。`DataTableColumnManagementMethod[1]` 裁定为可空
- [x] **变长尾部按转发型 / 消费型定案（片 2-C，已落地）**：`MaxMethod`（`MAX`/`MIN`）的 `VariadicInt` 与 `ArrayMultiSortMethod`（`ARRAYMSORT`）的变长尾标 `ArgType.NotNull`（函数体逐个循环 / 整表 `Cast` 消费，空槽无默认值可代）；`GetMethMethod`/`GetMethsMethod` 保持可空（`arguments.Skip(2)` 转发给下游）。两型的首槽都由 `i < OmitStart` 拒空，故标志只铺尾部。这三处在「字面量 `Required(`」车道之外，故不影响本表的行数与计数
- [x] **本表已从 76 扩到 96**（可省略 (类,槽) 全集）：补车道的 20 项见 E 段——其中「有意可空」的 5 项已按完成标准第 1 类在规则段旁写明空槽语义（`DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]` 为本次补写），转发槽保持可空不标 `NotNull`，「无意且可达」的 7 项已标 `NotNull` 并补用例，「硬转型解引用」的 4 项已由 arg-contract/issues/04 阶段 A 标 `ArgType.NotNull`（输出数组；显式空槽应拒绝；cast 的类型化移除归 issues/05）
- [x] UDF 侧 `UserDefinedRefMethodTerm` 已确认为死代码（见票内「UDF 族」），**不是**本表工作面；活路径的「省略实参 → 取 `FunctionLabelLine.Def` 默认值」已由 `CalledFunctionTests` 一条用例钉住
- [ ] 仍未纳入本表：活 `arguments[i]` 站点里落在**非可省略槽**上的那些（硬转型解引用共 24 处，落在可省略槽上的 4 项已见 E 段，其余归 arg-contract/issues/05（cast 的类型化移除））。变量下标 `Required(i)` 与 helper 传参路径已随 E 段核过
- [x] **片 5（B 表执行期语义）与片 6（指令侧两轴模型定案）已落地**：片 5 的用例在 `Emuera.Tests/Runtime/Script/Statements/Function/IntentionalEmptySlotExecutionTests.cs`（46 条用例；覆盖了 B 表哪些槽、各项钉住的默认值、未覆盖槽的理由均见 issues/03 片 5，逐槽证据留在票内而不在此处硬编码）；片 6 的工件是 [instruction-args.py](instruction-args.py) → [instruction-args.md](instruction-args.md)（不并入本表——它扫的是 `ArgumentBuilder.cs`，口径与本站点清单不同）
- [ ] 本票仍未做的一件（见票内「余项」7）：`docs/` 侧的内建函数契约表
