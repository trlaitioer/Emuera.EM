# Nullable:横切可空契约决策

Status: needs-triage

## 任务

链簇票(07~12)启用文件时,大量 CS86xx 的根因不在本文件,而在被多目录引用的共享类型签名上。若由各链票临时决定,会出现互相冲突的标注与无据 `null!`。本票集中决定横切契约:只改签名与字段声明,不启用任何文件、不以清零 CS86xx 为验收;决策引入的新警告由对应链簇票消化。

契约清单:

1. **ExpressionMediator 的 exm 参数**
   - 现状:`GetIntValue`/`GetStrValue`/`GetString` 等被以 `null` exm 调用(ArgumentBuilder.cs 多处、ExpressionParser.cs 等,调用点以 grep 实测为准)
   - 决策点:方法签名 exm 改 `ExpressionMediator?`,还是保持非空并修改调用点;影响面为所有 exm 传 null 的文件

2. **GlobalStatic 字段契约**
   - 现状:字段在 `Reset()` 中置 null、随后按非空使用(Console、Process、ConstantData、VariableData、IdentifierDictionary 等),被 42 个文件引用
   - 决策点:统一规则——初始化前不可达的字段保持非空声明,还是声明 `?` 并在使用点判空;给出字段清单与适用规则

3. **Config / ConfigData 叶子契约**
   - Config 被 76 个文件引用;ConfigData 单例在 Load 前不可达
   - 决策点:Load 后非空的成员保持非空声明;未配置时可能为 null 的配置项标 `?`

4. **Lang / LangManager 叶子契约**
   - Lang 被 57 个文件引用;LangManager 被 6 个文件引用(Config、IdentifierDictionary、StrForm、Creator.Method、Instraction.Child)
   - 决策点:翻译委托与字符串在初始化后的非空契约

完成标准:每项契约的决策与理由写入本文对应小节,签名/字段声明修改完成,受影响文件清单一并记录。

## 方案

1. 对每项契约:grep 调用点与字段引用 → 在对应小节写决策与理由 → 修改签名或字段声明
2. 决策影响的文件清单随决策一并记录
3. 不做任何文件启用

执行顺序:先于 issues/09(Instraction.Child 直接使用 UI 打印类型、Sound、Config、Lang)与 issues/12;其余链票可并行参考已定决策;与 issues/05(方法调用链元素可空)并列先行,两票无相互依赖。

## 影响

共享类型的可空契约由本票统一决定,避免各链簇票冲突标注;决策引入的新警告由对应链簇票消化。
