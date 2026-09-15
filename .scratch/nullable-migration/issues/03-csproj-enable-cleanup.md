# Nullable:csproj 恢复 enable 收尾

Status: needs-triage

Blocked by: 07, 08, 09, 10, 11, 12

## 任务

全部文件完成可空迁移后,把项目级配置恢复为全量启用:

- `Emuera/Emuera.csproj`:`<Nullable>annotations</Nullable>` 改回 `<Nullable>enable</Nullable>`
- 全量构建清零 CS86xx 后的最后确认:两种声音后端配置(`Debug-NAudio` 与 `Debug`)分别构建,后者需用 VS 的 MSBuild

依赖六张链簇票 issues/07~12(见 Blocked by);契约票 issues/05、issues/06 不作为阻塞边——05 已把元素可空契约贯通,随之在未启用文件里引入的 CS86xx 由链簇票在各自文件启用时消化。

完成标准:全量构建 CS86xx 为 0、运行行为不变。

### 非目标:翻转后仍存在的既有警告

以下警告在当前 `annotations` 模式下已存在,翻转 `enable` 后不会消失,不作为本票验收障碍:

- **CS8981**:小写 alias 声明(`trerror`/`trsl`/`trmb`/`trmk`/`treer`,共 64 条)。决策见 `.scratch/performance-optimization/issues/06-cs8981-alias.md`:暂不重命名(避免与 upstream 合并冲突);如成为干扰,优先在 csproj 用 NoWarn 抑制
- **CS4014**:`MainWindow.cs` 中未 await 的 `console.ReloadPartialErb` 调用(1 条,是否为有意 fire-and-forget 待人工确认)
- **CA1069**(13 条)、**CA2208**(4 条)、**CA1806**(2 条)、**CA1816**(1 条)、**WFO0003**(1 条):分析器与 WinForms 分析器既有警告,与可空无关

## 方案

1. 确认 issues/07~12(六张链簇票)已完成且无 CS86xx 残留
2. 修改 csproj 的 Nullable 配置
3. 全量构建验证 CS86xx 为 0、运行行为不变

迁移期各文件顶部的 `#nullable enable` 保留,不属于本票的删除项:与 `Program.cs`、`UI/Game/StringStyle.cs` 在 `annotations` 时期的既有做法一致,翻转后冗余但无害,删除反而要为同一批文件再做一次改动。

## 影响

`<Nullable>enable</Nullable>` 恢复后,后续新增/改动的文件默认同时启用可空注解与流分析警告;CS8981/CS4014 等既有警告仍存在,处理决策不在本票范围。
