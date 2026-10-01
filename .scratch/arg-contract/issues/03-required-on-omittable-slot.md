# 函数体在「设计上可省略」的实参槽上解引用 null

Status: in-progress
Type: bug

## 任务

与 arg-contract/issues/02 不同：本票里**检查器是对的**——规则块的 `OmitStart` 正确声明了「哪些槽可省略」，null 因此是设计允许的合法输入；**错的是函数体**，它用 `arguments.Required(i)`（即 `arguments[i]!`，运行时无操作）无条件解引用一个设计上可空的槽。

这条错误假设的书面出处有两处，均已核实：

- `.scratch/nullable-migration/issues/13-null-forgiving-cleanup.md` 的**提案代码块**把该访问器的注释写成「The parser rejects an omitted slot before an implementation reads it」；
- `.scratch/nullable-migration/issues/08-expression-method-chain.md` 写「`i >= OmitStart` 的槽位允许省略」，却据此把 `!` 论证为「可证的注解层断言」——缺口就在这两句之间。

落地版注释（`ArgumentListExtensions.Required` 的注释）已按 `nullable-migration/issues/13` 的结论改写，**不再断言那条不变量**，改为「担保只对规则表标为必填的槽成立」（无 `OmitStart` 的块 / `i < OmitStart` / `ArgType.NotNull`；变长尾部的标志由下标落到的那个变长规则决定）；而该不变量对 `i >= OmitStart` 的槽本就不成立（检查器只拒绝 `OmitStart` 未写的整块、`i < OmitStart` 及带 `NotNull` 的槽）。故本票要更正的是「调用方担保」这一措辞——对设计上可省略的槽，调用方担保不了。`nullable-migration/issues/13` 与 `nullable-migration/issues/08` 两票已回填（见「回填」）。

### 已核实可达的三例

`RAND`（规则见 `Creator.Method.cs` 的 `RandMethod` 构造器：`{Int, Int}`、`OmitStart = 1`；体 `RandMethod.GetIntValue`）：

```csharp
if (arguments.Count == 1) { max = arguments.Required(0)...; }
else {
    if (arguments[0] != null) { min = arguments.Required(0)...; }   // 槽 0 判了空
    max = arguments.Required(1).GetIntValue(exm);                   // 槽 1 没判
}
```

`RAND(1,,)` → `[1, null]`（长度 2，尾逗号被吞）→ `Count == 1` 为假 → `RandMethod.GetIntValue` 里那处 `Required(1)` 解引用 null → **NRE**。槽 1 被 `OmitStart = 1` 声明为可省略，故 null 是合法输入。

`HTML_STRINGLEN`（`HtmlStringLenMethod` 构造器 `{String, Int}`、`OmitStart = 1`；体 `HtmlStringLenMethod.GetIntValue`）：

```csharp
if (arguments.Count == 1 || arguments.Required(1).GetIntValue(exm) == 0)   // 只有 Count 守卫
```

`HTML_STRINGLEN("x",,)` → `["x", null]` → `Count == 1` 为假 → 同一句里那处 `Required(1)` 解引用 null → **NRE**。注意 `arguments.Count` 守卫**挡不住 `,,` 形态**——这是本类的关键形态。

`REGEXPMATCH`（`RegexpMatchMethod` 构造器段 1 `{String, String, Int}`、`OmitStart = 2`；段 2 无 `OmitStart` 本就拒 null；体 `RegexpMatchMethod.GetIntValue`）：

```csharp
if (arguments.Count == 3 && arguments.Required(2).GetIntValue(exm) != 0)   // Count == 3 也挡不住 ,,
```

`REGEXPMATCH("a","b",,)` → `["a","b",null]`（Count 保持 3）→ 该 `Count == 3` 分支里解引用 null → **NRE**。该槽在 [slots-worklist.md](../slots-worklist.md) A 表内，是片 1 的第三例。

### 附：口径外的第二类——数量省略即越界（已随片 1 附修并补用例）

A/B 表只建模「显式空槽 → null」。与它正交的还有一类：**函数体无条件读某槽，而规则表把该槽划进了可省略区间**（`i >= OmitStart`）。此时不需要显式空槽——「数量省略」本身就让列表短于该下标（`f(v0,…,v(i-1))` 的 `Count == i`），函数体读 `arguments[i]` 即越界（`ArgumentOutOfRangeException`）。

标 `ArgType.NotNull`（旧名 `DisallowVoid`）**修不掉**这一类：检查器的判空循环只走 `i < arguments.Count`（`FunctionMethod.CheckArgumentTypeEx`），槽 `i` 根本不在其中，故拒绝侧标志对它完全无效。正解是**抬 `OmitStart`**（或改函数体）。

已对 A 表（42 项 / 47 个站点）逐点核过（判据：读该 `Required(i)` 时是否存在保证 `Count >= i + 1` 的守卫），只有两处属这一类，均已抬 `OmitStart` 修掉（这两槽不再落在可省略区间；站点数按工作表 A 表「站点」列合计，同一方法内多处记 `×N`）：

`HOTKEY_STATE`（`HotkeyStateMethod`，规则 `{Int, Int}`、原 `OmitStart = 1`；体 `HotkeyStateMethod.GetIntValue`）：
- 函数体无条件读 `Required(0)`/`Required(1)`，第二参是要写入的热键状态，没有可代的默认值 ⇒ `OmitStart` 改 **2**（与槽数相等即不可省略；类里 `HotkeyStateInitMethod` 的 `{Int}` + `OmitStart = 1` 就是这个形状）。不删 `OmitStart` 是有意的：删掉会让「114 个块 / 91 个带 `OmitStart` / 23 个未写」这组 `arg-contract/issues/02` 的审计数字失效，而 `OmitStart = 2` 与删掉在「`Count >= 2` 且 `<= 2`、全槽 `i < OmitStart` 拒 null」上等价，**报错文案不同**：删掉（`OmitStart = -1`）走 `ArgsCountNotMatches`，`= 2`（等于槽数）走 `NotEnoughArgs`（`FunctionMethod.CheckArgumentTypeEx` 里对应的两条计数分支）。
- 修前 `HOTKEY_STATE(0)` → `[0]`（Count 1）→ 检查器放行（`Count >= OmitStart`）→ 函数体越界。

`GSETFONT`（`GraphicsSetFontMethod`，规则 `{Int, String, Int, Int}`、原 `OmitStart = 2`；体 `GraphicsSetFontMethod.GetIntValue`）：
- 函数体在 `if (!g.IsCreated) return 0;` 早退之后**无条件**读 `Required(1)`/`Required(2)` ⇒ `fontSize` 必填，故 `OmitStart` 改 3（只 `fontStyle` 可省）。类里被注释掉的旧契约与此一致（该构造器里被注释掉的旧手工检查要求 ≥3 个实参，更上方两条被注释掉的旧 `argumentTypeArray` 也把 `fontSize` 列为必填），是 `OmitStart = 2` 写小了的旁证。
- 修前 `GSETFONT(<已创建图形id>, "f")` → `[0,"f"]`（Count 2）→ 检查器放行 → 函数体越界（图形未创建时先在 `if (!g.IsCreated)` 处返回，故触发需要图形已创建）。

两处的「甲」（`HOTKEY_STATE(0,)`、`GSETFONT(0,"f",,)`）被同一处 `OmitStart` 一并拒掉——抬 `OmitStart` 同时覆盖两维，故这一类**不应**在函数体补 `Count` 守卫（那只修数量省略、不修显式空槽）。用例落在既有 seam（`Emuera.Tests/Runtime/Script/Statements/Function/CheckArgumentTypeEx.cs`）：计数省略与显式空槽各断言被拒，`GSETFONT` 的 3/4 实参与 `HOTKEY_STATE` 的 2 实参正控制继续通过。

同一把尺子的余项：A 表其余站点读该槽时都有保证 `Count >= i + 1` 的守卫（`Count > i`、`== i+1`、`>= i+1` 三种写法，含 `Count < k || …`、`Count == 1 || …` 的短路形态，如 `EnumFilesMethod.GetIntValue`、`VarSetExMethod.GetIntValue`）；另有 6 个 (类,槽) 连「甲」也够不到，其中 2 个是预筛的跨构造器假阳性（见 [slots-worklist.md](../slots-worklist.md) D 段）。**全集里未被 `Required` 读取的 20 项也已用同一把尺子逐项核过**（见片 2-D；无一处越界）。

### UDF 族：同型站点存在，但落在**死代码**上（已核实，本节移出本票止血范围）

用户定义函数（`@FOO`）**不经过** `argumentTypeArrayEx`/`CheckArgumentTypeEx`（`IdentifierDictionary.GetFunctionMethod` 在 `method.CheckArgumentType` 之前就按 `func.IsMethod` 返回），是另一套机制：**活路径**的形状来自函数标签的形参 `FunctionLabelLine.Arg`/`Def`（由 `ErbLoader.parseLabel` 建立），检查在 `CalledFunction.TryConvertArg`，产物是 `UserDefinedFunctionArgument`。而 `UserDefinedRefMethod.ArgTypeList`（枚举 `UserDifinedFunctionDataArgType`，注意拼写为 `Difined`）属于**另一条（已死的）ref 分支**，与活路径无关；`ArgTypeList` 这个名字在两族里指两样东西。

