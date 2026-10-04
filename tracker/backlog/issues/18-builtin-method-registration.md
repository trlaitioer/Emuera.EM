# 266 个内建函数的名字只存在于注册字典的键上，与实现分居两个文件且无一致性检查

id: backlog-18
Status: implemented

> 注（2026-10 补记）：本票正文的行号与文件位置是**改动前的单文件口径**——阶段 A 之后 `Creator.Method.cs` 已删除
> （实现分居 `BuiltinMethod/` 目录的 11 个文件），阶段 C 之后 `Creator.cs` 只剩建表逻辑，故正文里的
> `Creator.Method.cs:NNNN`、`Creator.cs:NNNN` 行号均已失效；定位按类名 + `grep -n '[BuiltinMethod("NAME")]'`。

## 任务

发现于 2026-09-29 的内建函数注册机制讨论（只读核查）。

`FunctionMethodCreator` 在静态构造里手写一张 `Dictionary<string, FunctionMethod>(Config.StrComper)`（`Emuera/Runtime/Script/Statements/Function/Creator.cs` 的 `FunctionMethodCreator` 静态构造）：**实测 266 条活注册**。口径为行首 `["NAME"] = `；宽松 grep 得 270，差额 4 条是注释掉的注册——`CHKVARDATA`/`CHKGLOBALDATA`/`FIND_VARDATA`（`Creator.cs` 里被注释掉的那三条，其类仍是活的，只是这个枚举值未注册）与 `GROTATE`（`Creator.cs` 里被注释掉的那条；其类 `GraphicsRotateMethod` 本身也被注释）。266 条注册的实现集中在 `Creator.Method.cs`（10215 行；活的具体子类 183 个）：`: FunctionMethod` 文本命中 186 处，减去 `GetRefMethod`（`:3940`）、`GraphicsRotateMethod`（`:7614`）、`GraphicsGetBrushMethod`（`:7731`，`EE_失敗作` region，整段包在 `/** **/` 里）三处注释定义，**活的具体子类 183 个，全部 183 个被实例化**（阶段 C 的 Gate B 实测：汇编里该命名空间恰好 183 个具体 `FunctionMethod` 子类，差集为空）。

由此产生的三个现象（均已核实）：

1. **名字与实现分居两个文件**：类不知道自己叫什么，靠静态构造末尾回填（`Creator.cs` 静态构造末尾的 `SetMethodName` 回填循环；`FunctionMethod.Name` 为 `protected` 只读属性）。266 个名字里只有 35 个以字符串字面量出现在 `Creator.Method.cs`，其余只能靠注册表定位——即按名字 `grep` 不到实现。
2. **`Name` 是行为的一部分，不只是诊断**：`Creator.Method.cs` 中 **79 个类**在代码里引用 `Name`（含注释文本的口径是 83：除 `CBGClearMethod`/`CBGClearButtonMethod`/`CBGRemoveBMapMethod` 三个只在 `//` 注释里出现的类，还多出整段包在 `/** **/` 里、不进汇编的 `GraphicsGetBrushMethod`），其中 **7 处是 `switch (Name)` 行为分派**（`GraphicsStateMethod`/`GraphicsStateStrMethod`/`SpriteStateMethod`/`SpriteSetPosMethod`/`ClientSizeMethod`/`GetKeyStateMethod`/`MousePosMethod`，共 22 个 `case` 标签，逐类与各自注册名相等）。即**同一类注册为多个名字、每名字一个独立实例，实例靠运行期的 `Name` 分派**；`StrForm.cs` 与 `OperatorMethod.cs` 两族不碰 `Name`。⇒ 名字进错误文案、进行为分派，是**每实例状态**。
3. **重名会静默覆盖**：`["K"] = v` 是**索引器赋值**而非 `Add`（`{ {"K", v} }` 才抛异常），同名时后者覆盖前者且无任何提示。今天**没有**重名（已机械核对，含大小写变体），所以这是潜在隐患而非现行缺陷——一旦发生，被覆盖的那个名字会成为死注册。

完成标准：

