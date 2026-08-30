# 决策:DLL 导入维持 DllImport 声明

Status: needs-triage
Type: task

## 背景

三处平台 P/Invoke 采用传统 `DllImport` 声明:`Runtime/Utils/WinInput.cs`(输入状态查询)、`Runtime/Utils/WinmmTimer.cs`(多媒体定时器)、`Runtime/Utils/WebPWrapper.cs`(WebP 解码)。net7+ 提供 `[LibraryImport]` 源生成替代(封送校验、AOT 兼容);本项目不以 AOT 为目标,现有声明已稳定运行。

## 决策建议

维持现状:改写收益仅为 AOT 兼容与封送校验,无行为收益,且涉及三个文件的封送签名重构。预期 wontfix。

## 验收

无需验收(决策记录)。

## Comments

### 2026-08-30

- 评估完成:使用点与维持理由写入正文。Status: needs-triage(待 triage 确认 wontfix)。
