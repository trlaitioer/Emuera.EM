# URL 打开:移除依赖 Framework 默认值的死路径

id: dotnet-modernization-03
Status: implemented

## 任务

`ConfigDialog.rikaiNote2_LinkClicked`(`UI/Framework/Forms/ConfigDialog.cs`)先用无参 `Process.Start(url)` 打开链接。.NET 上 `UseShellExecute` 默认 `false`,字符串重载把 URL 当可执行文件解析,该调用必然抛 `Win32Exception`(net10.0 实测:异常类型 `System.ComponentModel.Win32Exception`,消息为 "An error occurred trying to start process '…'"),再靠 catch 回退到显式 `UseShellExecute = true` 的写法(以及 xdg-open/open 分支)。首跳必败源于 Framework 时代默认值为 true 的遗留假设:行为正确但每次点击都走一次必然异常。

同一方法内的平台分支亦不可达:构建目标为 `net10.0-windows`,且原生互操作面全为 Windows(`winmm.dll`/`user32.dll`/`kernel32.dll`,`libwebp` 仅有 `libwebp.dll` 与 `libwebp_x86.dll`,无 `.so`/`.dylib` 加载路径),`OSPlatform.Linux`/`OSX` 分支在该方法执行流程中不会命中。

完成标准:构建通过 + 静态确认无参首跳与平台分支消失 + LinkLabel 点击打开浏览器冒烟(判定见下)。

## 方案

Windows 分支直接用 `new ProcessStartInfo(url) { UseShellExecute = true }` 替换整个 try/catch,一并删除:无参首跳、`url.Replace("&", "^&")`(该转义仅对经 cmd 间接执行有意义,`UseShellExecute = true` 下会把 `^` 并入 URL)、`OSPlatform.Linux`/`OSX` 分支,以及只服务于这三处平台判断的 `using System.Runtime.InteropServices;`。不再设 catch:shell 启动失败(如无默认浏览器)如实抛出,不复用异常做分支控制。

`MainWindow.Reboot` 与 `EmueraConsole.OpenErrorFile` 不设该属性仍可行,是因启动对象为可执行文件路径,与本次改动无关,维持原样。

### 验收

- `dotnet build Emuera.sln -c Debug-NAudio -p:Platform=x64` 通过(沙箱内 sln 级 Restore 不可用,降级为 `dotnet build Emuera/Emuera.csproj -c Debug-NAudio -p:Platform=x64 --no-restore`,已验证 0 错误);
- 静态:方法内不再出现无参 `Process.Start(url)`、`catch`、`OSPlatform` 判断(移除 using 后若有残留引用会直接编译失败,构成强制检查);
- 冒烟:打开 CONFIG → Rikai 页签(`rikaiNote2` 常显)→ 点击 → 浏览器打开 wiki 页且不经过任何回退路径;
- 前提复核(开工前一次):先以最小 net10.0 探针确认 `Process.Start(url)` 抛 `Win32Exception`;若不抛则本票退回 triage。

## 曾考虑的替代方案

1. 保留 try/catch 并改用显式 `UseShellExecute = true` 作首跳、catch 仅作兜底:仍以异常做控制流,且掩盖真实启动失败,不采用
2. 保留 Linux/OSX 分支以备将来跨平台:当前 TFM 为 `net10.0-windows`、原生依赖全为 Windows,分支不可达;若将来真要跨平台,需先解决 P/Invoke 面,届时重写本方法远比保留死分支清晰,不采用

## 影响

同型调用点核对结论为无需改动,但纠正评估措辞:并非"均已显式设置"——仅 `UPDATECHECK_Instruction.DoInstruction`(`Runtime/Script/Statements/Instraction.Child.cs`)的链接打开已显式 `UseShellExecute = true`;`MainWindow.Reboot` 用 `Process.Start(exe, args)` 简单重载、`EmueraConsole.OpenErrorFile` 的 ProcessStartInfo 未设该属性,二者可行只是因为启动对象是 exe 路径(`UseShellExecute = false` 下启动可执行文件本就可行),并非显式设置所致。

同文件 `ConfigDialog.button2_Click` 与 `Instraction.Child.cs` 三条字体指令另存 4 处不可达的 `OperatingSystem.IsWindows()` 守卫,已切至 issues/07,不在本票范围。
