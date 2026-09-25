# Nullable:调试期以 null 求值上下文折叠表达式

Status: ready-for-agent

## 任务

`#LOCALSIZE`/`#LOCALSSIZE` 与 `#DIM` 的元素数解析处，把通用表达式交给 `Restructure` 时传入 `null!` 作为求值上下文。多数表达式在折叠时不触碰该上下文，但调试标识符（`__LINE__`、`__FILE__`、`__FUNCTION__` 一类）的 token 具备可折叠性，其取值需要经上下文取当前行，于是解引用 null。仅运行时 `Program.DebugMode`（命令行 `-DEBUG`）为真时可达——该开关为假时这些标识符注册为 `EmptyIntToken`/`EmptyStrToken`，其 `CanRestructure` 仍为 true 但取值不触碰上下文，这也是该问题长期未被发现的原因。这里的开关是运行期配置，不是 `#if DEBUG` 条件编译。

本票让这两处要么持有真实上下文，要么拒绝折叠需要上下文的表达式。

### 范围

| 位置 | 现状 |
| --- | --- |
| `Parser/LogicalLineParser.cs`（`#LOCALSIZE`/`#LOCALSSIZE` 处理） | `arg.Restructure(null!)`，`arg` 来自通用整型表达式归约。现形态为 `is not SingleLongTerm { IsInteger: true } sizeTerm`，由 `83a959af style: 使用 IsString / IsInteger 简化…` 从 `is not SingleLongTerm sizeTerm \|\| sizeTerm.GetOperandType() != typeof(long)` 改写而来；`{ IsInteger: true }` 对 `SingleLongTerm` 恒真，原判据被当冗余删除，`Restructure(null!)` 未动 |
| `Data/UserDefinedVariable.cs`（`#DIM` 元素数处理，`UserDefinedVariableData.Create`） | 同上（写票时路径即为 `Data/`，非 `Statements/Variable/`）；`arg` 来自 `ExpressionParser.ReduceIntegerTerm(wc, TermEndWith.Comma_Assignment)` |

触发：`#DIM ARR, __LINE__`、`#LOCALSIZE __LINE__`，在 `Program.DebugMode`（命令行 `-DEBUG`）为真时解析即命中 NRE。后果随路径不同：ERB 内的两处被 `ParseSharpLine` 的 `catch (Exception)` 转成等级 2 警告并丢弃该指令；ERH 的全局 `#DIM` 走 `ErhLoader.analyzeSharpDimLines`（其 catch 只接 `CodeEE`），NRE 逃逸到 `Process.Initialize` 的 `catch (Exception)` 才被处理。故复现判据不是「是否崩溃」。

### 相关但不属本票

常量折叠处的 `GetIntValue(null!)`/`GetStrValue(null!)` 实参共 18 处（`ArgumentBuilder.cs` 14、`VariableEvaluator.cs` 的 `FindChara` 两重载 4），另有 `ExpressionParser.ToStrFormTerm` 的 1 处 `strf.GetString(null!)`（票面原先记的「约 65 处」是 issues/14 对全部「调用实参位 `null!`」的计数，口径不同）。它们是安全的，但判据分三类：`ArgumentBuilder` 各处由 `is SingleTerm`/`allConst` 支配，`ToStrFormTerm` 由 `strf.IsConst` 支配，`FindChara` 两重载没有此类判据——它靠 `FindcharaMethod` 的 `ArgType.CharacterData` 保证实参是 character-data 变量，而该类 token 的取值不读 `exm`；单值 term 的取值也确实不触碰上下文。它们不是缺陷，但契约不实（安全性依赖局部判据而非不变量），其具名入口的引入归 issues/22。本票不重复处理。

## 方案

1. 复现两处：`Program.DebugMode` 为真时用 `#DIM ARR, __LINE__` 与 `#LOCALSIZE __LINE__` 运行，确认异常位置在调试标识符取值处。注意 ERB 路径的 NRE 会被解析器宽 catch 吞掉（见「触发」），不能以是否崩溃为判据。
2. 选择修法：
   - 传真实求值上下文——需确认这两处的调用链是否已持有可用的上下文实例，若没有则要向上传递；
   - 或对需要上下文的表达式拒绝折叠并告警，保留 `VariableTerm` 形态留待运行期求值。
3. 判定依据是「该位置是否本就不该在对上下文无感知的阶段折叠」，需在票内写明，避免把问题简单转成又一次 `null!`。
4. 补 `Emuera.Tests` 用例：`#DIM`/`#LOCALSIZE` 的元素数为调试标识符时，解析不得抛 `NullReferenceException`。

## 影响

- 行为变更集中在 `Program.DebugMode` 为真时的解析期；该开关为假时的标识符语义不变
- 与 issues/22 的具名入口工作相邻但不重叠：本票处理两处真缺陷，issues/22 处理安全但契约不实的其余调用点
