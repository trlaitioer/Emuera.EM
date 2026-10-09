---
id: nullable-migration-15
status: implemented
---

# Nullable:省略参数以 null 到达消费点

## 任务

issues/05 把方法调用与指令参数的元素类型定为 `List<AExpression?>`,以 `null` 表达省略的参数。由此派生的缺陷是：若干消费点拿到该元素后立即解引用，而对应的参数构造器对省略是容忍的，于是合法的 eraBasic 脚本可以触发 `NullReferenceException`。

本票让「省略参数」在到达消费点前即被处置——要么替换为默认值（与 `Process.CalledFunction.ConvertArg` 对用户函数实参的处理一致），要么在解析期报 `CodeEE`；不再以 null 进入运行期。

完成标准：范围内每个触发式都由**构造器在解析期**给出归宿（默认值替换 / 解析报错 / 缺席即合法）并记录理由，消费点不再承担判空；每个触发式至少一条 `Emuera.Tests` 用例（省略实参到达消费点不得抛）。清单以本文「范围」为准，它不是 issues/21 的 `!` 清单子集（理由见「依据」末条）。

### 范围

| 位置 | 触发 | 后果 |
| --- | --- | --- |
| `Instraction.Child.cs` 的 `INPUT_Instruction`、`ONEINPUT_Instruction`、`BINPUT_Instruction`、`ONEBINPUT_Instruction` 的 `DoInstruction` | `INPUT ,,1`、`INPUT 5,,1`、`INPUT ,1,1` 一类只给第三实参（`CanSkip`）的写法，配合消息跳过开启 | `arg.Mouse!` / `arg.Def!` 先解引用 null |
| `Instraction.Child.cs` 的 `CALLSHARP_Instruction.DoInstruction` | `CALLSHARP "m", 1, , 3`（中间省略）或 `CALLSHARP "m", , 3`（首个省略），且该插件方法已注册 | `PluginMethodParameterBuilder.ConvertTerm(term!)` 解引用 null，插件方法不会被调用 |
| `Instraction.Child.cs` 的 `PRINT_SPACE_Instruction`、`PRINT_RECT_Instruction` | `PRINT_SPACE ,` / `PRINT_RECT ,`（1 个空元素）/ `PRINT_RECT ,,,,`（4 个空元素） | `MixedIntegerExprTerm.num` 为 null，取 `GetIntValue` 时抛。默认 `ReduceArgumentOnLoad=NO` 时在 `DoInstruction` 的 `num.GetIntValue` 处抛；若配为 `YES`，则先在 `SP_PRINT_SHAPE_ArgumentBuilder` 的 `arg!.Restructure(exm)` 处抛 |
| `ArgumentBuilder.cs` 的 `SP_PRINT_IMG_ArgumentBuilder.CreateArgument` | `PRINT_IMG "a", , 100`（中间空位） | `arg!.IsString` 在**解析期**即解引用 null（与上一行同根因，是 issues/21 清单新发现的漏点） |
| `ArgumentBuilder.cs` 的 `SP_HTML_PRINT_ArgumentBuilder.CreateArgument`（设计期新增） | `HTML_PRINT "a", ,`（第二实参空位） | `args.Required(1).IsInteger` 在解析期即解引用 null；即使绕过该处，随后的常量折叠 `args.Required(1).GetIntValue` 仍会解引用 |
| `Instraction.Child.cs` 的 `VARS_Instruction.DoInstruction` | `VARS FOO`（无初始化器）或 `VARS FOO = ` | `arg.Value!` 把 null 传入非空参数；不抛异常，变量被写入 null（读回为 `""`），属契约错误 |

### 依据