`UserDefinedMethodTerm.cs` 的 `UserDefinedRefMethodTerm.Restructure` 里的空槽断言确实存在：

```csharp
// TODO: srcArgs can hold null for an omitted argument, as FunctionMethodTerm.Restructure accepts;
// decide whether to skip null elements instead of asserting them non-null.
if ((reffunc.ArgTypeList[i] & ...__Ref) == ...__Ref)
    srcArgs.Required(i).Restructure(exm);                  // 空槽 → NRE
else
    srcArgs[i] = srcArgs.Required(i).Restructure(exm);     // 同
```

但**整条 ref 分支自解析起就不可达**，故它不是止血项：

- `refmethodDic` 只由 `IdentifierDictionary.AddRefMethod` 填充，而它唯一的调用点被注释掉（`ErhLoader.cs` 的 `ErhLoader.analyzeSharpFunction`；注意类名是 **ErhLoader**，不是 `ErbLoader`），该函数体直接 `throw new NotImplCodeEE()`——**.ERH 的声明式 ref 函数**（`#FUNCTION`/`#FUNCTIONS`）处于停用状态。这个限定语不能省：`.erb` 内的 `#FUNCTION` 标签属性仍活着（`LogicalLineParser.cs` 解析 `#FUNCTION` 标签属性时置 `label.IsMethod`），它正是 UDF 能作式中函数调用的机制；
- 故 `IdentifierDictionary.GetFunctionMethod` 的两处 `refmethodDic` 查询永不命中，`UserDefinedRefMethodTerm` / `UserDefinedRefMethodNoArgTerm` 永不被构造；`SP_REF_ArgumentBuilder.CreateArgument`（`ArgumentBuilder.cs` 的 `SP_REF_ArgumentBuilder.CreateArgument`）在 `GetRefMethod` 恒 null 后必进「标识符不是 `ReferenceToken` 就 `warn(ArgIsNotRef)` 并 `return null`」那一支，其后的 `refm != null` 分支不可达；`REF_Instruction.DoInstruction`（`Instraction.Child.cs`）里抛出 `CodeEE` 的那段之后，那句 `arg.RefMethodToken != null` 恒假（该段被 `#pragma warning disable CS0162` 标为不可达）。死代码判定已含反射/测试/插件路径：三个类均 `internal sealed`，构造点全库唯一，测试与插件 API 都没有入口。

活着的 UDF 路径（`@FOO(...)` → `UserDefinedMethodTerm.TryCreate` → `TryConvertArg`）**本就 null 安全**：省略实参在 `CalledFunction.TryConvertArg`（`CalledFunction.TryConvertArg`）即代入 `FunctionLabelLine.Def` 的默认值——该默认值由函数标签 `@NAME(ARG, ARG:1 = 5)` 的形参默认表达式在 `ErbLoader.parseLabel`（`ErbLoader.cs` 的 `parseLabel`）建立（形参是 ARG/ARGS/私有变量却未写 `=` 时填 0/空串；`CompatiFuncArgOptional` 默认 false，仅在 `Def[i]` 真为 null 时报 `CodeEE`）。只有 `Def[i]` 为空且开启 `CompatiFuncArgOptional` 时产物里才会留 null，而读 `Arguments[]` 的消费者都已跳过（`UserDefinedFunctionArgument.SetTransporter` 与 `.Restructure`，同文件的 `SetTransporter` 与 `Restructure`；另有 `Process.State.cs` 的 `IntoFunction`）。故 UDF 侧无止血项，TODO 的处置记为「随该特性复活再定」。

**两族空槽语义必须分开，不可统一**（已裁定，见 spec.md）：

| | 内建函数 | UDF |
| --- | --- | --- |
| 默认值声明处 | **无**（`NullTerm` 只出现在 UDF 默认值声明与其解析兜底：`ExpressionParser.cs` 的三处构造点，由 `ErbLoader.parseLabel` 消费；显式 `= 常量` 走同一处的另一分支） | **有**，逐参数（`@FOO(A = 1)` → `FunctionLabelLine.Def`） |
| 空槽应有语义 | 按错误处理 | 取已声明的默认值 |

### 范围（量级估计，非逐点定案）

以规则表对 `Creator.Method.cs` 的 372 个字面量索引 `Required` 站点逐点核对（口径：**忽略** `DisallowVoid`（今名 `ArgType.NotNull`，改名见片 3）；若把它计为必填，四格变为 135 / 107 / 73 / 57。405 处 `Required(` 中另 33 处为非字面量形态，不在本票扫描范围内）：

| 分类 | 处数 | 复核 |
| --- | --- | --- |
| 有据：`i < OmitStart`（规则块路径） | 132 | 首段口径；按「任一段」为 133，见下 |
| 有据：类只设 `argumentTypeArray`（无规则块），`FunctionMethod.CheckArgumentType` 对全部槽拒 null | 107 | 已复现 |
| 索赔未证：`i >= OmitStart` 且非 `DisallowVoid`（今 `NotNull`；设计上可省略） | 76 | 首段口径；按「任一段可省略」为 77，见下 |
| 索赔未证：所属类含 `OmitStart == -1` 的块（即 arg-contract/issues/02 的 16 个类） | 57 | 已复现（16 个类与 `arg-contract/issues/02` 列的 23 个块一致） |
| 合计 | 372 = 405 − 33 | 未证 132–134，约 36% |

**表内 132 / 76 是「按各类首段规则块 + 忽略 `DisallowVoid`」这一口径的记账值**（本节数字已含「附」的两处 `OmitStart` 附修）。同一首段口径下把标志计入必填即 135 / 73。多段类里同一个槽可能「被某段允许、被另一段拒绝」，故另有两种口径，且复算确认浮动只由两处站点决定——`XmlGetMethod` 的 `Required(2)`（`XmlGetMethod.GetIntValue` 读该槽处，段 `OmitStart = [2,3,2,3]`）与 `DataTableColumnManagementMethod` 的 `Required(1)`（`DataTableColumnManagementMethod.GetIntValue` 读该槽处，段 `OmitStart = [2,1]`）：

- 「任一段允许即算有据」→ 133 / 75（未证合计 132）
- 「任一段可省略即算未证」→ 131 / 77（未证合计 134）

**本票取 131 / 77（未证合计 134）**：检查器在首个无错段即 `return null`（`FunctionMethod.CheckArgumentTypeEx`）、段内可省略槽 `continue`（同处判据的 else 支），故只要**任一**段能放行该 null 就算「检查器不拒」——「任一段要求即有据」与实现相反。逐点裁定须按 (类,槽) 逐段核对，不能只用首段。

**「57 处」是类级代理，且描述的是 arg-contract/issues/02 修复前的行为**：「所属类含 `OmitStart == -1` 的块」这一格只说明该类里有必填块，不等于该站点所在的段必填。逐点复算 57 处：41 处首段本身即 `OmitStart == -1`、9 处 `i < 首段 OmitStart`（02 已落地，现状下检查器对全部槽拒 null ⇒ 实际**有据**），只有 7 处首段真可省略。

**「24 个类有多段」为真**（`XmlAddNodeMethod` 6 段、`XmlGetMethod` 4 段、`MaxArrayMethod` 4 段等）。

**站点级「有真实判空 / 只有 `Count` 守卫 / 无守卫」的分桶已废弃**：判定窗口与是否按同一索引未写明，不可复核，此处不再记数。槽级口径由工作表承担——判空谓词取 `arguments[i] == null`、`arguments[i]?.` 与列表模式 `arguments is [… not null …]` 三种写法（只认第一种时，16 个用列表模式判空的槽会被误判进 A 表，见风险 2），得 A 表 42 / B 表 34，见 [slots-worklist.md](../slots-worklist.md)。**未证 ≠ 缺陷**：未证站点里相当一部分因调用路径保证该槽必被填充而不可达，需逐点做可达性分析才能定案；但 `f(a,…,a(i-1),,)` 这种「显式空槽」形态对 `i >= OmitStart` 的槽一律能通过检查器（见机制事实 3），故可达面比直觉大，不能默认「多数不可达」。

上表是**站点级**口径；按 (类,槽) 去重、并限定「可省略且被 `Required` 读取」后的清单与 42/34 分桶见 [slots-worklist.md](../slots-worklist.md)——那是片 2 的实际工作面。

完成标准：

