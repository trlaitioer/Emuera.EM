# 规则块未写 OmitStart 时，该槽的省略检查恒假

Status: implemented
Type: bug

## 任务

`Emuera/Runtime/Script/Statements/Function/FunctionMethod.cs:166-177`：

```csharp
if (arguments[i] == null)
{
    if (i < list.OmitStart || (list.OmitStart > -1 && i >= list.OmitStart && rule.DisallowVoid))
    { /* errMsg = ArgCanNotBeNull */ break; }
    else { continue; }
}
```

`ArgTypeList.OmitStart` 默认 `-1`（`:99`）。未写 `OmitStart` 时 `i < -1` 与 `list.OmitStart > -1` 双假 → 判据恒假 → **该槽传入省略实参（`null`）一律放行**。

与同一方法 `:149` 的口径矛盾：`argsNotLessThanRule = list.OmitStart > -1 ? arguments.Count >= list.OmitStart : arguments.Count >= list.ArgTypes.Count`——未写 `OmitStart` 时要求实参个数**恰好等于**规则槽数。故这些块的意图明确是「不允许省略」。

23 个规则块未写 `OmitStart`（`Creator.Method.cs` 内）：行 714、974、1049、1056、1266、1717、2095、2102、2103、2298、2299、2809、4712、4771、4835、6177、8038、8039、8185、8186、8477、8478、9800。（16 个类；`SETVAR`、`REGEXPMATCH`、`XML_*`、`DT_ROW_*`、`MAP_GETKEYS`、`GROUPMATCH`/`NOSAMES`/`ALLSAMES`、`REPLACE`、`SPRITECREATE`、`GCLEAR`、`GDRAWSPRITE`、`GETDISPLAYLINE` 等。）

已核对的例子（`:712-715`，SETVAR）：`new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Any } }`，无 `OmitStart`。故 `SETVAR("TARGET", )` 在 `:168` 放行（`rule.Any` 使 `typeNotMatch` 也为假），随后函数体 `:739` 的 `arguments.Required(1)` 解引用 null。其余候选：`DT_ROW_REMOVE("t", , 1)`（规则 `:2299`，体 `:2317`）、`REPLACE("a","b",,0)`（规则 `:6177`，体 `:6253` 的 `Required(2)`）。

### 已核实：23 个块的意图复核完成

**23/23 全部是「不允许省略」；无一块该补 `OmitStart`；无一块永不收到 null。** 依据：

- 23/23 的函数体对**每一个**槽都做了 `Required(i)` / `as VariableTerm` 解引用（含 ReadGraphics/ReadColor/ReadPoint/ReadRectangle 等共享 helper），没有一块把某槽的 null 当作「省略」处理
- `DisallowVoid`（今名 `ArgType.NotNull`）全库仅出现在 `ArrayMultiSortExMethod` 的两段（审计时 `Creator.Method.cs:478-479`，现 `BuiltinMethod/PrivateAdditions.cs`；`OmitStart = 2`）——该标志的用途正是「在可省略区间内单独标注某槽不可省略」，反证未写 `OmitStart` 的块整体不可省略
- 旁证：`GroupMatchMethod`/`NosamesMethod`/`AllsamesMethod` 被注释掉的旧 `CheckArgumentType`（`:4717-4732`、`:4776-4791`、`:4840-4855`）逐槽拒绝 null，文案为「引数の1番目は省略できません」
- 计数复核：`Creator.Method.cs` 内 `new ArgTypeList` 114 处、`OmitStart` 91 处；每块独占一行且 `OmitStart` 为 init-only，故 114 − 91 = 23 成立

**null 元素确实可达**：`ExpressionParser.ReduceArguments` 的循环顶部只吃掉**单个**尾逗号，故槽数不变、任意槽可空——

| 写法 | 结果 |
| --- | --- |
| `f(a,b)` | `[a,b]` |
| `f(a,b,)` | `[a,b]`（尾逗号被吞） |
| `f(a,b,,)` | `[a,b,null]` |
| `f(a,,b)` | `[a,null,b]` |
| `f(,b)` | `[null,b]` |

完成标准：

- `:168` 改为 `list.OmitStart < 0 || i < list.OmitStart || (list.OmitStart > -1 && i >= list.OmitStart && rule.DisallowVoid)`，与 `:149` 的口径对齐
- 复现用例：`SETVAR("TARGET", )`、`DT_ROW_REMOVE("t", , 1)`、`REPLACE("a","b",,0)` 各断言得到 `CodeEE`（文案 `ArgCanNotBeNull`）而非 NRE / 静默成功
- 23 个块的逐块意图裁定已完成（见上），无需再核
- 本改动只把「元素为 null」的结果从 NRE 变为 `CodeEE`，**不改变任何个数判定**，不会让现存合法写法变非法

## 方案

落地的判据（与 issues/01 同批）：

```csharp
if (list.OmitStart < 0 || i < list.OmitStart || rule.DisallowVoid)
```

首个子句即本票的修复；第三个子句取代原先的 `(list.OmitStart > -1 && i >= list.OmitStart && rule.DisallowVoid)`——到达它时前两个子句已排除其余情形，故 `OmitStart > -1` 与 `i >= list.OmitStart` 都是冗余。语义未变：`OmitStart` 未写（= -1）时任何空槽都报错；写了 `OmitStart` 时**落在可省略区间内的空槽仍然放行**（那是「显式留空」，属既有的另一套语义，见 issues/03）。

## 影响

- 失败模式按构建时机分叉（同 `.scratch/nullable-migration/issues/15-omitted-args-null-consumers.md:28`）：装载期 NRE 由 `ErbLoader.ParseFunctionWithCatch` 的宽 catch 兜成「函数分析失败」并附栈；首次执行的懒构建或 `DoInstruction` 内则冒泡到 `Process.DoScript` → `handleException`，脚本以 NullReferenceException 中止
- 相关票：issues/01（维度标志位）、issues/03（可省略槽上的 `Required`）、issues/04（阶段 A 止血）、issues/05（槽位类型深化）
- 改动量估计：1 行 + 用例；风险已由逐块裁定消除
- 原始审查记录：`.scratch/backlog/issues/04`
