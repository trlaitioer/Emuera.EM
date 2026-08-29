# Nullable:进程与加载链启用

Status: needs-triage
Type: task

## 背景

进程与加载是总装层:Process 5 个 partial 文件经 GlobalStatic 调度 ErbLoader/ErhLoader 的解析产物(LogicalLine 家族)与符号表(LabelDictionary/IdentifierDictionary),运行期消费全部上游簇产物。相对自洽:不直接依赖 UI(经 GlobalStatic.Console),链内可解决大部分警告。

## 范围(启用 + 清零 CS86xx)

- `Runtime/Script/`:Process.cs、Process.ScriptProc.cs、Process.State.cs、Process.CalledFunction.cs、Process.SystemProc.cs、GlobalStatic.cs
- `Runtime/Script/Loader/`:ErbLoader.cs、ErhLoader.cs
- `Runtime/Script/Data/`:LabelDictionary.cs、GameBase.cs
- `Runtime/InputRequest.cs`(输入请求,Process 与 UI 的边界对象;归属以实测警告为准)

## 前置契约

- issues/07:LogicalLine 家族(InstructionLine/FunctionLabelLine 等)
- issues/10:IdentifierDictionary/VariableData
- issues/06:GlobalStatic 字段契约(Reset 置 null 后按非空用的统一决策)

## 方案

按 spec.md 通用方法逐文件启用。`CalledFunction.ConvertArg`(实参 VariableTerm)与 Process.ScriptProc 的 `GetVariableToken` 消费点按上游契约适配。

## 备注

- 旧 issues/01(核心运行时,已关闭)的剩余范围即本票主体;StrForm 修订历史与"关联模块"清单见该票
- 建议在 07/10 之后执行

## Comments

### 2026-08-29

- 新建:按调用链重切差分,原 issues/01 剩余范围(Process*、ErbLoader)与 issues/04 的 GameBase、LabelDictionary、ErhLoader、GlobalStatic 归入本票。
