# Nullable:断言审计收尾时发现的三处疑似缺陷与一处不一致

id: nullable-migration-32
Status: in-progress
Type: bug

## 任务

`nullable-migration-22` §1 补理由注释时逐站读码，发现下列位置「判 A（不变量成立）」的判定与代码事实不符，或规则与实现形状不一致。这些项都**未改动**（§1 是注释级工作，行为变更须另立判据），集中登记待分流；其中 1、2 两条是可达缺陷，3、5 是死判断/死分支，4、6 是产品一致性问题。分流时可按根因拆成多张票。第 6 项来自 `nullable-migration-22` §5-3 收尾时未决的产品判断，同属「已判出、待分流」，故并入本票。

### 1. `XML_ADDATTRIBUTE` 的 `node.Attributes!` 解引用非元素节点

- 位置：`Emuera/Runtime/Script/Statements/Function/BuiltinMethod/PrivateAdditions.cs` 的 `XmlAddNodeMethod.Insert`（`node.Attributes!.Append(newAttr)`，method 0 分支）
- 事实：`XmlNode.Attributes` 只有 `XmlElement` 覆写，文本/文档/属性节点均为 null（实测 `XmlText.Attributes == null`），而 method 0 分支没有 `node is XmlElement` 门禁
- 触发：`XML_ADDATTRIBUTE`（method 0）的 XPath 选中属性或文本节点
- 旁证：`docs/builtin-functions.md:595` 自己就记载「methodType=0 且 xpath 选中属性本身时 `Attributes` 为 null 抛空引用异常」——即该 NRE 是既有可观测行为
- `bang-sites.md` 对该站点判 A（理由是 XPath 字面量/文档契约），需更正为可达缺陷

### 2. `DATATABLE_TO_XML` 的 `(… as string[])!` 与规则声明的形状不一致

- 位置：同文件 `DataTableToXmlMethod.GetIntValue`（`output[0] = …(… as string[])!`）
- 事实：该方法的槽规则是 `{ String, RefString | NotNull }`（dims = 0，未声明 `Array1D`），而 dims = 0 只比较操作数类型、不约束数组形状
- 后果：标量字符串变量匹配后 `GetArray()` 抛 `CodeEE(GetSize0DVar)`（可读错误）；2 维字符串数组匹配后 `as string[]` 得 null → `output[0]` 空引用
- 旁证：`docs/builtin-functions.md:668` 把该参数描述为「写入所传数组下标 0」，与规则里没有维度声明相矛盾
- 处置方向：规则补 `RefString1D`（或实现显式拒绝非 1 维），二者取一都要给判据与用例

### 3. `PrintStringBuffer.getDivideIndex` 的判空疑为笔误

- 位置：`Emuera/UI/Game/PrintStringBuffer.cs:683` 的 `ConsoleStyledString? css = part as ConsoleStyledString; if (part == null) { return -1; }`
- 事实：`part` 是非可空形参，该判空恒假；疑应为 `css == null`
- 影响：当前不可达（上游 `!part.CanDivide` 门禁保证 `part` 是 `ConsoleStyledString`，全仓唯一 `CanDivide => true` 的实现就是它），故只登记为死判断；若判空语义改为 `css`，行为在不可达路径上会变（NRE → 返回 -1），需与 §1 的注释一起定

### 4. `SP_SPLIT_ArgumentBuilder` 的 `term!` 与姊妹 builder 的回退行为不一致

- 位置：`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs` 的 `SP_SPLIT_ArgumentBuilder.CreateArgument`（第 4 实参）
- 事实：第 4 实参不是「可改变量」时 `getChangeableVariable` 返回 null 并 `warn`，本 builder **没有**回退到 `RESULT` 变量；而姊妹 `SP_HTMLSPLIT_ArgumentBuilder` 在第 3 实参同情形下显式回退到 `RESULT`
- 后果：`term!` 严格为假，只因 `warn` 把行标成 `IsError`、执行期在 `DoInstruction` 前抛 `CodeEE` 才不可达（故 §1 按「靠错误标记成立」写了注释）
- 处置方向：与 `SP_HTMLSPLIT` 统一（补 `?? new VariableTerm(RESULT…)`）或明确保留差异，属产品判断