- 逐槽裁定（口径见 spec.md 的「逐槽可空性」），按下述五类归置：
  1. **有意**（工作表 B 表 34 项，另加未被 `Required` 读取的同类 5 项：`DT_SELECT[1]`/`[2]`、`GDRAWG[10]` 与片 2-D 补判的 `DT_ROW_ADD[2]`、`DT_CELL_SET[3]`；例：`GraphicsDrawGMethod.GetIntValue` 的 `arguments[10] == null` = 不带颜色矩阵，该槽未经 `Required` 读取故不在 B 表内）→ 规则表该槽**保持可空**，并把该语义写进注释（把「意外」变成「声明意图」）
  2. **无意但被自身 `Count` 守卫挡住** → 确认 `,,` 形态（Count 不变、槽为 null）确实够不到；够得到则补 `== null` 判空。守卫必须**保证 `Count >= i + 1`**（`Count > i` / `== i+1` / `>= i+1`）：只写「所在方法里出现过 `arguments.Count`」不算（见第 5 类）。
  3. **无意且可达**（`RAND(1,,)`、`HTML_STRINGLEN("x",,)`、`REGEXPMATCH("a","b",,)` 已核实；见片 1）→ **让检查器拒绝**（`CodeEE` + `ArgCanNotBeNull`）；规则表里没有默认值声明处（那是 UDF 侧的机制），故本票不发明默认值
  4. **转发槽**（函数体既不读也不解释，原样交给下游；例：`GetMethMethod.GetIntValue` 的 `arguments.Skip(2)` → `GetFunctionMethod(…, methArgs, true)`）→ 规则表该槽**必须保持可空**，注释写明「null 由下游解释：目标为 UDF 时取该 UDF 形参默认值（`ErbLoader.parseLabel` 建立；`CalledFunction.TryConvertArg` 代入，见 `Process.CalledFunction.cs`），目标不存在时该槽不被读取」。**不得**按 A 表机械标 `NotNull`
  5. **数量省略即越界**（与本票的 null 维度正交，见上「附」；已举证 2 处并附修）→ 函数体无条件读 `Required(i)` 而 `i >= OmitStart` 时，**少写尾实参**就足以越界，标 `NotNull` 无效；抬 `OmitStart`（或改函数体）才是正解。裁定 A 表任一槽前，先用这把尺子过一遍
- 裁定结果按槽写进规则表：**可空性是与 `OmitStart` 正交的逐槽属性**；`DisallowVoid` 就是这条路，本票**取 B（见片 3）——把它改名为 `NotNull` 并铺开**（片 1 前全库仅 `ArrayMultiSortExMethod` 构造器的两处字面量，加片 1 的三处共 5 处）
- 注意「索赔未证」的 134 处（口径 131 / 77）里有一部分**本就该可空**（属第 1 类），故「未证」不等于「缺陷」；本票的产出是**逐槽的语义裁定**，不是「全部改成拒绝」
- UDF 侧：ref 分支已确认为死代码（见上），本票不定其 null 处置；活路径的既有语义（空槽取 `FunctionLabelLine.Def` 的默认值）由 `CalledFunction.TryConvertArg` 承载，用一条「省略实参 → 取默认值」的用例钉住即可，不写死代码用例
- 表驱动用例：对每个含可省略槽的注册名，按显式空槽的**位置形态**各构造一例（前位 `f(,b)` / 中位 `f(a,,c)` / 末位 `f(a,,)`，断言得到 `CodeEE` 或该函数的默认值语义，而非 NRE）。原列的 `f(a,)`（单个尾逗号）按机制事实 3 **不产生空槽**，故它不是空槽形态，而作为「数量省略」这一正交轴单独成表（逐注册名钉住最小实参数 + 列表内无 null）；第 5 类的函数另补**数量省略**形态断言被计数判据拒绝
- 「读 `Required(i)` 时列表必不短于 `i + 1`」这条约束应能机械校验（否则 null 与数量省略两类缺陷都会再长出来）——归 arg-contract/issues/04 的槽位方案

## 方案

片 1 与片 2/3/4（含片 2-C 的变长尾部定案、片 2-D 的补车道 20 项）均已落地，含片 4 的 A10 位置形态与数量省略两轴。

**与 `arg-contract/issues/02` 的区别要在实现时守住**：02 修检查器（把设计上不可省略的槽拦住），本票修函数体（在设计上可省略的槽上不要假装非空）。02 **已落地**（`Status: implemented`，判据即 `FunctionMethod.CheckArgumentTypeEx` 里那句 `list.OmitStart < 0 || i < list.OmitStart || rule.NotNull`），故 `SETVAR("TARGET", )` 已由 02 拒绝，`RAND(1,,)` 仍归本票。

本票的裁定结果**是 arg-contract/issues/04 的设计输入**（访问器必须显式选 null 语义）。

### 决定实现形状的 5 条机制事实（均已核实）

1. **检查器「任一段通过即通过」**：`FunctionMethod.CheckArgumentTypeEx` 在首个无错规则段即 `return null`。⇒ 要让某槽拒 null，必须在该槽出现的**每一个可省略段**都标注；只标一段无效。`ArgType.DisallowVoid`（今 `ArgType.NotNull`）正是两段都标（`ArrayMultiSortExMethod` 的两段）；片 1 的三处各只有一个可省略段，故各标一处即可。
2. **判据落点唯一**：`FunctionMethod.CheckArgumentTypeEx` 的判空分支。
3. **空槽来源唯一**：`ExpressionParser.ReduceArguments` 只吞单个尾逗号（`f(a,b,)` → `[a,b]`、`f(a,b,,)` → `[a,b,null]`），已由既有用例 `CheckArgumentTypeEx.Shape_*` 钉住。
4. **折叠期跳过 null 已有两个范本**：`FunctionMethodTerm.Restructure`（`FunctionMethodTerm.cs`）、`UserDefinedFunctionArgument.Restructure`；UDF 的默认值代入在 `CalledFunction.TryConvertArg`。⇒ 内建槽若裁定为「有意可空」，折叠与取值两侧都要照此跳过；UDF 活路径两侧已具备，无需改动。
5. **`Required` 是本票的口径，不是函数体唯一的取值入口**：54 处活 `arguments[<字面量>]` 里 24 处是硬转型解引用（18 处 `(arguments[i] as VariableTerm)!`、6 处 `(VariableTerm)arguments[i]`），其余才是判空或传参。这些 `arguments[i]` 站点走的同样是「null 放行 + 直接解引用」，但归 arg-contract/issues/04 片②（spec.md 的 33 处活 cast），且与「数量省略」那把尺子也有交叉（见风险 7）；两个车道的槽集合不同，落地时要对齐，不能用本票的 42/34 覆盖 cast 车道的结论。

### 工作面（已量）

| 量 | 数 |
| --- | --- |
| 规则块 / 含可省略槽的块 / 可省略槽总数 | 114 / 88 / 139 |
| `arguments.Required(` 处数 | 405（404 活 + 1 在被注释掉的旧 `CheckArgumentType` 里：`GetnumBMethod.CheckArgumentType`） |
| 可省略 (类,槽) 全集（不论是否被读取） | 96 |
| ├ 被 `Required`(字面量) 读取——当前工作表口径 | 76 |
| │　├ 剥注释后该槽无 null 处理 ⇒ 需标 `NotNull` | 42（41 项已标 `NotNull`，唯 `DataTableColumnManagementMethod[1]` 裁定为可空；其中 2 项仅在注释里有 `== null`） |
| │　└ 剥注释后该槽有 null 处理 ⇒ 有意候选，需人工裁定 | 34 |
| └ 未被 `Required` 读取（片 2-D 已逐项裁定） | 20（有意可空 5 / 转发 2 / 消费型变长尾 2 / 无意且可达 7 / 硬转型解引用 4） |
| 涉及函数（用例表规模） | 52（口径：**被 `Required`(字面量) 读取的可省略槽所属的类数**；全集口径下为 61） |
| 可省略槽跨多段的类（标注勿漏段） | 16 |

上表随「附」的两处 `OmitStart` 附修更新（`HotkeyStateMethod` 不再有可省略槽、`GraphicsSetFontMethod` 只剩槽 3）。另有一把与 null 正交的尺子——「读该槽时是否保证 `Count >= i + 1`」——已对 A 表（42 项）与补车道的 20 项（片 2-D）逐点核过：只有「附」里那 2 处属数量省略越界，其余站点都有保证 `Count >= i + 1` 的守卫。

逐槽清单见 [slots-worklist.md](../slots-worklist.md)（生成器 [slots-worklist.py](../slots-worklist.py)，可复跑；口径见其头注释）。

### 片 1：止血——已举证的 3 处可达 NRE + 2 处数量省略越界（已落地）

```csharp
// Creator.Method.cs 的 RandMethod 构造器  RAND 槽 1
new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
// Creator.Method.cs 的 HtmlStringLenMethod 构造器  HTML_STRINGLEN 槽 1
new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
// Creator.Method.cs 的 RegexpMatchMethod 构造器  REGEXPMATCH 段 1 槽 2（段 2 要求 4 参且无 OmitStart，3 参形态下以计数不符被拒，非拒 null；见片 2 的机制事实 1）
new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
```

片 1 的 3 处标注直接写最终名 `ArgType.NotNull`（与片 3 的改名同批落地），**不在改名面内**；改名只覆盖此前已存在的 5 处（见片 3）。

