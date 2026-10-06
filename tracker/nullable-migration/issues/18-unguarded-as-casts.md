# Nullable:无保护的 as 转换三处（含同源的 MAXARRAY/MINARRAY）

id: nullable-migration-18
Status: implemented

## 任务

`(x as T)!` 的多数位置有前置类型检查（`FunctionMethod` 的实参类型表、`Word.Type` 分派、`VariableCode` 分派等），issues/22 为其补理由注释即可。三处没有保护：转换失败时 `as` 返回 null，`!` 只是把编译期警告压掉，运行期在此解引用。本票修正这三处，并一并修正同源的 `MAXARRAY`/`MINARRAY`（用直接转换，同一根因）。

### 范围

| 位置 | 为什么无保护 | 触发 |
| --- | --- | --- |
| `BuiltinMethod/PrivateAdditions.cs` 的 `XmlGetMethod.GetIntValue` | 第 3 实参规则是 `ArgType.Int`（`OmitStart = 2`）或 `RefString1D`；`IsInteger && 求值 != 0` 为假时进 `else`，取 `(arguments[2] as VariableTerm)!` | `XML_GET(xml, path, 0)`；整型表达式求值为 0 时同样进入（整型数组变量亦同，随后 `GetArray() as string[]` 得 null） |
| `BuiltinMethod/ArrayOperations.cs` 的 `MatchMethod.GetIntValue`（`CMATCH` 注册） | `CMATCH` 走 `MatchMethod(true)`，规则首项为 `ArgType.Any`（严格规则就在同处被注释掉），`CheckArgumentType` 因 `Any` 不做类型约束 | `CMATCH(0, 1)` |
| `BuiltinMethod/ArrayOperations.cs` 的 `InRangeArrayMethod.GetIntValue` | 解析期规则 `RefInt1D \| AllowConstRef` 保证是变量；但该类未设 `HasUniqueRestructure`，`FunctionMethodTerm.Restructure` 走非 unique 分支就地改写入参列表，用户定义常量一维数组被折叠成 `SingleTerm` | `INRANGEARRAY` 传常量数组变量（角色版 `INRANGECARRAY` 不可达：`CONST` 与 `CHARADATA` 互斥） |
| `BuiltinMethod/ArrayOperations.cs` 的 `MaxArrayMethod.GetIntValue`（同源追加） | 同上：`MAXARRAY`/`MINARRAY` 的非角色 ctor 规则含 `AllowConstRef`、未设 `HasUniqueRestructure`，取变量处是直接转换 `(VariableTerm)`，折叠后抛 `InvalidCastException` | `MAXARRAY(CONSTARR, 0, 3)` |

对照：`MATCH` 与 `FINDELEMENT`/`FINDLASTELEMENT` 安全。机制是 `FunctionMethodTerm.Restructure` 的 unique 分支把实参列表以 `[.. Arguments]` **副本**传入、改写落在副本上（不是「实参规则含 `AllowConstRef` 且丢弃结果」——`FINDELEMENT` 的规则是 `CharacterData | Any`，靠的是 `CharacterData` 检查）。折叠本身发生在 `VariableTerm.Restructure`：用户定义常量数组的 `CanRestructure = isConst`，裸一维数组名经 `VariableParser.ReduceVariable` 得到 `[ZeroTerm]`（`allArgIsConst` 为真），故取值后被折成 `SingleTerm`。

真正的不变量：**实参规则里出现 `AllowConstRef` 的引用型实参，方法必须设 `HasUniqueRestructure`**。按此扫全库，命中且可达的只有 `InRangeArrayMethod` 与 `MaxArrayMethod`（`MatchMethod`/`FindElementMethod`/`GetnumMethod`/`JoinMethod`/`ReplaceMethod`/`ArrayMultiSortEx`/`ArrayMultiSortMethod`/`ErdNameMethod` 均已 unique；`SumArrayMethod` 的非角色规则不含 `AllowConstRef`，常量数组在解析期即被拒）。

