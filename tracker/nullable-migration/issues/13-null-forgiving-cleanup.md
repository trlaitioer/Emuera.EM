# Nullable:`!` 收敛(冗余删除与单点断言入口)

id: nullable-migration-13
Status: implemented

## 任务

可空迁移落地后,全库 `!` 从 develop 基线的 80 处涨到 1839 处(统计口径见「盘点口径」),其中相当一部分既不承担编译期作用、又把同一条断言复制到了每个消费点。本票把 `!` 收敛到「必要时只有一处,且带理由」:

- 删除编译器已通过前置判空收窄、删掉后不产生任何 CS86xx 的冗余 `!`
- 为「读取必填参数槽位」与「读取指令已就绪的参数/跳转目标」提供单点断言入口,取代逐点 `!`
- 局部变量的 `= null!` 与常量折叠调用点的 `null!` 参数一并处置

完成标准:全量构建 CS86xx 为 0(两种声音后端配置)、`Emuera.Tests` 通过、运行行为不变;可机械收敛的 `!` 清零——判据是「该处的不变量编译器本来就能证明」或「断言已收敛到单点入口」,剩下每一处都需要逐点判断,归 issues/14。

本票只做元数据级改动(`!` 的增删与只读访问器),不含任何判空/异常/分支变化;需要逐点判断、可能触及运行行为的断言在 issues/14。

## 方案

### 盘点口径(2026-09-17 实测)

统计范围 `Emuera/` 与 `Emuera.Tests/`,剔除注释与字符串字面量,匹配后缀 `!`(`[\w\)\]]\s*!(?!=)`):全库 **1839** 处,develop 基线 80 处。

2026-09-21 复测(HEAD `1ed33b5c`,出现次数口径):全库 **1088** 处(`Emuera` 981 / `Emuera.Tests` 107),其中 `null!` 393、`Argument` 可选字段 98、其他 `list[i]!` 78、`(x as T)!` 63、`JumpTo` 族 43、`ArgumentListExtensions` 2、残差 411。匹配式的 `\s*` 会把前缀逻辑非计入(HEAD 12 处误计,`3dd766f1` 时为 0),复测时应收紧为 `[\w\)\]]!(?!=)`;同一匹配式按「行」与按「出现次数」统计结果不同(issues/14 的 1040 与 1075 即这两个口径,见其「口径与清单」)。2026-09-25 按收紧式重测:全库 **1076** 处(`Emuera` 969 / `Emuera.Tests` 107),残差 **399** 处(`Emuera` 344),清单见 issues/21 的 [bang-sites.md](../bang-sites.md)——上段 1088 与 411 均含那 12 处误计。

| 类别 | 处数 | 根因 |
| --- | --- | --- |
| `arguments[i]!` | 466 | issues/05 把 `List<AExpression>` 元素改可空 |
| 其他 `list[i]!`(`terms[i]!`/`vars[i]!`/`args[i]!`/`nodes[i]!` 等) | 181 | 同上,以及 BCL 的可空返回(`XmlNodeList.this[int]`、`LinkedList<T>.First`) |
| `X.Argument!` | 171 | `InstructionLine.Argument` 声明为 `Argument?` |
| `X.JumpTo`/`JumpToEndCatch`/`ParentLabelLine!` | 42 | 装载期解析产物,声明为 `LogicalLine?`/`FunctionLabelLine?` |
| `arg.Def!`/`arg.Mouse!`/`arg.Term!` 等 | 114 | `Argument` 派生类的可选字段 |
| `(x as T)!` | 51 | |
| `null!` 字面量 | 384 | 声明处延后初始化(SKILL 推荐写法)+ 局部变量 + 调用实参 |
| 其他成员/局部 `!` | 430 | |

### 冗余判据(实测方法)

判据不是「看起来多余」,而是构建结果。第一版判据是「按同一口径删光全库后缀 `!`(注释与字符串原样保留)后重新构建,凡该位置无诊断即冗余」:1621 个被删行里 141 个无诊断。但该判据不成立——`!` 除断言外还参与**流分析收窄**,删掉一处 `!` 会让编译器失去该位置之后的不变量,从而在**别处**产生诊断,因此「删得更多」并不单调地产生更多诊断:

- `var label = (nextLine as FunctionLabelLine)!;`:`!` 决定 `var` 的推导类型,删掉后 `label` 变为可空,后续未带 `!` 的读取(不在候选集内)全部报错
- `srcArgs.SetTransporter(exm!)`:`!` 施加在实参上会阻断调用后收窄,删掉后同一变量在后续实参位置报 CS8604