- 语义裁定：三处内建槽判为「无意且可达」→ **让检查器拒绝**（`CodeEE` + `ArgCanNotBeNull`），不发明默认值（规则表里没有默认值声明处，见 spec.md 的语义裁定）。
- 举证：`RAND(1,,)` 在 `RandMethod.GetIntValue` 取值，`HTML_STRINGLEN("x",,)` 在 `HtmlStringLenMethod.GetIntValue`，`REGEXPMATCH("a","b",,)` 在 `RegexpMatchMethod.GetIntValue`（`arguments.Count == 3` 守卫挡不住 `,,` 形态）。三者修前都直接 NRE ⇒ 执行到这些形态的脚本本就不能工作，把 NRE 换成 `CodeEE`（时机见风险 6）不动任何**修前能正常求值**的写法。注意这不覆盖「含这些写法、但所在分支从不执行」的脚本——它们修前能装载，收严后装载失败（见风险 6）。
- **「3 处」是已举证的站点数，不是可达站点总数**：`f(v0,…,v(i-1),,)` 这类显式空槽形态对 A 表里 `i >= OmitStart` 的槽一律能通过检查器（机制事实 3），函体取值即拿到 null；另有 24 处 `arguments[i]` 硬转型解引用走同一机制（机制事实 5）。其余站点按片 2 逐槽裁定，不可读成「只有这 3 处有缺陷」。
- UDF 侧**不在片 1**：ref 分支已确认为死代码（见「UDF 族」）。
- 用例（落在既有 seam，先红后绿）：由 `CheckArgumentTypeEx.Shape(...)` 造出含 null 的实参表 → `GetFunctionMethod(name, …)` 抛 `CodeEE(ArgCanNotBeNull)`（槽位取 1 基）；正控制 `RAND(5)`、`HTML_STRINGLEN("x")`、`REGEXPMATCH("a","b")` 与 `REGEXPMATCH("a","b",1)` 仍通过。
- **附修（同一片内，见上「附」）**：`HOTKEY_STATE` 的 `OmitStart` 1 → 2、`GSETFONT` 的 `OmitStart` 2 → 3。两处都是「函数体无条件读 `Required(i)` 而规则表把 `i` 划进可省略区间」，且都不靠显式空槽就能触发（数量省略即可）。用例（同文件）：计数省略形态断言被计数判据拒绝（`HOTKEY_STATE`/`GSETFONT` 均走 `NotEnoughArgs`），显式空槽形态断言 `CodeEE(ArgCanNotBeNull)`，`GSETFONT` 的 3/4 实参与 `HOTKEY_STATE` 的 2 实参正控制继续通过。

### 片 2：逐槽裁定与标注（本票主体）

1. **预筛**：`slots-worklist.py` 已剥注释并归并到 (类,槽)，产出 A/B 两表（A = 需标标志的 42 项（41 项已标 `NotNull`、1 项裁定可空）；B = 有意候选 34 项；口径是「被 `Required`(字面量) 读取」这条车道，全集 96 见风险 4）。「有 null 处理」的判据包含 `arguments[i] == null`、`arguments[i]?.` 与列表模式 `arguments is [… not null …]` 三种写法——只认第一种时，16 个用列表模式判空的槽会被误判进 A。启动本片前先过一遍与 null 正交的「数量省略」尺子（见上「附」；A 表已核过，仅 2 处并已附修）。
2. **裁定 B 表 34 项**：null 分支是「显式留空」还是防御性写法/注释残留；**防御性一律归 A**（否则等于凭空承认一个无脚本依赖的语义）。裁定结果写回本票或 worklist。
3. **标注**：A 表逐槽在**全部可省略段**上加标志（机制事实 1）；B 表裁定为有意的槽，在规则段旁写明语义（把「意外」变成「声明意图」）。
4. **机制事实 1 的两个特例**：`XmlGetMethod[2]` 与 `DataTableColumnManagementMethod[1]` 同时出现在「可省略段」与「必填段」里——可省略段仍会放行 null，标注不能因必填段的存在而省略。（附注：逐点核查后 `DataTableColumnManagementMethod[1]` 的可省略段属 `DT_COLUMN_NAMES`（`op == Names`）那一支——该支在读到该槽前就 `return dt.Columns.Count`，这个站点连 null 都够不到；真正需要注意多段归属的是 `XmlGetMethod[2]`。见 [slots-worklist.md](../slots-worklist.md) D 段。）

#### 片 2-A：A 表 42 项 —— 41 项铺标志、1 项裁定可空

逐槽在其**每一段**可省略段的该槽位加 `ArgType.NotNull`（只标一段仍会从别的段放行 null，机制事实 1）。工作表复核：`need 42 / flagged_need 41`。四处与原预筛不同，均已逐点核实：

| 项 | 原预筛 | 落地时核实 | 处置 |
| --- | --- | --- | --- |
| `ReplaceMethod[3]` | 在 A 表内 | 按类分批时被漏读；`REPLACE("a","b","c",,)` 由段 1（`OmitStart = 3`）放行 null → `ReplaceMethod.UniqueRestructure` 与 `ReplaceMethod.GetStrValue` 取值 NRE（段 2 无 `OmitStart`，Count = 4 时也拒 null，但检查器「任一段通过即通过」） | 补标 + 补用例 |
| `DataTableRowSetMethod[1]`（脚本按类内可省略段与站点做笛卡尔匹配时多出的那条） | 跨构造器假阳性（站点属 Set 分支） | 站点确属 Set 分支（`DT_ROW_SET` 走无 `OmitStart` 的段，已拒 null）；但 `DT_ROW_ADD` 的槽 1 由 `arguments.Required(pos)`（变量下标，不在本表车道）读取，`DT_ROW_ADD("t",,"c")` 修前 NRE | 在可省略段（`MatchVariadicGroup` 的 `VariadicString` 位）补标 + 用例 |
| `MapGetStrMethod[1]` | 两个站点里只有 `2849` 为真 | 可省略段只属 `MAP_GETKEYS`（`MAP_GET`/`MAP_TOXML` 走定长 `argumentTypeArray`） | 只标该段；用例用 `MAP_GETKEYS` |
| `DataTableColumnManagementMethod[1]` | A 表行 | 站点在 `op == Names` 早退之后；NAMES 的可省略段把 null 与「不传该实参」同样解释为「写进 RESULTS 数组」（`arguments[1] is VariableTerm` 对 null 为假），今天能正常求值 | **不标**（标了会把可用写法变成 `CodeEE`），加放行用例钉住 |

#### 片 2-B：B 表 34 项逐槽裁定 —— 全部「有意可空」

逐槽取判空行与上下文后，**无一项**属防御性写法或注释残留：34 项的函数体都把 null 与「不传该实参」读成同一个默认值（或走明确分支）。裁定表（空槽语义即已在规则段旁写明的注释）：

| 类[槽] | 空槽语义 | 类[槽] | 空槽语义 |
| --- | --- | --- | --- |
| `ArrayMultiSortExMethod[2]` | 升序（默认 true） | `MatchMethod[2]` | start = 0 |
| `CsvDataMethod[2]` | SP 标志 = 0 | `MatchMethod[3]` | end = 数组长度或 CHARANUM |
| `CsvStrDataMethod[1]` | SP 标志 = 0 | `MaxArrayMethod[1]` | start = 0 |
| `CsvcstrMethod[2]` | SP 标志 = 0 | `MaxArrayMethod[2]` | end = 数组长度或 CHARANUM |
| `EncodeToUniMethod[1]` | position = 0 | `MoneyStrMethod[1]` | `Config` 的默认货币格式 |
| `ExistCsvMethod[1]` | isSp = false | `StrfindMethod[2]` | start = 0 |
| `FindElementMethod[2]` | start = 0 | `SubstringMethod[1]` | start = 0 |
| `FindElementMethod[3]` | end = 数组长度 | `SubstringMethod[2]` | length = -1（到末尾） |
| `FindElementMethod[4]` | isExact = false | `SubstringuMethod[1]` | start = 0 |
| `FindFilesMethod[0]` | pattern = `"*"` | `SubstringuMethod[2]` | length = -1 |
| `FindcharaMethod[2]` | startindex = 0 | `SumArrayMethod[1]` | index1 = 0 |
| `FindcharaMethod[3]` | lastindex = CHARANUM | `SumArrayMethod[2]` | index2 = 数组长度或 CHARANUM |
| `GetMethMethod[1]` | 无兜底：目标未定义时抛 `CodeEE` | `ToStrMethod[1]` | 默认整数格式（`ToString()`） |
| `GetMethsMethod[1]` | 同上 | `VarsizeMethod[1]` | dim = 0 |
| `GetcharaMethod[1]` | CheckSp = false | `InRangeArrayMethod[3]` | start = 0 |
| `JoinMethod[1]` | 分隔符 = `","` | `InRangeArrayMethod[4]` | end = 数组长度或 CHARANUM |
| `JoinMethod[2]` | index1 = 0 | — | — |
| `JoinMethod[3]` | index2 = 余长 | — | — |

