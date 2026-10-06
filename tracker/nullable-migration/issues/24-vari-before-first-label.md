# Nullable:首个 `@函数` 之前的 VARI/VARS 语句解引用 null 的 parentLine

id: nullable-migration-24
Status: implemented

## 任务

`LogicalLineParser.ParseLine` 的 VARI/VARS 分支把 `parentLine` 断言为非空：

```csharp
parentLine!.AddPrivateVariable(varData);   // VARI 分支
...
parentLine!.AddPrivateVariable(varData);   // VARS 分支
```

而 `ErbLoader.loadErb` 在首个 `@函数` 之前只会**警告并继续解析**：

```csharp
if (lastLabelLine == null)
{
    ParserMediator.Warn(trerror.LineBeforeFunc.Text, position, 1);
}
nextLine = LogicalLineParser.ParseLine(st, position, output, lastLabelLine);
```

`lastLabelLine` 初值为 null、只在遇到标签后才被赋值，故首个标签之前的任何行都以 `parentLine == null` 进入 `ParseLine`。此时写 `VARI X` 或 `VARS X` 会走到上面两处断言，`AddPrivateVariable` 是实例方法，立即 `NullReferenceException`（装载期崩溃，而不是给出「函数定义之前不能声明私有变量」这类脚本错误）。

触发条件：`JSONConfig.Data.UseScopedVariableInstruction` 开启（配置对话框的 `_useVAR` 勾选项；`FunctionIdentifier` 仅在该开关下注册 `VARI`/`VARS`），且 `VARI`/`VARS` 语句出现在文件首个 `@` 标签之前。带初值的写法另有同族缺口：初值表达式解析时 `GlobalStatic.Process.scaningLine` 已指向本行，而本行的 `ParentLabelLine` 仍为 null，故初值里的显式 `@子键` 局部变量引用会先在 `GetVariableToken` 处空引用（见 issues/25）。

本票为断言审计（issues/21）判定的 **C 类可达缺陷**，对应站点：

- `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#2`（VARI）
- `Emuera/Runtime/Script/Parser/LogicalLineParser.cs#LogicalLineParser.ParseLine#3`（VARS）

## 方案

让「函数定义之前」这一状态显式化，而不是靠断言：

1. `ErbLoader.loadErb`：`lastLabelLine == null` 时把 `LineBeforeFunc` 从警告升级为错误并跳过该行（与 `nextLine is InvalidLine` 的处理一致），或至少不再把该行交给 VARI/VARS 分支；
2. `LogicalLineParser.ParseLine`：在 VARI/VARS 分支入口判 `parentLine == null`，返回 `new InvalidLine(position, trerror.LineBeforeFunc.Text)`，随后删掉两处 `!`。

两条任选其一即可消除 NRE；建议同时做（1）与（2），使错误信息指向脚本位置而非内部状态。

完成标准：`UseScopedVariableInstruction` 开启时，首个 `@函数` 之前的 `VARI`/`VARS` 不再产生 `NullReferenceException`；`Emuera` 与 `Emuera.Tests` 的 CS86xx 仍为 0，测试通过。

### 实施结果（2026-10-05）

方案（2）落地为分支入口守卫：`LogicalLineParser.ParseLine` 的 VARI/VARS 分支在 `parentLine == null` 时返回 `InvalidLine(position, trerror.LineBeforeFunc.Text)`，两处 `parentLine!` 随流分析一并删除。该守卫同时覆盖另一条入口——`ParseLine(string, EmueraConsole?)` 重载不给 `parentLine` 传值（插件/调试的单行入口），其 VARI/VARS 此前同样会空引用。

方案（1）按「不扩大行为变化」收窄落地：`ErbLoader.loadErb` 的 `lastLabelLine == null` 警告移到解析之后，且仅在解析结果不是 `InvalidLine` 时发出。故函数定义之前的普通语句仍是原级别 1 警告（与「影响」节所述范围一致），VARI/VARS 则由解析器给出级别 2 的 `LineBeforeFunc`，同一行不再报两次。

新增用例 5 例：`Emuera.Tests/Runtime/Script/Loader/ErbLoaderPreFunctionLineTests.cs`（VARI/VARS 装载期不抛异常且 `LoadErbList` 返回 false、正控制 1 例）与 `LogicalLineParserTests`（无父标签得 `InvalidLine`、有父标签的正控制）。装载夹具与 `ErbLoaderNestCheckTests`、`BreakLoopCounterTests` 共用新抽出的 `ErbLoaderFixture`（原为逐字复制的第 3、第 4 份拷贝）。红→绿实测：还原旧行为后 3 例报 `NullReferenceException`（两例从 `LoadErbList` 内逃逸，印证「装载期崩溃」），修复后全绿。

验证：`dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64` CS86xx = 0；`Emuera.Tests` 由 691 条增至 696 条，全绿。

## 影响

- 行为变化限于「函数定义之前的 VARI/VARS」这一非法写法：现状是空引用异常，改为脚本可见的错误
- 触及 `ErbLoader.loadErb` 与 `LogicalLineParser.ParseLine`；`VARI`/`VARS` 的合法用法（在 `@函数` 之后）不受影响
- 同一守卫还覆盖 `LogicalLineParser.ParseLine(string, EmueraConsole?)`（插件 `PluginManager.ExecuteLine` 与调试窗口的单行入口，该重载不给 `parentLine` 传值）：`VARI`/`VARS` 由空引用异常变为经 `MemoryLineEntry.TryBuild` 转出的可读错误
- `ErbLoader` 侧：函数定义之前且**解析失败**的行不再额外发一条级别 1 的 `LineBeforeFunc`（原路径先发级别 1、再由 `InvalidLine` 分支发级别 2）；现在只留级别 2 的准确报错，函数定义之前能正常解析的行仍为原级别 1
- 与 issues/25 同属「首个函数之前 / 无父标签」这一族，但根因不同（本票是行位置，issues/25 是显式 `@subKey`），故分列