- 省略位置产出 null 元素：`Expression/ExpressionParser.cs` 的 `ReduceArguments` 在 `isDefine == false` 时直接 `AddLast(ReduceExpressionTerm(...))`，后者遇逗号返回空栈的归约结果即 null。
- **触发式更正（2026-09-25 静态追踪）**：`reduceTerm` 在 `,`/`)`/`]`/`\0` 处 `return end(stack, ternaryCount)` 且不消费该字符（消费由 `ReduceArguments` 循环末尾的 `ShiftNext` 完成），故**只有首位与中间空位产出 null 元素，尾随逗号不产出**。据此：`PRINT_RECT ,,,`（3 元素）已在 `param.Count != 1 && param.Count != 4` 处报 `DifferentArgsCount`，不属本缺陷；`PRINT_IMG "a", `（尾随逗号）只产出 1 个元素、根本不进 `CreateArgument` 的循环。真正的触发式见范围表。
- 省略实参的可观测形态：`ArgumentParser.SetArgumentTo` 只 catch `EmueraException`。构造期 NRE 若发生在装载期（`FORCE_SETARG`/`NeedReduceArgumentOnLoad`/`AnalysisMode`），由 `ErbLoader.ParseFunctionWithCatch` 的 `catch (Exception)` 兜成「函数分析失败」并附栈；若发生在首次执行的懒构建或在 `DoInstruction` 内，则冒泡到 `Process.DoScript` 的 `catch (Exception)` → `handleException`，脚本以 `NullReferenceException` 中止。故票面的「NRE」成立，但不是进程崩溃，报错位置与文案随构建时机而变。
- `ArgumentBuilder.cs` 的 `SP_INPUT_ArgumentBuilder.CreateArgument` 不调用 `checkArgumentType`，只对非 null 元素做类型检查，因此 `[null, null, <expr>]` 会被原样装进 `SpInputsArgument`。同文件 `BINPUT_Instruction.DoInstruction` 在按钮计数为 0 的分支显式 `if (arg.Def == null) throw new CodeEE(...)`，说明该构造器允许 `Def` 为 null 是既有事实。
- `CALLSHARP` 一侧：`Argument.cs` 的 `SpCallSharpArgment.RowArgs` 为 `List<AExpression?>`，且 `SP_CALLSHARP_ArgumentBuilder` 在 `Restructure` 时对 `args` 已显式跳过 null 元素（`if (args[i] != null)`），是作者对省略的已知处理；同一方法对 `subNames` 未跳过，曾判为缺陷，**实测不可达**（见非目标）。
- **清单口径**：issues/21 的清单按后缀 `!` 枚举，而 `ArgumentListExtensions.Required` 把 `!` 藏在扩展方法内（issues/21 明确把该文件 2 处排除），因此「经 `.Required(` 读取可空元素」这一类站点在 `!` 清单里不可见——范围表新增的两行（`SP_HTML_PRINT_ArgumentBuilder`、`SP_CALL_ArgumentBuilder`）都属此类。本票的排查必须单独枚举 `.Required(` 与可空元素的直读。

### 非目标

以下位置已验证不变量成立，不在本票内改动，避免过度收紧：

- `INPUTS`/`ONEINPUTS`/`BINPUTS`/`ONEBINPUTS`：`SP_INPUTS_ArgumentBuilder` 在把第二实参用作 `CanSkip` 之前已要求首实参非空且为整数，`CanSkip != null` 蕴含 `Def`/`Mouse` 非空。已知不对称：`INPUTS "d", , 1`（`Mouse` 省略）会静默丢弃 `CanSkip`，属该构造器的既有行为，本票不动，也不作为 `INPUT` 一族的处置依据。
- `subNames` 类读取（`SP_CALLSHARP_ArgumentBuilder` 与 `SP_CALL_ArgumentBuilder` 的 `subNames.Required(i)`，原范围表第 3 行与设计期新增行）：**元素不可能为 null，原判定不成立**。依据：`ReduceArguments` 的 `switch (endWith)` 只对 `EoL`/`RightParenthesis` 设置 `TermEndWith`，`ArgsEndWith.RightBracket` 分支被注释掉、沿用 `TermEndWith.EoL`，故 `reduceTerm` 在下标列表内遇到逗号时走 `throw new CodeEE(UnexpectedSymbol)`，而不是返回空栈的归约结果。实测：`CALLSHARP "m"[1, ,3]` 与 `CALLF FOO[1, ,2]` 在改动前**已是解析错误**（`ArgumentBuilder.cs` 的 `subNames` 循环从未被 null 到达）。两条用例保留为钉子（`OmittedArgumentTests`，见「影响」）。
- `TINPUT`/`TINPUTS`：`SP_TINPUT_ArgumentBuilder`/`SP_TINPUTS_ArgumentBuilder` 走 `checkArgumentType`（`minArg = 2`，参数类型表无空位），省略实参在解析期即报错。
- `ArgumentBuilder.cs` 的 `INT_EXPRESSION_ArgumentBuilder`（省略时以 `SingleLongTerm(0)` 填充）、`STR_ArgumentBuilder`、`FORM_STR_ArgumentBuilder`（`ToStrFormTerm` 是全函数）。
- 其余调用 `checkArgumentType` 的构造器：`argumentTypeArray` 的非空类型位遇 null 即 `CanNotRecognizeArg`，省略已在解析期拒绝。
- 已显式判空、归宿已落实的构造器：`SP_PRINTV_ArgumentBuilder`、`SP_SAVEVAR_ArgumentBuilder`、`SP_HTML_PRINT_ArgumentBuilder` 的首实参。
- `SP_SWAP_ArgumentBuilder` 的 `terms = [terms[0], null]`（第二实参省略形）：只有 `SP_COPYCHARA` 用可省略变体，而该 arg 类型当前无任何指令引用，分支不可达；日后启用需一并处置。
- `ErbLoader` 的函数定义路径：`@FOO[A]`/`@FOO(A=1)` 侧已由 `CanNotOmitFuncDefineArg`/`CannotOmitFuncArg` 在解析期拒绝省略。
- `Process.CalledFunction.ConvertArg`：用户函数实参的省略已按「代入 `func.Def[i]`，无默认值则 `CanNotOmitArgWithMessage`」处置，是本票判据的来源，不改。

