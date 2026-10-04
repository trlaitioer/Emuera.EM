# Config 把解析期开关与 120 项全局值袋混在一个 static module

id: backlog-07
Status: needs-triage

## 任务

发现于 2026-09-27 的只读架构审查（装载/运行时数据）。

`Config` 的 interface 是「约 120 个可读静态属性 + 一个把它们一次性写满的方法 + 3 个事后打补丁的方法」：`SetConfig`（`Emuera/Runtime/Config/Config.cs:28-286`）只在启动时被 `ConfigData.LoadConfig` 调一次（`ConfigData.cs:817`），之后 `ApplyWindowGeometry`/`UpdateClipboardSetting`/`UpdateLangSetting`（`:320-352`）靠人工枚举「哪些值可以中途改」。没有任何调用方需要同时看到这 120 个值，各层需要的是自己那几个。

最独立也最该先做的一处：`Config.cs:776` 的

```csharp
public static StringComparer StrComper = StringComparer.OrdinalIgnoreCase;
```

是**可变 public 字段**，却在构造那一刻被至少 15 个字典拷走：`LabelDictionary.cs:23, 26, 59, 60`、`IdentifierDictionary.cs:108, 157, 444`、`VariableData.cs:65, 66, 394`、`LogicalLine.cs:296`、`Argument.cs:406`、`Creator.cs:13`、`AppContents.cs:17, 18, 20`。若 `SetConfig` 在某个字典构造之后以 `IgnoreCase=false` 再跑，进程进入精神分裂：先建的字典按 OrdinalIgnoreCase、后建的按 Ordinal——一个值的正确性不再由任何一处决定。

解析期真正读到的开关是少数几个：`SystemAllowFullSpace`、`SystemIgnoreTripleSymbol`、`IgnoreCase`/`StrComper`、`DisplayWarningLevel`、`WarnBackCompatibility`、解析相关兼容项、`ReduceArgumentOnLoad`。读点分布在 `ErbLoader.cs`(16)、`LexicalAnalyzer.cs:919, 998-1001`、`IdentifierDictionary.cs`(12)、`ErhLoader.cs`(12)、`LogicalLineParser.cs`(14)、`ConstantData.cs`(14)，全库 `Config.Xxx` 读取约 844 处。

附带缺陷：`Config.GetConfigName`（`:23-26`）的 `nameDic` 只在 `SetConfig` 里赋值，而 `LexicalAnalyzer.cs:1001` 在**报错路径**调用它 → 未加载配置时抛 NRE 而非脚本错误。

完成标准：

- 划出「解析期开关」这一小簇，给它们一个随解析链传入的选项 module；seam 落在「解析链入口」（`ErbLoader`/`ErhLoader`/`LexicalAnalyzer.Analyse`/`ExpressionParser`）与全局 `Config` 之间
- `StrComper` 不再是可变字段，而是「构造解析期表时传入一次的比较器」；「什么时候可以改 IgnoreCase」这个问题随之消失
- `config` 保留同名只读属性作为 adapter，844 处读点不必一次改动
- `nameDic` 的 `null!` 状态要么去掉（未知 code 返回枚举名），要么让「未加载」在类型上可表达
- 用例：解析选项的 2×2×2 组合（IgnoreCase × FullSpace × TripleSymbol）；`IgnoreCase=false` 时的标签/宏/user-defined 方法查找

## 方案

待评估。**建议分两步**：`StrComper`/`nameDic` 是可立即做的小口；120 值袋是长期方向。

## 影响

- **测试面**：只有 `ConfigGetFilesTests`(4)、`ConfigGetValueInErbTests`、`ConfigTextEditorLoadTests` 直接碰 `Config`，且都因「不能安全地调 `SetConfig`」而绕行。4 个用例文件用 `typeof(Config).GetProperty(...).SetValue(null, …)` 反射改单个静态（`StringMeasureTests.cs:296-311`、`StBarTests.cs:194-203`、`SaveTextMethodTests.cs:81-93`、`FontMetricsTests.cs:131-139`，共 7 处、helper 还被复制 4 份）；`SaveTextMethodTests.cs:82` 的注释写明理由是 `SetConfig` 会连带改写别的静态。`CallformCacheTests.cs:141-143` 断言的 `Config.StrComper` 实际取决于哪个用例先跑——`TestBootstrap.cs:35` 只调 `SetReplace`，从不调 `SetConfig`。`IgnoreCase=false` 分支**完全没覆盖**
- **与既有票的关系**：`Config.getFiles` 的选项问题另见 issues/08；`path-utils` 系列已把 `getFiles` 的路径前缀改经 `PurePath.TryRelativeTo`（`.scratch/path-utils/issues/01-engine-path-prefix.md`），本票不重复那部分
- 改动量估计：中到大。小口（`StrComper`/`nameDic`）为小
- 相关票：issues/05（console seam）、issues/12（运行时数据根对象）
