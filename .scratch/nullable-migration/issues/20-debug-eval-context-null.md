# Nullable:调试期以 null 求值上下文折叠表达式

Status: ready-for-agent

## 任务

`#LOCALSIZE`/`#LOCALSSIZE` 与 `#DIM` 的元素数解析处，把通用表达式交给 `Restructure` 时传入 `null!` 作为求值上下文。多数表达式在折叠时不触碰该上下文，但调试标识符（`__LINE__`、`__FILE__`、`__FUNCTION__` 一类）的 token 具备可折叠性，其取值需要经上下文取当前行，于是解引用 null。仅 `-DEBUG` 模式可达——非调试构建下这些标识符映射到忽略上下文的空值 token，这也是该问题长期未被发现的原因。

本票让这两处要么持有真实上下文，要么拒绝折叠需要上下文的表达式。

### 范围

| 位置 | 现状 |
| --- | --- |
| `Parser/LogicalLineParser.cs`（`#LOCALSIZE`/`#LOCALSSIZE` 处理） | `arg.Restructure(null!)`，`arg` 来自通用整型表达式归约 |
| `Statements/Variable/UserDefinedVariable.cs`（`#DIM` 元素数处理） | 同上 |

触发：`#DIM ARR, __LINE__`、`#LOCALSIZE __LINE__`，在 `-DEBUG` 下解析时即抛。

### 相关但不属本票

常量折叠处的 `GetIntValue(null!)`/`GetStrValue(null!)` 实参（约 65 处，多在 `ArgumentBuilder.cs`，另有 `VariableEvaluator.cs` 的 `FindChara` 系列与 `ExpressionParser.cs` 的 `ToStrFormTerm`）是安全的——每处都被 `is SingleTerm`/`IsConst` 判定支配，而单值 term 的取值不触碰上下文。它们不是缺陷，但契约不实（安全性依赖局部判据而非不变量），其具名入口的引入归 issues/22。本票不重复处理。

## 方案

1. 复现两处：`-DEBUG` 构建下用 `#DIM ARR, __LINE__` 与 `#LOCALSIZE __LINE__` 运行，确认异常位置在调试标识符取值处。
2. 选择修法：
   - 传真实求值上下文——需确认这两处的调用链是否已持有可用的上下文实例，若没有则要向上传递；
   - 或对需要上下文的表达式拒绝折叠并告警，保留 `VariableTerm` 形态留待运行期求值。
3. 判定依据是「该位置是否本就不该在对上下文无感知的阶段折叠」，需在票内写明，避免把问题简单转成又一次 `null!`。
4. 补 `Emuera.Tests` 用例：`#DIM`/`#LOCALSIZE` 的元素数为调试标识符时，解析不得抛 `NullReferenceException`。

## 影响

- 行为变更集中在 `-DEBUG` 解析期；非调试构建的标识符语义不变
- 与 issues/22 的具名入口工作相邻但不重叠：本票处理两处真缺陷，issues/22 处理安全但契约不实的其余调用点
