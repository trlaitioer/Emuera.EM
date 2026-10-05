# arg-contract/issues/05 阶段 B 进度：访问器覆盖与归零目标

本文件由 [arg-slots.py](arg-slots.py) 生成（默认产物；`--phase-a` 才另写阶段 A 快照
[slots-consumption.md](slots-consumption.md)，口径见脚本头注释）。票见
`arg-contract-05`。

## G. 阶段 B 验收口径：访问器覆盖（issues/05）

站点全集 = 本文件 live `arguments.Required(<任意下标>)` **24** 处（含文件作用域 helper 内部的读取，
如 `ReadPoint`/`ReadColormatrix`）。形态按站点后随成员归类（词表同 `read_forms`，前缀窗 24 字符），
面按花括号配对归属到方法体。**阶段 B 只迁消费面**：13 个 `UniqueRestructure` 保持旧签名
（旧签名收 `List<AExpression?>`，写回被丢弃），故门禁目标是**消费面归零**，不是全文件归零。

| 面 | `Int` | `Str` | `IsInt` | `IsStr` | `term` | `as Var` | `cast Var` | `折叠` | 合计 | 四访问器覆盖 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 折叠面 | 1 | 2 | — | — | — | — | 2 | 19 | 24 | 3 |
| 合计 | 1 | 2 | — | — | — | — | 2 | 19 | 24 | 3 |

全文件四访问器覆盖 **3/24**（= 12.5%）：`Int` 1 + `Str` 2 + `IsInt` 0 + `IsStr` 0；剔除折叠面后的**消费面覆盖 0/0**。基线见「归零目标」表——迁移中站点数递减，故这两个比值只描述当前未迁移的部分。

| 访问器 | 站点数（当前） | 说明 |
| --- | --- | --- |
| `Int`/`Str`/`IsInt`/`IsStr` | 3 | 全文件；消费面 0 |
| `Var` | 5 | as Var 1 + cast Var 2 + is Var 2（全文件；其中消费面 0、折叠面 5，切片④只归零消费面） |
| `Term`/`TermOrNull` | 0 | 消费面里的裸句柄逃生口 |
| `Tail` | 0 | `arguments.Skip(k)` 整段转发（GETMETH/GETMETHS） |
| `Count` | 12 | `arguments.Count` 12 + 列表模式 0 + `Cast<>` 0 |
| `Has`/`IsEmpty`/`IsMissing` | 7 | 判空写法 7（`==/!= null` 7） + 列表模式 0 |

### Trusted 三族（绕过规则表、由调用点担保，issues/05 的 A 边界）

| 文件 | `arguments.Required(` | `arguments[<字面量>]` | 硬转型 | `new FunctionMethodTerm(` | 旧签名 override |
| --- | --- | --- | --- | --- | --- |
| `OperatorMethod.cs` | 0 | 0 | 0 | 4 | 0 |
| `StrForm.cs` | 0 | 0 | 0 | 8 | 0 |
| `Process.CalledFunction.cs` | 0 | 0 | 1 | 1 | 0 |
| 合计（Trusted 三族） | 0 | 0 | 1 | **13** | 0 |

全库构造点 **15** = Trusted 三族 13 + 规则表族 2（`IdentifierDictionary.cs` 字面 1 + `ArgumentBuilder.cs` target-typed 1）；字面 `new FunctionMethodTerm(` 全库 14。`ArgumentSlots.Trusted(` 调用点 15（调用点担保族 13 + `FunctionMethod.ResolveSlots` 的无声明分支 2）、`ArgumentSlots.Checked(` 2（同一个工厂：命中段 / 定数表）。测试侧另有 7 处直调工厂（`Emuera.Tests` 不在门禁扫描根内，故单列）。

阶段 B 进度：消费面 `arguments.Required(` 剩余 **0**（基线 379 → 目标 0）；旧签名 override 剩余 **0**（规则表族 0 + Trusted 三族 0，目标 0，见 issues/05 切片④）。

### 阶段 B 的归零目标（切片④的门禁）

| 量 | 基线（HEAD 30686cf4） | 当前 | 目标 |
| --- | --- | --- | --- |
| 消费面 `arguments.Required(` | 379 | 0 | 0 |
| 消费面 `Var` 家族（`as`/cast/`is VariableTerm`） | 32 | 0 | 0（迁到 `Var(i)`） |
| 消费面硬转型（`as` + cast） | 29 | 0 | 0 |
| 列表模式 `arguments is […]` | 16 | 0 | 0（迁到 `Has`/`IsEmpty`/`IsMissing`） |
| `arguments.Count` | 119 | 0 | 0（消费面）；折叠面 11 处、检查面 1 处 保留 |
| `arguments.Skip(` / `Cast<` | 2 / 1 | 0 / 0 | 0 / 0 |
| 旧签名 override（规则表族 0 + Trusted 三族 0） | 229 | 0 | 0 |
| 折叠面 `arguments.Required(` | 25 | 24 | 24（**不变**，旧签名保留；arg-contract-09 删掉 `ArrayMultiSortExMethod` 的死 override 后由 25 减 1） |
| `ArgumentSlots` 快照整表复制（ctor 的 `terms = [.. arguments]`） | 0 | 1 | 1（类型落地时即应有；与上面那条折叠面副本理由不同） |

