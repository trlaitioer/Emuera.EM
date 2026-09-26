# 平台守卫:清理 Windows-only 目标下的恒真判断

Status: ready-for-agent

## 任务

扫描确认 `OperatingSystem.IsWindows()` 守卫共 4 处,分布如下:

- `ConfigDialog.button2_Click`(`UI/Framework/Forms/ConfigDialog.cs`,枚举 `InstalledFontCollection` 之前)
- `FONTBOLD_Instruction.DoInstruction`(`Runtime/Script/Statements/Instraction.Child.cs`)
- `FONTITALIC_Instruction.DoInstruction`(同文件)
- `FONTREGULAR_Instruction.DoInstruction`(同文件)

构建目标为 `net10.0-windows` + WinForms,平台 `x64`/`x86`,且原生互操作面全为 Windows(见「方案 · 参考」),故这 4 处判断恒真、"非 Windows"分支不可达。`ConfigDialog` 处与 issues/03 属同类残留;三条字体指令处则涉及脚本语义:守卫使 `FONTBOLD`/`FONTITALIC`/`FONTREGULAR` 在非 Windows 宿主上静默失效,与"平台不可达"是两个不同问题,故本票单列评估。

完成标准:给出各处的处置结论(清理或保留并说明理由),涉及的代码改动构建通过。

## 方案

- `ConfigDialog.button2_Click`:删除守卫。该处后续代码在 Windows 上本就执行,删除属行为等价清理。
- 三条字体指令:待定。清理即保持现状;保留则把"指令携带平台语义"作为已知事实记录。取舍依据是是否存在非 Windows 宿主预期——目前无该预期(原生互操作面见下),倾向清理,但需确认脚本层是否要求这三条指令具备跨平台语义。

### 参考:原生互操作面(不可达判断的依据)

全仓 `DllImport` 目标库为 `winmm.dll`(`Runtime/Utils/WinmmTimer.cs`)、`user32.dll`(`Runtime/Utils/WinInput.cs`)、`kernel32.dll` 与 `libwebp.dll`/`libwebp_x86.dll`(`Runtime/Utils/WebPWrapper.cs`),无 `.so`/`.dylib` 加载路径;`Emuera.sln` 仅含 `Emuera` 与 `EmueraPluginExample`,无多 RID 或平台条件属性。任何非 Windows 宿主会在这些 P/Invoke 处先失败,而非走到上述守卫。

## 影响

`ConfigDialog` 处为等价清理;字体指令处若清理,eraBasic 脚本的字体样式行为不变,仅移除不可达分支。
