# Nullable:`ConstantData.output` 的非空断言与测试夹具的 null console 冲突

id: nullable-migration-33
Status: needs-triage
Type: bug

## 任务

`nullable-migration-22` 收尾后的两轴 review 逐站复核时发现:`ConstantData` 把 `output` 写成非空字段,但同文件另有按 null 守卫的读取点,两条 catch 分支上的 `output!` 是**假断言**——断言的内容与该字段的既有用法不符。本票给出处置选择,不动代码。

### 事实

- 字段:`Emuera/Runtime/Script/Data/ConstantData.cs:138` 的 `private EmueraConsole output = null!;`
- 赋值:构造函数形参 `console`(`:140`,声明为非可空 `EmueraConsole`)经 `LoadData`(`:761`)在 `:763` 写入 `output = console;`
- 但两个测试夹具都以 `null!` 传该形参:`Emuera.Tests/TestBootstrap.cs:47`、`Emuera.Tests/Runtime/Script/Data/ConstantDataTests.cs:28`
- 同文件另有 3 处按 null 守卫:`:2068`(`!eReader.Open(csvPath) && output != null`)、`:2082`(`(disp || Program.AnalysisMode) && output != null`)、`:2181`(`!eReader.Open(aliasPath) && output != null`)——即作者知道 `output` 可能为 null

### 后果

- `:2152`(`loadDataTo` 的 catch)与 `:2223`(别名装载的 catch)的 `output!` 在 null console 下为假:命中这两条分支会 NRE,而同类的 `:2068`/`:2082`/`:2181` 是静默跳过——同一文件内两种不一致的处置
- 触发条件:构造时 console 为 null(测试夹具),且 `loadDataTo`/别名装载在读到第一行之前抛异常(`ScriptPosition? position` 仍为 null,故走 `else` 的 `output!.PrintError`)
- 生产路径不受影响:`Process.cs:191` 传的是真实 console
- `nullable-migration-22` §1 未给这两处补理由注释——任何注释在此都只能陈述一个假不变量,故按契约问题登记而非注释问题

## 方案

三条取法,择一;共同判据是「改后 `output` 的声明与所有读取点的处置一致」:

1. **诚实化类型**:字段与构造函数形参改 `EmueraConsole?`,`:2152`/`:2223` 补判空(与同文件另 3 处一致)。代价:null console 下的这两条路径由 NRE 变为静默跳过——属行为变更(仅测试夹具路径可观测),须给判据
2. **收紧契约**:保持 `output` 非空,改测试夹具不再传 `null!`。前置:先确认 `EmueraConsole` 能否在 `Emuera.Tests` 中构造(现在传 `null!` 很可能正是因为构造不出来),否则该取法不成立
3. **显式的无 UI 控制台**:引入一个「无实现」的 `EmueraConsole` 子类或空对象,替代 `null!`,使「没有 UI」成为一种可命名的状态而非 null

取法 2/3 的收益都大于 1:它们不引入新的可空传播,且让夹具的意图写在类型上。但 2 受可行性约束,3 有实现成本。

## 影响

- 取法 1 会把 `output` 的可空性沿 `ConstantData` 内的其余读取点传播,须逐个确认是新断言还是新判空(本票的核心工作量在此)
- 取法 2 触及 `Emuera.Tests` 的运行时初始化(`TestBootstrap.cs`),该文件的初始化顺序是既有约定,改动须重跑全量测试
- 取法 3 新增一个类型,须与 `ParserMediator` 现有的 null console 约定(见 `Emuera.Tests/README.md` 与 issues/22 §1 提到的「`ParserMediator.Warn` 在 null console 下不写 `warningList`」)对齐,否则会形成第三套「没有 UI」的表达
- 本票与 [issues/31](31-assertion-honesty-nonnull-consumers.md) 同属「断言内容为假」的诚实化,但根因不同:31 的 null 来自被调方在运行期真的会缺值,本票的 null 只来自测试夹具,故未并入 31
