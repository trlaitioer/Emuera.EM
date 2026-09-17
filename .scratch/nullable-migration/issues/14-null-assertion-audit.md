# Nullable:非空断言的逐点复核

Status: needs-triage

## 任务

issues/13 消掉的是「可以用机制替代」的 `!`;剩下的断言里有一批不能靠机制消除,`!` 是否成立只能逐点判断。本票逐个复核并给出结论(保留+理由注释 / 补判空 / 改契约),其中可能改动运行行为的部分单独成节,不与 issues/13 的元数据改动混在一起。

复核对象(处数为 issues/13「盘点口径」下的统计结果,分类以 issues/13 落地后的状态为准):

1. **`Argument` 可选字段断言(83 处)**——`arg.Def!`/`arg.Mouse!`/`arg.Term!`/`tinputarg.Mouse!` 等,都是编译器在缺少断言时无法自行证明非空的位置(可删的已在 issues/13 删除;其中仍有确实被省略参数命中的路径),`!` 掩盖的是「该参数被省略时解引用 null」。形态见 `Instraction.Child.cs` 的 `INPUT_Instruction`/`INPUTS_Instruction`/`TINPUTS_Instruction`:`CanSkip != null && GlobalStatic.Console.MesSkip` 分支内直接取 `arg.Def!`,而 `arg.Def` 的判空只发生在同方法更早的另一个分支里
2. **`(x as T)!`(51 处)**:转成 `(T)x` 会把类型不符时的 `NullReferenceException` 变成 `InvalidCastException`,属可观测差异(即便在当前调用点不可达),需要拍板是接受、还是改用 `is T t` 模式匹配
3. **`Emuera.Tests` 内的 `!`**:SKILL 允许测试代码更自由地断言,确认是否维持现状
4. **迁移期为「疑似断言错误」留下的 TODO 注释**,它们是当时已识别但未决的点:
   - `ConstantData.cs` 的 `GetCharacterTemplate*`:未列出的 `VariableCode` 返回 null,而调用方直接解引用
   - `ExpressionMediator.cs` 与 `Creator.Method.cs` 的 `Strings.StrConv` 调用:不支持的转换会返回 null
   - `Creator.Method.cs` 内 `getCurrentLine.Position` 只做非空断言,未做判空
   - `UserDefinedMethodTerm.cs` 的 `UserDefinedRefMethodTerm.Restructure`:`srcArgs` 允许携带表示省略参数的 null,却逐元素断言非空
   - `Instraction.Child.cs` 中「`ConvertTerm` 拒绝 null,但省略实参以 null 到达此处」的位置
   - `CharacterData.cs` 的 `temp_SortKey`:声明为非空,却在某分支被写入 null
5. **`JumpTo`/`JumpToEndCatch`/`ParentLabelLine` 族断言(40 处)**:issues/13 原计划按 `Argument` 同法收敛为单点入口,实测发现 `Instraction.Child.cs` 的 `GOTO_Instruction.SetJumpTo` 存在 `func.JumpTo = null` 的复位路径(未定义标签且 TRY 系时不报错直接返回),「执行期必然非空」需逐点核实后再决定是收敛入口还是补判空
6. **issues/13 未覆盖的可空元素容器(79 处)**:接收者类型不是 `AExpression?` 的 `list[i]!`(`vars` 为 `VariableTerm?[]`、`nodes` 为 `XmlNodeList`、`Columns` 等),以及其余接收者尚未逐一核实的容器
7. **延后初始化的 `= null!` 复核**:成员/属性声明处的 `= null!` 是 SKILL 推荐写法,但局部变量的 `= null!`(如 `ErbLoader.setJumpTo` 的 `FunctionNotFoundName`、`GraphicsImage` 内的 `Brush`)意味着编译器无法证明赋值路径,应改为可空局部或提取方法;常量折叠调用点的 `null!` 实参(如 `AExpression.GetIntValue(null!)`)需要一个具名入口

完成标准:每处给出结论与依据;保留的断言补上说明为何编译器看不到该不变量;认定为真问题的另立修复票,不在本票顺手改。

## 方案

按「先不改行为的复核,后行为决策」两步:

1. 逐个断言点判断三选一——不变量成立(补理由注释)、不变量不成立但当前不可达(补理由注释 + 记入潜在缺陷清单)、不变量不成立且可达(另立修复票)
2. `(x as T)!` 与 `Emuera.Tests` 两项单独提问,确认后再改

复核时以实际调用路径为准:`Argument` 派生类字段的可空性来自 `ArgumentBuilder` 各 `CreateArgument` 分支,判断某处是否可能为 null 必须回到对应构造分支,不能只看字段声明。

## 影响

- 本票不产出机械变换,结论写入正文;需要改代码的部分按结论另开票
- 判定为可达缺陷的位置会成为独立缺陷票,与本票的可空迁移收尾解耦