### 5. `FORM_STR_ANY_ArgumentBuilder` 内的 `RETURNFORM` 分支今天不可达

- 位置：`Emuera/Runtime/Script/Statements/ArgumentBuilder.cs` 的 `FORM_STR_ANY_ArgumentBuilder.CreateArgument` 内 `line.FunctionCode == FunctionCode.RETURNFORM` 的分支（`termList.Add(new SingleStrTerm("0"))`）
- 事实：`RETURNFORM` 的唯一注册点（`FunctionIdentifier.cs`）用的是 `RETURNFORM_Instruction`，其 `ArgBuilder` 是 `FORM_STR`（`FORM_STR_ANY` 那一行被注释掉），故该分支永不执行；`FORM_STR_ANY` 今天的活消费者只有 `SETBGIMAGE`/`REMOVEBGIMAGE`
- 旁证：为 issues/22 §5/§6 补的回归网（`Emuera.Tests` 的 `ArraycopyArgumentTests`/`ExpressionArrayArgumentTests`）无法为该分支写出可执行用例
- 处置方向：删掉死分支，或把注册改回 `FORM_STR_ANY`（后者会改变 `RETURNFORM` 的参数形状，须先定语义）

### 6. `DT_COLUMN_OPTIONS` 是否应受「`id` 列不可改」守卫约束（未决）

- 位置：`Emuera/Runtime/Script/Statements/Instraction.Child.cs` 的 `DT_COLUMN_OPTIONS_Instruction.DoInstruction`
- 事实：族内 `DTCanNotEditIdColumn` 守卫只加在写行的路径（`DataTableRowSetMethod`）上；`DT_COLUMN_OPTIONS` 改的是列默认值，未加该守卫
- 来源：`nullable-migration-22` §5-3 把表/列解析抽到 `Utils.DataTable.FindTable`/`RequireColumn` 时写明「是否需要该守卫属产品判断，需与共享解析一起定」；共享解析已落地，该判断未做、也无人承接
- 处置方向：把守卫扩展到改列默认值的路径，或明确「改默认值不算改 `id` 列」并保留差异——二者取一都要给判据与用例

## 方案

本票只登记、不实施；分流时按根因拆票，每项各自给判据与 `Emuera.Tests` 用例。1、2 是可达缺陷，优先级高于 3~6：

| # | 待定事项 | 两种取法 |
| --- | --- | --- |
| 1 | `XML_ADDATTRIBUTE` method 0 的 `node.Attributes!` | method 0 分支补 `node is XmlElement` 门禁并抛 `CodeEE`（NRE → 可读错误），`docs/builtin-functions.md:595` 记载的既有 NRE 行为随之变更，须同步该文档。**判据已由实施结果取代**：核对在线文档后改为按「Returns 0 on failure」返 0，且同批扩到 `XML_ADDNODE` 的同类节点种类（实施范围大于本表原定） |
| 2 | `DATATABLE_TO_XML` 的 `(… as string[])!` | 规则补 `RefString1D`，或实现显式拒绝非一维；同时核对 `docs/builtin-functions.md:668` 的描述（**已取前一取法**） |
| 3 | `getDivideIndex` 的 `if (part == null)` | 改 `css == null`（不可达路径上 NRE → -1），或保留现状只作注释清理 |
| 4 | `SP_SPLIT` 与 `SP_HTMLSPLIT` 的回退不一致 | 统一（补 `?? new VariableTerm(RESULT…)`），或明确保留差异 |
| 5 | `FORM_STR_ANY` 的 `RETURNFORM` 死分支 | 删掉，或把 `RETURNFORM` 的注册改回 `FORM_STR_ANY`（后者改变参数形状，须先定语义） |
| 6 | `DT_COLUMN_OPTIONS` 的 `id` 列守卫 | 扩展守卫，或明确保留差异 |

## 实施结果(2026-10-06)