### 修法约束

三处都**不能**用 `(T)x` 替换：直接转换会把类型不符时的 `NullReferenceException` 变成 `InvalidCastException`；值为 null 时两种写法都在转换后的第一次解引用处抛 `NullReferenceException`（转换本身对 null 不抛）。差异只在「非 null 但类型不符」这一种情形——改它是可观测的行为变更，不是可空迁移的元数据改动。

## 方案

### 1. `XmlGetMethod.GetIntValue`：按文档分流，不收紧实参规则

`XML_GET` 第 3 实参有两种书式：`doOutput:int`（`0` 或省略 = 只返回合致结果数，非 0 则写入 `RESULTS`）与 `outputArray:ref string[]`。当前实现把「整型」与「变量」写在同一个 `if` 的 `&&` 里，`doOutput == 0` 于是掉进变量分支。改为按类型分流：

```csharp
if (arguments.Count >= 3)
{
    AExpression outputArg = arguments.Required(2);
    if (outputArg.IsInteger)   // 書式1/3: doOutput。0(または省略)は合致結果数のみ
    {
        if (outputArg.GetIntValue(exm) != 0) { /* RESULTS へ出力 */ }
    }
    else                       // 書式2/4: 実参規則2(RefString1D)が VariableTerm を保証
    {
        string[] arr = ((outputArg as VariableTerm)!.Identifier.GetArray() as string[])!;
        /* arr へ出力 */
    }
}
```

两条实参规则保持不变（它们差的是第 3 实参类型 `Int` vs `RefString1D`，不是只差 `OmitStart`，故不存在「收紧后合并」的问题）。`doOutput = 0` 由 NRE 变为文档行为；`doOutput = 1` 写 `RESULTS` 与 `outputArray` 写法均不变。

### 2. `MatchMethod(bool isChara)`：恢复严格规则

首项 `ArgType.Any` → `ArgType.CharacterData | ArgType.RefAny1D | ArgType.AllowConstRef`（即恢复同处被注释掉的那条；`RefAny1D` 已含 `Any`，不必再写）。效果：`CMATCH` 的非角色变量实参由「解析通过 + 运行期 NRE／被 `GetMatchChara` 静默按角色数组扫描」变为解析期 `CodeEE`，与文档「`charaArray` でなければなりません」及非角色版 `MATCH` 的规则一致。角色变量带显式下标（`CMATCH(CFLAG:0:2, 1)`）与角色字符串数组不受影响。

### 3. `InRangeArrayMethod`、`MaxArrayMethod`：补 `HasUniqueRestructure`

两类（各自全部 ctor）设 `HasUniqueRestructure = true`，并照 `MatchMethod.UniqueRestructure` 的形态覆写 `UniqueRestructure`：第 1 实参只调用 `Restructure` 以整形其内部、其返回值不用于替换实参；其余实参照旧处理；返回 false。`CanRestructure` 本为 false，故这类调用不会被整体折叠，实参列表是副本 —— 该实现同时把「变量槽位不被折叠」这一编译器看不到的不变量写明。

### 4. 测试（`Emuera.Tests`）

每条先跑红（NRE／`InvalidCastException`／无 `CodeEE`），再实现：

| 用例 | 缝 | 断言 |
| --- | --- | --- |
| `XML_GET(xml, path, 0)` | `FunctionMethod.GetIntValue`（首实参为字符串时不经 `DataXmlDocument`，不需要 exm） | 返回合致结果数；正控制：`doOutput = 1` 仍写 `RESULTS`、`outputArray` 写法不变 |
| `CMATCH(0, 1)` | `IdentifierDictionary.GetFunctionMethod`（解析期契约） | 抛 `CodeEE`；正控制：传角色变量项时不抛 |
| `INRANGEARRAY(CONSTARR, 0, 9)` | ErbLoader 装载真实脚本（临时游戏根 + `#DIM CONST`），再 `InstructionLine.DoInstruction` | `RESULT == 2`（`CONSTARR = 5, 1, 9`） |
| `MAXARRAY(CONSTARR, 0, 3)` | 同上 | `RESULT == 9` |

