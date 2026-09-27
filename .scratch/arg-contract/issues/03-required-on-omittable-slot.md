# 函数体在「设计上可省略」的实参槽上解引用 null

Status: needs-triage
Type: bug

## 任务

与 issues/02 不同：本票里**检查器是对的**——规则块的 `OmitStart` 正确声明了「哪些槽可省略」，null 因此是设计允许的合法输入；**错的是函数体**，它用 `arguments.Required(i)`（即 `arguments[i]!`，运行时无操作）无条件解引用一个设计上可空的槽。

`ArgumentListExtensions.cs:11-13` 的注释把这条不变量写成「The parser rejects an omitted slot before an implementation reads it」，而该不变量对 `i >= OmitStart` 的槽**不成立**（检查器只拒绝 `i < OmitStart` 及 `DisallowVoid` 槽）。这条错误假设的书面出处是 `.scratch/nullable-migration/issues/08-expression-method-chain.md:49`，它写「`i >= OmitStart` 的槽位允许省略」却据此把 `!` 论证为「可证的注解层断言」——缺口就在这两句之间。落地时须回填该票。

### 已核实可达的两例

`RAND`（规则 `Creator.Method.cs:4124` `{Int, Int}`、`OmitStart = 1`；体 `:4152-4167`）：

```csharp
if (arguments.Count == 1) { max = arguments.Required(0)...; }
else {
    if (arguments[0] != null) { min = arguments.Required(0)...; }   // 槽 0 判了空
    max = arguments.Required(1).GetIntValue(exm);                   // :4166 槽 1 没判
}
```

`RAND(1,,)` → `[1, null]`（长度 2，尾逗号被吞）→ `Count == 1` 为假 → `:4166` 解引用 null → **NRE**。槽 1 被 `OmitStart = 1` 声明为可省略，故 null 是合法输入。

`HTML_STRINGLEN`（`:39` `{String, Int}`、`OmitStart = 1`；体 `:44-60`）：

```csharp
if (arguments.Count == 1 || arguments.Required(1).GetIntValue(exm) == 0)   // :47 只有 Count 守卫
```

`HTML_STRINGLEN("x",,)` → `["x", null]` → `Count == 1` 为假 → `:47` 解引用 null → **NRE**。注意 `arguments.Count` 守卫**挡不住 `,,` 形态**——这是本类的关键形态。

### 同型缺陷存在于 UDF 族（本票范围包含）

用户定义函数（`@FOO`）**不经过** `argumentTypeArrayEx`/`CheckArgumentTypeEx`，是另一套机制：形状声明在 `UserDefinedRefMethod.ArgTypeList`（枚举 `UserDifinedFunctionDataArgType`，注意拼写为 `Difined`），检查在 `CalledFunction.TryConvertArg`，产物是 `UserDefinedFunctionArgument`（自带一份 `Restructure`）。`ArgTypeList` 这个名字在两族里指两样东西。

该族已有一条同型活路径，且**代码里已有未决 TODO**（`UserDefinedMethodTerm.cs:156-166`）：

```csharp
// TODO: srcArgs can hold null for an omitted argument, as FunctionMethodTerm.Restructure accepts;
// decide whether to skip null elements instead of asserting them non-null.
if ((reffunc.ArgTypeList[i] & ...__Ref) == ...__Ref)
    srcArgs.Required(i).Restructure(exm);                  // :161 空槽 → NRE
else
    srcArgs[i] = srcArgs.Required(i).Restructure(exm);     // :165 同
```

**两族空槽语义必须分开，不可统一**（已裁定，见 spec.md）：

| | 内建函数 | UDF |
| --- | --- | --- |
| 默认值声明处 | **无**（`NullTerm` 只用于函数定义路径 `ExpressionParser.cs:152/156`） | **有**，逐参数（`@FOO(A = 1)` → `FunctionLabelLine.Def`） |
| 空槽应有语义 | 按错误处理 | 取已声明的默认值 |

### 范围（量级估计，非逐点定案）

以规则表对 `Creator.Method.cs` 的 372 个字面量索引 `Required` 站点逐点核对（`DisallowVoid` 已计入）：

