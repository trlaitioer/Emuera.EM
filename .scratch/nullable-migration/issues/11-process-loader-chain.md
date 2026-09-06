# Nullable:进程与加载链启用

Status: needs-triage

## 任务

进程与加载是总装层:Process 5 个 partial 文件经 GlobalStatic 调度 ErbLoader/ErhLoader 的解析产物(LogicalLine 家族)与符号表(LabelDictionary/IdentifierDictionary),运行期消费全部上游簇产物。相对自洽:不直接依赖 UI(经 GlobalStatic.Console),链内可解决大部分警告。

范围(启用 + 清零 CS86xx):

- `Runtime/Script/`:Process.cs、Process.ScriptProc.cs、Process.State.cs、Process.CalledFunction.cs、Process.SystemProc.cs、GlobalStatic.cs
- `Runtime/Script/Loader/`:ErbLoader.cs、ErhLoader.cs
- `Runtime/Script/Data/`:LabelDictionary.cs、GameBase.cs
- `Runtime/InputRequest.cs`(输入请求,Process 与 UI 的边界对象;归属以实测警告为准)

完成标准:范围内文件 `#nullable enable` 且 CS86xx 清零;为本链警告修改票外文件签名时,修改内容记录在票内正文。

## 方案

按 spec.md 通用方法逐文件启用。`CalledFunction.ConvertArg`(实参 VariableTerm)与 Process.ScriptProc 的 `GetVariableToken` 消费点按上游契约适配。

### 前置契约

- issues/07:LogicalLine 家族(InstructionLine/FunctionLabelLine 等)
- issues/10:IdentifierDictionary/VariableData
- issues/06:GlobalStatic 字段契约(Reset 置 null 后按非空用的统一决策)

执行顺序:建议在 07/10 之后执行;旧 issues/01(superseded)的剩余范围即本票主体,StrForm 修订历史与「关联模块」清单见该票。

## 影响

进程与加载链为总装层,其契约收敛后 issues/03 的 csproj 收尾仅剩 UI 与外围(issues/12)依赖。