### 访问器前置核对（切片④）

扫描迁移后的槽位入口函数体：对**规则表允许显式空槽**的槽（`OmitStart` 之后且未标 `ArgType.NotNull`），
`Int`/`Str`/`IsInteger`/`IsString`/`Var` 的读取必须由同体的 `Has`/`IsEmpty`/`IsMissing` 守卫兜住。
`Term` 是原始句柄逃生口，列在表内只作观察、不计入判定。

**判据的边界（据实写清）**：守卫判据是「同体内出现过该下标的一次判空」，既不限读取点先后，
也不覆盖**非字面下标**（下标是变量/偏移时算不出落在哪一槽，`may_be_empty` 无从使用）——后者见下一张表。

| 类 | 槽 | 访问器 | 体内有守卫 | 判定 |
| --- | --- | --- | --- | --- |
| `ArrayMultiSortExMethod` | 2 | `Int` | 是 | 通过 |
| `CsvDataMethod` | 2 | `Int` | 是 | 通过 |
| `CsvStrDataMethod` | 1 | `Int` | 是 | 通过 |
| `CsvcstrMethod` | 2 | `Int` | 是 | 通过 |
| `DataTableColumnManagementMethod` | 1 | `Str` | 是 | 通过 |
| `DataTableColumnManagementMethod` | 1 | `Var` | 是 | 通过 |
| `DataTableSelectMethod` | 1 | `Str` | 是 | 通过 |
| `DataTableSelectMethod` | 2 | `Str` | 是 | 通过 |
| `EncodeToUniMethod` | 1 | `Int` | 是 | 通过 |
| `ExistCsvMethod` | 1 | `Int` | 是 | 通过 |
| `FindElementMethod` | 2 | `Int` | 是 | 通过 |
| `FindElementMethod` | 3 | `Int` | 是 | 通过 |
| `FindElementMethod` | 4 | `Int` | 是 | 通过 |
| `FindFilesMethod` | 0 | `Str` | 是 | 通过 |
| `FindcharaMethod` | 2 | `Int` | 是 | 通过 |
| `FindcharaMethod` | 3 | `Int` | 是 | 通过 |
| `GetMethMethod` | 1 | `Int` | 是 | 通过 |
| `GetMethsMethod` | 1 | `Str` | 是 | 通过 |
| `GetcharaMethod` | 1 | `Int` | 是 | 通过 |
| `InRangeArrayMethod` | 3 | `Int` | 是 | 通过 |
| `InRangeArrayMethod` | 4 | `Int` | 是 | 通过 |
| `JoinMethod` | 1 | `Str` | 是 | 通过 |
| `JoinMethod` | 2 | `Int` | 是 | 通过 |
| `JoinMethod` | 3 | `Int` | 是 | 通过 |
| `MatchMethod` | 2 | `Int` | 是 | 通过 |
| `MatchMethod` | 3 | `Int` | 是 | 通过 |
| `MaxArrayMethod` | 1 | `Int` | 是 | 通过 |
| `MaxArrayMethod` | 2 | `Int` | 是 | 通过 |
| `MoneyStrMethod` | 1 | `Str` | 是 | 通过 |
| `StrfindMethod` | 2 | `Int` | 是 | 通过 |
| `StrfindMethod` | 2 | `Int` | 是 | 通过 |
| `SubstringMethod` | 1 | `Int` | 是 | 通过 |
| `SubstringMethod` | 2 | `Int` | 是 | 通过 |
| `SubstringuMethod` | 1 | `Int` | 是 | 通过 |
| `SubstringuMethod` | 2 | `Int` | 是 | 通过 |
| `SumArrayMethod` | 1 | `Int` | 是 | 通过 |
| `SumArrayMethod` | 2 | `Int` | 是 | 通过 |
| `ToStrMethod` | 1 | `Str` | 是 | 通过 |
| `VarsizeMethod` | 1 | `Int` | 是 | 通过 |

可空槽上的字面下标取值站点共 **39** 处，其中未证明 **0** 处。

### 访问器前置核对：非字面下标（弱判据）

下标是变量/偏移表达式的读取。判据只到「同体内出现过 `Count`/`Has`/`IsEmpty`/`IsMissing` 之一」
这一量级（弱于上表），故逐点仍需人工核对；列全是为了让「覆盖了多少」不再只是叙述。