故改用**应用—构建—回退**迭代:按候选集删除 → 构建 → 把仍报诊断的位置回退为原样 → 重复至 CS86xx 为 0。最终删除 109 处(12 个文件),每处都经编译器确认「这个不变量编译器本来就已经知道」。

典型形态是只读字段在既有判空块内的再断言:

```csharp
if (arg.Def != null)
{
    long def;
    def = arg.Def!.GetIntValue(exm);   // 判空收窄跨方法调用保持,`!` 无作用
```

这类位置说明当时的 `!` 是机械补齐而非编译器施压的结果。

### A 类:删除冗余 `!`

按上述迭代法定位后逐个删除,不改变任何其他标记。

### B1:`List<AExpression?>` 的命名访问器

元素可空是 issues/05 的既定契约——「省略参数以 `null` 表达」被 `FunctionMethod.CheckArgumentTypeEx`/`CheckArgumentType` 与 `Process.CalledFunction.ConvertArg` 依赖,不能退回非空。需要命名的是「读取必填槽位」这一动作,入口落在 `Emuera/Runtime/Script/Statements/Expression/ArgumentListExtensions.cs`:

```csharp
internal static class ArgumentListExtensions
{
	// The parser rejects an omitted slot before an implementation reads it, so a caller that
	// wants a filled slot asserts once here instead of at every read site.
	internal static AExpression Required(this List<AExpression?> arguments, int index) => arguments[index]!;

	internal static AExpression Required(this AExpression?[] arguments, int index) => arguments[index]!;
}
```

> **`arg-contract-03` 回填（措辞更正）**：上面这段提案注释的第 1 句（「The parser rejects an omitted slot before an implementation reads it」）**不成立**，落地版没有采用它。检查器只拒绝**规则表标记为必填**的位置（`OmitStart == -1` 的块、`i < OmitStart`，以及 `arg-contract-03` 片 3 铺开的 `ArgType.NotNull`）；`i >= OmitStart` 的槽是设计上可省略的，`f(a,b,,)` 这类显式空槽在那里**会**带着 null 进入函数体（`RAND(1,,)`、`HTML_STRINGLEN("x",,)`、`REGEXPMATCH("a","b",,)` 三处已举证为可达 NRE）。故「调用方担保」只对前一类槽成立，落地注释已按此改写（`ArgumentListExtensions.cs`），`arg-contract-03` 也据此逐槽铺开 `ArgType.NotNull`。

调用点由 `arguments[0]!.GetStrValue(exm)` 改为 `arguments.Required(0).GetStrValue(exm)`。同一入口覆盖 `terms`/`args`/`srcTerms`/`termList`/`Arguments` 等元素可空的容器;接收者类型不是 `AExpression?` 的(如 `VariableTerm?[]` 的 `vars`、`XmlNodeList` 的 `nodes`)保持原断言,由编译错误逐个回退区分。

不采用 SKILL 里 `[MemberNotNull]`/`Debug.Assert` 一类提示手段:可空性在**元素**上而非成员上,流分析的提示无处附着。

### B2:`InstructionLine` 的已建参数入口

`Process.ScriptProc` 派发 `DoInstruction` 之前必然已由 `ArgumentParser.SetArgumentTo` 填好 `Argument`(派发点本身即以 `if (func.Argument == null) ArgumentParser.SetArgumentTo(func)` 兜底,失败则抛出),`ErbLoader.setArgument` 也在 `SetJumpTo` 之前完成。声明侧必须保留 `?`(解析期确实为 null,`ErbLoader`、`Process.ScriptProc`、`Instraction.Child`、`ArgumentParser` 共有 13 处判空依赖它),读取侧加只读入口:

```csharp
public Argument? Argument { get; set; }
// Argument stays null until ArgumentParser builds it; every read through BuiltArgument happens
// after that point (ErbLoader.setArgument before SetJumpTo, Process.ScriptProc before
// DoInstruction), so the parser-facing null tests keep the nullable property and readers assert
// in this one place.
public Argument BuiltArgument => Argument!;
```

对应 SKILL 的「被访问 50 次的字段应当只有一处 `null!`,而不是 50 处 `field!`」。原先依赖 `func.Argument!` 的收窄才成立的读取,一并改为 `BuiltArgument`。

