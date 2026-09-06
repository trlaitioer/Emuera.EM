# Nullable:csproj 恢复 enable 收尾

Status: needs-triage

Blocked by: 07, 08, 09, 10, 11, 12

## 任务

全部文件完成可空迁移后,把项目级配置恢复为全量启用:

- `Emuera/Emuera.csproj`:`<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`
- 全量构建清零 CS86xx 后的最后确认

依赖六张链簇票 issues/07~12(见 Blocked by);契约票 issues/05、issues/06 不阻塞本票(占位 `null!` 与契约决策不产生 CS86xx)。

完成标准:全量构建 CS86xx 为 0、运行行为不变。

### 非目标:翻转后仍存在的既有警告

以下警告在当前 `annotations` 模式下已存在,翻转 `enable` 后不会消失,不作为本票验收障碍:

- **CS8981**:小写 alias 声明(`trerror`/`trsl`/`trmb`/`trmk`/`treer`,共 64 处)。决策见 `.scratch/performance-optimization/issues/06-cs8981-alias.md`:暂不重命名(避免与 upstream 合并冲突);如成为干扰,优先在 csproj 用 NoWarn 抑制
- **CS0472**:`WebPWrapper.cs` 中 6 处恒为 false 的 `nint` 判空
- **CS4014**:`MainWindow.cs` 键盘处理中未 await 的 `console.ReloadPartialErb` 调用(是否为有意 fire-and-forget 待人工确认)

## 方案

1. 确认 issues/07~12(六张链簇票)已完成且无 CS86xx 残留
2. 修改 csproj 的 Nullable 配置
3. 全量构建验证 CS86xx 为 0、运行行为不变

## 影响

`<Nullable>enable</Nullable>` 恢复后,后续新增/改动的文件默认同时启用可空注解与流分析警告;CS8981/CS0472/CS4014 等既有警告仍存在,处理决策不在本票范围。
