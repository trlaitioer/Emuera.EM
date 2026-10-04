# Emuera 架构(大图景)

> 所有路径与类名已对照实际代码核对。

## 仓库布局

- 根目录 `Emuera.sln` = `Emuera/`(解释器本体)+ `EmueraPluginExample/`(插件 API 示例)。注意 `Emuera/` 目录下还有一份旧的 `Emuera.sln`(仅含 Emuera 项目、`Any CPU`、无 NAudio 配置),构建请用根目录的 `Emuera.sln`
- `Emuera/` — C# WinForms 主程序,`net10.0-windows`,x64/x86;`Emuera.Tests/` 为 xUnit 单元测试工程(不纳入根 `Emuera.sln`,运行方式见其 README);验证方式是测试 + 编译 + 用真实 era 游戏(含 `csv/`、`erb/` 目录)启动运行
- `Readme/` — 各版本 readme(`EmueraEE_readme.txt`、`Emuera.EM_readme.txt`)与 changelog
- `docs/` — 长期参考:[`README.md`](README.md) 为索引(`architecture.md`、`csv-erb-syntax.md`);`docs/tracking/` 为议题跟踪约定(issue tracker / triage 标签)
- CI:`.github/workflows/dotnet-build.yaml`(tag 触发发布,windows-latest)

## 仓库与历史

- Emuera 更新到 `1.824` 后停止更新([License](../Readme/License/Emuera.LICENSE.txt))；私家改造版接手，仅修正 bug，更新到 `v24`([License](../Readme/私家改造版Emuera_readme.txt))
- EM 在私家改造版基础上修改，EE 在 EM 基础上修改，随后两者合并为 EM；迭代过程中项目从 `.Net Framework` 基本迁移到 `.Net`，版本至今仍取自 `Emuera/Emuera.csproj` 的 `InformationalVersion`(`1.824+v24+EMv18+EEv56`)，CI 发布的 tag 必须与它一致
- 本仓库 fork 自 EM(`upstream` remote)：`master` 保持与 `upstream` 同步，个人改动只留在 `develop` 并推送到 `origin`，不推送到 `upstream`
- 仓库若直接 clone 自 `origin`，会缺少 `upstream` remote，按需执行：

```bash
git remote add upstream https://gitlab.com/EvilMask/emuera.em.git
git remote set-url upstream --push no-push
git remote set-branches upstream master
```

## 启动链

启动链:`Program.Main`(CLI 解析、目录设置、配置加载)→ `MainWindow`(WinForms 宿主)→ `EmueraConsole`(终端式显示)→ `Process`(解释器引擎)。全局实例(Process、Console、VariableData 等)集中在 `GlobalStatic.cs`(位于 `Emuera/` 根)。

## 解释器核心(`Runtime/Script/`)

脚本执行管线:**ERB 文件 → LexicalAnalyzer(词法)→ LogicalLineParser(句法)→ LogicalLine → Process.runScriptProc 主循环 → AInstruction 执行**

> 脚本/数据文件的语法规则(行路由、表达式、FORM 字符串、CSV 格式与怪癖)另见 [`csv-erb-syntax.md`](csv-erb-syntax.md)。

- `Parser/LexicalAnalyzer.cs` — `Analyse()` 将 `CharStream` 切成 `WordCollection`(token 流)
- `Parser/LogicalLineParser.cs` — 生成 `LogicalLine` 子类:`InstructionLine`(带 `FunctionIdentifier`)、`FunctionLabelLine`(@标签)、`GotoLabelLine`($标签)、`NullLine`、`InvalidLine`
- `Loader/ErbLoader.cs` — 从 erb 目录加载全部 `*.ERB`(名称中含 `#` 的目录先加载,EE 扩展的加载顺序),随后做全量语法检查(`ParseScript`);`ErhLoader.cs` 读取 CSV 头声明与 `#define` 宏
- `Process.cs`(主文件 + 下列 partial,`GameProc` 命名空间)— 解释器引擎:
  - `Process.ScriptProc.cs` — `runScriptProc()` 主循环:按行类型分派(SIF 跳过、流控制、普通函数);`saveCurrentState`/`loadPrevState` 实现 BACK 类功能
  - `Process.State.cs` — `ProcessState`:行指针、历史/返回栈
  - `Process.CalledFunction.cs` — CALL / CALLF 等函数调用
  - `Process.SystemProc.cs` — 系统级处理(BEGIN/END、初始化流程)
