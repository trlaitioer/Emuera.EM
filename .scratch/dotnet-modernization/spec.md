# 迁移方案:.NET 10 现代化收尾

状态:评估完成(2026-08-30),各项处置待 triage。

## 目标

项目已运行于 net10.0-windows,但 Framework → .NET 迁移以可运行为准,部分写法与 API 保留原样。本 tracker 记录依据 dotnet 8→9 / 9→10 破坏性变更清单(WinForms 与 core-libraries 部分)逐项扫描出的行为变化接触面与迁移残留,并给出处置;另含 C# 语言特性现代化的采纳策略评估(issues/06)。

## 条目

1. UPDATECHECK 换 HttpClient(issues/01):WebClient 已弃用(SYSLIB0014)、同步阻塞 UI 线程、部分路径未释放
2. 存档读取加固(issues/02):net9 起 `BinaryReader.ReadString()` 畸形序列静默返回 U+FFFD,损坏存档文本检出弱化
3. URL 打开死路径清理(issues/03):`Process.Start(url)` 依赖 Framework 时代 `UseShellExecute` 默认值,首跳必败靠 catch 回退
4. Microsoft.VisualBasic 依赖决策(issues/04):`StrConv` 假名/全半角转换,当前目标框架可用,建议保留
5. DllImport → LibraryImport 决策(issues/05):无 AOT 需求,建议维持现状
6. C# 语言特性现代化策略决策(issues/06):C# 14 已随 TFM 默认生效,建议整体不立项、按需(on-touch)采纳,待 triage

UI 层的 WinForms 升级评估与显示改造(DPI 缩放、等宽网格渲染)已于 2026-08-30 切至独立 tracker `.scratch/ui-modernization/`,本 tracker 不再跟踪。

另有 `Application.DoEvents()`(ErhLoader / ErbLoader 装载解析循环内泵 UI)不单独立票,并入 `performance-optimization/issues/04`(解析并行化)处理,该票正文已列为本票风险点。

## 核查通过(2026-08-30 扫描,避免重复排查)

BinaryFormatter/SoapFormatter(net9 起恒抛)、非泛型集合(ArrayList/Hashtable 等)、`[Serializable]`/ISerializable、Thread.Abort/Suspend、Registry、ServicePointManager/WebRequest 系、VolatileRead/VolatileWrite、SystemEvents、`Encoding.Default`、BufferedStream、StatusStrip/PictureBox URL 加载、`RegexOptions.Compiled`(仅注释与运行时构造的正则,见 `RegexFactory`)、Expression lambda 的 params span 重载陷阱、System.Drawing `OutOfMemoryException` 显式捕获(net10 改抛 ExternalException,无显式 catch)。配置读写已使用 System.Text.Json。

9→10 补充核查:默认终止信号处理移除(代码无 `AppDomain.ProcessExit`/控制台信号处理依赖,EmueraConsole 不受影响)、SYSLIB0058–0062 系(Rfc2898DeriveBytes/X509/SslStream/XsltSettings/Queryable MaxBy/MinBy)全无、未引用 System.Linq.Async 包(包引用仅 Enums.NET 与 System.CommandLine)。

## 验收

各项按各自 ticket 验收;决策记录票以文档定案。