- **名字与实现同处一处声明**：`grep` 某个名字能在实现处命中
- **「恰好注册一次」可机械核对**。原表述「装配中非抽象子类集 == 注册表键集」按字面不可实现（184 个类 ≠ 266 个名字，同名一类多实例是真实现状），改写为三条断言：
  - **Gate A（恰好一次）**：扫描得到的工厂数 == `GetMethodList().Count` == 名字去重数；且 266 个 value **两两引用不等**（一名一实例是 `Name` 语义的前提）。另以 `Static|Instance` 全量扫一遍程序集，断言**带 `[BuiltinMethod]` 的方法全是 `static`、无参、返回具体 `FunctionMethod` 子类**——注册层的扫描只取 `static` 方法，被漏写 `static` 的工厂在那里查不到，只能靠这条闸门点名（复核修订，见「复核补记」）
  - **Gate B（无死类/无死注册）**：命名空间 `MinorShift.Emuera.GameData.Function` 下的具体 `FunctionMethod` 子类集合，减去「被 ≥1 个注册实例实例化的类」的差集**必须为空**；反向：每个注册实例的类型必须落在该集合内；另断言子类数 ≥ 183 防空跑。原写「差集**只允许** `GraphicsGetBrushMethod`（显式白名单）」，实施时实测那个类连同它的 `/** **/` 整段在 `Graphics.cs` 里是注释、并不进汇编，白名单里放一个永不出现的名字只会掩盖真正的死类，故改为空差集断言（实施结论）
  - **Gate C（名字不腐烂）**：`GetMethodList().Keys` 与入库的黄金名单快照（266 行、序数排序）**键集相等**，任何改名/删名/加名都必须显式改快照。**注册表自身的枚举序不是契约**：它是反射扫描序，唯一可观测点是 `OverWriteSystemFuncWarn` 的打印序，而该顺序无意义（复核修订：原写「按 Ordinal 精确等于」，实施时误加了排序与顺序断言，已撤销）
  - **Gate D（按名字分派的类不落空）**：7 个按运行期 `Name` 分派行为的类（`GraphicsStateMethod`/`GraphicsStateStrMethod`/`SpriteStateMethod`/`SpriteSetPosMethod`/`ClientSizeMethod`/`GetKeyStateMethod`/`MousePosMethod`），其 `switch (Name)` 的 `case` 标签集合必须等于该类的注册名集合——注册名加/改而 `switch` 没跟上时，该名字会落到 `switch` 之后的 `throw new ExeEE(…AbnormalBranch…)`，是运行到该函数才炸的路径，Gate A/B/C 都看不到。`case` 标签不可反射，故这条闸门读源码比对（`BuiltinMethod/*.cs` 随测试产物拷出，见 csproj 的 `Content`）。现状 7 类 / 22 个 `case` 逐类相等，做过一次改名变异验证（改一个 `case` 名 → 变红）。**过渡件**：`Name` 退役（`backlog-19`）后应改为「枚举/类型 ↔ 注册名」的等价断言，csproj 里拷源码的 `Content` 一并删除
- **逐字不变的不变量**：`GetMethodList()` 的形状（`Dictionary<string, FunctionMethod>`，`Config.StrComper` 比较器，23 处调用点含测试 = 生产 3 + 测试 20，其中 4 处是本次新增的闸门用例——口径是**代码调用点**，`FunctionMethodTests.cs:10` 的文档注释提及不计；Gate D 落地后由 22 变 23）、`SetMethodName` 的回填语义、`Name` 在 7 处 `switch` 下的取值、注册表仍在 `FunctionMethodCreator` 静态构造内建完（首次查找前）。旧脚本可观测行为不变
- 装载报告（`ErbLoader` 的 `OverWriteSystemFuncWarn` 列表，经 `GetOverloadedList` 迭代 `methodDic`）的打印序 = 扫描序，**不是契约**（先前已定该顺序无意义），不作为验收项

## 方案

**机制**：名字以 attribute 声明在**静态工厂方法**上（注册单元是实例而非类型，同名一类多实例且构造实参不同），注册表不再手写，由静态构造扫描本程序集建表。

