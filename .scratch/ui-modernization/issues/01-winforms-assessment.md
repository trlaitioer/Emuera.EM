# 决策:WinForms 层升级需求评估

Status: needs-triage
Type: task

## 背景

- UI 代码在 `Emuera/UI/`(7 个窗体 + 各自 Designer 文件 + 自定义控件 `EraPictureBox`),Framework 时代迁移而来。本票按 winforms skill 各场景对 WinForms 层做升级评估(2026-08-30 实测扫描),给出分区域现状与建议。
- 与 `.scratch/dotnet-modernization/` 既有票的重叠不再重复评估:同步网络 I/O 阻塞 UI 线程(其 issues/01)、`Process.Start(url)` 死路径(其 issues/03)已单独立票。

## 各区域现状(实测)

- **入口/工程结构**:已是现代形态——`Program.cs` 为 `[STAThread]` + `ApplicationConfiguration.Initialize()` + `Application.Run(win)`;csproj 设 `ApplicationHighDpiMode=PerMonitorV2`。残留:`Initialize()` 之前手工调用一次 `SetCompatibleTextRenderingDefault`(服务于早于它执行的 `Dialog.Show`/`MessageBox`,功能正确);`Application.Run(win)` 之后残留大段被注释的 VVII 合并前旧启动代码。
- **DPI**:`emuera.manifest`(`dpiAwareness=PerMonitorV2`)、csproj、`SetHighDpiMode` 三处声明一致(冗余但无害);全部窗体 `AutoScaleMode.None` + 固定坐标——主画面为自绘像素画布,属有意为之。
- **跨线程更新**:仅 4 处 `window.Invoke(...)`——3 处在 `tickTimer`(`System.Timers.Timer` 10ms 刻度回调,刷新剩余时间显示),1 处在 `RefreshStrings`;UI 层无 `new Thread`。
- **模态框**:10 处活动 `ShowDialog()`(另有 1 处注释残留),均为文件/文件夹/颜色选择器与小型对话框。
- **渲染/自定义控件**:`EraPictureBox` 的 `SetStyle` 双缓冲配置完整(历史试错以注释保留);无 WebBrowser/ActiveX 遗留控件;定时器用法(WinForms Timer 管按键宏、`System.Timers.Timer` 管通用刻度)恰当。
- UI 事件处理路径中另有若干 `Application.DoEvents()`(`MainWindow.update_lastinput`、`EmueraConsole.Await` / `RefreshStrings` 等),不在装载循环内,不并入 `.scratch/performance-optimization/` 的解析并行化票;本票不改动。

## 评估结论(建议,待 triage)

- `Control.Invoke` → `InvokeAsync`(.NET 9):**不建议**。现有调用均为高频刻度回调的阻塞式 Send;`InvokeAsync` 的 Post 语义会让 UI 线程繁忙时 10ms 刻度的更新在消息队列堆积,反而劣化。无阻塞卡死的具体报告。
- `ShowDialog` → `ShowDialogAsync`(.NET 10):**不建议**。模态语义本就要求阻塞调用方(UI 线程运行对话框自身消息循环,不冻结);转换需把事件处理链整体 async 化,收益仅剩同窗体多实例场景,本项目无此需求。
- 高 DPI 流式布局(TableLayoutPanel/FlowLayoutPanel 改造):**不立项**。固定坐标与自绘像素画布的逐像素定位耦合;高分屏显示过小的现象由本 tracker issues/02(DPI 缩放)单独立票承接,本票维持流式布局不立项的结论。
- 入口清理(删除注释死代码块、收敛 `SetCompatibleTextRenderingDefault` 时机):**可选的低价值清理**,随其它改动顺手做,不单独立票。

预期以 wontfix 定案并记录。

## 验收

无需验收(决策记录)。

## Comments

### 2026-08-30

- 评估完成:分区域实测现状与 API 采纳建议写入正文。Status: needs-triage。