| 分类 | 处数 |
| --- | --- |
| 有据：`i < OmitStart`（规则块路径） | 130 |
| 有据：类只设 `argumentTypeArray`（无规则块），`FunctionMethod.cs:332-336` 对全部槽拒 null | 107 |
| 索赔未证：`i >= OmitStart` 且非 `DisallowVoid`（设计上可省略） | 78 |
| 索赔未证：所属类含 `OmitStart == -1` 的块（即 issues/02 的 16 个类） | 57 |
| 合计 | 372（未证 135，占 36%） |

对 135 处按「函数体内是否有真实判空」机械分桶（按行区间判定，存在高估）：约 34 处有真实判空、约 27 处只有 `arguments.Count` 守卫（对 `,,` 无效）、其余无守卫。**未证 ≠ 缺陷**：其中相当一部分因调用路径保证该槽必被填充而不可达，需要逐点做可达性分析才能定案。

**计数口径必须在裁定前统一**：上表的 135 是「类内**全部** `ArgTypes` 块」的口径；另一次独立核查只读各类**首段**规则块，得 116（多规则类被漏算——`XmlGetMethod` 4 段、`XmlAddNodeMethod` 6 段、`MaxArrayMethod` 4 段等，24 个类有多段）。两数站点集合不同，不是矛盾。逐点裁定前须先钉死口径（建议按全部规则块，并逐槽归属到具体规则段），否则「已裁定」的结论不可核对。

完成标准：

- 逐槽裁定（口径见 spec.md 的「逐槽可空性」），按「函数体是否**有意**把 null 当作『显式留空』」分三类：
  1. **有意**（约 34 处，如 `GraphicsDrawGMethod:8373` 的 `arguments[10] == null` = 不带颜色矩阵）→ 规则表该槽**显式声明为可空**，并把该语义写进注释（把「意外」变成「声明意图」）
  2. **无意但被 `arguments.Count` 守卫挡住** → 确认 `,,` 形态（Count 不变、槽为 null）确实够不到；够得到则补 `== null` 判空
  3. **无意且可达**（`RAND(1,,)`、`HTML_STRINGLEN("x",,)` 已核实）→ 或补判空走默认语义，或把该槽标为不可空使检查器拒绝
- 裁定结果按槽写进规则表：**可空性是与 `OmitStart` 正交的逐槽属性**，`DisallowVoid` 是其半成品（全库只用 1 次），本票决定它被一般化还是被取代
- 注意 135 处「索赔未证」里有一部分**本就该可空**（属第 1 类），故「未证」不等于「缺陷」；本票的产出是**逐槽的语义裁定**，不是「全部改成拒绝」
- UDF 侧：按 TODO 的方向裁定 `srcArgs` 的 null 处置（跳过 vs 取默认值 vs 报错），并写出依据；取默认值则需说明默认值来自哪一处
- 表驱动用例：对每个含可省略槽的函数，按上表形态各构造一例（`f(a,)` / `f(a,,)` / `f(a,,b)`），断言得到 `CodeEE` 或该函数的默认值语义，而非 NRE
- 「`Required` 只能在 `i < OmitStart` 的槽上使用」这条约束应能机械校验（否则同类缺陷会再长出来）——归 issues/04 的槽位方案

## 方案

待评估。**与 issues/02 的区别要在实现时守住**：02 修检查器（把设计上不可省略的槽拦住），本票修函数体（在设计上可省略的槽上不要假装非空）。两者同时落地后，`SETVAR("TARGET", )` 归 02，`RAND(1,,)` 归本票。

本票的裁定结果**是 issues/04 的设计输入**（访问器必须显式选 null 语义）。

## 影响

- `.scratch/nullable-migration/issues/08:49` 是本票所纠的书面假设的出处；`issues/13` 引入 `Required` 并把「调用方担保」写进注释，本票不改该结论但更正措辞；`issues/14` 的断言审计扫的是 `!`，本票扫的是 `Required`，口径相关但站点集合不同
- 失败模式同 issues/02：装载期 NRE 由 `ErbLoader.ParseFunctionWithCatch` 兜成「函数分析失败」；执行期冒泡到 `Process.DoScript` → `handleException`
- 改动量估计：中（未证站点逐点裁定 + 用例）。**裁定工作可独立于 issues/04 先行开展**，且其结论正是 04 设计槽位类型时需要的输入
- 相关票：issues/04（深化）、issues/02（检查器侧的省略判据）、issues/01（维度标志位）
- 原始审查记录：`.scratch/backlog/issues/16`