落地形态：不加 `NotNull`；在 `Creator.Method.cs` 承载 B 表槽的**每一处**规则段赋值前加一行英文语义注释（可复核口径：注释里点名 `empty slot`/`omitted slot`，实测 **37** 行 = B 表 34 槽用 30 行 + `DT_COLUMN_NAMES` 例外 1 行 + 片 2-C 的 `ARRAYMSORT` 消费型依据 1 行 + 余项 5 的 `DT_SELECT`/`GDRAWG` 2 行 + 片 2-D 的 `DT_ROW_ADD`/`DT_CELL_SET` 3 行；同一类的多个 ctor 分支各写一遍、`DT_ROW_ADD` 那条跨两行，故行数与槽数不等。计数由 `slots-worklist.py` 的计数表复核），覆盖 B 表全部 34 个槽；用例逐槽断言放行。

#### 片 2-C：变长尾部分型定案（风险 3）

6 个含 `Variadic` 且设了 `OmitStart` 的块逐块定案（`DT_ROW_ADD` 的 `VariadicString` 位已在片 2-A，其余 5 块在此）：

| 块 | 型 | 处置 |
| --- | --- | --- |
| `MaxMethod`（`MAX`/`MIN`，`{Int, VariadicInt}`、`OmitStart = 1`，两个 ctor 各一段） | 消费型 | 尾部标 `ArgType.NotNull`：`GetIntValue` 以 `for (int i = 1; i < arguments.Count; i++) arguments.Required(i)` 逐个取值，空槽没有默认值可代 |
| `ArrayMultiSortMethod`（`ARRAYMSORT`，`{RefAny1D, RefAnyArray \| Variadic}`、`OmitStart = 1`） | 消费型 | 尾部标 `ArgType.NotNull`：`GetIntValue` 起手 `arguments[0] as VariableTerm`、末尾 `foreach (VariableTerm term in arguments.Cast<VariableTerm>())` 遍历全部实参；类内被注释掉的旧手工检查器同样要求每个实参非 null |
| `GetMethMethod`/`GetMethsMethod`（`GETMETH`/`GETMETHS`，`OmitStart = 1`） | 转发型 | 尾部**保持可空**（不得标 `NotNull`）：`arguments.Skip(2)` 原样交给下游，null 由被调方解释，规则段旁已写明该语义 |

- 两型的首槽都仍由 `i < OmitStart` 拒空（`MAX(,2)`、`ARRAYMSORT(,DA:0)` 修前即被拒），故标志只铺尾部；数量省略不受影响（`MAX(1)`、`ARRAYMSORT(DA:0)` 继续通过）
- 用例：片 4 的 `MaxMin_WithExplicitEmptySlotInVariadicTail_IsRejected`、`ArrayMsort_WithExplicitEmptySlotInVariadicTail_IsRejected`、`ConsumedVariadic_WithEmptyLeadingSlot_IsRejected`，正控制两条

#### 片 2-D：补车道——未被 `Required`(字面量) 读取的可省略槽（20 项，已落地）

片 2-A/2-B 只覆盖「被 `Required`(字面量) 读取」这条车道（76 / 全集 96）。余下 20 项由 helper 传参、
变长下标 `Required(i)`、`arguments[i]` 硬转型或 `arguments.Skip(k)` 转发读取，机械预筛看不见它们。
本片逐项核查读取路径与数量省略尺子；清单、读取痕迹与逐项结论见 [slots-worklist.md](../slots-worklist.md) 的 E 段
（类别/处置/尺子三列由人工核查填入，脚本负责机械列出读取痕迹、校验「裁定为需标 `NotNull` 的槽是否在全部
可省略段都标了」以及「裁定为可空的槽有没有被误标」）：

| 类别 | 项数 | 槽与处置 |
| --- | --- | --- |
| 有意可空 | 5 | `DataTableSelectMethod[1]`/`[2]`、`GraphicsDrawGMethod[10]`（余项 5 已补注释）；`DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]` **本次补注释 + 用例**（空槽 = 写 DBNull） |
| 转发槽 | 2 | `GetMethMethod[2]`/`GetMethsMethod[2]`，保持可空（片 2-C 注释已写） |
| 消费型变长尾 | 2 | `MaxMethod[1]`/`ArrayMultiSortMethod[1]`，片 2-C 已标 `NotNull` |
| 无意且可达 → 标 `NotNull` | 7 | `XmlAddNodeMethod[4]`/`[5]`、`GraphicsDrawStringMethod[2]`/`[3]`、`GraphicsDrawGWithRotateMethod[3]`/`[4]`、`GraphicsDrawSpriteMethod[6]` |
| 硬转型解引用 | 4 | `EnumNameMethod[1]`、`EnumFilesMethod[3]`、`DataTableSelectMethod[3]`、`DataTableToXmlMethod[1]` → arg-contract/issues/04 片② |

- **7 处「无意且可达」是同一形态**：函数体经 helper（`ReadPoint`/`ReadColormatrix` 内部直接 `Required(argNo)`/`Required(argNo + 1)` 取值，均无判空）或变长下标读取该槽，而规则表把它划进可省略区间，显式空槽一路放行到取值处 → NRE。
  - `XmlAddNodeMethod[4]`：Node 段的 `setAllPos`（`Count == 5 && Required(4)`）与 Attribute 段的 `methodPos`（`Count >= 5 && Required(4)`）都读它，故 Node/Attribute 的**每个**规则块都要标；`XmlAddNodeMethod[5]` 只出现在 Attribute 段（`Count == 6 && Required(5)`）。
  - `GraphicsDrawStringMethod[2]`/`[3]`：`GDRAWTEXT` 的 4 实参形态 `ReadPoint(Name, exm, arguments, 2)` 一次读两槽；`GraphicsDrawGWithRotateMethod[3]`/`[4]`：`GDRAWGWITHROTATE` 的 `ReadPoint(..., 3)`；`GraphicsDrawSpriteMethod[6]`：`GDRAWSPRITE` 7 实参形态的 `ReadColormatrix(..., 6)`（`(VariableTerm)null` 后取成员即 NRE）。
  - 判「拒绝」而非「空槽取默认值」：规则表没有默认值声明处（同片 1），而这些槽的**数量省略**形态本就继续合法，拒绝侧只把修前必 NRE 的写法换成 `CodeEE(ArgCanNotBeNull)`。
- **`DataTableRowSetMethod[2]` 是形态分裂的槽，故不标 `NotNull`**：`DT_ROW_ADD` 的变长对形态里它是 value 位，空槽经 `SetValue(..., AExpression v)` 的 `v == null` 分支写成 `DBNull`（**有意**）；4 实参数组形态共用该槽，但同类第二段（无 `OmitStart`）拒绝一切空槽，函数体在数组形态下读不到 null。标 `NotNull` 会连带拒掉变长形态的 DBNull 写法 → 只补注释。这条也是「同一槽在不同调用形态里语义不同」的首个实例，是 arg-contract/issues/04 槽位三态（缺失/显式留空/有值）的现成用例。
- **数量省略尺子**：20 项逐项核过（E 段「尺子」列），**无一处**「少写尾实参即越界」，故本片没有第三处 `OmitStart` 附修。
- 用例（落在既有 seam `CheckArgumentTypeEx`）：`OmittableSlot_WithExplicitEmptySlot_IsRejected` 追加 11 行（7 槽的必要注册名/形态）、`IntentionalEmptySlot_IsAccepted` 追加 2 行、`CountOmittedArgument_IsAccepted_AndYieldsNoEmptySlot` 追加 3 行（新收紧的三个注册名）、正控制 `SupplementalSlots_WithFilledArgs_AreAccepted`（9 条填满形态）。
- 落地后计数（由 `slots-worklist.py` 复核）：`ArgType.NotNull` 83 处 / 73 行（含注释；剥注释 77 处 / 67 行）；拒绝侧共 **50** 个 (类,槽) = A 表 41 + 片 2-C 的 2（消费型变长尾）+ 本片 7；可省略 (类,槽) 全集 96 = A/B 两段 76 + 本片 20。

### 片 3：机制收尾——取 B（已定），A 留作收尾条件

| | 选项 A（默认翻转） | 选项 B（保守，**已选**） |
| --- | --- | --- |
| 默认 | **拒绝** null，显式 `Nullable` 才放行 | 保持放行，逐槽标 `NotNull` |
| 收益 | 未枚举到的槽（含日后新增的函数）也被拦住 | 纯增量、可随时停、每次改动可证明不回退 |
| 前提 | 96 个 (类,槽) 的 `Nullable` 集合**枚举完整**（含未被 `Required` 读取的 20 项与转发槽）+ variadic 尾部的标注语义定案 | 无 |
| 代价 | 枚举不全即误拒现存脚本，且罚则是硬错误（`CodeEE`，默认在首次执行该行时抛出；AnalysisMode / `ReduceArgumentOnLoad` 下提前到装载期），无警告级过渡 | 漏标即漏防；新增函数默认继续不安全 |

