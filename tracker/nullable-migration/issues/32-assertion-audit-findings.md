# Nullable:断言审计收尾时发现的三处疑似缺陷与一处不一致

id: nullable-migration-32
Status: needs-triage
Type: bug

## 任务

`nullable-migration-22` §1 补理由注释时逐站读码，发现下列位置「判 A（不变量成立）」的判定与代码事实不符，或规则与实现形状不一致。四处都**未改动**（§1 是注释级工作，行为变更须另立判据），集中登记待分流；其中 1、2 两条是可达缺陷，3 是死判断，4 是产品一致性问题。分流时可按根因拆成多张票。

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

## 影响

- 1、2 触及内建函数 `XML_ADDATTRIBUTE`/`DATATABLE_TO_XML` 的实参校验，修法会影响脚本可观测行为（NRE → `CodeEE`），须各自给判据与 `Emuera.Tests` 用例
- 3、4 当前不可达，属清理项；4 的两种取法行为不同（回退到 RESULT vs 报错），需先定语义
- 本票不动 `nullable-migration-22` §1 已补的理由注释；判定更正后（若 1、2 改为 C 类）应同步 `bang-sites.md` 的结论列
