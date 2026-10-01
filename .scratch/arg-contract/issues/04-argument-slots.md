# 实参契约表：让声明兑现为槽位类型

Status: needs-triage

## 任务

本 effort 的深化票。方案形状、语义裁定与可行性结论见 [spec.md](../spec.md)；本票承担**任务、完成标准与风险**。

### 背景（一句话）

`ArgTypeList` 是声明式实参契约，但它只有三个独立解释者（检查器 / 折叠 / 函数体），彼此无同步机制，编译器又因「形状是数据不是类型」而无法介入。结果是函数体侧约 **682 处取值 + 264 处计数**各自重签规矩，其中 **33 处活 cast**、**可能为 null 的槽 132–134 处**（口径已由 arg-contract/issues/03 钉为 131 / 77 = 未证合计 134）的「本槽位必已填充」无从证明。

### 完成标准

- 规则表在**检查期**产出「已解析槽位集合」新类型：每槽确定为值或变量实体；该类型**只能由检查器构造**
- 基类提供角色化访问器（`Int(i)`/`Str(i)`/`IsInt(i)`/`IsStr(i)`/`Var(i)`…），覆盖 366/405 = 90.4% 的现有消费点；函数体不再判空、不再 cast
- 旧签名 `GetIntValue(ExpressionMediator, List<AExpression?>)` 保留为**转发适配器**，184 个类按切片迁移；**迁移完成后删除适配器**（它是只有一个真实消费者的过渡物，留着就变成永久的浅层）
- 按决策 1 落地折叠顺序：**把折叠并入或前移到检查之内**（不是把两个相邻调用换序——检查在 `ExpressionParser.cs:320`，折叠在 `ArgumentBuilder.cs:126`）
- 按决策 2 确认别名的新形态：`IdentifierDictionary.cs:624` 不复制直接构造、CALLF/CALLFORMF 的 `SpCallArgment.RowArgs` + `CallformCache` 跨调用复用、`StrForm` 的 5 个 `static readonly` 共享 term
- 按决策 3 建立**可机械核对**的迁移门禁（`[Obsolete(error: true)]` / 分析器 / 调用计数门禁）
- 槽位类型表达**三态**（决策 5）：**缺失**（数量省略、列表更短）/ **显式留空**（null 元素）/ **有值**；「可空性」是与 `OmitStart` 正交的逐槽声明，由 issues/03 的裁定提供输入
- 消化的 10% 难点：可能为 null 的槽（三态语义）、**20 处折叠期专属站点**（留在类型化访问器之外）、`VariableTerm` 实体槽（独立 `Var(i)`）、54 处类型询问（暴露 operand type）、3 处「原始 term」逃生口、`Int(i)`/`Str(i)` 对 `Ref` 槽继续返回变量值
- 表驱动用例：每个函数 × {正常 / 少一参 / 中间空参 / 错类型 / 错维度 / Ref 槽传字面量 / AllowConstRef 传常量数组}，覆盖 114 个规则块——**这是该面在仓库内的首个测试覆盖**（今天 `CheckArgumentType`/`ArgTypeList`/`argumentTypeArrayEx` 零命中）

### 切片（每片可独立验收）

| 片 | 内容 | 依赖 |
| --- | --- | --- |
| ① | 10 处可达 null 站点 | issues/03 的裁定 |
| ② | 33 处活 cast（其中 4 处落在可省略槽上：`EnumNameMethod[1]`、`EnumFilesMethod[3]`、`DataTableSelectMethod[3]`、`DataTableToXmlMethod[1]`——issues/03 片 2-D 已裁定「显式空槽应拒绝」但留给本片实现，避免两套改法） | — |
| ③ | 132–134 处未证站点的裁定（口径已由 issues/03 钉为 131 / 77，并收窄为 76 个 (类,槽) 的 A/B 表） | issues/03 的口径统一 |
| ④ | 其余机械迁移（约 682 处消费面） | 新访问器落地 |

## 方案

见 [spec.md](../spec.md) 的「整体方案」。关键决策摘要：

1. 顺序：`check → fold → 建集合`，且折叠需**并入/前移**
2. 别名：不可变集合会迫使 `Restructure` 返回**新 term**——新共享语义须显式确认
3. 迁移门禁：C# 对漏迁**不报警**，必须自建
4. 理由必须是**契约安全**，不是速度

## 影响

- 改动面：`FunctionMethod.cs`、`Creator.Method.cs`（114 个规则块 + 约 682 处消费点）、`FunctionMethodTerm.cs`、`ArgumentBuilder.cs`、`IdentifierDictionary.cs`、`ArgumentListExtensions.cs`、`UserDefinedMethodTerm.cs`
- **性能**：per-call 拿不到可测收益（`Restructure` 每求值 0 次、每调用成本 = 1 虚派发 + N×(索引器 + 虚派发)、无装箱）。可省成本在构造/折叠期（17 个 unique 方法每次折叠分配 `[.. Arguments]`、`VariableTerm.Restructure` 重复 `CheckElement`）。**仓库无基准设施**（`BenchmarkDotNet`/`[Benchmark]` 零命中）——任何性能主张必须先补基准
- **并发现象（非缺陷，但本票必须处理）**：`StrForm.cs:59-63,87-91` 的 5 个 `static readonly FunctionMethodTerm` 被每个含三连符号的 StrForm 共享，`StrForm.Restructure`（`:235`）重复折叠**同一对象**；在 `ErbLoader.cs:986` 的 `Parallel.ForEach` 下即对同一 `List` 的并发写。写入同值（引用写入原子），与 `.scratch/performance-optimization/issues/04-parse-parallel.md` 对 `Depth` 的论证同型
- **不改**：`argumentTypeArrayEx` 的声明式方向；184 个子类的扁平结构（删掉会在 107+ 个调用点重长，且改 `BARSTR` 只动一个类，locality 本已良好）；UDF 族的默认值语义
- 相关票：issues/01/02（止血，先落地）、issues/03（设计输入）、`.scratch/backlog/issues/15`（`Argument` 三字段折叠协议，同一 seam 的另一侧，**本 effort 不并轨**）
- 原始审查记录：`.scratch/backlog/issues/02`
