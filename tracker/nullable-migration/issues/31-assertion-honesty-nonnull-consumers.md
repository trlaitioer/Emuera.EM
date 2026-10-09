---
id: nullable-migration-31
status: draft
type: task
---

# Nullable:可空化被非空消费方挡住的断言链

## 任务

`nullable-migration-22` §3「契约诚实化」在实施时逐项取证，其中四条链的断言**内容确实为假**（`!` 掩盖了真实的 null 契约），但把被调方写诚实会牵动**迁移清单之外**的非空消费方，且其中两处必须先在「报错语义」或「哨兵设计」上做决定，故未实施，移出该票。本票承接这四条链。

共同判据：`!` 的删除不是纯元数据改动——被调方的可空化会沿消费链传播，直到某个把值当真值使用的调用点；那个点要么补判空（改变行为：NRE → 静默/异常），要么留在上游断言（等于把断言搬家，净不减少）。

### 范围

| # | 链 | 现状 | 被谁挡住 |
| --- | --- | --- | --- |
| 1 | `UI/Game/Image/GraphicsImage.cs` 的 `brush`/`pen`/`font` | 字段在 `GDispose` 置 null，类内读取点全判空，但公开访问器（`Brush`/`Pen`/`Fnt`/`Fontname`/`Fontsize`）传播非空签名 | 唯一消费方是 `Runtime/Script/Statements/Function/BuiltinMethod/Graphics.cs` 的 6 处（`Fnt` 541、`Pen` 112/114、`Brush` 116、`Fontname` 160、`Fontsize` 108），其中 `Pen`/`Brush` 直接被当非空用（`g.Pen.Color`、`(SolidBrush)g.Brush`）。访问器改可空即 6 处 CS86xx；需要先定「字体/画笔缺席时 `GGETFONTSIZE`/`GGETPEN` 一类返回什么」——属报错语义决定，不是注解决定 |
| 2 | `UI/Game/Image/ConstImage.cs` 的 `RealBitmap = ImgUtils.LoadImage(Filepath)!` | `LoadImage` 返回 `Bitmap?`，下一行即判空，故断言为假 | 写诚实需 `RealBitmap` 与 `AbstractImage.Bitmap`（`UI/Game/Image/AImage.cs`）改 `Bitmap?`；`BuiltinMethod/Graphics.cs:2800` 的 `g.Bitmap.Save(filepath)` 无判空 |
| 3 | `UI/Game/HtmlManager.cs` 的 `ButtonValueStr = value!` | `<button>` 无 `value` 属性时 `isButton` 为真而 `value` 为 null，断言为假 | 读取点把它喂进 `ConsoleButtonString` 的非空 `string inputs`（其 `Inputs` 字段 `= null!`，注释称只用于比较），整链写诚实需 `Inputs` 可空，而消费方含 `BuiltinMethod/Graphics.cs:2436`（直接 `return …Inputs;`）。折中（字段可空 + 读取点加 `!`）净增 1 处断言 |
| 4 | `UI/Game/HtmlManager.cs` 的换行哨兵 `buttonList.Add(null!)` | 容器元素类型 `List<ConsoleButtonString>` 与实际写入的 null 哨兵不符 | （a）`List<T>` → `List<T?>` 后按**值**传递也报 CS8620，`m_buttonList`/`Html2ButtonList` 的 `parts` 必须一起可空；（b）循环内 `buttonList[i]` 的判空不被流分析跟踪，循环体需改为「先取局部」的等值重写；（c）整链撞上 `ConsoleDisplayLine.buttons` 的消费方（`Instraction.Child.cs` 8 处、`Runtime/Utils/EvilMask/ConsoleDivPart.cs` 3 处）；（d）`ConsoleDisplayLine` 构造器 `foreach (button) button.ParentLine = this` 今天遇到哨兵即 NRE，真要诚实化须在此跳过 null（行为变化）或改用非 null 的哨兵对象（redesign） |

### 已取证的周边结论（不再复核）