新增 `Emuera/Runtime/Script/Statements/Function/BuiltinMethod/BuiltinMethodAttribute.cs`：`[AttributeUsage(AttributeTargets.Method, Inherited = false)]`（`AllowMultiple` 用默认的 `false`——"一个工厂 = 一个注册单元"要作为结构保证），主构造器取名字，`Name` 只读。**命名空间保留 `MinorShift.Emuera.GameData.Function`**：目录与命名空间不一致在本仓库是常态，不为此改命名空间、不单写注释。

**分界**（已定）：attribute 类内只放**声明形状 + 发现**（`GetDefinitions()`：`typeof(BuiltinMethodAttribute).Assembly` + `Public|NonPublic|Static|DeclaredOnly`，返回延迟查询，调用方只枚举一次）；**校验、去重、建表、回填**全在注册层：

- 校验：无参、返回类型是具体 `FunctionMethod` 子类；不合法抛带 `DeclaringType.Name.MethodName` 的消息。**`static` 一条不在注册层**：扫描本身只取 `static` 方法，故注册层看不到非 static 的候选（写在那里就是死判据），改由 Gate A 的 `Static|Instance` 全量扫描覆盖并用消息点名（复核修订）
- 去重：`methodList.Add(name, …)`（不是索引器）+ 无条件 `StringComparer.OrdinalIgnoreCase` 唯一性。注意 `Config.StrComper` 随 `IgnoreCase` 配置在 `OrdinalIgnoreCase`/`Ordinal` 间切换（`Emuera/Runtime/Config/Config.cs` 的 `SetConfig`），故这条比字典比较器在 `Ordinal` 模式下更严——这是**有意的收紧**：今天无大小写变体名，两口径等价；一旦出现，`IgnoreCase=YES` 时必然静默覆盖，正是本票要消灭的形态
- **不排序**：建表按扫描序（反射元数据序）。原写「按名字序数排序后插入 → 注册表顺序确定、与反射元数据序/文件布局解耦」，复核时认定为**误加**：该确定性唯一影响 `OverWriteSystemFuncWarn` 的打印序，而该顺序先前已定无意义，不值得为它引入一条与名字绑定的隐式顺序（复核修订：`.OrderBy` 已从静态构造移除，Gate C 亦不比对顺序）
- 构造：`factory.Invoke(null, null)`，`TargetInvocationException` 用 `ExceptionDispatchInfo` 拆包重抛
- 回填：`SetMethodName(pair.Key)` 循环原样保留

**工厂形态**：`[BuiltinMethod("NAME")]` 独立一行（方法 attribute 在本仓库一律独立一行），紧跟块体工厂 `private static X CreateNAME() { return new X(); }`，写在类体首位（实测 266/266 都是块体、无表达式体，见「阶段 C 收尾补记」）。**第一版不把名字传进构造函数**，与今天逐字等价；`Name` 只读化留作后续单独清理。

**分三阶段，各自可证**：

**阶段 A —— 把 184 个类搬出嵌套、单开目录（行为零变化）**

搬到 `Function/BuiltinMethod/`，按来源 region 分十个文件：

| 新文件 | 来源 region | 类数 | region 行数（拆分前） |
| --- | --- | --- | --- |
| `PrivateAdditions.cs` | `EM_私家版_追加関数` | 36 | 2971 |
| `CsvData.cs` | `CSVデータ関係` | 7 | 408 |
| `GeneralPurpose.cs` | `汎用処理系` | 17 | 559 |
| `Constants.cs` | `定数取得` | 8 | 172 |
| `MathMethods.cs` | `数学関数` | 10 | 393 |
| `ArrayOperations.cs` | `変数操作系` | 15 | 1240 |
| `StringOperations.cs` | `文字列操作系` | 22 | 1022 |
| `Html.cs` | `html系` | 4 | 103 |
| `Graphics.cs` | `画像処理系` | 47 | 2719 |
| `EeAndPatches.cs` | `EE_EXISTSOUND` 起至文件末（EE 系 + daughter patch + Bitmap Cache/Hotkey/Outputlog） | 18 | 600 |

