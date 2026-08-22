# 性能:CSV 查询字典化

Status: needs-triage
Type: task

## 背景

角色模板 CSV 查询为线性扫描(已评估、未实施)。

## 方案

- `ConstantData.GetCharacterTemplate` / `GetCharacterTemplateFromCsvNo` 为线性扫描,可改字典 O(1)

## Comments
