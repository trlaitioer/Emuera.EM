---
id: backlog-07
status: draft
---

# Config 把解析期开关与 120 项全局值袋混在一个 static module

## 任务

发现于 2026-09-27 的只读架构审查（装载/运行时数据）。

`Config` 的 interface 是「约 120 个可读静态属性 + 一个把它们一次性写满的方法 + 3 个事后打补丁的方法」：[Emuera/Runtime/Config/Config.cs](../../../Emuera/Runtime/Config/Config.cs) 的 `SetConfig` 只在启动时被 `ConfigData.LoadConfig` 调一次（调用点在同目录 [ConfigData.cs](../../../Emuera/Runtime/Config/ConfigData.cs) 的 `LoadConfig` 内），之后同文件的 `ApplyWindowGeometry`/`UpdateClipboardSetting`/`UpdateLangSetting` 靠人工枚举「哪些值可以中途改」。没有任何调用方需要同时看到这 120 个值，各层需要的是自己那几个。

最独立也最该先做的一处：[Emuera/Runtime/Config/Config.cs](../../../Emuera/Runtime/Config/Config.cs) 的 `Config.StrComper`

```csharp
public static StringComparer StrComper = StringComparer.OrdinalIgnoreCase;
```

是**可变 public 字段**，却在构造那一刻被至少 15 个字典拷走：[Emuera/Runtime/Script/Data/LabelDictionary.cs](../../../Emuera/Runtime/Script/Data/LabelDictionary.cs) 的 `labelAtDic`/`labelDollarList`/`eventLabelDic`/`noneventLabelDic`、[Emuera/Runtime/Script/Data/IdentifierDictionary.cs](../../../Emuera/Runtime/Script/Data/IdentifierDictionary.cs) 的 `nameDic`/构造函数里的 `refmethodDic`/字段初始化器的 `macroDic`、[Emuera/Runtime/Script/Statements/Variable/VariableData.cs](../../../Emuera/Runtime/Script/Statements/Variable/VariableData.cs) 的 `varTokenDic`/`localvarTokenDic`/`GetVarTokenDicClone`、[Emuera/Runtime/Script/Statements/LogicalLine.cs](../../../Emuera/Runtime/Script/Statements/LogicalLine.cs) 的 `privateVar`、[Emuera/Runtime/Script/Statements/Argument.cs](../../../Emuera/Runtime/Script/Statements/Argument.cs) 的 `CallformCache` 构造函数（`strDic`）、[Emuera/Runtime/Script/Statements/Function/Creator.cs](../../../Emuera/Runtime/Script/Statements/Function/Creator.cs) 的 `FunctionMethodCreator` 静态构造函数（`methodList`）、[Emuera/UI/Game/Image/AppContents.cs](../../../Emuera/UI/Game/Image/AppContents.cs) 的 `resourceDic`/`imageDictionary`/`resourceImageDictionary`。若 `SetConfig` 在某个字典构造之后以 `IgnoreCase=false` 再跑，进程进入精神分裂：先建的字典按 OrdinalIgnoreCase、后建的按 Ordinal——一个值的正确性不再由任何一处决定。

解析期真正读到的开关是少数几个：`SystemAllowFullSpace`、`SystemIgnoreTripleSymbol`、`IgnoreCase`/`StrComper`、`DisplayWarningLevel`、`WarnBackCompatibility`、解析相关兼容项、`ReduceArgumentOnLoad`。读点分布在 [ErbLoader.cs](../../../Emuera/Runtime/Script/Loader/ErbLoader.cs)(16)、[LexicalAnalyzer.cs](../../../Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs) 的 `SkipWhiteSpace`/`Analyse`、[IdentifierDictionary.cs](../../../Emuera/Runtime/Script/Data/IdentifierDictionary.cs)(12)、[ErhLoader.cs](../../../Emuera/Runtime/Script/Loader/ErhLoader.cs)(12)、[LogicalLineParser.cs](../../../Emuera/Runtime/Script/Parser/LogicalLineParser.cs)(14)、[ConstantData.cs](../../../Emuera/Runtime/Script/Data/ConstantData.cs)(14)，全库 `Config.Xxx` 读取约 844 处。

附带缺陷：`Config.GetConfigName` 的 `nameDic` 只在 `SetConfig` 里赋值，而 [Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs](../../../Emuera/Runtime/Script/Parser/LexicalAnalyzer.cs) 的 `Analyse` 在**报错路径**调用它 → 未加载配置时抛 NRE 而非脚本错误。

完成标准：