「行数」列的口径是**拆分前该来源 region 的行数**（票面这一列合计 10187；个别格与按区间实测有 ±1 出入，如 `EeAndPatches`），**不是新文件的行数**——阶段 A 提交时这 10 个新文件总行 10117，现已随阶段 B/C 增长（当前 `Graphics.cs` 2910、`PrivateAdditions.cs` 3444、`ArrayOperations.cs` 1383）。

- 可访问性翻译：`private/public sealed class` → 顶层 `internal sealed class`（原 `public` 嵌在 `internal` 类里，有效可访问性本来就是 internal）
- `FunctionMethodCreator` 自己的 **10** 个成员重新落位：6 个图形实参 helper（`ReadGraphics(string,…)`/`ReadGraphics(int)`/`ReadColor`/`ReadPoint`/`ReadRectangle`/`ReadColormatrix`）→ 新增 `BuiltinArgReaders`（`internal static class`；文件外唯一调用点 `Emuera/UI/Game/EmueraConsole.cs:2510,2545` 跟着改）；`keytoggle`（`:9084` 的 `private static readonly short[]`）→ 移进 `GetKeyStateMethod` 私有静态字段；`GetSaveDataPathText`/`GetSaveDataPathGraphics` → 与各自使用者同文件的 `file static class`；`StrFormType`（`:6148-6154` 的 `private enum`）→ 顶层 `internal enum` 落进 `StringOperations.cs`（采用者为 `StrChangeStyleMethod`；阶段 A 时另有 `Creator.cs` 的四条注册行按非限定名解析，阶段 C 后这些行已换成 attribute 工厂）
- **文件归宿**：阶段 A 之后 `Creator.Method.cs` 只剩 `FunctionMethodCreator` 的 partial 外壳（184 类与 10 个成员全部迁出）；收尾时该文件已删除、`Creator.cs` 里的 `FunctionMethodCreator` 去掉 `partial`，`Creator.cs` 顶部的合并配方随之改为「保持该文件处于删除状态」
- **证明方式**：namespace 不变 ⇒ `Creator.cs` 的 266 行字面量**一个字不改**就继续编译（非限定名照旧解析）；再加上编译通过 + 全量测试绿 + 键集/实例数与今天一字不差。这是"纯搬家"的判据
- 前置已核：184 个类名在文件外无同名类型；文件外没有生产代码按类型名引用它们（唯一真实引用是上面那 2 处 `ReadGraphics`）
- 同步 `tracker/arg-contract/` 的三个扫描脚本（`arg-slots.py`/`builtin-contract.py`/`slots-worklist.py`：`SRC` 改 `BuiltinMethod/*.cs` 通配，`_scan.py` 的 `CLASS_RE` 改顶层声明）——它们自带的 266 名 / 114 块 / 183 子类自校验正好当第二重无损证明

**阶段 A 实施补记**（方案落地时的三处必要细化，均属"纯搬家"范围内）：

