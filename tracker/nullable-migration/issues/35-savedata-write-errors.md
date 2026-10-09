# 存档写入侧的错误分级与「写失败毁旧档」

id: nullable-migration-35
Status: in-progress
Type: bug

## 任务

`nullable-migration-30` 的存档侧残留（装载侧已分级，写入侧未覆盖，`tracker/nullable-migration/spec.md` 记作「写入侧 18 处 `EraDataWriter` 仍欠一票」）正式落票。复核该缺口的成因时另发现同一组写入方法上的一个数据保全缺陷，与「失败原因被吞掉」同根因（写入失败的处置缺失），故合并为本票。

### 1. 失败原因被吞掉，三个调用点各打一句「予期しないエラー」

- `VariableEvaluator.SaveTo` 以 `catch (Exception) { return false; }` 收尾，原因被丢弃
- 三个调用点只知道成功/失败：`SAVEDATA`（`Process.ScriptProc.cs`）、手动存档与自动存档（`Process.SystemProc.cs` 的 `endCallSaveInfo`/`endAutoSaveCallSaveInfo`），分别打 `UnexpectedErrorInSavedata`/`UnexpectedSaveError`/`AutoSaveError1`
- 即 `nullable-migration-30` 判出的「写入侧仍是旧观感」：玩家拿到「予期しないエラー」，拿不到「是磁盘满了还是文件被锁」

### 2. 写失败会毁掉原有存档并留下半截文件

- `SaveTo` 用 `new FileStream(filepath, FileMode.Create, FileAccess.Write)` 打开目标档：`FileMode.Create` 在写入第一个字节之前就把文件截断。任何写入期失败（磁盘满、编码失败、介质故障）都让旧存档消失，同时留下一个半截文件
- 半截文件不是无关产物：它正是读取侧要报成损坏存档的输入（`nullable-migration-34` 第 1 组），也就是说写入侧的失败会自己制造读取侧的缺陷样本
- 同一写法另见于 `SaveChara`、`SaveVariable`、`SaveGlobal`（`SaveVariable` 经 `SAVEVAR` 指令为 `NotImplCodeEE`，今天不可达）

完成标准：写入失败的玩家可见文本带上具体原因（三条「予期しない」抬头合并为一条 + 原因行）；四条写入路径的写失败都不再毁掉既有存档，也不留下半截文件；新增路径在 `Emuera.Tests` 有能分辨旧行为的用例。

## 方案

1. 抽 `Runtime/Utils/TempFileWriter.cs`：写入先落到 `filepath + ".tmp"`（同目录，保证移动不跨卷），`write` 回调返回后 `File.Move(..., overwrite: true)` 替换目标；失败时删临时文件并原样抛。目标是「新内容落盘之前不动目标档」
2. 写入方法接入该类，并把失败原因带回调用点：
   - `SaveTo(int, string, [NotNullWhen(false)] out string? errorReason)` 保持不抛的契约（自动存档的调用点有状态机要继续），路径解析也放进自己的 try——它读 `Config.SavDir`，只有初始化异常的进程才会为空，但既然承诺不抛就不能留这个出口
   - 抽出 `SaveToFile(string filepath, string saveText, out string? errorReason)`：原 `SaveTo` 的目标路径取自 `Config.SavDir`，测试无法把它指到别处（`SavDir` 只有 private setter），而这条链此前完全没有测试
   - `SaveChara`/`SaveVariable` 的失败包成 `FileEE`（归入存档档，不再以裸 BCL 异常落 `Unexpected` + CLR 堆栈）；`SaveGlobal` 保留既有 `CodeEE(ErrorSavingGlobalData)`
3. `ErrorReport.Reason(Exception)`：`EmueraException` 取消息，其余带 CLR 类型名（这行不打堆栈，类型名是区分「存档坏了」与「介质故障」的唯一线索）
4. 三个调用点打「抬头 + 原因行」，抬头统一为新增的 `Error.FailedToSaveData`；自动存档另有 `AutoSaveError2`（跳过提示）保留
5. Lang 三处同步（`Lang.cs`、`emuera-eng.xml`、`emuera-zhs.xml`）：新增 `Error.FailedToSaveData`；删除被取代、改动后不再被引用的 `UnexpectedErrorInSavedata`、`AutoSaveError1`、`UnexpectedSaveError`

## 实施结果(2026-10-07)

落地内容即上述方案，实施中确认的边界：