**决定：取 B。** 依据：

1. **每次改动可证明不回退**——`NotNull` 只加在「函数体必须非空才能工作」的槽上，效果是把修前的 NRE 变成 `CodeEE`：被执行到的调用，其取值语义一律不变（装载面见风险 6）。
2. **A 要求先补齐枚举**——工作表只覆盖 76 / 全集 96，未覆盖的 20 项里既有转发槽、也有类内**有意**处理 null 的槽（`DT_SELECT[1]`/`[2]`、`GetMethMethod[2]`/`GetMethsMethod[2]`，见风险 4）；枚举补齐前 A 会误拒这些槽上的写法，B 不要求枚举完整。
3. **主流语言翻默认极性都带非致命诊断作过渡**（TS `strictNullChecks` 先作 opt-in、C# NRT 是警告并按文件开启）；Emuera 的 `CodeEE` 是装载期硬错误，缺这一层缓冲。
4. B 与原裁定的**默认极性**相反（原裁定假定「有意可空」需在规则表显式声明，而放行在 B 下是默认）——spec.md 的语义裁定已就地补注落地顺序与「翻转前不引入当前无作用的 `Nullable` 标志位」，见该节。

**落地内容**：`ArgType.DisallowVoid` → `ArgType.NotNull`，**改名面 5 处**（`FunctionMethod.cs` 的枚举成员、`_ArgType` 属性与 `CheckArgumentTypeEx` 的判据共 3 处；`Creator.Method.cs` 里 `ArrayMultiSortExMethod` 的两段共 2 处），随后按片 2 的 A 表逐槽铺开。片 1 新增标注的 3 处（`HtmlStringLenMethod`、`RegexpMatchMethod`、`RandMethod`）写的就是新名，不属改名面。判据与语义不变，改名后既有用例全绿作护栏。

**A 的收尾条件（修正后）**：A 与 B 等价的时点不是「139 个可省略槽裁定完」（139 是 (块,槽) 段实例数），而是 **96 个 (类,槽) 的 `Nullable` 集合枚举完整**；届时可机械翻转。**枚举已由片 2-D 补齐**——A/B 两段 76 + 补车道 20 全部有裁定，故这一半条件已满足；翻转动作仍留在 arg-contract/issues/04（要用槽位类型承载「显式声明为可空」，且那 4 个归 04 的 cast 槽的处置也在那里）。若将来引入警告级诊断通道（`ParserMediator.Warn` 已有设施，`FunctionMethod` 目前只回错误串），A 可更早温和推进。翻转前「显式声明为可空」用**裁定表 + 规则段注释**承载，不引入当前无作用的 `Nullable` 标志位（那是 arg-contract/issues/04 的槽位类型该定的事，裁定表正是它的输入）。

### 片 4：用例网（A 表 42 项 / B 表 34 项，纯文本表；A10 另加「位置形态」与「数量省略」两轴）

用既有夹具（`CheckArgumentTypeEx.Shape` / `AssertRejectedWith`）驱动，实参用内层文本表达（Ref 槽写作 `DA:0:0` 之类）：

```csharp
[Theory]
[InlineData("RAND",           "1,,",          2)]   // slot 是报错文案里的 1 基槽位 = 列表索引 + 1
[InlineData("HTML_STRINGLEN", "\"x\",,",      2)]
[InlineData("REGEXPMATCH",    "\"a\",\"b\",,", 3)]
[InlineData("XML_GET",        "1,\"a\",,",    3)]
// …A 表逐槽断言 CodeEE(ArgCanNotBeNull, slot)，B 表逐槽断言通过
public void OmittableSlot_WithExplicitEmptySlot_IsRejected(string name, string inner, int slot)
```

- 用例表以**注册名**驱动，而 worklist 只到类：多段类是多个名字共用一个规则数组（`XmlAddNodeMethod` 4 个名字、`DataTableColumnManagementMethod` 多个名字），落地前要补一份「类 → 注册名 + 合法前置实参」映射，否则 A/B 表无法直接写成用例。
- B 表槽只断言「检查器放行」；执行期语义（如 `GDRAWG` 的 10 实参形态）另加少量 `GetIntValue(null!, args)` 用例（沿用 `FunctionMethodTests` 的 seam），其余靠人工抽查。

#### 用例网（片 4 落地）

`Emuera.Tests/Runtime/Script/Statements/Function/CheckArgumentTypeEx.cs`：

- `OmittableSlot_WithExplicitEmptySlot_IsRejected`：**83** 行（A 表 41 槽的 72 行 + 片 2-D 的 11 行）、断言 `CodeEE(ArgCanNotBeNull, slot)`——覆盖 A 表除 `DataTableColumnManagementMethod[1]` 外的 41 个槽（片 1 的 3 行在内）与片 2-D 新标的 7 个槽，多段/多注册名的槽各占一行（`XML_GET`/`XML_GET_BYNAME`、`XML_SET`/`XML_SET_BYNAME`、`XML_ADDNODE`/`_BYNAME`、`XML_REMOVENODE`/`_BYNAME`、`XML_REPLACE`/`_BYNAME`、`XML_ADDATTRIBUTE`/`_BYNAME`、`XML_REMOVEATTRIBUTE`/`_BYNAME`）。A10 在其中补了 18 行**中位形态**（空槽后面还有实参，如 `XML_GET "1,\"a\",,0"`）与 2 行**同槽的「更长实参表」形态**（`REGEXPMATCH`、`MAP_GETKEYS`：该槽在自身可省略段里已是末槽，没有中位形态，这两行由同一规则表里另一段（无 `OmitStart`）拒 null——即 arg-contract/issues/02 的判据，不是本票新铺的 `NotNull`）
- `IntentionalEmptySlot_IsAccepted`：**51** 行（B 表 34 槽 + A10 补的 15 行末位/中位形态 + 片 2-D 的 2 行），与上一条成对覆盖 A/B 两段的 76 项，另加片 2-D 的 13 行（拒 11 + 放行 2）
- `CountOmittedArgument_IsAccepted_AndYieldsNoEmptySlot`（A10 新增，**65** 行 = A10 的 62 行 + 片 2-D 新收紧的三个注册名 3 行）：逐个注册名钉住「最小实参数形态」——断言实参个数等于该注册名的最小实参数（多段类按 ctor 分支取该注册名实际使用的那一段）、列表内**无 null**、检查器放行。它覆盖写法 `f(a,)` 并同时是 `OmitStart` 的护栏（`GSETFONT`/`HOTKEY_STATE` 的附修正是「抬 `OmitStart`」这类改动）
- `DataTableRowAdd_WithTailEmptySlot_IsRejectedByVariadicGroup`：`DT_ROW_ADD` 的末位形态（列表长 2）被变长组判据拒掉、够不到 null 判据——这是该槽**没有末位形态**可断言的原因
- 正控制：按批各一条「填满 / 数量省略仍接受」的用例；片 2-D 另加 `SupplementalSlots_WithFilledArgs_AreAccepted`（9 条填满形态）
- UDF：`Emuera.Tests/Runtime/Script/CalledFunctionTests.cs` 的 `ConvertArg_WithOmittedArgument_TakesTheDeclaredDefault` 钉住「省略实参 → 取 `FunctionLabelLine.Def` 的默认值」；ref 分支按票内裁定不写用例

**位置形态的可达性**（用例表按此取舍）：末位形态任何可省略槽都有；中位形态要求该槽所在的可省略段还有后继槽（`argc > i + 1`），一槽可省且它就是末槽的类（`RAND`、`HTML_STRINGLEN`、`XmlRemoveNodeMethod` 族等）没有；前位形态要求该槽是 0 号槽，即只有 `OmitStart == 0` 的段（`HTML_GETPRINTEDSTR`、`OUTPUTLOG`、`FIND_CHARADATA`，三处都已有行）。`REGEXPMATCH` 与 `MAP_GETKEYS` 属「末槽 + 更长列表」的特例，单列（见上）。

**已知的口径边界**：用例网是按「注册名 × 其实际使用的那段规则」建的，同类同 ctor 分支的兄弟注册名（`DT_CELL_GETS`/`DT_CELL_ISNULL`、`CSVABL`/`CSVCALLNAME`、`FINDLASTELEMENT`、`STRFINDU` 等）共用同一 `argumentTypeArrayEx`，由同一行覆盖；ctor 分支不同的兄弟名（`INRANGECARRAY`、`CMATCH`、`MAXCARRAY`/`MINARRAY`/`MINCARRAY`、`SUMCARRAY`——段只差槽 0 的 `CharacterData` 标记，形状集合与已覆盖的名字相同）未逐名出行。
- 片 2-C 新增 4 条（10 个用例）：`MAX`/`MIN` 与 `ARRAYMSORT` 的尾部空槽各被拒、首槽空槽的回归钉子、以及两条正控制（填满 / 数量省略）

### 顺序与验收