- 6 个图形实参 helper 与 `GetSaveDataPath*` 搬迁后**调用点原样不动**：C# 的非限定方法调用不搜索命名空间，只认外层类型与 `using static`，故 `Graphics.cs` 另加两条 `using static`（`BuiltinArgReaders` / `SaveDataPathBuilder`）。`SaveDataPathBuilder` 是与使用者同文件的 `file static class`
- `Creator.Method.cs` 阶段 A 后只留**空 partial 外壳**，收尾时已删除（`Creator.cs` 的 `FunctionMethodCreator` 随之去掉 `partial`，见「文件归宿」）
- 扫描脚本的两处细化：`_scan.CLASS_RE`/`METHOD_SIG_RE*` 改为**缩进不敏感**（`ArgumentBuilder.cs` 一族仍是嵌套布局，`instruction-args.py` 因此不受影响）；扫描源 `_scan.BUILTIN_METHOD_SRC` 按拆分前的区域顺序列出 11 个文件（含 `BuiltinArgReaders.cs`：G 段要把它当文件作用域 helper 扫）
- **无损证明**（复核后精化）：184 个类体中 183 个与搬迁前**语句逐字节相同**（整文件减一级 Tab 缩进），余下 1 个（`GetKeyStateMethod`）只多吸收了 `keytoggle` 字段；另 `StrFormType` 上移时去掉了枚举声明后多余的一个 `;`。逐字节层面另有四处差异（均非语义）：184 个类声明的可访问性由 `private/public` 统一为 `internal`、9 个搬迁成员同样由 `private/public` 改 `internal`、`ReadColormatrix` 少一对冗余括号（`(slots.Var(argNo)).Get…`）、注册表插入序（见下条「不排序」）。`docs/builtin-functions.md` 与 `slots-progress.md` 在新布局上复跑**逐字节复现**；`slots-worklist.md` 用同一份（改后的）脚本分别扫"搬迁前单文件 / 搬迁后 11 文件"，产物逐字节相同
- 已知遗留：入库的 `slots-worklist.md` 在阶段 B（`arguments` → `slots`）之后就已经落后于代码，与本次搬迁无关，故未随本次再生
- **警告数 86 → 96（用户复核后补记）**：非增量重编译下 `Emuera.csproj` 的警告由 86 增至 **96**，增量全在同一形态上——CS8981（小写 `using` 别名，如 `using trerror = …`）：拆出的 11 个文件各自重声明别名（`Creator.Method.cs` 原有 1 处）⇒ +10。其余码型（CA1069 13、CA1806 2、CA1816 1、CA2019 1、CS4014 1、项目级 WFO0003 1）与 `Emuera.Tests` 的 7 条前后一致、无新增，编译 0 错误。属 `performance-optimization-06`（`wontfix`）那一类；要压掉可用 csproj `NoWarn` 或收成一处全局别名

**阶段 C 实施补记**：`Creator.cs` 现在只剩「扫表 → `seen` 按 `OrdinalIgnoreCase` 去重 → `Add` → 回填 `SetMethodName`」的静态构造 + `GetMethodList()`；非法工厂抛带 `DeclaringType.Name.MethodName` 的 `ExeEE`（`static` 一条由 Gate A 覆盖，见「复核补记」），工厂内部异常用 `ExceptionDispatchInfo` 拆包重抛。闸门落在 `Emuera.Tests/Runtime/Script/Statements/Function/BuiltinMethodRegistryTests.cs`（Gate A/B/C/D），A/B/C 三条各做过一次变异验证（改名、加未注册类 → 对应闸门变红），Gate D 的变异验证见「完成标准」的 Gate D 条。黄金名单快照为 `Emuera.Tests/Runtime/Script/Statements/Function/BuiltinMethodNames.snapshot.txt`（266 行、序数排序，csproj 里以 `Content` 拷进测试产物）。

阶段 A 的脚本同步不够用：`builtin-contract.py` 的注册名解析原本读 `Creator.cs` 的**字面量表**，阶段 C 之后那里已无字面量可读（实测自校验报 7 项、注册名 0），故它一并改为解析 `[BuiltinMethod("NAME")]` 工厂（名字、实现类、构造实参都取自同一行工厂体）——`docs/builtin-functions.md` 因此由**与运行时同一处声明**推出，复跑后除两行路径文案外逐字节复现。

**阶段 C 收尾补记**：`builtin-contract.py` 的工厂正则当时只认表达式体单行工厂（`() => new(...);`），而 266 个工厂实际都是块体（`{ return new X(...); }`），实测匹配 **0/266**——该状态下复跑会写出空表（`write_doc` 无条件执行）。已改为「attribute 与工厂签名相邻」+ 括号配对取构造实参（两种体形都收），并在 `main()` 加 attribute 处数交叉校验，漏匹配不再只剩 `EXPECTED_NAMES` 一个总数兜底。修好后复跑 `docs/builtin-functions.md` 与改动前**逐字节相同**，自校验打印 266 名 / 183 类 / 114 规则段、可省略 96 / 拒绝显式空槽 54。