- `nullable-migration-22` §3 的 `MainWindow.console` 改 `readonly` **不成立**：`MainWindow_FormClosing`（`UI/Framework/Forms/MainWindow.cs:1515`）有 `console.Dispose(); console = null;`，故 `readonly` 不可能；`if (console == null)` 共 26 处也**不是**死代码（构造器第 25 行 `InitializeComponent()` 先接线设计器事件、第 62 行才赋值）。
- `ConsoleButtonString` 构造器的 `parent = console!` 保留：`parent` 类内 9 处读取，改可空后即使把 4 处改写成 `console.NewButtonGeneration` 仍需 5 处新断言，净亏；已把原注释（称 console 只在 `DivideAt` 拆出的串上缺失，与事实不符）改为真实不变量。
- `VariableToken` 构造函数「未使用的 `VariableData` 参数」**不成立**：`VariableToken.cs:127` 的 `varData` 字段在 Chara/CHARANUM/LASTLOAD 等分支被读 60+ 处；真正的假断言只在 `:361` 的 `: base(varCode, null!)`（`UserDefinedVariableToken` 及其全部子类都不读该字段）。两种诚实化（字段改可空 / 字段下移）分别为「1 处假断言换 25-30 处真断言」与结构性改动，收益均为负。

## 方案

四条链互相独立，可分四次实施；每条都以「改后断言处数不增加」为验收判据（否则等于把断言从被调方搬到消费方，净不减少）。共同前置是先定「值缺席时的报错语义」或「哨兵设计」，故本票不预设单一做法：

| # | 做法 | 前置决定 |
| --- | --- | --- |
| 1 | 公开访问器改可空，同步 `Graphics.cs` 的 6 处消费点 | 画笔/字体缺席时 `GGETPEN`/`GGETFONTSIZE` 一类返回什么 |
| 2 | `RealBitmap` 与 `AImage.Bitmap` 改 `Bitmap?`，为 `Graphics.cs:2800` 的 `g.Bitmap.Save(filepath)` 补处置 | 写文件失败时报 `CodeEE` 还是静默 |
| 3 | 整链可空化，`Graphics.cs:2436` 的 `return …Inputs` 需给可空返回签名 | 可空返回值是否落到 `Graphics` 的调用方 |
| 4 | 二选一：(a) `List<T?>` 全链可空化并在 `ConsoleDisplayLine` 构造器跳过 null；(b) 改用非 null 的哨兵对象 | (a) 会改行为（今日遇哨兵即 NRE），须先定 `ConsoleDisplayLine.buttons` 消费方的语义 |

链 3 的折中（字段可空 + 读取点加 `!`）净增 1 处断言，与验收判据相反，不作备选。

## 曾考虑的替代方案

1. 留在 `nullable-migration-22` 内实施：放弃——该票的前提是「元数据级、零行为变化」，而本票四条链都必须先定行为或设计。
2. 链 1/2/3 就地补判空（NRE → 回退值/异常）：放弃——属可观测行为变更，须单独给判据与验收，不与可空迁移同批。
3. 链 4 保留哨兵、只把 `!` 换成 `Array.Clear` 一类等价写法：不适用——该链的问题是元素类型与哨兵语义，不是单个断言。

## 影响

- 四条链都超出「注解与注释级」：链 1/2/3 的可空化会改变消费方在「值缺席」时的行为（今日是 NRE），链 4 两条路都要动容器元素类型或哨兵设计
- 波及迁移清单之外的文件：`Runtime/Script/Statements/Function/BuiltinMethod/Graphics.cs`、`Runtime/Utils/EvilMask/ConsoleDivPart.cs`、`Instraction.Child.cs`。它们的 CS86xx 本已归零，改动后须重新保证两种声音后端配置下仍为 0
- 链 4 的 (a) 方案会改变 `ConsoleDisplayLine` 构造器对哨兵的处置（NRE → 跳过 null），属可观测行为变更，须单独给判据与验收
- 本票不回改 `nullable-migration-22` §1 已补的理由注释；实施后应按 `bang-sites.md` 的站点表复核对应行的结论列
