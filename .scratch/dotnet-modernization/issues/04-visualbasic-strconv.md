# 决策:保留 Microsoft.VisualBasic 的 StrConv 依赖

Status: needs-triage

## 任务

评估两处 Framework 时代依赖 Microsoft.VisualBasic 的 `Strings.StrConv` 用法是否替换:

- `ExpressionMediator.ConvertStringType`(`Runtime/Script/Statements/ExpressionMediator.cs`):假名/平假名转换(0x0411 日语 LCID),受 forceHiragana/forceKatakana/halftoFull 强制度控制;
- 内建字符串函数 TOFULL/TOHALF(`Runtime/Script/Statements/Function/Creator.Method.cs`,strType 的 Full/Half 分支):全角/半角转换。

net10.0-windows 的 Windows Desktop runtime 自带 VB runtime,依赖 NLS,在当前"Windows 专用"的平台目标下行为正确。完成标准:决策记录于本文,提交 triage 定案。

## 方案

保留:自实现假名/全半角映射成本高,且易引入与 NLS 结果不一致的行为差异;替换收益仅"摆脱 Framework 时代依赖"本身。预期以 wontfix 定案并记录。

## 影响

无代码改动;`Microsoft.VisualBasic` 依赖维持现状。