## 方案

1. **判据**：一个被省略的实参，其语义由构造器在解析期三选一落实，消费点不再承担判空——①该位置有既定默认语义 → 替换为默认值；②省略没有可实现的语义（无默认值，或省略后的组合自相矛盾）→ `warn(string.Format(trerror.CanNotOmitArg.Text, i + 1), line, 2, false)` 后 `return null`（`SetArgumentTo` 随即置 `line.IsError`）；③该实参对象/字段本就以 null 表示「无」→ 构造器不写入该项，消费点原有的判空是语义判断、保留。判据来源是 `ConvertArg` 对用户函数实参的处理（有默认值代入、无默认值报错）。
2. **`INPUT`/`ONEINPUT`/`BINPUT`/`ONEBINPUT`**（`SP_INPUT_ArgumentBuilder`）：`CanSkip != null` ⇒ 第 1、第 2 实参必须非 null，否则 `CanNotOmitArg`(1)/(2)。理由：CanSkip 分支的语义是用默认值自动填 `RESULT`（`Mouse` 决定填 `RESULT` 还是 `RESULT:1`，`Def` 提供填充值）；`Def` 省略时该指令在交互路径上本不允许空输入（`EmueraConsole.doInputToEmueraProgram` 的空串走 `long.TryParse` 失败），没有可填的值；`Mouse` 省略后 CanSkip 只剩一半语义，与 `TINPUT` 一族「省略即报错」一致。四个 `DoInstruction` 不改，保留的 `!` 由构造器保证，并以注释指回本判据。
3. **`CALLSHARP` 的实参**（`SP_CALLSHARP_ArgumentBuilder`）：`args[i] == null` ⇒ `CanNotOmitArg(i + 1)`。理由：插件签名 `void Execute(PluginMethodParameter[] args)` 既无逐参数类型也无默认值，「省略」在 API 内没有可表达的值；代入 0/"" 与显式传 0/"" 无法区分，且插件按 `strValue` 读取时会继续 NRE。`CALLSHARP` 带 `FORCE_SETARG`，实参在装载期构造 ⇒ 装载即报错并带行号。`DoInstruction` 的 `!` 与 `ConvertTerm` 的非空契约保持，TODO 换成指向本判据的注释。
4. **`CALLSHARP` 的 `subNames`**（同方法）：**不加守卫**——理由与判定见 D1（`[...]` 形式整体不可用）；`SpCallSharpArgment.SubNames` 对 CALLSHARP 无读取点。
5. **`SP_CALL_ArgumentBuilder` 的 `subNames`**：同第 4 条（`CALLF FOO[1, ,2]` 一类在解析期即被拒）；CALL 一族的 `SubNames` 除 `ErbLoader` 的 `TRYGOTOLIST` 检查外无读取点。两条用例已改述为「`[...]` 形式整体被拒」（见决策记录）。
6. **`PRINT_SPACE`/`PRINT_RECT`**（`SP_PRINT_SHAPE_ArgumentBuilder`）：循环内遇 null 元素 ⇒ `CanNotOmitArg(param.Count)`。理由：px/尺寸数值没有默认值，`MixedIntegerExprTerm.num` 声明为非空，构造器不该产出 `num == null` 的项。
7. **`PRINT_IMG`**（`SP_PRINT_IMG_ArgumentBuilder`）：null 元素按缺席处理（`continue`，不写 `Nameb`/`Namem`、不进 `param`）。理由：第 2/3 实参对应本就声明可空、消费点以 `?.` 读取的 `Nameb`/`Namem`，「无备用图名」是合法状态；`PRINT_IMG "a", , 100` 有自洽实现（省略 nameb、给 x），不应一刀切拒绝。
8. **`HTML_PRINT`**（`SP_HTML_PRINT_ArgumentBuilder`）：第 2 实参为 null 时按缺席处理（`SpHtmlPrint.Opt = null`，消费点已是 `arg.Opt != null && …`）；构造与常量折叠两处都要改用该局部，否则 `args.Required(1).GetIntValue` 仍会解引用。
9. **`VARS`**（`LogicalLineParser` 的 VARI/VARS 分支 + `StrAsignArgument` + `VARS_Instruction.DoInstruction`）：无初始化器（`VARS FOO` / `VARS FOO = `）替换为 `""`——解析处 `value ?? ""`，`StrAsignArgument.Value` 收窄为非空 `string`，`DoInstruction` 去掉 `!`。理由：与 `VARI` 无初始化器时替换 `SingleLongTerm(0)` 对称，也与字符串变量读回 `""` 的既有行为一致；现行写 null 只是靠 `SetValue` 容忍。无构造点的死代码 `StrAsignArgument(string)`/`IntAsignArgument(string, int[])` 可在本票一并清理。
10. **同类站点排查**（完成标准的必要条件）：枚举 `ArgumentBuilder.cs` 内全部 `.Required(` 与可空元素直读（`terms[i]`/`args[i]`/`param[i].num`），再枚举指令侧对可空参数数组的读取（`SpPrintVArgument.Terms`、`SpCallSharpArgment.RowArgs`、`SpSetArrayArgument.TermList`、`SpDtColumnOptions.Values`、`SpTInputsArgument.*`、`SpSaveVarArgument` 等），逐点给出「该构造路径能否产出 null 元素」的结论并写入本文（定位用「文件#类型.成员」，不用行号）。新发现的可达缺陷按同根因并入本票，不同根因的转票。
11. **测试**（`Emuera.Tests`）：复用 `TestBootstrap.Initialize()` + `LogicalLineParser.ParseLine` + `ArgumentParser.SetArgumentTo` 的解析期 seam，逐触发式断言 `line.IsError` 或构造结果形态。用例清单：`INPUT ,,1` 报错、`INPUT 5,1,1` 三实参非空、`INPUT 5`/`INPUT ,1` 仍非错误；`PRINT_SPACE ,`/`PRINT_RECT ,`/`PRINT_RECT ,,,,` 报错、`PRINT_SPACE 10px` 仍可解析；`PRINT_IMG "a", , 100` 非错误且 `Nameb == null`、`Param[0].num` 非空；`HTML_PRINT "a", ,` 非错误且 `Opt == null`；`CALLSHARP "m", , 3`/`CALLSHARP "m"[,3]`/`CALLF FOO[1, ,2]` 报错（构造器早于插件查找，无需注册插件）；`VARS FOO`/`VARS FOO = ` 得到 `Value == ""`。`INPUT` 族 `DoInstruction` 需要 `EmueraConsole`、无法 headless 覆盖，以构造器不变量为准并在票内记明。
12. **顺序**：先按范围表写测试复现（票面原记触发式已按「依据」更正），再改构造器，最后跑排查。构建 `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-restore --nologo -v:minimal`；测试 `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`（沙箱内需申请权限）。

