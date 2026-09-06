# Nullable:变量/表达式 + UI/Plugin 迁移

Status: superseded

## 任务

项目级 `<Nullable>annotations</Nullable>` 已开启。本票原覆盖核心运行时之外的第二批文件:

- `Statements/Variable/`(变量系统)
- `Statements/Expression/`(表达式)
- `UI/`(UI 层)
- `Runtime/Utils/PluginSystem/`(插件系统)

## 方案

原方案:按 spec.md 的通用方法,文件顶部加 `#nullable enable` → 构建 → 清零该文件 CS86xx;修复原则同 issues/01。

## 影响

差分按调用链重切(见 spec.md 迁移方法),本票把互不相关的四个簇捆绑在一起的模式废止,范围由以下票承接:

- `Statements/Expression/` → issues/08(表达式与方法链)
- `Statements/Variable/` → issues/10(变量与数据链)
- `UI/`、`Runtime/Utils/PluginSystem/` → issues/12(UI 与外围;UI/Game/StringStyle.cs 早于本迁移即已启用的事实随迁该票)

本票未执行,无警告量统计。
