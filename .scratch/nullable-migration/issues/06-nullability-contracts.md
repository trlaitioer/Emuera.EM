# Nullable:横切可空契约决策

id: nullable-migration-06
Status: implemented

## 任务

链簇票(07~12)启用文件时,大量 CS86xx 的根因不在本文件,而在被多目录引用的共享类型签名上。若由各链票临时决定,会出现互相冲突的标注与无据 `null!`。本票集中决定横切契约:只改签名与字段声明,不启用任何文件、不以清零 CS86xx 为验收;决策引入的新警告由对应链簇票消化。

契约清单:

1. **ExpressionMediator 的 exm 参数**
   - 现状:`GetIntValue`/`GetStrValue`/`GetString` 等被以 `null` exm 调用(2026-09-15 实测 19 处:`ArgumentBuilder.cs` 14、`VariableEvaluator.cs` 4、`ExpressionParser.cs` 1;另有 `EvilMask/Lang.cs` 的反射取属性调用,与本契约无关)
   - 决策点:方法签名 exm 改 `ExpressionMediator?`,还是保持非空并修改调用点;影响面为所有 exm 传 null 的文件(含 issues/10 的 `VariableEvaluator.cs`)
   - **决策:保持 exm 非空,调用点以 `null!` 断言**。理由:这些调用点都在常量折叠/解析期路径上,接收方是 `SingleTerm`(`ArgumentBuilder` 各处以 `is SingleTerm` 前置判断)或 `FixedVariableTerm`(`VariableEvaluator.FindChara`/`FindCharaStr` 的 `fvp`,其 `VariableTerm` 不触碰 exm),不需要求值上下文;反之把参数改为 `ExpressionMediator?` 会让约 230 个 `FunctionMethod`/`AExpression` 实现(其中 `Creator.Method.cs` 118 处、`VariableToken.cs` 7 处确实解引用 exm)都变成"允许 null",把一个调用点断言扩散成全实现的判空负担
   - `EvilMask/Lang.cs` 的 `GetValue(null, null)` 为反射 API,不适用本契约,保持原样

2. **GlobalStatic 字段契约**
   - 现状:字段在 `Reset()` 中置 null、随后按非空使用(Console、Process、ConstantData、VariableData、IdentifierDictionary 等),被 42 个文件引用
   - 决策点:统一规则——初始化前不可达的字段保持非空声明,还是声明 `?` 并在使用点判空;给出字段清单与适用规则
   - **决策:全部保持非空声明,仅 `Reset()` 内的 9 个赋 null 改 `null!`**。字段清单:`Console`、`Process`、`GameBaseData`、`ConstantData`、`VariableData`、`VEvaluator`、`IdentifierDictionary`、`EMediator`、`LabelDictionary`;适用规则是"初始化前不可达即保持非空,置空只发生在 `Reset()` 这一处,由 `null!` 表达"
   - 理由:声明 `?` 会把判空负担推给 42 个引用文件(其中多数在初始化后才访问,达不到可空性收益);`tempDic`/`TempDicLock`/`Pfc`/`ctrlZ` 等已内联初始化,无需处理

3. **Config / ConfigData 叶子契约**
   - Config 被 76 个文件引用;ConfigData 单例在 Load 前不可达
   - 决策点:Load 后非空的成员保持非空声明;未配置时可能为 null 的配置项标 `?`
   - **决策:初始化点之前未赋值的成员保持非空声明并在声明处 `null!`;查找未命中会返回 null 的 API 标 `?`**,具体:
     - `Config.nameDic`(仅由 `Config.SetConfig` 赋值)→ `= null!`
     - `ConfigData` 的 8 个查找 API(`GetItem`/`GetConfigItem`/`GetReplaceItem`/`GetDebugItem` 的 `ConfigCode` 与 `string` 重载)确实以 `return null` 表达未命中 → 返回类型改 `AConfigItem?`
     - `AConfigItem.Copy<T>` 以 `return null` 表达空输入 → 返回 `ConfigItem<T>?`,参数 `ConfigItem<T>?`;`TryParse` 自身判 `param == null` → 参数改 `string?`
     - `ConfigData.Instance` 由内联 `new()` 创建,Load 前即非空;`GetConfigValue<T>` 保持非空返回(未命中时现状即抛 NRE,行为不变,不属本票)
   - 影响面:`ConfigDialog.cs`(+137)、`DebugConfigDialog.cs`(+11)、`MainWindow.cs`(+2)、`ConfigData.cs` 内部(+10)在启用后会新增"查找结果可能为 null"的警告,由 issues/12 消化;若 12 认为 60 余处 `(ConfigItem<T>)GetItem(code)` 逐处 `!` 过重,可在 12 内增补一个按 `ConfigCode` 的泛型非空访问器

4. **Lang / LangManager 叶子契约**
   - Lang 被 57 个文件引用;LangManager 被 6 个文件引用(Config、IdentifierDictionary、StrForm、Creator.Method、Instraction.Child)
   - 决策点:翻译委托与字符串在初始化后的非空契约
   - **决策:Lang 的约 1,500 个 `[Managed] TranslatableString` 成员与 `TranslatableString.text` 全部内联初始化,保持非空;唯一可空处是 `TranslatableString.tr`(构造器与 `Clear()` 显式置 null、`Text` 以 `tr == null ? text : tr` 兜底)→ 声明改 `string?`**
   - `LangManager.lang`/`japanese` 由 `setEncode(int)` 赋值,其后所有 API 都依赖它们 → 保持非空声明并在声明处 `= null!`,`setEncode` 即契约点;`TranslatableString.Set(string)` 的唯一调用方传 `XmlNode.InnerText`(非空),保持 `string`

完成标准:每项契约的决策与理由写入本文对应小节,签名/字段声明修改完成,受影响文件清单一并记录。

## 方案

1. 对每项契约:grep 调用点与字段引用 → 在对应小节写决策与理由 → 修改签名或字段声明
2. 决策影响的文件清单随决策一并记录
3. 不做任何文件启用

执行顺序:先于 issues/09(Instraction.Child 直接使用 UI 打印类型、Sound、Config、Lang)与 issues/12;其余链票可并行参考已定决策(issues/10 的 `VariableEvaluator.cs` 有 4 处 exm 传 null,同样按本票决策适配);与 issues/05(方法调用链元素可空)并列先行,两票无相互依赖。

## 影响

共享类型的可空契约由本票统一决定,避免各链簇票冲突标注;决策引入的新警告由对应链簇票消化。

实测(全量构建 `-p:Nullable=enable`,本票改动前 2,427 条 → 改动后 2,547 条):减少来自 `null!` 断言与声明处初始化(`GlobalStatic.cs` −9、`ArgumentBuilder.cs` −14、`VariableEvaluator.cs` −4、`EvilMask/Lang.cs` −3、`LangManager.cs` −2、`ExpressionParser.cs` −1、`ConfigItem.cs` −1);增加集中在查找 API 变可空后的消费点(`ConfigDialog.cs` +137、`DebugConfigDialog.cs` +11、`ConfigData.cs` +10、`MainWindow.cs` +2),主要由 issues/12 在 UI 文件启用时消化。默认构建 CS86xx 仍为 0。
