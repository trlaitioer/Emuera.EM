# Nullable:省略参数以 null 到达消费点

Status: ready-for-agent

## 任务

issues/05 把方法调用与指令参数的元素类型定为 `List<AExpression?>`,以 `null` 表达省略的参数。由此派生的缺陷是：若干消费点拿到该元素后立即解引用，而对应的参数构造器对省略是容忍的，于是合法的 eraBasic 脚本可以触发 `NullReferenceException`。

本票让「省略参数」在到达消费点前即被处置——要么替换为默认值（与 `Process.CalledFunction.ConvertArg` 对用户函数实参的处理一致），要么在解析期报 `CodeEE`；不再以 null 进入运行期。

各触发路径为静态分析定位，尚未实际执行验证；落地时先复现再改。

### 范围

| 位置 | 触发 | 后果 |
| --- | --- | --- |
| `Instraction.Child.cs` 的 `INPUT_Instruction`、`ONEINPUT_Instruction`、`BINPUT_Instruction`、`ONEBINPUT_Instruction` 的 `DoInstruction` | `INPUT ,,1` 一类只给第三实参（`CanSkip`）的写法，配合消息跳过开启 | `arg.Mouse!` 先解引用 null |
| `Instraction.Child.cs` 的 `CALLSHARP_Instruction.DoInstruction` | `CALLSHARP "m", 1, , 3`（中间省略）或 `CALLSHARP "m", , 3`（首个省略），且该插件方法已注册 | `PluginMethodParameterBuilder.ConvertTerm(term!)` 解引用 null，插件方法不会被调用 |
| `ArgumentBuilder.cs` 的 `SP_CALLSHARP_ArgumentBuilder.CreateArgument` | `CALLSHARP "m"[1, ,3]` | `subNames.Required(i)` 取到 null，在解析期即抛 |
| `Instraction.Child.cs` 的 `PRINT_SPACE_Instruction`、`PRINT_RECT_Instruction` | `PRINT_SPACE ,` / `PRINT_RECT ,,,` | `MixedIntegerExprTerm.num` 为 null，取 `GetIntValue` 时抛。默认 `ReduceArgumentOnLoad=NO` 时在 `DoInstruction` 的 `num.GetIntValue` 处抛；若配为 `YES`，则先在 `SP_PRINT_SHAPE_ArgumentBuilder` 的 `arg!.Restructure(exm)` 处抛 |
| `Instraction.Child.cs` 的 `VARS_Instruction.DoInstruction` | `VARS FOO`（无初始化器）或 `VARS FOO = ` | `arg.Value!` 把 null 传入非空参数；不抛异常，变量被写入 null（读回为 `""`），属契约错误 |

### 依据

- 省略位置产出 null 元素：`Expression/ExpressionParser.cs` 的 `ReduceArguments` 在 `isDefine == false` 时直接 `AddLast(ReduceExpressionTerm(...))`，后者遇逗号返回空栈的归约结果即 null。
- `ArgumentBuilder.cs` 的 `SP_INPUT_ArgumentBuilder.CreateArgument` 不调用 `checkArgumentType`，只对非 null 元素做类型检查，因此 `[null, null, <expr>]` 会被原样装进 `SpInputsArgument`。同文件 `BINPUT_Instruction.DoInstruction` 在按钮计数为 0 的分支显式 `if (arg.Def == null) throw new CodeEE(...)`，说明该构造器允许 `Def` 为 null 是既有事实。
- `CALLSHARP` 一侧：`Argument.cs` 的 `SpCallSharpArgment.RowArgs` 为 `List<AExpression?>`，且 `SP_CALLSHARP_ArgumentBuilder` 在 `Restructure` 时对 `args` 已显式跳过 null 元素（`if (args[i] != null)`），是作者对省略的已知处理；但同一方法对 `subNames` 未跳过——这正是范围表第 3 行的缺陷。

### 非目标

以下位置已验证不变量成立，不在本票内改动，避免过度收紧：

- `INPUTS`/`ONEINPUTS`/`BINPUTS`/`ONEBINPUTS`：`SP_INPUTS_ArgumentBuilder` 在把第二实参用作 `CanSkip` 之前已要求首实参非空且为整数，`CanSkip != null` 蕴含 `Def`/`Mouse` 非空。
- `TINPUT`/`TINPUTS`：`SP_TINPUT_ArgumentBuilder`/`SP_TINPUTS_ArgumentBuilder` 走 `checkArgumentType`（`minArg = 2`，参数类型表无空位），省略实参在解析期即报错。
- `ArgumentBuilder.cs` 的 `INT_EXPRESSION_ArgumentBuilder`（省略时以 `SingleLongTerm(0)` 填充）、`STR_ArgumentBuilder`、`FORM_STR_ArgumentBuilder`（`ToStrFormTerm` 是全函数）。

## 方案

1. 先为每个位置写出最小 eraBasic 复现（`INPUT ,,1` 需在消息跳过开启下执行；`CALLSHARP` 需 `EmueraPluginExample` 提供已注册方法），确认异常类型与栈顶位置。
2. 统一判据：一个被省略的实参，其语义应由构造器在解析期落实（默认值或 `CodeEE`），消费点不再承担判空。据此优先改构造器而非逐点加判空。
3. `SP_INPUT_ArgumentBuilder` 是在解析期拒绝省略、还是像 `TINPUT` 一样以类型检查约束，取决于 eraBasic 侧是否需要保留 `INPUT ,,1` 这一写法；改动会影响既有脚本的兼容性，需在票内记录选择及理由。
4. `VARS` 的 `arg.Value` 按「无初始化器即空串」处理，与字符串变量读回 `""` 的既有行为对齐。
5. 修完补 `Emuera.Tests` 用例：每个触发式至少一条（省略实参到达消费点不得抛）。

## 影响

- 改动集中在 `ArgumentBuilder.cs` 与 `Instraction.Child.cs`，涉及解析期行为（省略实参是否报错）与运行期行为（省略实参的取值），须与可空迁移的元数据改动分开提交
- 若选择在解析期拒绝省略，`INPUT ,,1` 一类既有脚本会从「崩溃」变为「解析报错」，属可观测变化，需在 changelog 记录