### 5. 验证

`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-restore` 与 `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio -p:Platform=x64`。本票改动处保留的 `!` 是否就地补理由注释由 issues/22 承接，本票不重复。

### 实施结果（2026-09-26）

- `XmlGetMethod.GetIntValue` 改为按 `outputArg.IsInteger` 分流：整型书式（`doOutput`）为 0 时只返回合致结果数、非 0 写 `RESULTS`；变量书式保留 `as`+`!`，由实参规则 2（`RefString1D`）保证 `VariableTerm`。
- `MatchMethod(bool)` 的首项规则恢复为 `CharacterData | RefAny1D | AllowConstRef`：`CMATCH` 的非角色变量实参由运行期 NRE／静默错扫变为解析期 `CodeEE`，角色变量与角色字符串数组不受影响。
- `InRangeArrayMethod`（2 个 ctor）与 `MaxArrayMethod`（3 个 ctor）设 `HasUniqueRestructure = true` 并覆写 `UniqueRestructure`：第 1 实参只调用 `Restructure` 以整形其内部、其返回值不写回实参列表，其余实参照旧处理，返回 false；常量一维数组实参不再在解析期被折叠。
- 新增 `Emuera.Tests` 的 `UnguardedAsCastTests`，覆盖 `XML_GET` 的 `doOutput = 0`/非 0/`outputArray` 三种书式、`CMATCH` 的解析期拒绝与角色变量正控制、`INRANGEARRAY`/`MAXARRAY` 传常量数组。

## 曾考虑的替代方案

1. 把 `XmlGetMethod` 第 3 实参规则收紧为 `RefString1D`（原票方案 1 的首选）：放弃——`doOutput` 为 `0` 或省略是文档化的书式，收紧会把它变成解析错误，方向与缺陷相反，且两条规则只在收紧后才重合、收紧后 `doOutput` 书式不再存在。
2. 只给 `XmlGetMethod` 的 `else` 补 `CodeEE` 而不改分流：放弃——`doOutput = 0` 是合法输入，报错等于把文档行为判为非法（整型变量求值为 0 时也会被一并拒绝）。
3. `MaxArrayMethod` 另开一票：放弃——同根因、同文件、同修法；不并入则 `MAXARRAY(CONSTARR, …)` 的 `InvalidCastException` 仍在，「实参规则含 `AllowConstRef` 就必须 unique」这一不变量名不副实。
4. 在 `FunctionMethodTerm` 的非 unique 分支禁止替换引用型实参：放弃——该分支服务全部非 unique 方法，面过大；以 `HasUniqueRestructure` 单点声明不变量。

## 影响

- 本票只动 `BuiltinMethod/*.cs` 的四处（`XmlGetMethod`、`MatchMethod(bool)`、`InRangeArrayMethod`、`MaxArrayMethod`）及其契约，不触碰 issues/22 的其余站点（那些保留 `as`+`!` 并补注释）
- 行为变更：`XML_GET`/`XML_GET_BYNAME` 的 `doOutput` 为 0（或求值为 0）由 NRE 变为按文档只返回结果数；`CMATCH` 的非角色变量实参由运行期崩溃/静默错扫变为解析期 `CodeEE`；`INRANGEARRAY`/`INRANGECARRAY`/`MAXARRAY`/`MINARRAY` 族传常量一维数组时不再在解析期被折叠。三处都不是「本来能正常工作的写法被改掉」
- 相邻观察（不在本票）：`XmlSetMethod.saveToArg0` 路径（与其后的 `(arguments[0] as VariableTerm)!`）不可达——两个 ctor 的 arg0 规则分别是 `Int` 与 `String`，`IsInteger || (byName && IsString)` 恒真；`XmlGetMethod` 的 `doc.SelectNodes(path)!` 得 null 的情形仍归 issues/22
