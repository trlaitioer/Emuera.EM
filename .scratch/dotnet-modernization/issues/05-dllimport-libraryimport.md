# 决策:DLL 导入维持 DllImport 声明

Status: wontfix
Type: task

## 背景

三处平台 P/Invoke 采用传统 `DllImport` 声明:`Runtime/Utils/WinInput.cs`(输入状态查询)、`Runtime/Utils/WinmmTimer.cs`(多媒体定时器)、`Runtime/Utils/WebPWrapper.cs`(WebP 解码)。net7+ 提供 `[LibraryImport]` 源生成替代(封送校验、AOT 兼容);本项目不以 AOT 为目标,现有声明已稳定运行。

## 决策

维持现状,不迁移 `[LibraryImport]`。已确认 wontfix。

理由:

- 迁移收益接近零。三处共 50 余个 DllImport 声明签名全为 blittable 基本类型(WebPWrapper.cs 的 49 个声明无 bool/string/数组参数,结构体字段全为基本类型;WinInput.cs 的 `GetKeyState`、WinmmTimer.cs 的三个 mm_* 均为基本类型),编译期封送校验没有可校验内容,生成的封送代码为恒等操作。真正的 ABI 风险在结构体 `StructLayout` 布局与原生库的一致性,该风险在任何声明方式下都靠手工保证,LibraryImport 不校验。
- 性能差异仅为 DllImport 首次调用生成 stub 与编译期生成的区别,对本项目可忽略。
- SYSLIB1054 仅出现在 IDE 分析器,构建警告中不存在(WebPWrapper.cs 重编译验证),不构成警告清零诉求。
- AOT 收益线在可见未来无法兑现:WinForms 的 NativeAOT 截至 .NET 10 不受官方支持,前提性的 trim 兼容改造尚未完成(dotnet/winforms#4649);本项目存在更优先的原生阻塞——插件系统依赖运行时加载外部 IL 程序集(`Runtime/Utils/PluginSystem/PluginManager.cs` 的 `LoadPlugins()` 使用 `Assembly.LoadFrom` 加载 `Plugins/*.dll` 并反射实例化 manifest),NativeAOT 无法支持;WMP 声音后端使用 WMPLib COM 互操作(`Runtime/Utils/Sound.WMP.cs`),NativeAOT 不支持内建 COM 互操作。

## 验收

无需验收(决策记录)。

## Comments

### 2026-08-30

- 评估完成:使用点与维持理由写入正文。Status: needs-triage(待 triage 确认 wontfix)。

### 2026-09-06

- 复核确认 wontfix:blittable 签名核查、SYSLIB1054 构建验证、AOT 阻塞分析补充进正文。Status: wontfix。