**文档同步补记**：长期文档已同步——`docs/architecture.md`、`docs/csv-erb-syntax.md`、`docs/builtin-functions.md`（生成器 LEGEND）、`AGENTS.md` 第 6 条的合并配方。`tracker/` 下未结票与冻结工件（`slots-consumption.md`、`slots-worklist.md` 两处路径文案）里指向 `Creator.Method.cs` 的死引用改指 `BuiltinMethod/` 目录（按类定位，旧行号不照搬；生成器仍用 `BuiltinMethod/*.cs` 通配，而通配不是合格的「代码位置」口径，见「复核补记」）；已结票按「过程靠 git」不动。

**复核补记（代码复核后修订）**：

- 「排序」按**误加**处理并从静态构造移除：Gate C 只钉键集，注册表枚举序不作契约（理由见「完成标准」末条）
- 注册层不再写 `!factory.IsStatic`：扫描只取 `static` 方法，该判据在扫描结果上不可达；改由 Gate A 的 `Static|Instance` 全量扫描断言「带 `[BuiltinMethod]` 的方法全是合法 `static` 工厂」并用 `类型名.方法名` 点名（闸门所述「非法工厂抛 `DeclaringType.Name.MethodName` 消息」对非 static 这一形态由闸门承担，其余形态仍在注册层）
- `CreateMethod` 里被 `IsAbstract` 覆盖的 `returnType.IsInterface` 一并去掉
- `docs/` 与生成器 LEGEND 里作为**代码位置**的 `BuiltinMethod/*.cs` 通配改为 `BuiltinMethod/` 目录 + `grep -n '[BuiltinMethod("NAME")]'` 定位口径（`AGENTS.md` 代码/文档 4 要求给出可定位的路径，通配符两者都不是）
- `docs/architecture.md` 的「各含多个 region」改为「各由多个来源 region 合并而成」（文件内 `#region` 实测 `Graphics.cs` 17、`ArrayOperations.cs` 4、`GeneralPurpose.cs`/`StringOperations.cs` 各 1）
- 无损证明措辞按机械核验结果精化（见「阶段 A 实施补记」的**无损证明**条）

**阶段 B —— 名字内联（行为零变化）**

加 266 个 attribute 工厂（`.temp/` 一次性脚本按现有注册机械生成再人工过一遍；`utf-8-sig` 读、Tab 与 BOM 交给 `restore_bom.py` 统一收尾），同时产出黄金名单快照（来源是**改动前的注册表**，与 `upstream/master` 的注册名集合完全一致，可作 provenance）。此时旧字面量注册表仍在，是独立 oracle ⇒ 新闸门「属性名字集 == 注册表键集（含返回类型一致、实例两两互异）」跑绿即证明 266 处搬迁等价。

**阶段 C —— 替换注册层**

删掉 `Creator.cs` 里 266 行字面量，换成"扫表 + 校验 + `Add` + 回填（排序已按「复核补记」作为误加移除）"（约 +60/−270 行）。用例从"属性集 vs 字典键集"（此时变自证）切换为 Gate A/B/C。

**顺序理由**：阶段 A 放最前，attribute 一次就写进最终位置、不随搬迁二次移动；10k 行搬家不夹带语义改动，review 时"内容并集不变"最好证。反过来会让 266 行 attribute 白搬一次。

**一个已知边界**：静态构造里逃出去的任何异常都会被 CLR 包成 `TypeInitializationException`（`FunctionMethodCreator` 一层，再经 `IdentifierDictionary.cs:156` 一层），而 `Process.cs` 的报错路径只打印 `type + Message + StackTrace`、不打印 `InnerException`（`:611`、`:696`）⇒ **启动期失败的可读性不能靠异常消息**，靠闸门用例（编译/测试期暴露）。若将来要在游戏内看到可读原因，正解是把建表从静态构造改成显式初始化调用（异常不再被包装、`handleException` 的 `ExeEE`/`CodeEE` 分支可用）——那会改动本票"静态构造内建完"的不变量，记为可选后续，本票不动。

## 曾考虑的替代方案