### 实施结果（2026-09-25，2026-10-05 结票时回填）

按判据在构造器侧处置省略实参、消费点不再判空，落地于 `e9eaaf32`（`fix(script): 省略实参在解析期处置，不再以 null 到达消费点`）。范围内 3 处断言站点随之删除（[bang-sites.md](../bang-sites.md) 的「已失效站点」记为 `SP_PRINT_IMG_ArgumentBuilder.CreateArgument#1`、`SP_PRINT_SHAPE_ArgumentBuilder.CreateArgument#1`/`#2`）；用例落在 `Emuera.Tests/Runtime/Script/Statements/OmittedArgumentTests.cs`，`.Required(` 与可空元素直读的排查结论见「影响」末条。

票面此前只记到「决策记录」，无实施结果节，此处按提交与站点清单回填。

## 曾考虑的替代方案

1. `INPUT` 一族「`Mouse` 省略代入 0、`Def` 省略代入 0/空串」：放弃。`Def = 0` 是交互路径上永远产生不了的值（空输入被 `long.TryParse` 拒绝），属发明语义；且 CanSkip 只在消息跳过时生效，静默填错值难以被发现。
2. `INPUT` 一族「像 `INPUTS` 一样在 `Def`/`Mouse` 缺失时静默丢弃 `CanSkip`」：放弃。与 `INPUTS` 对称，但把用户的显式意图无声吞掉；该写法今天必崩，报错不会破坏任何可运行的脚本。
3. `CALLSHARP` 省略实参代入 `PluginMethodParameter(0L)`：放弃。`PluginMethodParameter` 没有「省略」的表示，代入值与显式传 0 不可区分，插件按字符串读取时故障点只是从解释器移进插件。
4. `PRINT_IMG` 与 `PRINT_SPACE`/`PRINT_RECT` 一样在解析期拒绝省略：放弃。`Nameb`/`Namem` 本就以 null 为合法取值，`PRINT_IMG "a", , 100` 有自洽实现，一刀切会把可用写法变成拒绝。
5. 在各消费点逐点加判空（不改构造器）：放弃。与「构造器落实语义」相悖，且 `VARS` 的 null 不抛异常而是把变量写成 null（读回 `""`），判空也修不掉这类契约错误。
6. 把 `SpInputsArgument.Mouse`/`Def` 收窄为非空字段（构造器恒填）：放弃。`INPUT`（无实参）与 `INPUT 5` 的 `Def`/`Mouse` 为 null 是既有语义（`HasDefValue` 由此决定），收窄会连带改变交互路径。