- `Statements/Instruction.cs` + `Instraction.Child.cs` — **所有命令的实现**:`AInstruction` 抽象基类,`Instraction.Child.cs` 内含全部 `*_Instruction` 指令类(PRINT、INPUT、WAIT、SAVECHARA、PLAYSOUND…)
- `Statements/FunctionIdentifier.cs` — 命令注册表:静态构造函数调用 `addFunction(FunctionCode, ...)` 注册全部内置命令,flag 常量(PARTIAL、IS_PRINT、FLOW_CONTROL…)决定运行时分派行为
- `Statements/Function/Creator.cs` + `Statements/Function/BuiltinMethod/` — `FunctionMethodCreator` 静态字典注册**所有式中函数/内建函数**(数值/字符串操作、数学、CSV 查询等),每个函数是一个 `FunctionMethod` 子类。名字不再手写在注册表里,而是以 `[BuiltinMethod("NAME")]` 声明在该类体内的静态工厂方法上(`BuiltinMethodAttribute.cs`);静态构造扫描这些工厂建表(重名以 `OrdinalIgnoreCase` 拒绝),「名字 ↔ 实现」因此同处一处,`BuiltinMethod/` 目录按来源 region 大致分组(`Graphics.cs`/`ArrayOperations.cs`/`GeneralPurpose.cs`/`StringOperations.cs` 各由多个来源 region 合并而成;该目录同时放着图形实参读取器 `BuiltinArgReaders`;某注册名的实现与工厂位置 = `grep -rnF '[BuiltinMethod("NAME")]'` 的命中处)
- `Statements/Variable/` — 变量系统:`VariableEvaluator`(变量存储与求值)、`VariableParser`(解析 `A[1]:B[2]` 类变量引用)、`VariableCode`/`VariableTerm`
- `Statements/Expression/` — 表达式:`ExpressionParser` 生成 `AExpression` 树,`Term` 为叶节点,`OperatorMethod.cs`(+`OperatorCode.cs`)实现运算符
- `Statements/Expression/ArgumentListExtensions.cs` — 提供参数槽位的具名非空读取 `Required(i)`(担保只对该槽被规则表标为必填时成立:无 `OmitStart` 的块、`i < OmitStart`、`ArgType.NotNull`,以及 `SameAsFirst` 块里 `OmitStart == 0` 时被要求必填的 0 号槽;数量省略只让实参表更短、不产生 null,而显式空槽以 null 元素进入函数体,`i >= OmitStart` 的槽须由函数体自行判空)。
- `Statements/Function/FunctionMethod.cs` — **槽位读取分两面**:消费面(函数体)走内嵌的 `ArgumentSlots` 视图(`Int`/`Str`/`IsInteger`/`IsString`/`Var`/`Has`/`IsEmpty`/`IsMissing`/`Count`/`Term`/`TermOrNull`/`Tail`),它只认检查期快照、不跟随检查之后的原地折叠(活体表经 `FoldTarget` 留给折叠与整表转交);构造点统一走 `CheckAndResolveSlots`,一次算清检查消息与逐槽角色 `SlotRole`(规则表族由命中段投影(与检查器共用 `RejectsEmptySlot`/`RuleForSlot`/`ExpectedType` 三个判据),运算符 / StrForm / TOSTR 包装三族由调用点声明)。
- 折叠面(14 个 `UniqueRestructure`)与折叠期 helper 仍用上面那条 `Required(i)` 读旧签名的 `List<AExpression?>`
- 旧签名的取值入口与检查入口的两参形态都是非虚门面(残留 override 报 CS0115),按 `CallerOwnsCheck` 决定是否向检查器求证。
- `Statements/ArgumentParser.cs` + `Statements/ArgumentBuilder.cs` — 指令实参解析;50 个 `ArgumentBuilder` 子类里 28 个带一份声明(逐槽类型位 `argumentTypeArray` + 最少实参数 `minArg` + 数量不限 `argAny`)交由 `checkArgumentType` 校验,另 22 个在 `CreateArgument` 里自建解析、不读该声明(其中若干把 `argumentTypeArray` 置为 `null!`,若干连类型表都不设)。**与式中函数的规则表极性相反**:类型位为 `null!` 才放行显式空槽(该槽同时不做类型检查),`typeof(void)` 与具体类型一律拒绝;指令实参的省略由 builder 在**实参构建期**(默认配置下即首次执行该行时,与内建函数同一个落点)三选一处置(代入默认值 / `CanNotOmitArg` / 缺席即合法)。`CircularBuffer.cs` 实现 `GETCHARAMARK` 等循环缓冲
- `Statements/MemoryLineEntry.cs` — **单行执行入口的共用校验**:调试命令与 `PluginManager.ExecuteLine` 都只跑一行,该类型统一拒绝非指令行、流控制、输入等待、非 `METHOD_SAFE`、`PARTIAL`、`PUTFORM`/`UPCHECK`/`CUPCHECK`/`SAVEDATA`、实参解析失败以及非 `-Debug` 下的 Debug 系指令,并构建实参(此前两处各写一份,`ExecuteLine` 那份缺失)
- `Statements/LineLocationText.cs` — 错误报告与调试输出的行位置文本(`file:line@label`、当前函数、调用栈一项、跳转回溯);内存行(调试命令/插件构造的行)没有父标签、`Position` 又是 `ScriptPosition` 的默认值,故按可用部分降级,无法构成时返回 null 由调用方跳过
- `Data/` — 运行时数据:`ConstantData`(定数)、`GameBase`(角色/CSV 数据)、`LabelDictionary`/`IdentifierDictionary`、`UserDefinedFunction`/`UserDefinedVariable`(用户定义函数与变量)、`DefineMacro`(`#define` 宏)、`StrForm`(FORM 字符串格式化)、`ParserMediator`(静态中介:警告列表、RenameDic)
- `Runtime/Utils/PathUtils/` — 脚本路径的类型化边界:`PurePath`(纯词法路径,折叠 `.`/`..` 而不触碰文件系统)、`GamePath`(只能由工厂产出,实例保证位于游戏目录内)、`NamePattern`/`PathReject`;脚本传入的路径字符串必须经 `GamePath` 才能到达文件系统调用
- 其他:`KeyMacro.cs`(按键宏);`InputRequest.cs` 位于 `Runtime/` 根;`AssemblyData.cs`(`Runtime/Utils/`,exe 路径与版本文本)

