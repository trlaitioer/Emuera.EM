# 性能:CALLFORM 动态调用缓存

Status: 已完成(2026-08-30)
Type: task

## 背景

`CALLFORM` 等动态函数名调用的函数名解析无缓存。静态 CALL(IsConst)在 `SetJumpTo` 已预解析并把 `CalledFunction`/`UDFArgument` 缓存在 `SpCallArgment` 上,动态路径每次执行都重走「拼接函数名 → 字典查函数 → ConvertArg 分配」。

### 语料普查(2026-08-30)

- 动态调用指令占比:TRY 系(TRYCALLFORM/TRYCCALLFORM/TRYCALLFORMF)约 68%,CALLFORM、CALLFORMF 次之;GOTOFORM / TRYCALLLIST / TRYGOTOLIST 约为零。
- 名字形态:字面量(已走静态 IsConst 路径)与拼接约各半;单变量形态 `{VAR}` 约为零。
- 拼接约九成为单插值,插值几乎全为单个 int 表达式(`{NO:ARG}`、`{LOCAL}`、`{SELECTCOM}` 类变量读)。
- 热点为每命令 × 每角色、每帧状态显示一类按角色号分发的函数;分发目标按具体角色号稀疏定义,TRY 落空是高频稳态。

## 方案(依据语料定案)

per-callsite 惰性缓存,挂在 `SpCallArgment`/`SpCallFArgment` 实例上(每指令行独享,ERB 重载重建 InstructionLine 时自然失效):

1. **形态判定**(首次执行一次后固定):`FuncnameTerm` 为 StrFormTerm 且恰一个 term、int 型 → `Dictionary<long, Entry>`(每次只求值该 term,**零字符串拼接**;覆盖约九成拼接名);其余 → `Dictionary<string, Entry>`(comparer = `Config.StrComper`,与 LabelDictionary 各字典一致)。
2. **缓存值**:CALL 系 = `CalledFunction` + `UserDefinedFunctionArgument`(ConvertArg 产物);GOTO 系 = `GotoLabelLine`;CALLFORMF 系 = `GetFunctionMethod` 返回的 `AExpression`(包同一 RowArgs,实参每次 GetValue 重求)。Entry 同时保留拼接结果 `LabelName` 供命中路径报错使用。
3. **正负均缓存,异常不缓存**:函数/标签不存在(null)在两次 ERB 重建之间稳定(函数表仅在重建时变化);event 函数 / #FUNCTION 误调等 throw 路径每次原样重抛。
4. **`RunERBFromMemory`(热重载)不读不写缓存**,与 CALLF/TRYCALLF 现行旁路一致(Instraction.Child.cs CALLF/TRYCALLF 的 DoInstruction)。
5. 原方案「key 用槽值(引用相等)」作废:语料中单变量形态为 0,且拼接形态每次产新 string,引用 key 命中率恒 0。

### 复用安全性依据

- `CalledFunction` 类注释:イベント関数を除いて実行中に内部状態は変化しないので使いまわしても良い(Process.CalledFunction.cs)。
- `UserDefinedFunctionArgument.SetTransporter` 每次 IntoFunction 重算实参 transporter,复用实例无残留。
- 静态 CALL 的 IsConst 分支已跨执行复用同一 `CalledFunction`/`UDFArgument`,动态缓存与其语义一致。
- `GetNonEventLabel`/`GetLabelDollar` 均为纯 TryGetValue;`CallFunction` 仅在 event 函数/#FUNCTION 误调时 throw。

## 验收

构建通过 + Emuera.Tests 缓存语义单测(形态判定 / long 键命中 / 内容键回退 / 负缓存 / StrComper 大小写)+ 全量测试套件;人工冒烟。

## Comments

### 2026-08-29

- 初版评估:方案「`FuncnameTerm.GetStrValue` 共 6 处、per-callsite、key 用槽值」。

### 2026-08-30

- 以大型 ERB 语料普查修正方案:单变量形态未出现 → 槽引用 key 作废;TRY 系约 68% 且目标稀疏定义 → 负缓存成为核心收益;拼接约九成单 int 插值 → 单整数键无拼接快路径纳入核心。详见背景与方案。
- 实施完成(分支 `perf/callform-cache`):`CallformCache`/`CallformEntry` 挂在 `SpCallArgment`/`SpCallFArgment`(Statements/Argument.cs),形态判定经 `StrForm.SingleIntFormTerm` 识别 `{int}` 插值(生产链中 `{…}` 被包为 FunctionMethodTerm(CurlyBrace),判定须解包,`FunctionMethodTerm` 为此新增 Method/Arguments internal 访问器);6 处调用点接入(CALL/GOTO/CALLF/TRYCALLF 于 Instraction.Child.cs,TRYCALLLIST/TRYGOTOLIST 于 Process.ScriptProc.cs),现有查表/错误处理保留为 miss 路径。构建 0 错误,测试套件 86/86(新增 5 例:long 键零拼接、裸单插值、负结果往返、多插值内容键、StrComper 大小写语义)。Status: ready-for-agent → ready-for-human;命中率计数补丁与人工冒烟留待验收。
- review 修订:`FunctionMethodTerm` 的私有字段改为 `{ get; }` 自动属性(构造后无赋值,编译器强制不可变,消除字段/访问器双命名);`longDic` 初始化改用空集合表达式 `[]`。
- 人工冒烟验收通过,关闭。Status: ready-for-human → 已完成。