## 影响

- 改动集中在 `ArgumentBuilder.cs`、`LogicalLineParser.cs`、`Argument.cs` 与 `Instraction.Child.cs`，含运行期取值变更（`VARS FOO` 由写 null 改为写 `""`）与解析期行为变更，须与可空迁移的元数据改动分开提交
- 兼容性变化**并非全是「崩溃变报错」**，有两处需要正视：
  - `INPUT ,,1`、`INPUT ,1,1`、`INPUT 5,,1` 在 `MesSkip` 关闭时 CanSkip 分支根本不进入，此前能正常加载并等价于 `INPUT` / `INPUT 5` 运行；`PRINT_SPACE ,`、`PRINT_RECT ,` 在默认 `ReduceArgumentOnLoad=NO` 下只构造出 `num` 为 null 的参数，仅在该行被执行时才崩。这些写法现在报 `CanNotOmitArg`，属可观测的兼容性变化
  - 报错时机取决于实参是否懒构造：`CALLSHARP`（`FORCE_SETARG`）与 `VARS`（`ParseLine` 内直接构造）在装载期报错；`INPUT`/`PRINT_*` 默认懒构造，错误出现在该行首次执行时（配 `NeedReduceArgumentOnLoad=YES` 或 AnalysisMode 才提前到装载期）