`JumpTo`/`JumpToEndCatch`/`ParentLabelLine` 原计划同法处理,实测发现 `Instraction.Child.cs` 的 `GOTO_Instruction.SetJumpTo` 存在 `func.JumpTo = null` 的复位路径(未定义标签且 TRY 系时不报错直接返回),「执行期必然非空」这一不变量需要逐点核实,不在本票做,移交 issues/14。

### B3:局部变量与常量折叠实参(移交 issues/14)

- 局部变量的 `= null!`(如 `ErbLoader.setJumpTo` 的 `FunctionNotFoundName`、`GraphicsImage` 内的 `Brush`)改为可空局部或提取方法,由真正的构造点保证
- `AExpression.GetIntValue(null!)`/`GetStrValue(null!)` 等常量折叠调用点(约 25 处,issues/06 已决定 `exm` 保持非空)改为具名入口,让「此处无求值上下文」表达一次

这两项都要求逐处判断初始化路径(局部 `= null!` 的存在本身就是「编译器无法证明被赋值」的信号),不是机械变换,故不在本票做,连同成员声明处保留的 `= null!` 一起交 issues/14 复核。

### 验证

- `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 -p:Nullable=enable`:CS86xx = 0
- WMP 后端(`Debug`,VS amd64 `MSBuild.exe`,清空 `*_PROXY`)同样为 0
- `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64` 通过

### 执行结果(2026-09-17)

| 项 | 结果 |
| --- | --- |
| `!` 总量 | 1839 → 1040(匹配行数口径) |
| A 类删除 | 109 处 / 12 个文件 |
| B1 `Required(...)` | 429 处调用 |
| B2 `BuiltArgument` | 167 处 |
| 构建 | `Debug-NAudio` 与 `Debug`(WMP,amd64 `MSBuild.exe`)CS86xx 均为 0 |
| `Emuera.Tests` | 144 通过 / 0 失败 |

剩余 1040 处的构成:`null!` 字面量 384(多为 SKILL 认可的声明处延后初始化,局部变量部分交 issues/14)、其他成员/局部 `!` 401、`arg.Def!` 等可选字段 83、其他 `list[i]!` 79(BCL 可空返回,以及接收者类型尚未逐一核实的容器)、`(x as T)!` 51、`JumpTo` 族 40,以及 `ArgumentListExtensions.Required` 内承担全部参数槽位断言的 2 处,均在 issues/14 的复核范围。

以上为「匹配行数」口径。2026-09-21 复测(出现次数口径,HEAD `1ed33b5c`):全库 1088 处,`null!` 393、`Argument` 可选字段 98、其他 `list[i]!` 78、`(x as T)!` 63、`JumpTo` 族 43、`ArgumentListExtensions` 2、残差 411;两个「移除冗余的 `!`」提交(`e70ec779`/`cb8661ba`)只删 19 处,`3dd766f1`→HEAD 净 +13(`Emuera.Tests` +25、`Emuera` −12)。

## 曾考虑的替代方案

1. 把 `List<AExpression?>` 退回 `List<AExpression>`,用哨兵元素表达省略参数:能一次性消掉 466 处断言,但省略标记要贯穿 `ReduceArguments`/`RowArgs`/`ConvertArg`/`CheckArgumentType*`,且 `null` 判断全部变成类型判断,属契约与运行行为改动,超出本票范围
2. 引入 `ArgumentList` 包装类型,把索引器直接做成非空、另设可空访问器:语义最正,但要改 305 处签名与全部构造点,改动量大于它消除的 `!`;留待 issues/14 评估
3. 把 `InstructionLine.Argument` 直接声明为非空 + `= null!`:解析期确实为 null(13 处判空依赖),属 SKILL 明确反对的「用非空签名掩盖可空状态」
4. 把 `exm` 参数改 `ExpressionMediator?`:issues/06 已否决——会把一个调用点断言扩散成约 230 个实现的判空负担
5. 在 `AInstruction` 里加 `Debug.Assert(func.Argument != null)`:只影响 Debug 构建,且每个 `DoInstruction` 都要重复一次,断言点数量不降

## 影响

- 全库 `!` 由 1839 处降到 1040 处,保留部分集中在 `Argument` 可选字段与 `JumpTo` 族的逐点判断、`(x as T)!`,以及声明处延后初始化的 `null!`(见 issues/14)
- 新增 `Emuera/Runtime/Script/Statements/Expression/ArgumentListExtensions.cs`,以及 `InstructionLine` 的只读属性 `BuiltArgument`;均为 `internal`,不影响插件 API
- 无运行行为变化,IL 除元数据外不变;`Emuera.Tests` 作为回归安全网