- `TempFileWriter` 的回调拥有流（`EraDataWriter.Close()`/`EraBinaryDataWriter` 的 `Dispose` 会关掉底层 `FileStream`），故该类在回调返回后不再触碰流；`using` 对已关闭的流是幂等的
- 读写两侧的失败语义按 `nullable-migration-34` 的裁定（保留返回值契约 + 打出可读错误）分工：写入侧把原因回传由调用点打印；`SaveGlobal` 的失败文案未带原因（保留既有 `ErrorSavingGlobalData`），属本票未纳入的一处
- `SaveChara`/`SaveVariable` 的 catch 只包非 `CodeEE` 的异常：`VariableToken.GetArray()` 对空引用变量抛 `CodeEE(EmptyRefVar)`，那是脚本自身的错误，按分级语义应留在 Code 档并保留该档打出的脚本侧调用栈（方案第 2 条只写「失败包成 `FileEE`」，此处按 `ErrorReport` 的档位收窄）
- 目标档为只读文件时新旧写法都失败（旧：`FileMode.Create` 打不开；新：`File.Move` 拒绝覆盖），行为一致，`DelData` 的只读检查不受影响

用例（`Emuera.Tests`，本票 7 条：`TempFileWriterTests` 3 + `SaveDataWriteTests` 4）：

- 新增 `Runtime/Utils/TempFileWriterTests.cs` 3 条：写入期失败保留目标且不留临时文件、成功替换、目标不存在时创建
- 新增 `Runtime/Script/Statements/Variable/SaveDataWriteTests.cs` 4 条（其中 `SaveChara` 的写入失败归存档档 1 条为评审修订追加）：目标不可写时带回原因、写入无法开始时保留已有存档、成功路径替换且不留临时文件、`SaveChara` 的写入失败抛 `FileEE`
- 旧行为复测（把 `TempFileWriter` 临时换回「直接写目标档」）：`TempFileWriterTests.Write_WhenTheWriteFails_KeepsTheTargetAndDropsTheTemporaryFile` 与 `SaveDataWriteTests.SaveToFile_WhenTheWriteCannotStart_KeepsTheExistingSave` **2 条红**，与「旧写法截断目标档」的判定一一对应；把 `SaveChara` 的 catch 临时换回原样抛出 → `SaveDataWriteTests.SaveChara_WhenTheWriteCannotStart_ThrowsASaveDataError` **红**

验证：`Debug-NAudio` 全量重建 0 错误、CS86xx 0 条；`Debug`(WMP，amd64 `MSBuild.exe` + 清空 `*_PROXY`) 0 错误、CS86xx 0 条；`Emuera.Tests` **804** 全绿；后缀断言计数不变（`Emuera` 799 / 残差 266，本票未增删断言）

### 评审修订(2026-10-09)

评审判出四处，a1/a2/c1 已落地，c2 待裁定（落地后本节与状态一并收尾）：

- **a1 路径解析不设防（已修）**：`LoadGlobal` 与 `SaveGlobal` 的 `getSaveDataPathG()` 都在任何 try 之外。方案第 2 条只把这条规则写给了 `SaveTo`（「路径解析也放进自己的 try」），本批推广到这两个入口——`LoadGlobal` 的失败走 `FailWith`，`SaveGlobal` 的解析移进它自己的 try（原 `catch (SystemException)` 接不住 NRE）
- **a2 `SaveGlobal` 的档位与原因（已修）**：原 `catch (SystemException) { throw new CodeEE(ErrorSavingGlobalData); }` 有两处问题——不带原因，且 `IOException` 是 `SystemException` 的子类，磁盘满、文件被锁、`File.Move` 被拒都会落 Code 档（「脚本错误」抬头 + 出错脚本行 + eraBasic 函数调用栈），即介质故障被报成脚本自身的错误。改为 `catch (Exception e) { throw new FileEE($"{ErrorSavingGlobalData} ({Reason(e)})"); }`：抬头文案保留、附原因、归存档档。方案第 3 条「wherever it is reported」原先比字面窄，至此覆盖
- **c1 `LoadChara` 的 catch 比注释宽（已收窄）**：`catch (EmueraException e)` 也吞 `CodeEE`，与 `SaveChara`/`SaveVariable` 的 `when (e is not CodeEE)` 相反；改为同一判据，注释改成准确说法。**复核发现该路径今天不产生 `CodeEE`**：`CharacterData.LoadFromStreamBinary` 写的是 `dataIntegerArray[codeInt]`/`UserDefCVarDataList[...]` 这类现成数组，不调 `GetArray()`，故这一处是「判据统一 + 注释诚实」，不是修活缺陷，也因此没有对应用例（无法构造）
- **c2 `SaveToFile` 的 catch 不过滤（待裁定）**：原判「`SaveToStreamBinary` 经 `GetArray()` 抛 `CodeEE(EmptyRefVar)` 被吞」——**该前提经复核不成立**：`ReferenceToken` 只由 `CreatePrivateVariable` 的 `data.Reference` 分支产生，而它不往任何存档列表注册（`userDefinedStaticVarList`/`userDefinedSaveVarList`/`userDefinedGlobalVarList` 只由 `CreateUserDefVariable` 填，那里不产参照型），`CharacterData.SaveToStreamBinary` 写的是 `UserDefCVarDataList[...]` 数组。故方案第 40 行那条规则唯一有实际主体的地方是 `SAVEVAR`（`NotImplCodeEE`；实现后其实参可来自脚本、才可能拿到参照型），`SAVEDATA`/`SaveChara` 上的 `CodeEE` 过滤器是策略声明而非活分支。裁定待定：选「过滤 + 两个系统流程调用点兜住」（`SaveToFile` 过滤后若不在 `endCallSaveInfo`/`endAutoSaveCallSaveInfo` 兜住，自动存档遇异常会中止 `DoScript()`）还是「维持宽 catch，只把规则的适用范围写清楚」