片 1（3 例 + 2 处数量省略附修 + 用例，已落地）→ 片 2（A/B 表裁定先评审，再分批标注）→ 片 3（改名 `NotNull` + 按 A 表铺开）→ 片 4 随片走。验收以片 4 的用例网为准（A 表逐槽断言 `CodeEE(ArgCanNotBeNull)`、B 表逐槽断言放行，另加 B 表少量执行期语义抽查；第 5 类的函数另断言数量省略形态被计数判据拒绝）。每片：`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-restore` + `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`。

### 风险与未决

1. **漏标一段 = 该槽仍放行**（机制事实 1）⇒ 用例须逐槽覆盖，不能只测首段。
2. **判空判据漏检会反向致错**：只认 `arguments[i] == null` 时，16 个用列表模式判空的槽被判进 A 表；照 A 加标志会**破坏既有语义**（`SUBSTRING("abc",,2)` 的 start 取 0、`SUMARRAY(DA:0,,10)` 的 index1 取 0、`STRJOIN(DA:0,,,)`、`VARSIZE("X",)` 等）。已把 `?.` 与列表模式并入谓词（A/B 现为 42/34）。剥注释仍是逐行朴素实现（不识别字符串字面量里的 `//`），命中处仍需人工确认。
3. **variadic 槽**（`ArgType.Variadic*`，`LastVariadics` 的 rule 取自 `vs[]`）分两型，**已逐块定案**（片 2-C）：**转发型**（`GETMETH`/`GETMETHS`，尾部交给下游）必须整条保持可空；**消费型**（`MaxMethod.GetIntValue` 的 `for (int i = 1; i < arguments.Count; i++) arguments.Required(i)`、`ArrayMultiSortMethod.GetIntValue` 的 `foreach (… in arguments.Cast<VariableTerm>())`）传入 null 即 NRE，整条标 `NotNull`。6 个含 `Variadic` 且设了 `OmitStart` 的块全部落在两类里：`DT_ROW_ADD` 的 `VariadicString` 位见片 2-A，其余 5 块（`MAX`/`MIN` 各一段、`ARRAYMSORT`、`GETMETH`/`GETMETHS`）见片 2-C。
4. **工作表原先只覆盖「被 `Required`(字面量) 读取」这一条车道**（76 / 全集 96），缺口已由片 2-D 补齐：那 20 项里 5 项有意可空、2 项转发槽、4 项硬转型解引用（今天 NRE，归 04）、7 项标 `NotNull`、2 项消费型变长尾已标。**裁定表与代码现状的一致性靠脚本校验**（裁定为需标而未标全、或裁定为可空却被误标时 `slots-worklist.py` 报警）；B 的残余风险因此从「漏标一段」变成「E 段裁定本身判断错」——那 7 项的可达性都靠函数体读取路径逐点核过，且都有对应用例
5. **原「`IndexOutOfRangeException`」疑点不成立**：`UserDefinedRefMethodTerm.Restructure` 的循环上界 `srcArgs.Count` 与 `reffunc.ArgTypeList[i]` 确实不同源，但该类从不被构造（见「UDF 族」），随死代码一并失效；该特性若复活需重新核。
6. **拒绝侧是硬错误，失败时机比「装载期」复杂**：默认配置（`ReduceArgumentOnLoad = NO`、非 `AnalysisMode`）下实参在**首次执行**该行时构建（`Process.ScriptProc.cs` 的 `ArgumentParser.SetArgumentTo`），故含显式空槽（或数量省略）的写法**只有被执行到才会失败**——「位于从不执行分支」的脚本不受影响。提前到装载/解析期的是四种情形：`AnalysisMode`（命令行传入待解析文件时置位，见 `Program.cs`，失败由 `ErbLoader.ParseFunctionWithCatch` 兜成「函数分析失败」警告）、`ReduceArgumentOnLoad = YES/ONCE`、指令声明了 `FORCE_SETARG`（四者同见 `ErbLoader.cs` 里那个 `NeedReduceArgumentOnLoad || AnalysisMode || IsForceSetArg` 的条件），以及 `FOR` 行（同文件的 `FOR` 分支无条件调 `ArgumentParser.SetArgumentTo`）。片 1 三处已举证修前必 NRE（执行到即崩溃）；A 表其余槽的实际使用面没有量化数据可用，故按片 2 逐槽裁定推进。
7. **数量省略越界这把尺子已核到 A 表 + 补车道 20 项**（A 表见「附」，补车道见片 2-D），两处都只有「附」里那 2 处 `OmitStart` 附修，无新增。**仍未过尺子的只剩机制事实 5 的 24 处硬转型站点里落在非可省略槽上的部分**——那些槽满足 `i < OmitStart`，检查器要求 `Count >= OmitStart > i`，故「列表短于下标」在构造上就不可能，实际只剩 null 维度（归 04 片②）。片 2-C 已先按这把尺子核过变长尾三处：`MAX()`/`ARRAYMSORT()` 由计数判据拒，`MAX(1)`/`ARRAYMSORT(DA:0)` 的列表长度足够函数体读取（循环/遍历都从 1 或 0 起且不越界）。
8. **检查器自身隐含「`SameAsFirst` 规则的首槽必非 null」**：`FunctionMethod.CheckArgumentTypeEx` 在该规则上用 `arguments.Required(0)` 取 operand type——若某天的 `SameAsFirst`/`VariadicSameAsFirst` 块带上 `OmitStart = 0`，0 号槽的显式空槽会被判据放行，随后**检查器自己 NRE**。现状无此形态（`Creator.Method.cs` 内 7 处该规则的 `OmitStart` 均为 -1 或 >= 2），且该形态已在检查器里就地拦住：`FunctionMethod.CheckArgumentTypeEx` 对「`OmitStart == 0` 且块内有 `SameAsFirst`」的块把 0 号槽按必填处理（判据来自 `ArgTypeList.UsesSameAsFirst`），报 `CodeEE(ArgCanNotBeNull)` 而非 NRE。这一支没有可达用例（7 处现有块都够不到 `OmitStart == 0`），故只由代码与注释承载

### 余项（不在本票）

1. 工作表从 76 扩到 96：**已完成**（片 2-D）。未被 `Required` 读取的 20 项逐项过尺子并裁定，清单见 [slots-worklist.md](../slots-worklist.md) 的 E 段
   - 口径同工作表：该槽在某段满足 `OmitStart <= i < ArgTypes.Count`，且类内没有 `arguments.Required(<i 的字面量>)` 站点
   - **有意可空 5 项**：`DataTableSelectMethod[1]`/`[2]`（DT_SELECT 的 filter/sort，空槽 = 不筛 / 不排序）、`GraphicsDrawGMethod[10]`（空槽 = 不带颜色矩阵）——规则段注释已补齐（见余项 5）；`DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]`——片 2-D 补注释 + 用例（空槽 = 写 DBNull）
   - **转发 2 项**：`GetMethMethod[2]`、`GetMethsMethod[2]`（变长尾交给下游，规则段旁已有注释，片 2-C）
   - **硬转型解引用 4 项**：`EnumNameMethod[1]`、`EnumFilesMethod[3]`、`DataTableSelectMethod[3]`、`DataTableToXmlMethod[1]`（今天 NRE，与 24 处活 cast 同车道，归 arg-contract/issues/04 片②）
   - **其余 9 项**：`XmlAddNodeMethod[4]`/`[5]`、`GraphicsDrawStringMethod[2]`/`[3]`、`GraphicsDrawGWithRotateMethod[3]`/`[4]`、`GraphicsDrawSpriteMethod[6]` 由片 2-D 裁定为「无意且可达」并标 `NotNull` + 补用例；`MaxMethod[1]`/`ArrayMultiSortMethod[1]` 已由片 2-C 标 `NotNull`
