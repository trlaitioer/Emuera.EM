# URL 打开:移除依赖 Framework 默认值的死路径

Status: needs-triage
Type: task

## 背景

`ConfigDialog.rikaiNote2_LinkClicked`(`UI/Framework/Forms/ConfigDialog.cs`)先用无参 `Process.Start(url)` 打开链接。.NET 上 `UseShellExecute` 默认 false,该调用对 URL 必然抛 Win32Exception,再靠 catch 回退到显式 `UseShellExecute = true` 的写法(以及 xdg-open/open 分支)。首跳必败源于 Framework 时代默认值为 true 的遗留假设:行为正确但每次点击都走一次必然异常。

## 方案

- Windows 分支直接用 `new ProcessStartInfo(url) { UseShellExecute = true }`,删除无参首跳,并删除 `url.Replace("&", "^&")`(该转义仅对经 cmd 间接执行有意义,`UseShellExecute = true` 下会把 `^` 并入 URL);非 Windows 分支保留;
- 同型调用点核对结论为无需改动,但纠正评估措辞:并非"均已显式设置"——仅 `UPDATECHECK_Instruction.DoInstruction` 的链接打开已显式 `UseShellExecute = true`;`MainWindow.Reboot` 用 `Process.Start(exe, args)` 简单重载、`EmueraConsole.OpenErrorFile` 的 ProcessStartInfo 未设该属性,二者可行只是因为启动对象是 exe 路径(`UseShellExecute = false` 下启动可执行文件本就可行),并非显式设置所致。

## 验收

构建通过 + LinkLabel 点击打开浏览器冒烟。

## Comments

### 2026-08-30

- 评估完成:定位与方案写入正文。Status: needs-triage。