| 类 | 函数 | 访问器 | 下标表达式 | 体内有守卫 | 判定 |
| --- | --- | --- | --- | --- | --- |
| `AllsamesMethod` | `GetIntValue` | `Int` | `i` | 是 | 通过（弱） |
| `AllsamesMethod` | `GetIntValue` | `Str` | `i` | 是 | 通过（弱） |
| `ArrayMultiSortMethod` | `GetIntValue` | `Var` | `slot` | 是 | 通过（弱） |
| `DataTableRowSetMethod` | `GetIntValue` | `Int` | `b + 3` | 是 | 通过（弱） |
| `DataTableRowSetMethod` | `GetIntValue` | `IsString` | `b + 2` | 是 | 通过（弱） |
| `DataTableRowSetMethod` | `GetIntValue` | `Str` | `pos` | 是 | 通过（弱） |
| `DataTableRowSetMethod` | `GetIntValue` | `TermOrNull` | `pos + 1` | 是 | 通过（弱） |
| `DataTableRowSetMethod` | `GetIntValue` | `Var` | `b + 1` | 是 | 通过（弱） |
| `DataTableRowSetMethod` | `GetIntValue` | `Var` | `b + 2` | 是 | 通过（弱） |
| `GroupMatchMethod` | `GetIntValue` | `Int` | `i` | 是 | 通过（弱） |
| `GroupMatchMethod` | `GetIntValue` | `Str` | `i` | 是 | 通过（弱） |
| `MaxMethod` | `GetIntValue` | `Int` | `i` | 是 | 通过（弱） |
| `NosamesMethod` | `GetIntValue` | `Int` | `i` | 是 | 通过（弱） |
| `NosamesMethod` | `GetIntValue` | `Str` | `i` | 是 | 通过（弱） |
| `XmlAddNodeMethod` | `GetIntValue` | `Int` | `methodPos - 1` | 是 | 通过（弱） |
| `XmlAddNodeMethod` | `GetIntValue` | `Int` | `setAllPos - 1` | 是 | 通过（弱） |

槽位入口函数体里的非字面下标站点去重后 **16** 处（原始读取 17 次），其中未证明 **0** 处；
另有 **9** 处落在文件作用域 helper 内（`GraphicsDrawG` 族的 `ReadGraphics`/`ReadColor`/`ReadPoint`/`ReadRectangle`/`ReadColormatrix`：
调用点传常量 `argNo`、规则表把那些槽标成必填，故这些 helper 体内不写守卫，由调用点担保）。
helper 内明细：

| helper | 访问器 | 下标表达式 |
| --- | --- | --- |
| `ReadColor` | `Int` | `argNo` |
| `ReadColormatrix` | `Var` | `argNo` |
| `ReadGraphics` | `Int` | `argNo` |
| `ReadPoint` | `Int` | `argNo` |
| `ReadPoint` | `Int` | `argNo + 1` |
| `ReadRectangle` | `Int` | `argNo` |
| `ReadRectangle` | `Int` | `argNo + 1` |
| `ReadRectangle` | `Int` | `argNo + 2` |
| `ReadRectangle` | `Int` | `argNo + 3` |

## 自校验

| 检查 | 结果 |
| --- | --- |
| G 段站点全集 == 本文件 live `arguments.Required(` | 通过 |
| G 段形态划分和 == 站点全集 | 通过 |
| G 段面划分和（消费 + 检查 + 折叠）== 站点全集 | 通过 |
| G 段基线不增（全文件覆盖 372 / 消费面 379 / 消费面 `Var` 32 / 硬转型 29） | 通过 |
| Trusted 三族构造点 == 13，全库字面 == 14，target-typed == 1 | 通过 |
| `ArgumentSlots.Trusted(` == 15（担保族 13 + 工厂的无声明分支 2），`ArgumentSlots.Checked(` == 2（工厂：命中段 / 定数表） | 通过 |
| 阶段 B 终态一致性（规则表族：消费面归零 ⟺ 旧签名 override 归零） | 通过 |
| 阶段 B 终态一致性（Trusted 三族：`Required(` 归零 ⟺ 旧签名 override 归零） | 通过 |
| 访问器前置：可空槽上的 Int/Str/IsInteger/IsString/Var 读取都有体内守卫（`Count` 不算守卫） | 通过 |
| 访问器前置：动态下标站点所在的函数体都有守卫（弱判据：体内出现 `Count`/`Has`/`IsEmpty`/`IsMissing`） | 通过 |
| 动态下标站点划分 == 全文件扫描（函数体内 + helper 内） | 通过 |
| `ArgumentSlots` 快照的整表复制 == 1（ctor 的 `terms = [.. arguments]`） | 通过 |
| 折叠面不变量（17/13、override 13、整表复制 1） | 通过 |
| `ArgType.NotNull` 计数 == (88, 77, 81, 70)（与 slots-worklist.py 同函数同口径） | 通过 |
| 全仓 live 计数 >= 本文件 | 通过 |
| issues/18 不变量（`AllowConstRef` ⇒ `HasUniqueRestructure`）无违例 | 通过 |
| 测试侧的槽位构造点 == 7（ArgumentSlotsTests 直调 `Trusted(`；门禁扫描根只含 `Emuera/`） | 通过 |

