# Emuera 架构(大图景)

> 所有路径与类名已对照实际代码核对。

## 仓库布局

- 根目录 `Emuera.sln` = `Emuera/`(解释器本体)+ `EmueraPluginExample/`(插件 API 示例)。注意 `Emuera/` 目录下还有一份旧的 `Emuera.sln`(仅含 Emuera 项目、`Any CPU`、无 NAudio 配置),构建请用根目录的 `Emuera.sln`
- `Emuera/` — C# WinForms 主程序,`net10.0-windows`,x64/x86;**无测试项目**,验证方式是编译 + 用真实 era 游戏(含 `csv/`、`erb/` 目录)启动运行
- `Readme/` — 各版本 readme(`EmueraEE_readme.txt`、`Emuera.EM_readme.txt`)与 changelog
- `docs/agents/` — agent 操作约定(issue tracker / triage 标签 / 领域文档)
- CI:`.github/workflows/dotnet-build.yaml`(tag 触发发布,windows-latest)

## 启动链

启动链:`Program.Main`(CLI 解析、目录设置、配置加载)→ `MainWindow`(WinForms 宿主)→ `EmueraConsole`(终端式显示)→ `Process`(解释器引擎)。全局实例(Process、Console、VariableData 等)集中在 `GlobalStatic.cs`(位于 `Emuera/` 根)。

## 解释器核心(`Runtime/Script/`)

脚本执行管线:**ERB 文件 → LexicalAnalyzer(词法)→ LogicalLineParser(句法)→ LogicalLine → Process.runScriptProc 主循环 → AInstruction 执行**

- `Parser/LexicalAnalyzer.cs` — `Analyse()` 将 `CharStream` 切成 `WordCollection`(token 流)
- `Parser/LogicalLineParser.cs` — 生成 `LogicalLine` 子类:`InstructionLine`(带 `FunctionIdentifier`)、`FunctionLabelLine`(@标签)、`GotoLabelLine`($标签)、`NullLine`、`InvalidLine`
- `Loader/ErbLoader.cs` — 从 erb 目录加载全部 `*.ERB`(先加载 `#` 前缀目录,EE 扩展的加载顺序),随后做全量语法检查(`ParseScript`);`ErhLoader.cs` 读取 CSV 头声明
- `Process.cs`(1 个主文件 + 4 个 partial,`GameProc` 命名空间)— 解释器引擎:
  - `Process.ScriptProc.cs` — `runScriptProc()` 主循环:按行类型分派(SIF 跳过、流控制、普通函数);`saveCurrentState`/`loadPrevState` 实现 BACK 类功能
  - `Process.State.cs` — `ProcessState`:行指针、历史/返回栈
  - `Process.CalledFunction.cs` — CALL / CALLF 等函数调用
  - `Process.SystemProc.cs` — 系统级处理(BEGIN/END、初始化流程)
- `Statements/Instruction.cs` + `Instraction.Child.cs` — **所有命令的实现**:`AInstruction` 抽象基类,`Instraction.Child.cs` 内含 ~110 个 `*_Instruction` 指令类(PRINT、INPUT、WAIT、SAVECHARA、PLAYSOUND…)
- `Statements/FunctionIdentifier.cs` — 命令注册表:静态构造函数调用 `addFunction(FunctionCode, ...)` 注册全部内置命令,flag 常量(PARTIAL、IS_PRINT、FLOW_CONTROL…)决定运行时分派行为
- `Statements/Function/Creator.cs` + `Creator.Method.cs`(约 7600 行)— `FunctionMethodCreator` 静态字典注册**所有式中函数/内建函数**(数值/字符串操作、数学、CSV 查询等),每个函数是一个 `FunctionMethod` 子类
- `Statements/Variable/` — 变量系统:`VariableEvaluator`(变量存储与求值)、`VariableParser`(解析 `A[1]:B[2]` 类变量引用)、`VariableCode`/`VariableTerm`
- `Statements/Expression/` — 表达式:`ExpressionParser` 生成 `AExpression` 树,`Term` 为叶节点,`OperatorMethod.cs`(+`OperatorCode.cs`)实现运算符
- `Statements/ArgumentParser.cs` — 参数解析;`CircularBuffer.cs` 实现 `GETCHARAMARK` 等循环缓冲
- `Data/` — 运行时数据:`ConstantData`(定数)、`GameBase`(角色/CSV 数据)、`LabelDictionary`/`IdentifierDictionary`、`UserDefinedFunction`/`UserDefinedVariable`(用户定义函数与变量)、`StrForm`(FORM 字符串格式化)、`ParserMediator`(静态中介:警告列表、RenameDic)
- 其他:`KeyMacro.cs`(按键宏);`InputRequest.cs` 位于 `Runtime/` 根

## UI 层(`UI/`)

- `UI/Game/EmueraConsole.cs` — 核心显示类:屏幕缓冲、按钮生成、样式、滚动画布;`EmueraConsole.Print.cs` 为输出 partial
- `UI/Game/HtmlManager.cs` — era 的 PRINT HTML 语法解析与渲染
- `UI/Game/Image/` — 资源图像系统:`AppContents`(resources/ 目录装载)、`AImage` 层级(WebP 支持在 `Runtime/Utils/WebPWrapper.cs`)
- `UI/Game/Rikaichan.cs` — 辞典弹窗
- `UI/Framework/Forms/` — 各窗体:`MainWindow`(宿主)、`ConfigDialog`、`DebugConfigDialog`、`DebugDialog`、`RikaiDialog`、`ClipBoardDialog`,以及 `ColorBox`、`EraPictureBox` 控件
- 散置:`Dialog.cs`、`FontFactory.cs`;`UI/Game/` 附带 `HOTKEY.ERB.example.txt`

## 配置与多语言

- `Runtime/Config/ConfigData.cs` 解析 `emuera.config`(键值对),`Config.cs` 为全局配置访问器(`Config.Xxx` 静态属性);`Runtime/Config/JSON/` 为 EE 的 JSON 配置(`JSONConfig`)
- **多语言系统(EM 分支核心)**:`Runtime/Utils/EvilMask/Lang.cs` 中 `Lang.UI.*` 层级访问器类用 `[Translate("…")]` 标注默认日语文本,由 `Properties/lang/emuera-eng.xml`、`emuera-zhs.xml`(嵌入式资源)按语言覆盖。系统消息经 `trerror`/`trmb`/`trsl` 别名引用。**新增或修改 UI 文本时必须走 `Lang.UI` 或 `Lang.Error`/`Lang.MessageBox`/`Lang.SystemLine`,不要硬编码字符串**

## 插件系统

`Runtime/Utils/PluginSystem/` — .dll 插件 API(`PluginManager`、`IPluginMethod`、`BasePluginManifest`),脚本侧通过 `CALLSHARP` 调用插件方法。`EmueraPluginExample/Plugin.cs` 是最小示例。

## 构建杂项

- 声音后端互斥:默认配置用 `Runtime/Utils/Sound.WMP.cs`(WMP,需 VS 的 MSBuild 构建 COM 引用);`Debug-NAudio` / `Release-NAudio` 用 `Sound.NAudio.cs` + `NAudio_LoopStream.cs`(`dotnet build` 可用)。csproj 按配置排除源文件
- 原生库:`Libs/Webp/{x86,x86_64}`(WebP 解码)
- 规模:Emuera 项目约 136 个 `.cs` / 164 个文件;其中 `Runtime/Script/` 56 个(`Statements/` 34 个)、`Runtime/Utils/` 30 个、`UI/` 41 个
