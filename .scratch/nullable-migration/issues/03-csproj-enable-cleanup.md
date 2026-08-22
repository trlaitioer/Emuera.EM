# Nullable:csproj 恢复 enable 收尾

Status: needs-triage
Type: task
Blocked by: 01, 02

## 背景

全部文件完成可空迁移后,需把项目级配置恢复为全量启用。

## 范围

- `Emuera/Emuera.csproj`:`<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`
- 全量构建清零 CS86xx 后的最后确认

## 方案

1. 确认 issues/01、issues/02 已完成且无 CS86xx 残留
2. 修改 csproj 的 Nullable 配置
3. 全量构建验证无新增警告、运行行为不变

## 备注

本票依赖 issues/01 与 issues/02(Blocked by: 01, 02)。

## Comments
