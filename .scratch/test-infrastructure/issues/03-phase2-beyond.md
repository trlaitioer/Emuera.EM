# Phase 2+:StrForm 与后续

Status: 已完成(2026-08-29)
Type: task

## 目标

- `StrForm` 测试:覆盖已迁移文件的 `null!` 语义,为"把 `!` 替换为真检查"铺路(联动模块清单见 `../nullable-migration/issues/01-core-runtime.md`)
- `ExpressionParser`、`VariableParser` 测试
- `LogicalLineParser` 测试(原判断"需先解决 `EmueraConsole` STA 线程"已被论证不成立,见方案)
- eraBasic 脚本级冒烟测试(AnalysisMode 子进程方案不可行,改为进程内逐行 `ParseLine`)

## 方案(2026-08-29 评审定稿)

### 前置结论(源码核实)

1. **最小初始化序列已探明**,等价 `Process.Initialize` 的解析器子集,无 UI/ERB/ERH 依赖:
   - `ParserMediator.Initialize(null)`(`Warn` 有 null 保护;运行期重载代码本就有 `Initialize(null)` 先例)
   - `new GameBase()`(GAMEBASE.CSV 缺失 → `LoadGameBaseCsv` 直接跳过、全默认值)
   - `new ConstantData()` + `LoadData(空csv目录, null, false)`(无文件时各 loader early-return)
   - `new VariableEvaluator(gamebase, constant)`(ctor 内自动赋 `GlobalStatic.VariableData`)
   - `new IdentifierDictionary(VEvaluator.VariableData)`
   - `StrForm.Initialize()` / `VariableParser.Initialize()`(依赖 NAME/CALLNAME/TARGET/MASTER/PLAYER/ASSI,`VariableData` ctor 全部注册)
   - `new ExpressionMediator(null, VEvaluator, null)`(ctor 仅存引用,print 系方法才解引用 console)
   - `LangManager.setEncode(932)`(`GetStrlenLang` 的 ASCII 快路径不碰静态字段,非 ASCII 时 `lang` 为 null 必 NRE)
   - `GlobalStatic.Process = new Process(null)`(primary ctor 只存 view;`GetVariableToken` 的 private 变量路径需调 `Process.GetScaningLine`)
   - `GlobalStatic.LabelDictionary = new LabelDictionary()`
2. **`LogicalLineParser` 的 `console` 参数非 AnalysisMode 下可传 null**:全文件仅 AnalysisMode 下的 `PrintC` 与一处已有 `console != null` 检查使用。无需 STA 线程、无需实例化 WinForms 控件。注意:console=null 时警告只进 `ParserMediator.warningList` 不打印,`FlushWarningList` 会 NRE → 测试断言返回行类型/`IsError`/`ErrMes`,不调 Flush。
3. **AnalysisMode 子进程冒烟不可行**:`Program.Main` 在 AnalysisMode 下仍 `Application.Run` 起主窗口且无自动退出逻辑。

### 分步实施(每步全绿再进下一步)

- **Step 1 测试夹具** `Emuera.Tests/TestBootstrap.cs`:一次性执行最小初始化序列;`GlobalStatic.Reset()` + 重建做隔离(程序集已禁并行);csv fixture 用临时空目录。风险收口:`ConstantData.LoadData` 传 null output 仅"文件存在但打不开"路径解引用,空目录不触发;若仍暴露问题只记录、不改 Emuera/ 代码。验收:初始化通过 + sanity 测试。
- **Step 2 StrForm 测试** `Runtime/Script/Data/StrFormTests.cs`:走真实调用链 `LexicalAnalyzer.Analyse` → `StrFormWord` → `FromWordToken`。覆盖:纯字面量(`IsConst`)、`{n}`/`%s%` 内插、对齐参数(触发 LangManager)、三连符号 `***`/`+++`/`===`/`///`/`$$$`(先设角色数据再断言求值,验证 `Initialize` 绑定)、`\@…?…#…\@` 嵌套、错误路径(空 `{}`/`%%`、非法三连符号、LEFT/RIGHT 校验)、`Restructure` 折叠。
- **Step 3 ExpressionParser/VariableParser 扩充**:现有 4 例基础上加变量引用(`FLAG:0`、`CALLNAME:MASTER`)、方法调用、三元短路、未知标识符异常路径;新增 `VariableParserTests.cs`(`IsVariable`、`ReduceVariable` 带下标、禁止变量 `IsForbid`)。
- **Step 4 LogicalLineParser + 冒烟** `Runtime/Script/Parser/LogicalLineParserTests.cs`:`ParseLine(string, null)` 覆盖空行/注释/`@FUNC`/`$LABEL`/赋值/前置自增/指令行/`#DIM` 等属性行、错误行 → `InvalidLine` 且 `ErrMes` 非空;冒烟用内联多行脚本片段(`@EVENTFIRST` + PRINT/IF/CALL),逐行断言无 `IsError`,CALL 目标先注册进 LabelDictionary。

### 边界

- 不做 ErbLoader/ERH 装载级冒烟(需全套 Process 运行时,留待后续评估)。
- 不动 Emuera/ 源码。

## 验收

- `dotnet test Emuera.Tests/Emuera.Tests.csproj -c Debug-NAudio` 全绿(38 + 新增)
- 主工程构建不受影响(零 Emuera/ 改动)
- StrForm 部分全绿后,`../nullable-migration/issues/01-core-runtime.md` 的 StrForm 联动项解锁

## Comments

### 2026-08-29

- 方案评审通过定稿;前置调研结论(最小初始化序列/`console` 可传 null/AnalysisMode 行为)逐条源码核实后写入方案。
- 完成:77/77 全绿(原 38 + 新增 39:TestBootstrap sanity 4、StrForm 14、ExpressionParser 扩充 5、VariableParser 5、LogicalLineParser+冒烟 11)。零 Emuera/ 改动。

### 实测补充的踩坑(方案之外)

1. `VariableData.SetDefaultValue` 依赖 `Config.PalamLvDef/ExpLvDef` → 夹具需先 `Config.SetReplace(ConfigData.Instance)`(取 ConfigData 内建默认值,不依赖配置文件)
2. `FunctionIdentifier` 静态构造读取 `JSONConfig.Data.UseScopedVariableInstruction`,`JSONConfig.Load()` 依赖 `Program.ExeDir`(测试中未设,NRE 且被 `IdentifierDictionary` ctor 的 catch 换成误导性 WMP 文案)→ 直接 `JSONConfig.Data = new JSONConfigData()`(等价无 json 配置的首次启动)
3. `Process.GetScaningLine` 在 `scaningLine==null` 时解引用仅 `Initialize` 创建的 `state` → 置 `scaningLine` 占位行(解析期语义本就非空)
4. 表达式三元分隔符是 `?` 与 `#`(`:` 保留给变量下标),非 `?:`
5. CP932 不可映射汉字(如"测试")在 `GetStrlenLang` 宽度按 1 计;对齐用例应选假名等可映射字符
6. xUnit `Assert.Throws<T>` 要求精确类型匹配,子类(如 `IdentifierNotFoundCodeEE` ⊂ `CodeEE`)需断言具体类型
