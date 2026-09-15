# Nullable:省略参数可空贯通

Status: implemented

## 任务

StrForm 修订(issues/01)时确认:基类 `FunctionMethod` 以 `arguments[i] == null` 表达"省略参数"(`CheckArgumentTypeEx`/`CheckArgumentType` 判空),但整条方法调用链的列表元素类型都声明为非空 `AExpression`,StrForm 只能以 `null!` 占位。元素可空化曾试验性贯通到基类签名,因链路深度远超 override 签名而回退,归入本票整体执行。

完成标准:链路签名与列表元素类型一次性贯通为 `AExpression?`,决策与影响面记录于本文;不启用任何文件。

## 方案

### 贯通链路(2026-09-15 实测)

- 基类:`FunctionMethod` 的 `CheckArgumentTypeEx`/`CheckArgumentType`/`GetIntValue`/`GetStrValue`/`GetReturnValue`/`UniqueRestructure` 共 6 个签名(`FunctionMethod.cs` 内 `List<AExpression>` 出现 6 次)
- 分发:`FunctionMethodTerm` 的构造器与字段
- 声明与 override 触点(按 `List<AExpression>` 出现次数):`Creator.Method.cs` 209、`OperatorMethod.cs` 40、`Argument.cs` 20、`ArgumentBuilder.cs` 13、`StrForm.cs` 7(其中 6 处为 override 签名)、`UserDefinedMethodTerm.cs` 3、`ExpressionParser.cs` 2、`FunctionMethodTerm.cs` 2、`Process.CalledFunction.cs` 1、`IdentifierDictionary.cs` 1、`ErbLoader.cs` 1,连同基类 6 处共 305 处
- 边界链:`IdentifierDictionary.GetFunctionMethod` 的 9 个调用点(`Argument.cs`×2、`Instraction.Child.cs`×2、`ExpressionParser.cs`×2、`Creator.Method.cs`×3,含直接传 `null` 的无参调用)、其上游 `callfArg.RowArgs` 等列表类型(`Argument.cs` 的 `SpCallFArgment`/`SpCallArgment`/`SpCallSharpArgment`)、`ExpressionParser.ReduceArguments` 的返回类型
- 方法体:各 override 内 `arguments[i]` 解引用在元素可空化后需逐处处理(`!` 或判断,依据是 `CheckArgumentTypeEx` 已在类型检查阶段拒绝不允许省略位置的 null),随各文件启用 `#nullable` 时进行

### 执行

1. 元素类型 `AExpression` → `AExpression?` 沿上述链路一次性贯通(签名与列表类型定义)
2. 方法体适配随 issues/08/09 的文件启用进行,不提前
3. 完成后 StrForm 的 7 处省略参数 `null!` 替换为 `null`

执行约束:

- 不阻塞 issues/03:契约本身不启用文件;元素可空化在未启用文件里引入的 CS86xx 由各链簇票消化(见影响),收尾验收仍只看链簇票完成后的全量构建
- 第一优先执行(与 issues/06 横切契约票并列先行):Creator.Method 等最大 override 集中在 issues/08,若在其后翻转,已启用文件将集中出现方法体 CS8602
- 与 `FunctionMethod.argumentTypeArray` 的 `Type[]?`(已在 issues/01 中完成)无关

## 影响

- `FunctionMethod` 全链签名元素可空化,"省略参数以 null 表达"成为显式契约;元素可空语义从解析产物(`ReduceArguments`/`RowArgs`)经 `GetFunctionMethod`、`FunctionMethodTerm` 一直贯通到 override 方法体
- 方法体 `arguments[i]` 解引用的适配随 issues/08/09 文件启用消化

### 决策记录

- **UDF 参数数组同批可空化**:`Process.CalledFunction.cs` 的 `UserDefinedFunctionArgument` 构造器与 `Arguments` 字段、`ConvertArg` 的 `convertedArg`/`term` 改 `AExpression?[]`/`AExpression?`。依据是该类 `SetTransporter`/`Restructure` 早已以 `Arguments[i] == null` 判空,省略实参经 `ConvertArg` 的默认值分支后确实可能保持 null
- **例外**:`StrForm.Restructure` 内的 `List<AExpression> termList = [.. terms]` 收窄为可空会在 `terms = [.. termList]` 处产生 CS8601,而 `terms` 的元素由 `FromWordToken` 逐路径保证非空,故保持不变
- **StrForm 适配口径**(唯一已启用文件,默认构建 CS86xx 保持 0):5 处省略参数占位与 2 个局部变量改 `null`(局部变量声明改 `AExpression?`/`SingleTerm?`);`FormatCurlyBrace`/`FormatPercent` 的宽度参数改为模式匹配取局部变量;各 `arguments[0]`(`FormatYenAt` 另含 `arguments[1]`/`arguments[2]`)以 `!` 断言非空,依据是这些槽位由 `FromWordToken` 构造、不可能省略
- **`SingleIntFormTerm` 返回契约**:无匹配形态时返回 `null`,故返回类型为 `AExpression?`;调用点 `CallformCache.longTerm`(`Argument.cs`,归 issues/09)本就以判空使用,字段同步标注为可空,全量构建下该处原有的 CS8618 一并消除。该改动在 issues/09 同步记录
- **链外留待各票**:`Argument` 子类自身的参数数组(`SpTInputsArgument.Terms`、`SpDtColumnOptions.Values`、`BitArgument.Term` 等)按 issues/09"按构造路径核实后标注"处理,不在本票范围
- 元素可空化使全量构建警告数上升(增量集中在链内未启用文件的 `arguments[i]` 解引用),各链簇票开工时须重新实测本链数字
