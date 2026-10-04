# 函数体在「设计上可省略」的实参槽上解引用 null

id: backlog-16
Status: superseded
Type: bug

## 任务

发现于 2026-09-27 的只读架构审查后的逐站点核查。

与 issues/04 不同：本票里**检查器是对的**——规则块的 `OmitStart` 正确声明了「哪些槽可省略」，null 因此是设计允许的合法输入；**错的是函数体**，它用 `arguments.Required(i)`（即 `arguments[i]!`，运行时无操作）无条件解引用一个设计上可空的槽。

`ArgumentListExtensions.cs:11-13` 的注释把这条不变量写成「The parser rejects an omitted slot before an implementation reads it」，而该不变量对 `i >= OmitStart` 的槽**不成立**（检查器只拒绝 `i < OmitStart` 及 `DisallowVoid` 槽）。这条错误假设的书面出处是 `.scratch/nullable-migration/issues/08-expression-method-chain.md:49`：

> `argumentTypeArrayEx` 路径下 `i < OmitStart` …… 故 `!` 是可证的注解层断言；`i >= OmitStart` 的槽位允许省略

后半句正是「允许省略」与「函数体不判空」之间的缺口。

### 已核实可达的两例

`RAND`（`Creator.Method.cs:4124` 规则 `{Int, Int}`、`OmitStart = 1`；体 `:4152-4167`）：

```csharp
if (arguments.Count == 1) { max = arguments.Required(0)...; }
else {
    if (arguments[0] != null) { min = arguments.Required(0)...; }   // 槽 0 判了空
    max = arguments.Required(1).GetIntValue(exm);                   // :4166 槽 1 没判
}
```

`RAND(1,,)` → `[1, null]`（长度 2，尾逗号被吞）→ `Count == 1` 为假 → 进 else → `:4166` 解引用 null → **NRE**。槽 1 被 `OmitStart = 1` 声明为可省略，故 null 是合法输入。

`HTML_STRINGLEN`（`:39` 规则 `{String, Int}`、`OmitStart = 1`；体 `:44-60`）：

```csharp
int len = HtmlManager.HtmlLength(arguments.Required(0).GetStrValue(exm));
if (arguments.Count == 1 || arguments.Required(1).GetIntValue(exm) == 0)   // :47 只有 Count 守卫
```

`HTML_STRINGLEN("x",,)` → `["x", null]` → `Count == 1` 为假 → `:47` 解引用 null → **NRE**。注意 `arguments.Count` 守卫**挡不住 `,,` 形态**——这是本类的关键形态。

### 同型缺陷存在于 UDF 族（本票范围包含）

用户定义函数（`@FOO`）**不经过** `argumentTypeArrayEx`/`CheckArgumentTypeEx`，是另一套机制：形状声明在 `UserDefinedRefMethod.ArgTypeList`（枚举 `UserDifinedFunctionDataArgType`，注意该名拼写为 `Difined`），检查在 `CalledFunction.TryConvertArg`，实参产物是 `UserDefinedFunctionArgument`（自带一份 `Restructure`）。`ArgTypeList` 这个名字在两族里指两样东西（`FunctionMethod.ArgTypeList` 是规则块类）。

该族已有一条同型的活路径，且**代码里已有未决 TODO**（`UserDefinedMethodTerm.cs:156-166`）：

```csharp
// TODO: srcArgs can hold null for an omitted argument, as FunctionMethodTerm.Restructure accepts;
// decide whether to skip null elements instead of asserting them non-null.
for (int i = 0; i < srcArgs.Count; i++)
{
    if ((reffunc.ArgTypeList[i] & ...__Ref) == ...__Ref)
        srcArgs.Required(i).Restructure(exm);                  // :161 空槽 → NRE
    else
        srcArgs[i] = srcArgs.Required(i).Restructure(exm);     // :165 同
}
```

即：TODO 自陈 `srcArgs` 可带 null，但代码目前断言非空。

**两族的空槽语义必须分开，不可统一**：

| | 内建函数 | UDF |
| --- | --- | --- |
| 默认值声明处 | **无**（`NullTerm` 只用于函数定义路径 `ExpressionParser.cs:152/156`） | **有**，逐参数（`@FOO(A = 1)` → `FunctionLabelLine.Def`） |
| 空槽应有语义 | 按错误处理 | 取已声明的默认值 |

把内建的「错误」套到 UDF 会毁掉 `@FOO(A = 1)` 的省略调用；把 UDF 的「取默认」套到内建则无默认值可取而只能静默取 0/`""`。