用例新增 2 条（本批）：`SaveDataWriteTests.SaveGlobal_WhenTheWriteFails_ThrowsASaveDataError`、`VariableEvaluatorLoadTests.LoadGlobal_WhenThePathCannotBeResolved_ReturnsFalse`；两条都做了旧行为复测（把路径守卫换回不设防、`SaveGlobal` 换回 `CodeEE` → 2 条红）。验证：`Debug-NAudio` 0 错误、97 条既有分析器警告（无 CS86xx）；`Emuera.Tests` 804 → **806** 全绿。

## 曾考虑的替代方案

1. `SaveTo` 改为抛 `FileEE`（与 `SaveChara` 一致）：放弃——自动存档的调用点在失败后还要打「跳过」提示并继续状态机（`endAutoSaveCallSaveInfo` 之后是 `endAutoSave()`），异常会跳过这些步骤；「不抛」是这条链的既有前提，故用 `[NotNullWhen(false)] out string?` 报原因
2. 只补原因、不改写入形式（保留 `FileMode.Create`）：放弃——「原因不明」与「旧档被毁」是同一个失败的后果，只报原因等于让玩家知道存档写坏了却没能保住它
3. 把 `TempFileWriter` 做成 `VariableEvaluator` 的私有静态方法：可行，但这条不变量（新内容落盘之前不动目标档）不属于变量求值，四条写入路径共用一个命名出口更易在评审中认出，且独立成类后可以直接单测

## 影响

- 玩家可观测：写入失败时看到「存档数据写入失败」+ 具体原因，且**旧存档不再被毁**；写失败也不再留下半截存档（读取侧那类坏输入的来源之一随之减少）
- 行为变更须标注：`SAVECHARA` 的失败由「裸 BCL 异常 → `Unexpected` 档 + CLR 堆栈」改为「`FileEE` → 存档档、只打消息」（`CodeEE` 除外，它是脚本自身的错误，保持 Code 档与该档打出的脚本侧调用栈）；`SAVEDATA`/手动存档/自动存档的失败抬头文案由三条「予期しない」合并为一条 `FailedToSaveData`
- Lang 键的删除是对外可见面（外部语言包按 id 覆盖）：`UnexpectedErrorInSavedata`/`AutoSaveError1`/`UnexpectedSaveError` 三个 id 消失，其译文条目在语言包中成为无效条目（不报错）
- 2026-10-09 追加的行为变更：`SAVEGLOBAL` 的失败由「Code 档 + 出错脚本行 + 脚本侧调用栈 + 一句 `ErrorSavingGlobalData`」改为「存档档 + 只打消息 + 抬头带原因」——原写法把 `IOException`（`SystemException` 的子类，即磁盘满、文件被锁这类介质故障）误分到 Code 档；`SAVEDATA` 路径上的 `CodeEE` 过滤器（`SaveChara`/`SaveVariable` 已有、`LoadChara` 本批补齐）今天都无活分支，其唯一有实际主体的位置是 `SAVEVAR`（见「评审修订」的 c2）
- 本票不动 `Process.handleException`/`handleExceptionInSystemProc` 的分级（`nullable-migration-30` 的成果），也不动读取侧剩余出口（`nullable-migration-34`）