- 划出「解析期开关」这一小簇，给它们一个随解析链传入的选项 module；seam 落在「解析链入口」（`ErbLoader`/`ErhLoader`/`LexicalAnalyzer.Analyse`/`ExpressionParser`）与全局 `Config` 之间
- `StrComper` 不再是可变字段，而是「构造解析期表时传入一次的比较器」；「什么时候可以改 IgnoreCase」这个问题随之消失
- `config` 保留同名只读属性作为 adapter，844 处读点不必一次改动
- `nameDic` 的 `null!` 状态要么去掉（未知 code 返回枚举名），要么让「未加载」在类型上可表达
- 用例：解析选项的 2×2×2 组合（IgnoreCase × FullSpace × TripleSymbol）；`IgnoreCase=false` 时的标签/宏/user-defined 方法查找
- 测试侧：解析选项用例自设所依赖的 `Config` 静态，不再依赖 `TestBootstrap` 的默认值；`CallformCacheTests.StrKey_UsesStrComper` 对全局 `Config.StrComper` 的断言随之改为自设值或改用本类自控变量

## 方案

待评估。**建议分两步**：`StrComper`/`nameDic` 是可立即做的小口；120 值袋是长期方向。

## 影响

- **测试面**：`ConfigGetFilesTests`(4)、`ConfigGetValueInErbTests`、`ConfigTextEditorLoadTests` 这几个配置装载相关的用例都不调 `SetConfig`（全测试工程亦无一处真调）——[Emuera.Tests/Runtime/Config/ConfigTextEditorLoadTests.cs](../../../Emuera.Tests/Runtime/Config/ConfigTextEditorLoadTests.cs) 的注释写明理由是避免 `LoadConfig` 连带 `SetConfig`/`CheckUpdate` 改写其它用例依赖的静态；需要改 `Config` 单个静态的用例一律走反射，共 6 个文件、9 处 `typeof(Config).GetProperty(...).SetValue(null, …)`（[Emuera.Tests/UI/Game/StringMeasureTests.cs](../../../Emuera.Tests/UI/Game/StringMeasureTests.cs) 的 `SetFontNameForTests`/`SetTextDrawingMode`、[Emuera.Tests/UI/Game/StBarTests.cs](../../../Emuera.Tests/UI/Game/StBarTests.cs) 的 `SetTextDrawingMode`/`EnsureFontName`（注释标明「Kept identical to StringMeasureTests」）、[Emuera.Tests/Runtime/Script/Statements/Function/SaveTextMethodTests.cs](../../../Emuera.Tests/Runtime/Script/Statements/Function/SaveTextMethodTests.cs) 的 `EnsureValidExtension`、[Emuera.Tests/UI/FontMetricsTests.cs](../../../Emuera.Tests/UI/FontMetricsTests.cs) 的 `EnsureFontName`、[Emuera.Tests/UI/Game/ConsoleDisplayLineTests.cs](../../../Emuera.Tests/UI/Game/ConsoleDisplayLineTests.cs) 的 `SetTextDrawingMode`、[Emuera.Tests/Runtime/Config/ConfigKeySymmetryTests.cs](../../../Emuera.Tests/Runtime/Config/ConfigKeySymmetryTests.cs) 的 `SetEnglishConfigOutput`/`DebugStaticProperty`）；被复制的 helper 是 `EnsureFontName`（3 个文件各一份）与 `SetTextDrawingMode`（3 个文件各一份）；`SaveTextMethodTests.EnsureValidExtension` 的注释写明理由是 `SetConfig` 会连带改写别的静态。`CallformCacheTests.StrKey_UsesStrComper` 断言全局 `Config.StrComper` 却**不自设值**：该静态由 [Emuera/Runtime/Config/Config.cs](../../../Emuera/Runtime/Config/Config.cs) 的字段初始化器定为 `OrdinalIgnoreCase`，`SetConfig` 则按 `IgnoreCase` 写成 `OrdinalIgnoreCase` 或 `Ordinal`，而 [Emuera.Tests/TestBootstrap.cs](../../../Emuera.Tests/TestBootstrap.cs) 的夹具初始化只调 `Config.SetReplace`、从不调 `SetConfig`，也没有用例直接写入 `IgnoreCase`，故该断言恒过（`EnsureFontName` 的注释即因「`SetConfig` 会连带改写 `IgnoreCase`」而绕行）。[Emuera.Tests/README.md](../../../Emuera.Tests/README.md) 的「共享全局状态(写过的系统变量必须还原)」一节要求读系统变量的用例先自己写入确定值，该用例是这条约定的现存例外。`IgnoreCase=false` 分支**完全没覆盖**
- **测试侧的归属**：`CallformCacheTests.MultiInterpolationForm_UsesStrKey` 的顺序依赖已修复，教训与排查手法记在上述一节；[nullable-migration-19](../../nullable-migration/issues/19-plugin-and-error-path-nulls.md) 正文里「宜记入 `tracker/test-infrastructure/`」是该次修复之前的观察，该票已终态、正文不再改动，故不另立票，测试侧的处置统一收在本票
- **与既有票的关系**：`Config.getFiles` 的选项问题另见 issues/08；`path-utils` 系列已把 `getFiles` 的路径前缀改经 `PurePath.TryRelativeTo`（`path-utils-01`），本票不重复那部分
- 改动量估计：中到大。小口（`StrComper`/`nameDic`）为小
- 相关票：issues/05（console seam）、issues/12（运行时数据根对象）