### 范围（量级估计，非逐点定案）

以规则表对 `Creator.Method.cs` 的 372 个字面量索引 `Required` 站点逐点核对（`DisallowVoid` 已计入）：

| 分类 | 处数 |
| --- | --- |
| 有据：`i < OmitStart`（规则块路径） | 130 |
| 有据：类只设 `argumentTypeArray`（无规则块），`FunctionMethod.cs:332-336` 对全部槽拒 null | 107 |
| 索赔未证：`i >= OmitStart` 且非 `DisallowVoid`（设计上可省略） | 78 |
| 索赔未证：所属类含 `OmitStart == -1` 的块（即 issues/04 的 16 个类） | 57 |
| 合计 | 372（未证 135，占 36%） |

对 135 处按「函数体内是否有真实判空」机械分桶（按行区间判定，存在高估）：约 34 处有真实判空、约 27 处只有 `arguments.Count` 守卫（对 `,,` 无效）、其余无守卫。**未证 ≠ 缺陷**：其中相当一部分因调用路径保证该槽必被填充而不可达，需要逐点做可达性分析才能定案。

**计数口径必须在裁定前统一**：上表的 135 是「类内**全部** `ArgTypes` 块」的口径；另一次独立核查只读各类**首段**规则块，得 116（其中多规则类被漏算——`XmlGetMethod` 4 段、`XmlAddNodeMethod` 6 段、`MaxArrayMethod` 4 段等，24 个类有多段）。两数站点集合不同，不是矛盾。逐点裁定前须先钉死口径（建议按全部规则块，并逐槽归属到具体规则段），否则「已裁定」的结论不可核对。

完成标准：

- 逐点裁定 135 处未证站点，分成「可达 → 修」与「不可达 → 在站点旁写明依据」两类；不可达的结论要写成注释，避免下次审计重新怀疑
- 可达站点的修法二选一：函数体补 `arguments[i] == null` 判空（走既有默认语义），或规则块改声明为不可省略（仅当该槽本不该允许省略）
- 表驱动用例：对每个含可省略槽的函数，按上表形态各构造一例（`f(a,)` / `f(a,,)` / `f(a,,b)`），断言得到 `CodeEE` 或该函数的默认值语义，而非 NRE
- 若结论是「`Required` 只能在 `i < OmitStart` 处使用」，则该约束应能机械校验（否则同类缺陷会再长出来）——这一点归 issues/02 的槽位解析期方案

## 方案

待评估。**与 issues/04 的区别要在实现时守住**：04 修检查器（把设计上不可省略的槽拦住），本票修函数体（在设计上可省略的槽上不要假装非空）。两者若同时落地，`SETVAR("TARGET", )` 归 04，`RAND(1,,)` 归本票。

## 影响

- 与 `.scratch/nullable-migration/issues/08-expression-method-chain.md:49` 的关系：该票为 `!` 断言给出的理由是「`i >= OmitStart` 的槽位允许省略」——本票证明这句话与函数体的实际写法不一致。落地时应回填该票，或在该票正文标注本票承接
- 与 `.scratch/nullable-migration/issues/13-null-forgiving-cleanup.md` 的关系：该票把散落的 `!` 收成 `Required` 具名入口，判据是「把不可见的 `!` 变成可检索的具名声明」。本票不改该结论，但要指出该入口的注释把「调用方担保」写成了「解析器担保」（`ArgumentListExtensions.cs:11-13`）
- 与 `.scratch/nullable-migration/issues/14-null-assertion-audit.md`：其断言审计按「可达/不可达」分类，本票的 135 处属该口径的延伸，但站点集合不同（审计扫的是 `!`，本票扫的是 `Required`）
- 失败模式同 issues/04：装载期 NRE 由 `ErbLoader.ParseFunctionWithCatch` 兜成「函数分析失败」；执行期冒泡到 `Process.DoScript` → `handleException`
- 改动量估计：中（135 处逐点裁定 + 用例）。**裁定工作可独立于 02 的深化先行开展**，且裁定结果正是 02 设计槽位类型时需要的输入
- 相关票：issues/02（实参契约表兑现）、issues/04（检查器侧的省略判据）、issues/03（维度标志位）
- **已转出（2026-09-27）**：承接方 `.scratch/arg-contract/issues/03-required-on-omittable-slot.md`。本票作为审查记录保留（文件不移动）