## UI 层(`UI/`)

- `UI/Game/EmueraConsole.cs` — 核心显示类:屏幕缓冲、按钮生成、样式、滚动画布;`EmueraConsole.Print.cs` 为输出 partial
- `UI/Game/HtmlManager.cs` — era 的 PRINT HTML 语法解析与渲染
- `UI/Game/GraphicsTextRenderer.cs` — `TextDrawingMode.GRAPHICS` 的文本量宽与绘制(以 GDI advance 为排布基准,与 TEXTRENDERER 模式一致)
- `UI/Game/Image/` — 资源图像系统:`AppContents`(resources/ 目录装载)、`AImage` 层级(WebP 支持在 `Runtime/Utils/WebPWrapper.cs`)
- `UI/Game/Rikaichan.cs` — 辞典弹窗
- `UI/Framework/Forms/` — 各窗体:`MainWindow`(宿主)、`ConfigDialog`、`DebugConfigDialog`、`DebugDialog`、`RikaiDialog`、`ClipBoardDialog`,以及 `ColorBox`、`EraPictureBox` 控件
- `UI/` 根:`Dialog.cs`、`FontFactory.cs`、`FontMetrics.cs`(按全宽字步进反查渲染字号,保住 em 基准)、`WindowZoom.cs`(运行时窗口缩放的几何计算)
- `UI/Game/` 附带 `HOTKEY.ERB.example.txt`

## 配置与多语言

- `Runtime/Config/ConfigData.cs` 解析 `emuera.config`(键值对),`Config.cs` 为全局配置访问器(`Config.Xxx` 静态属性);`Runtime/Config/JSON/` 为 EE 的 JSON 配置(`JSONConfig`)
- **多语言系统(EM 分支核心)**:`Runtime/Utils/EvilMask/Lang.cs` 中 `Lang.UI.*` 层级访问器类用 `[Translate("…")]` 标注默认日语文本,由 `Properties/lang/emuera-eng.xml`、`emuera-zhs.xml`(嵌入式资源)按语言覆盖。系统消息经 `trerror`/`trmb`/`trsl` 别名引用。**新增或修改 UI 文本时必须走 `Lang.UI` 或 `Lang.Error`/`Lang.MessageBox`/`Lang.SystemLine`,不要硬编码字符串**

## 插件系统

`Runtime/Utils/PluginSystem/` — .dll 插件 API(`PluginManager`、`IPluginMethod`、`BasePluginManifest`),脚本侧通过 `CALLSHARP` 调用插件方法。`EmueraPluginExample/Plugin.cs` 是最小示例。

`PluginManager.ExecuteLine` 是**单行执行入口**:只接受 `METHOD_SAFE`、非流控、非输入等待、非 `PARTIAL`、非 `PUTFORM`/`UPCHECK`/`CUPCHECK`/`SAVEDATA`、实参可解析的单行指令(Debug 系指令仅 `-Debug` 下可用),其余抛 `CodeEE`;规则与调试命令共用(`Runtime/Script/Statements/MemoryLineEntry.cs`)。`ExecuteLine` 的分派走 `Process.DoDebugNormalFunction`,故 `PRINTPLAIN`/`PRINTBUTTON` 这类由 `ArgumentBuilder` 注册、没有 `AInstruction` 的命令同样可用。

## 构建杂项

- 声音后端互斥:默认配置用 `Runtime/Utils/Sound.WMP.cs`(WMP,需 VS 的 MSBuild 构建 COM 引用);`Debug-NAudio` / `Release-NAudio` 用 `Sound.NAudio.cs` + `NAudio_LoopStream.cs`(`dotnet build` 可用)。csproj 按配置排除源文件
- 原生库:`Libs/Webp/{x86,x86_64}`(WebP 解码)