2. 活 `arguments[i]` 站点（24 处硬转型解引用）→ arg-contract/issues/04 片②（机制事实 5）。落在可省略槽上的 4 处（`EnumNameMethod[1]`、`EnumFilesMethod[3]`、`DataTableSelectMethod[3]`、`DataTableToXmlMethod[1]`）已由片 2-D 裁定「归 04」，本票不标 `NotNull`：它们的显式空槽是同一根因（`arguments[i]` 直接解引用），在 04 的槽位类型里一并解决才不留两套改法
3. 变量下标 `Required(i)` 与 helper 传参路径**已随片 2-D 并入**：这 20 项就是由这两条路读取的槽（如 `XmlAddNodeMethod` 的 `methodPos`/`setAllPos`、`DataTableRowSetMethod.SetValue`、`ReadPoint`/`ReadColormatrix`）；片 2-A 另有一条例外记录——`DT_ROW_ADD` 的槽 1 虽由变量下标读取，但已举证 `DT_ROW_ADD("t",,"c")` 修前 NRE，故在片 2-A 一并补标 `NotNull`
4. 片 4 里「B 表少量执行期语义抽查」（`GDRAWG` 的 10 实参形态等，见上）未做：A10 的范围是显式空槽的位置形态与数量省略形态，执行期取值语义不在其中。「每一段都标了标志」这条已由 `slots-worklist.py` 的 `已带标志` 列机械校验（A 表逐行 + E 段逐槽，缺标即报警），不再只靠用例
5. **（已落地）完成标准第 1 类点名的「另加未被 `Required` 读取的同类 3 项」**：3 项即余项 1 清单里的 `DataTableSelectMethod[1]`/`[2]` 与 `GraphicsDrawGMethod[10]`；已按片 2-B 的落地形态在各自规则段旁补英文语义注释（此前全库含 `empty slot`/`omitted slot` 的 32 行注释里没有这两个类）。`DataTableSelectMethod[3]`（输出数组的硬转型）**不在此列**，它属余项 2 的 cast 车道
6. **指令侧（`ArgumentBuilder.cs`）的同一条「可省略 + 可空」两轴从未定案**：spec.md 把 `ArgumentBuilder.CreateArgument` 列为同一份声明的第 4 个解释者，而它用的是另一套模型（`argumentTypeArray[i] == null` = 该槽可空、`minArg` = 最少几个实参，见 `ArgumentBuilder.checkArgumentType`）。抽查 `SP_FOR_NEXT`（读 `terms[1]`/`terms[3]` 前判空）与 `INT_EXPRESSION`（`Count == 0` 分支）均安全，但没有这一侧的核查结论，也没写明是否需要与规则表的 `OmitStart`/`ArgType.NotNull` 对齐
7. **`docs/` 侧无本次脚本可见语义变化的完整记录**：约 **50** 个内建槽上「显式空槽」由运行期 NRE 变为 `CodeEE(ArgCanNotBeNull)`（默认在首次执行该行时抛，时机见风险 6），数量省略继续合法。`docs/csv-erb-syntax.md` §10 已补一条「少写尾实参 vs 显式空槽」的陷阱（含 `Required` 的担保范围），并点名新收紧的 `GDRAWTEXT`/`GDRAWGWITHROTATE`/`GDRAWSPRITE`/`XML_ADDNODE`/`XML_ADDATTRIBUTE` 形态；仍未做的是**内建函数的契约表**（每个注册名的实参形状）——该文与 `docs/` 其余文档都还没有这张表

## 曾考虑的替代方案

1. **默认翻转（选项 A）**：规则表默认拒绝 null，只有显式声明 `Nullable` 的槽才放行。放弃（留作收尾动作，触发条件见片 3）——前提是 96 个 (类,槽) 的 `Nullable` 集合枚举完整，否则误拒现存脚本，而罚则是 `CodeEE` 硬错误、没有警告级过渡；枚举虽已由片 2-D 补齐，但「显式声明为可空」要由槽位类型承载，故翻转动作归 arg-contract/issues/04。两案的对比见片 3。
2. **把可省略槽上的 `Required(i)` 改成判空走默认语义**：放弃——规则表没有默认值声明处（逐参数默认值是 UDF 侧的机制），内建槽发明不出默认值；片 1 的三处与补车道 7 处都只能变成 `CodeEE`。
3. **UDF 侧的同型站点一并处置**：放弃——`UserDefinedRefMethodTerm` 所在的 ref 分支自解析起不可达（死代码），活路径 `CalledFunction.TryConvertArg` 已把省略实参换成默认值，UDF 侧无止血项（见「UDF 族」）。

## 影响

- `.scratch/nullable-migration/issues/08-expression-method-chain.md` 与 `13-null-forgiving-cleanup.md` 是本票所纠的书面假设的出处；`nullable-migration/issues/13` 引入 `Required` 并把「调用方担保」写进注释，本票不改该结论但更正措辞（落地注释已自行改写过一次，见「任务」）；`nullable-migration/issues/14` 的断言审计扫的是 `!`，本票扫的是 `Required`，口径相关但站点集合不同
- 失败模式同 arg-contract/issues/02：`AnalysisMode` / `ReduceArgumentOnLoad` / `FOR` 行下的装载期异常由 `ErbLoader.ParseFunctionWithCatch` 兜成「函数分析失败」；其余在首次执行该行时冒泡到 `Process.DoScript` → `handleException`（时机见风险 6）
- UDF 侧本票**不改动**：`UserDefinedRefMethodTerm` 一行是死代码，活路径在 `CalledFunction.TryConvertArg` 已把省略实参换成默认值（默认值由 `ErbLoader.parseLabel` 建立，见「UDF 族」）
- 片 1 已落地（3 处 NRE + 2 处数量省略越界 + 用例）。两处 `OmitStart` 改动后，工作表 A 表 42 项、全集 96 项、涉及函数 52 个
- 已定方向：**拒绝侧走 B**（片 3）——`DisallowVoid` 改名 `NotNull`（改名面 5 处，另 3 处为片 1 新增标注）并按工作表 A 表逐槽铺开；A（默认翻转）留作收尾条件，触发条件是 96 个 (类,槽) 的 `Nullable` 集合枚举完整
- 片 2/3/4 已落地：`ArgType.NotNull` 改名 5 处 + 片 1 新增标注 3 处；A 表 41 槽铺标志 + 片 2-D 的补车道 7 槽，拒绝侧共 **50** 个 (类,槽)（`Creator.Method.cs` 内 `ArgType.NotNull` 实测 83 处 / 73 行（含注释口径），剥注释 77 处 / 67 行——均以 `slots-worklist.py` 的计数表为准）+ B 表 34 槽与片 2-D 的 2 槽规则段注释（实测 37 行含 `empty slot`/`omitted slot`）；用例网按 A 表逐槽拒 / B 表逐槽放行覆盖两段 76 项，片 2-D 另加 13 行（拒 11 + 放行 2）与 3 行数量省略、正控制 1 条，另加 UDF 默认值 1 条、A10 的 18 行中位形态 + 2 行末槽的更长列表形态 + 15 行末位/中位形态 + 62 行数量省略形态（验收结果见「用例网」）
- 片 2-D 已落地（补车道 20 项）：7 处「无意且可达」标 `NotNull`（`XmlAddNodeMethod[4]`/`[5]`、`GraphicsDrawStringMethod[2]`/`[3]`、`GraphicsDrawGWithRotateMethod[3]`/`[4]`、`GraphicsDrawSpriteMethod[6]`）、2 处「有意可空」补注释（`DataTableRowSetMethod[2]`、`DataTableCellSetMethod[3]`）、20 项逐项过数量省略尺子（无一处越界）。改动面：`Creator.Method.cs` 的 6 个类规则表 + `CheckArgumentTypeEx.cs` 18 条用例 + 工作表生成器 E 段
- 改动量估计：中——未证合计 134 处（口径 131 / 77）→ 槽级 96 个（A/B 两段 76 个已裁定；补车道 20 个已由片 2-D 裁定），拒绝侧 50 个 (类,槽) + 52 个函数的用例表 + 5 处改名（另 3 处是片 1 的新增标注）。片 1 的 3 处与两处数量省略越界已落地，其余**裁定工作可独立于 arg-contract/issues/04 先行开展**，且其结论正是 04 设计槽位类型时需要的输入
- **待办（尚未做）**：见「余项」4/6/7——B 表执行期语义抽查、指令侧 `ArgumentBuilder` 的两轴模型未定案、`docs/` 侧仍缺内建函数契约表。余项 1/2/3 的补车道裁定与余项 5 的 3 项注释均已落地；原「回填 `nullable-migration/issues/13` 与 `nullable-migration/issues/08`」也已落地（含 08 的**影响段**）
- 相关票：arg-contract/issues/04（深化）、arg-contract/issues/02（检查器侧的省略判据）、arg-contract/issues/01（维度标志位）
- 原始审查记录：`.scratch/backlog/issues/16`

### 回填

- `nullable-migration/issues/13`：提案注释「The parser rejects an omitted slot before an implementation reads it」已就地标注为**不成立**（`i >= OmitStart` 的槽会带 null 进函数体），并指向落地版注释
- `nullable-migration/issues/08`：把「`i >= OmitStart` 的槽位允许省略」更正为「**数量上可省略 ≠ 该槽可以为 null**」——显式空槽是另一维，需该槽判空或标 `ArgType.NotNull`。该文件的**影响段**（「省略槽位(`OmitStart` 之后)按方法体既有分支处理」）是同一条被证伪的前提，已随本票一并补齐
- `ArgumentListExtensions.Required` 的注释改为「担保只对规则表标记必填的槽成立（无 `OmitStart` 的块 / `i < OmitStart` / `ArgType.NotNull`，外加 `SameAsFirst` 块里 `OmitStart == 0` 时必填的 0 号槽；变长尾部的标志由下标落到的那个变长规则决定）」
- `docs/architecture.md` 的 `Statements/Expression/` 一行（原「提供参数槽位的非空读取(省略实参以 null 承载)」）是被本票推翻的同一措辞，已改为「具名非空读取 `Required(i)`，担保只对规则表标为必填的槽成立；数量省略不产生 null，显式空槽以 null 元素进入函数体」