1. **方法名即内建名**（`static GraphicsStateMethod GCREATED()`，连 attribute 都省掉）：放弃——技术上可行（266 个名字全是合法 C# 标识符、无关键字冲突，已核），但 266 个全大写方法名与本仓库 C# 约定冲突，且 attribute 位置是唯一能承载后续注册元数据的地方
2. **类级 attribute**（`[BuiltinFunction("MIN","MAX")]`）：放弃——注册单元是实例，类级 attribute 表达不了「两个实例、两套构造参数」
3. **只拆文件、不动注册机制**：放弃——文件拆分只改善导航，名字与实现的分居、静默覆盖、无全域闸门三条都不动；它降级为本方案的阶段 A
4. **把入参规则也写进 attribute**：放弃——`ArgTypeList` 是构造函数里组合出的对象图（`OmitStart` + `ArgType` 标志位），attribute 只能带常量/`typeof`/数组；且多条规则的顺序无法靠重复 attribute 可靠表达。本票只做「命名 + 注册」
5. **`AllowMultiple = true`（一个工厂挂多个名字，注册层每 attribute 调一次）**：放弃——实测能合并的只有 6 组、21 个名字（构造实参完全相同的组），工厂数 266→251，**省 15 行**；代价是把「一名一实例」从结构保证降级为实现约定（工厂若返回缓存实例、或未来消费方只调一次，`Name` 分派即静默错乱），且闸门只能在运行/测试期抓到
6. **单例化 / 多名字共享实例**：放弃——实例数由「名字 → 行为+实参」的组合数决定，已是 266 的最小值。实测：149/184 个类无自身实例状态，但其中「不引用 `Name`」的 84 个类**每个只挂一个名字**（本来就是一名一实例）；35 个持有 `private readonly` 字段的类多名字注册时构造实参都不同；唯一同构造表达式的 6 组（21 名）**恰好就是 6 个 `switch (Name)` 类**，共享即行为错乱。理论收益上限 15 个实例 ≈ 1.3 KB（注册表对象图量级几十 KB），拿它要改基类虚方法签名带上名字、牵动 184 个类与所有调用点
7. **本轮拆掉 7 个 `switch (Name)` 类**（按名字拆成 22 个类，或改成 ctor 携带 `Operation` 枚举）：放弃——实例数/内存/启动/调用性能收益全为 0（每实例 ~90 B；字符串 `switch` 每次调用 ~10 ns 量级），唯一实质收益是消掉 22 处"字符串即行为"耦合；而这一条有更便宜的替代（沿用文件里 `Xml*`/`Map*`/`DT*` 既有的 `Operation` 范式 ≈ +30 行，或一条断言「7 个类的 `case` 标签集合 == 该类的注册名集合」的闸门用例 ≈ 30 行）；且这 7 个函数今天**没有任何行为用例**（`CheckArgumentTypeEx` 只管实参契约），按名字拆分等于一次无回归网的行为重写。归入后续「`Name` 退役」那一笔（`Operation` 方案是它的前置）；其中**后者**（「7 个类的 `case` 标签集合 == 该类的注册名集合」的闸门）已提前落地，见「完成标准」的 Gate D 条——按名字拆分 / 改 `Operation` 枚举的改造仍归后续
8. **把发现逻辑搬进注册层**（attribute 只留 `Name`）：放弃——"声明长什么样、怎么找到它"属 attribute，"这个候选能不能用"才是注册层的规则；按此分界 `GetDefinitions()` 不依赖 `FunctionMethod`，规则也没有分居两处
9. **建表时机改为显式初始化调用**：本次不做（见「方案」末的已知边界），记为可选后续

## 影响

