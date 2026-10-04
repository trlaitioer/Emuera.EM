# ReduceArgumentOnLoad 的 ONCE 档不生效，等价于 NO

id: backlog-17
Status: needs-triage
Type: bug

## 任务

发现于 2026-09-27 的折叠时序核查（只读静态推导，未在运行期复现）。

`Emuera/Runtime/Config/Config.cs:447-467` 的 `CheckUpdate()`：

```csharp
public static bool CheckUpdate()
{
    if (ReduceArgumentOnLoad != ReduceArgumentOnLoadFlag.ONCE)
    {
        if (ReduceArgumentOnLoad == ReduceArgumentOnLoadFlag.YES)  NeedReduceArgumentOnLoad = true;
        else if (ReduceArgumentOnLoad == ReduceArgumentOnLoadFlag.NO) NeedReduceArgumentOnLoad = false;
        return false;
    }

    long key = getUpdateKey();
    bool updated = LastKey != key;
    LastKey = key;
    return updated;
}
```

`ONCE` 分支**从不给 `NeedReduceArgumentOnLoad` 赋值**，而该标志是自动属性 `bool`，初值 `false`；全仓库只有 `Config.cs:453` / `:457` 两处赋值。`NeedReduceArgumentOnLoad` 的唯一消费点是 `ErbLoader.cs:1314`：

```csharp
if (Config.Config.NeedReduceArgumentOnLoad || Program.AnalysisMode || func.Function.IsForceSetArg())
    ArgumentParser.SetArgumentTo(func);
```

⇒ 选 `ONCE`（UI 文案为 **OnUpdate**，`ConfigDialog.cs:202-204` 的 `ReduceArgs.Never/OnUpdate/Always`）时 `NeedReduceArgumentOnLoad` 保持 `false`，与 `NO`（Never）在**折叠行为上完全等价**；`ONCE` 的唯一实际效果是 `CheckUpdate` 的返回值变化（`true` 当数据被更新），该返回值按注释「戻り値はセーブが必要かどうか」用于决定是否存配置。

即：一个可见配置项（`emuera.config` 的 `ReduceArgumentOnLoad`）在 UI 上提供三档，其中 OnUpdate 档对「是否在装载时解析实参」没有任何影响。

完成标准：

- 裁定 `ONCE`（OnUpdate）的**预期语义**，二选一并取证：
  - (i) 「数据被更新过 → 本次装载折叠实参」：则应在 `ONCE` 分支写入 `NeedReduceArgumentOnLoad = updated`（或等价的显式路径）
  - (ii) 该档已被废弃：则应从 UI 与 `ConfigCode` 中移除，避免提供无效选项
- 无论哪种，都要消除「UI 提供三档、其中一档无行为差异」的状态；若保留，需有可观测的用例
- 用例：`ReduceArgumentOnLoad = ONCE` 且数据未更新 vs 已更新，断言 `ArgumentParser.SetArgumentTo` 的调用（或 `line.Argument != null`）在两态下不同

## 方案

待评估。倾向 (i)：`ONCE` 的档名与 UI 文案（OnUpdate）表明意图是「更新后折叠一次」，故「不赋值」更像漏写而非废弃。落地前先确认 `LastKey`/`getUpdateKey` 的更新时机与 `Program` 启动顺序的相对关系——若 `CheckUpdate` 在装载之后才被调用，则 (i) 也无法影响本次装载，那时应判为 (ii)。

## 影响

- **与本仓库其他票的关系**：与 `arg-contract` effort 无关（该 effort 关心的是折叠与检查的顺序，不是何时触发折叠）。但**若按 (i) 修复，会改变装载期折叠的发生面**，从而影响 `arg-contract` effort 里「默认只有首次执行时折叠」这一前提——两处需对齐
- 现状后果：`ReduceArgumentOnLoad` 默认 `NO`，故默认行为不变；缺陷只在显式选择 OnUpdate 档的用户上体现（`ConfigDialog.cs:483-495`、`715-722` 提供该档）
- 改动量估计：小（一处赋值 + 用例）；风险在语义裁定，不在代码