第 1、2 项已落地(第 3~6 项仍待定),两者都先核对了 upstream 在线文档再定做法。

### 第 1 项:`XML_ADDATTRIBUTE`/`XML_ADDNODE` 的节点种类

在线文档([XML_ADDATTRIBUTE](https://evilmask.gitlab.io/emuera.em.doc/en/Reference/XML_ADDATTRIBUTE.html) / [XML_ADDNODE](https://evilmask.gitlab.io/emuera.em.doc/en/Reference/XML_ADDNODE.html))把「xpath 选中元素节点」写进契约(methodType 0 以选中元素为父/宿主,1、2 才是相对选中**属性**/**非根**元素),且失败语义统一写作「Returns the match count on success. **Returns `0` on failure.**」,两页都没有记载非元素节点上的任何行为。故本项**不新增 Lang 文案、不抛 `CodeEE`**,改为按文档返 0——这也与族内既有约定一致(1/2 选中元素时本就返 0),`!` 的删除因此不是「另立新错误」而是「让结果回到文档的失败语义」。做法:

- `Insert` 的 Attribute 臂:`method == 0` 加 `node is not XmlElement` 门禁(文本/属性/文档节点的 `XmlNode.Attributes` 为 null);`method 1/2` 的 `node is XmlAttribute` 门禁改为属性模式 `node is not XmlAttribute { OwnerElement: XmlElement owner }`,顺带消掉 `attr!`/`OwnerElement!` 四处假断言(`attr!`×2、`OwnerElement!`×2)。
- `Insert` 的 Node 臂(同批处理,同根因):`method == 0` 加 `node is not XmlElement`(文本节点 `AppendChild` 抛「The current node cannot contain other nodes」、属性节点抛「the specified node is the wrong type」);`method 1/2` 的判据由 `node.ParentNode == null` 收紧为 `node.ParentNode is not XmlElement`——新情况是**父节点为文档的参照节点**:根元素(文档正写明 1/2 不作用于根节点),以及顶层注释/处理指令等同样以文档为父的节点(实测这两类 `ParentNode` 都是 `XmlDocument`,`InsertBefore` 都抛「This document already has a 'DocumentElement' node」);属性节点与文档节点的 `ParentNode` 本就是 null、旧实现已返 0,不属新情况。
- 调用点 `XmlAddNodeMethod.GetIntValue` 的单匹配分支去掉了 `&& method > 0`:任何 method 的失败都返 0(多匹配仍沿用「返匹配数而不新增」的既定行为,与本地契约表一致,不在本次改动内)。
- 实测边界(`.NET 10`,探针取证):`XmlAttribute.ParentNode` 为 **null**(属性不经 `ChildNodes` 到达,只经 `OwnerElement`),故「属性作参照节点的 1/2」与「文本节点的 1/2」在旧实现下**本就成立**——这两条按钉子保留,收紧只覆盖今天会抛异常的组合。

`docs/builtin-functions.md` 的说明列由 `tracker/arg-contract/builtin-notes.json` 生成,两个注册名的 notes 已改写为「不新增」并写明返 0 只发生在单匹配(多匹配沿用既定的「返匹配数」)——多匹配下 setAll 循环丢弃 `Insert` 的返回值,原「不新增并返 0」对多匹配不成立;`bang-sites.md` 的 `XmlAddNodeMethod.Insert#1` 结论列由 A 更正为 C(判定进度表 A 282 → 281、C 16 → 17)。

### 第 2 项:`DT_TOXML` 的输出槽缺维度声明

与 `tracker/arg-contract` 的 `arg-contract-05` 同源:该票把规则表解释收敛到检查期的同时四处「读输出数组」的硬转型槽已标 `ArgType.NotNull`,而**同形的姊妹站点都已带维度**(`DataTableSelectMethod` 的 `RefInt1D`、`EnumNameMethod`/`EnumFilesMethod` 的 `RefString1D`),只有 `DataTableToXmlMethod` 仍是 `RefString`,属遗漏。故只改一处:slot 1 声明 `ArgType.RefString1D | ArgType.NotNull`(与三处姊妹同形),函数体的 `as string[]` 因 `as` 语义保留 `!`。

行为变更(实现期实测):二维/三维字符串数组由运行期空引用变为检查期可读 `CodeEE(ArgIsNotNDStrArray)`;标量字符串变量的报错由运行期 `CodeEE(GetSize0DVar)` 提前为检查期 `ArgIsNotNDStrArray`(都是 `CodeEE`,文案不同);一维字符串数组与数量省略不受影响。`docs/builtin-functions.md` 重跑生成器后 `DT_TOXML` 的实参形状由 `str, ref str?` 变为 `str, ref str[]?`;说明列的 `desc` 原写「模式写入 ref 字符串变量」、与新的 1 维要求相矛盾,已改为「模式写入所传的一维字符串数组变量」,并在 `notes` 补上「槽2 给出时须为一维字符串数组变量(规则档 `RefString1D`+`NotNull`)、标量/二维/三维在检查期报 `CodeEE(ArgIsNotNDStrArray)`」。

### 验证

- 新增 `Emuera.Tests/Runtime/Script/Statements/Function/XmlInsertTargetKindTests.cs` 11 条(属性/文本/根/元素/正控制)+ `CheckArgumentTypeEx` 4 条(1D 通过,标量/2D/3D 被拒)。先把实现 `git stash` 回退复测:本批 15 条新用例中 **8 条红**,与「今天会抛异常或放行错误形状」的判定一一对应(3 条 §2 + 5 条 §1;属性作参照节点与文本节点那两条按预期在旧实现下也绿)。
- 全量构建 0 错误、CS86xx 0 条;`Emuera.Tests` 784 全绿。
- `python3.14 tracker/arg-contract/builtin-contract.py` 自校验通过(266 名 / 183 类 / 114 规则段,可省略 96、拒绝空槽 54 均未变)。
- 余下四处假断言(`attr!`×2、`OwnerElement!`×2)随 `Insert` 改写消失,`bang-sites.md` 的「已失效站点」已按该文的既有约定登记这 5 处(含判定更正的 `Insert#1`)。

### 仍未做

第 3~6 项(`getDivideIndex` 笔误、`SP_SPLIT` 与 `SP_HTMLSPLIT` 的回退不一致、`RETURNFORM` 死分支、`DT_COLUMN_OPTIONS` 的 `id` 列守卫)保持原判,未实施。

## 影响

- 1、2 触及内建函数 `XML_ADDATTRIBUTE`/`XML_ADDNODE`/`DATATABLE_TO_XML` 的插入目标与实参校验，修法改变脚本可观测行为，已各自给出判据与 `Emuera.Tests` 用例；实际取法见「实施结果」——**第 1 项不是票面原写的 `CodeEE`，而是按在线文档的失败语义返 0**（原写的 NRE/`InvalidOperationException` → 返 0），第 2 项取「规则补 `RefString1D`」
- 第 1 项的实施范围包含票面「任务」未列的 `XML_ADDNODE`（同一处 `Insert` 的 Node 臂：method 0 的非元素节点、method 1/2 的以文档为父的参照节点——根元素与顶层注释/处理指令），属同根因同批处理；`docs/builtin-functions.md` 的 `XML_ADDNODE` 说明列随之改写
- 3、4 当前不可达，属清理项；4 的两种取法行为不同（回退到 RESULT vs 报错），需先定语义
- 5 的注册改动会改变 `RETURNFORM` 的参数形状；6 的两种取法行为不同（拦下改列默认值 vs 放行），若判定加守卫则 `DT_COLUMN_OPTIONS` 的可观测行为随之变化，须给 `Emuera.Tests` 用例
- 本票不动 `nullable-migration-22` §1 已补的理由注释；§1 的判定更正（`node.Attributes!` 由 A 改为 C）已同步 `bang-sites.md` 的结论列、判定进度表（A 281 / C 17）与「已失效站点」；§2 的站点属 `(x as T)!` 桶，不在 `bang-sites.md` 的残差范围内，无需同步