- **与 backlog/issues/06 是同一病的两张表**：06 是指令表（`FunctionIdentifier` 的两条 `addFunction` 路径 + `Process.ScriptProc.cs` 的两个 switch），本票是内建函数表（`methodDic`）。两者都缺「一处声明」，但 seam 不同，宜各自独立评估
- **与 `arg-contract` effort 正交**：该 effort 明确不改 `argumentTypeArrayEx` 的声明式方向与 184 个子类的扁平结构（`tracker/arg-contract/spec.md` 的「影响」节末条）；本票只动注册与命名，不动规则表。若将来上源生成器，`[BuiltinMethod]` 可与该 effort 的「角色化访问器」合并为同一笔投资
- **名字复写全景（35 个名字、41 处命中）**：`Creator.Method.cs` 内 38 处 = 22 处 `case "NAME"` 行为分派 + 16 处普通字面量（`funcName = "MAXARRAY"`/`"MAXCARRAY"`/`"GETCONFIG(S)"`、`funcName = isLast ? "FINDLASTELEMENT" : "FINDELEMENT"`、`p.IsArrayRangeValid(…, "SUMARRAY"/"MATCH"/"INRANGEARRAY"/"STRJOIN", …)`、`ParserMediator.Warn(…, "MESSKIP")`、两条 `CodeEE(…, "GETCONFIG(S)")`）；文件外 3 处 = `Process.CalledFunction.cs:252` 的 `GetMethodList()["TOSTR"]`（本票口径内的按名访问）+ `VariableData.cs:343` 的变量 token `"RAND"`、`EmueraConsole.cs:1967` 的指令名 `"OUTPUTLOG"`（同名不同物）。本票只把「注册名」收敛为一处声明；22 处 `case` 的一致性已由闸门覆盖（`BuiltinMethodRegistryTests` 的 Gate D：`case` 标签集合 == 该类注册名集合；今天 7/7 类相等，做过一次改名变异验证；闸门本体、边界与退役安排见「完成标准」的 Gate D 条），16 处 `funcName`/文案属 `Name` 退役那一笔
- **与上游的关系（合并配方）**：实测本仓库的 266 个注册名与 `upstream/master` **完全相同**（双向差集为空），186 个类声明也相同；上游确实会新增内建函数（历史上 `Creator.cs` 9 次提交、`Creator.Method.cs` 36 次提交，新增一条 = `Creator.cs` 一行 + 一个类）。阶段 C 之后这类改动会与本仓库重写过的 `Creator.cs` 冲突，固定解法写进 `Creator.cs` 顶部注释：**不恢复字面量表，把名字搬成对应类体内的 attribute 工厂（2 行）**
- **Gate B 的口径依赖命名空间**：`OperatorMethod`（`…Statements.Expression`）与 `StrForm` 的 `FormattedStringMethod` 族（`…Script.Data`）是注册在别处、不碰 `Name` 的族，按命名空间 `MinorShift.Emuera.GameData.Function` 圈定即天然排除
- **顺带发现（归属 `arg-contract`，已回填）**：`tracker/arg-contract/spec.md` 的「问题/背景」节原写「184 个**活的**子类 / 270 个注册名」——前半句口径正确（188 个 `: FunctionMethod` 命中减去 2 处注释掉的类声明与 2 个抽象基类），后半句是宽松口径（含 4 条注释注册），活注册名实为 266；spec.md 已按 266 改写。该偏差不影响 `slots-worklist.py` 的计数：它按**类**计数，而 4 条注释注册里 3 条的类（`CheckdataStrMethod`/`CheckdataMethod`/`FindFilesMethod`）另有活注册名（`CHKCHARADATA`/`CHKDATA`/`FIND_CHARADATA`），余下 1 条的类（`GraphicsRotateMethod`）整段被注释、不进解析
- **本票留下的后续（已各自落票）**：「`Name` 退役」→ `backlog-19`；「建表时机改为显式初始化调用」→ `backlog-20`
- **不在本票范围**：`PluginManager` 的 `methods` 表（`PluginManager.GetMethod`，仅由 `CALLSHARP` 经 `Instraction.Child.cs` 调用）与内建、UDF 均无交集，无优先级问题；UDF（`@FOO`）是另一套机制
- **改动量**：中（阶段 A 是最大 diff：184 类搬家 + 10 个成员落位 + 3 个脚本同步；阶段 B 266 处纯新增；阶段 C 一处替换）。风险集中在 `Name` 语义与建表时机，不在反射本身
- **文档同步**：`docs/architecture.md`、`docs/csv-erb-syntax.md` 里指向 `Creator.Method.cs` 的注册表定位改指 `BuiltinMethod/` 目录下的 attribute 工厂；`docs/builtin-functions.md` 的「代码位置」行由生成器（`builtin-contract.py` 的 LEGEND）改指同一目录 + `grep -n '[BuiltinMethod("NAME")]'` 定位口径