- `PRINT_IMG` 的缺席只允许出现在 `Nameb`/`Namem` 槽位（`argCount <= 3` 且尚无整数参数）：`PRINT_IMG "a", , 100` 仍按缺席处理；`PRINT_IMG "a", "b", "c", , 100` 与 `PRINT_IMG "a", 100, , 200`（坐标槽位省略）报 `CanNotOmitArg`——后者改动前是 NRE，不会被可运行脚本依赖
- 本仓库无实际维护的 changelog（`Readme/` 下为上游旧档），行为变更记入本文；若后续要求同步写某个 Readme 文件，另行指定
- 相邻但不同根因的四条缺陷（D1–D4）就地记录于文末「相邻缺陷（记录于本票，未修）」；`VARS FOO = abc`（无引号）在 `right[(literalStart + 1)..literalEnd]` 抛 `ArgumentOutOfRangeException` 而 `ParseLine` 只 catch `CodeEE`，与可空无关，仍按既有去向归 issues/28
- 用例落在 `Emuera.Tests` 的 `OmittedArgumentTests`（seam 为 `LogicalLineParser.ParseLine` → `ArgumentParser.SetArgumentTo`）。夹具 `TestBootstrap` 打开 `UseScopedVariableInstruction`：`VARI`/`VARS` 的注册受该 JSONConfig 开关门控，而 `FunctionIdentifier` 是静态构造、首次触碰后无法补注册，故必须在任何解析之前打开
- `.Required(` 类站点的核对：按该模式枚举 `ArgumentBuilder.cs` 的读取点并逐点确认守卫，除本票已修的三处（`SP_PRINT_SHAPE`、`SP_PRINT_IMG`、`SP_HTML_PRINT`）外，其余或走 `checkArgumentType`（非空类型位遇 null 即 `CanNotRecognizeArg`），或自带显式判空（`SP_PRINTV`、`SP_SAVEVAR`、`SP_SET`/`SP_SETS`）；指令侧对 `RowArgs`/`Terms`/`TermList`/`Values` 的读取由构造器保证，未发现新的可达点

### 决策记录（2026-09-25）

只记无法从「方案」「曾考虑的替代方案」直接读出的决定：

- 审查后收紧 `PRINT_IMG`：缺席只允许出现在 `Nameb`/`Namem` 槽位，坐标槽位省略报错（原方案 7 的论据覆盖过宽，跳过坐标会把 `Param` 压缩、让后续数字改换角色）
- 同句不变量注释由 8 处副本改为写在类型 `SpInputsArgument` 一处；`SP_CALLSHARP_ArgumentBuilder` 判空后改读局部变量；`SP_HTML_PRINT_ArgumentBuilder` 去掉冗余的 `constInt = false`（`opt != null ? … : 0` 已兜底）；删除票面点名的死构造器 `IntAsignArgument(string, int[])`
- `[...]` 下标形式整体不可用（见 D1）：「`subNames` 元素不可为 null」的判定成立；两条用例改述为「该形式整体被拒」，缺陷本身属解析器状态机，不在本票

### 相邻缺陷（记录于本票，未修）

本票只修「省略实参以 null 到达消费点」这一根因；以下四条在同一批代码中被顺带发现，均与省略无关，就地记录以备另立票：

| ID | 位置 | 现象与机制 |
| --- | --- | --- |
| D1 | `ExpressionParser.ReduceArguments` / `reduceTerm` | `ArgsEndWith.RightBracket` 的 `TermEndWith` 映射被注释掉，`termEndWith` 停在 `EoL`（`0x1`，同时缺 `Comma` 与 `RightBracket` 位），于是下标列表内的逗号与右括号都抛 `UnexpectedSymbol`——`CALLF FOO[1]`、`@FOO[1]` 一类方括号形式整体不可用；`ErbLoader` 里 `CanNotEmptyFuncSBrackets`/`CanNotOmitFuncDefineArg` 那套检查连带不可达 |
| D2 | `SP_HTML_PRINT_ArgumentBuilder` | 构造器从不设 `IsConst`，故 `HTML_PRINT_Instruction.DoInstruction` 的 `if (arg.IsConst)` 分支与构造器内的 `ConstStr`/`ConstInt` 折叠整体是死代码（行为无差异，只是白算） |
| D3 | `SP_INPUT_ArgumentBuilder` | 第 4 个及以后实参被静默丢弃（只取前三项，无 `TooManyArg`），属元数缺陷 |
| D4 | `LexicalAnalyzer.ReadInt64` | 紧跟数字的 `p` 被当作二进制指数（`expBase = 2`），`10px` 随后在 `x` 处读到空串 → `long.Parse("")` → `CodeEE(CanNotConvertToInt)`；能用写法只有加空格的 `10 px` |
