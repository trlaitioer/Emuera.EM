# Nullable:省略参数可空贯通

Status: needs-triage
Type: task

## 背景

StrForm 修订(issues/01)时确认:基类 `FunctionMethod` 以 `arguments[i] == null` 表达"省略参数"(`CheckArgumentTypeEx`/`CheckArgumentType` 判空),但整条方法调用链的列表元素类型都声明为非空 `AExpression`,StrForm 只能以 `null!` 占位。元素可空化曾试验性贯通到基类签名,因链路深度远超 override 签名而回退,归入本票整体执行。

## 链路(2026-08-29 实测)

- 基类:`FunctionMethod` 的 `CheckArgumentTypeEx`/`CheckArgumentType`/`GetIntValue`/`GetStrValue`/`GetReturnValue`/`UniqueRestructure` 共 6 个签名
- 分发:`FunctionMethodTerm` 的构造器与字段
- override 签名:`Creator.Method.cs`(204 处)、`OperatorMethod.cs`(40 处)、`ArgumentBuilder.cs`(1 处)、`IdentifierDictionary.cs`(1 处)、`StrForm.cs`(6 处)
- 边界链:`IdentifierDictionary.GetFunctionMethod` 的 9 个调用点(Instraction.Child×4、ExpressionParser×2、Creator.Method×3,含直接传 `null` 的无参调用)、其上游 `callfArg.RowArgs` 等列表类型(Argument 侧)、`ExpressionParser.ReduceArguments` 的返回类型
- 方法体:各 override 内 `arguments[i]` 解引用在元素可空化后需逐处处理(`!` 或判断,依据是 `CheckArgumentTypeEx` 已在类型检查阶段拒绝不允许省略位置的 null),随各文件启用 `#nullable` 时进行

## 方案

1. 元素类型 `AExpression` → `AExpression?` 沿上述链路一次性贯通(签名与列表类型定义)
2. 方法体适配随 issues/08/09 的文件启用进行,不提前
3. 完成后 StrForm 的 7 处省略参数 `null!` 替换为 `null`

## 备注

- 不阻塞 issues/03(占位 `null!` 不产生 CS86xx,收尾验收可达成)
- 第一优先执行(与 issues/06 横切契约票并列先行):Creator.Method 等最大 override 集中在 issues/08,若在其后翻转,已启用文件将集中出现方法体 CS8602
- 与 `FunctionMethod.argumentTypeArray` 的 `Type[]?`(已在 issues/01 中完成)无关

## Comments

### 2026-08-29

- 新建:StrForm 修订中试验性贯通基类签名后回退,链路调查结论与执行范围归入本票。
- 差分按调用链重切:本票定为先行契约票(与 issues/06 并列),执行时机与 08/09 的关系在备注中更新。
