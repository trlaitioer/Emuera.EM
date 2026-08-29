# Nullable:csproj 恢复 enable 收尾

Status: needs-triage
Type: task
Blocked by: 07, 08, 09, 10, 11, 12

## 背景

全部文件完成可空迁移后,需把项目级配置恢复为全量启用。

## 范围

- `Emuera/Emuera.csproj`:`<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`
- 全量构建清零 CS86xx 后的最后确认

## 非目标:翻转后仍存在的既有警告

以下警告在当前 `annotations` 模式下已存在,翻转 `enable` 后不会消失,不作为本票验收障碍:

- **CS8981**:小写 alias 声明(`trerror`/`trsl`/`trmb`/`trmk`/`treer`,共 64 处)。决策见 `.scratch/performance-optimization/issues/06-cs8981-alias.md`:暂不重命名(避免与 upstream 合并冲突);如成为干扰,优先在 csproj 用 NoWarn 抑制
- **CS0472**:`WebPWrapper.cs` 中 6 处恒为 false 的 `nint` 判空
- **CS4014**:`MainWindow.cs` 键盘处理中未 await 的 `console.ReloadPartialErb` 调用(是否为有意 fire-and-forget 待人工确认)

## 方案

1. 确认 issues/07~12(六张链簇票)已完成且无 CS86xx 残留
2. 修改 csproj 的 Nullable 配置
3. 全量构建验证 CS86xx 为 0、运行行为不变

## 备注

本票依赖六张链簇票 issues/07~12(Blocked by: 07~12);契约票 issues/05、issues/06 不阻塞本票(占位 `null!` 与契约决策不产生 CS86xx)。

## Comments

### 2026-08-29

- 阻塞关系加入 issues/04(范围外清尾票);新增“非目标”小节,记录翻转后仍存在的 CS8981/CS0472/CS4014 及 CS8981 的处理决策引用;验收表述改为“CS86xx 为 0”。
- 差分按调用链重切:阻塞关系改为 issues/07~12,方案与备注的旧票引用同步更新。
