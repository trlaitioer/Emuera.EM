# UPDATECHECK 换用 HttpClient

Status: needs-triage
Type: task

## 背景

`UPDATECHECK_Instruction.DoInstruction`(`Statements/Instraction.Child.cs`,经 `FunctionIdentifier` 以 `FunctionCode.UPDATECHECK` 注册)用 `WebClient.OpenRead` 读取更新检查 URL 的前两行文本(新版本号、链接)。问题:

- WebClient 属 SYSLIB0014 弃用 API(Framework 时代网络栈);
- 同步网络 I/O 执行于脚本主线程(UI 线程),网络慢时冻结窗口;
- 失败与提前返回的部分路径未 `Dispose` WebClient 与 Stream。

## 方案

- 换 `HttpClient`:静态共享实例(避免每次调用新建连接池),`using`/try-finally 保证释放;
- 保持指令同步语义不变(阻塞等待与现状等价);async 化需指令层配合,超出本票范围;
- 行为保持:仅取前两行非空文本,RESULT 码与确认弹窗逻辑不变。

## 验收

构建通过 + UPDATECHECK 冒烟(无网络、URL 为空、版本一致/不一致)。

## Comments

### 2026-08-30

- 评估完成:问题定位与方案写入正文。Status: needs-triage。
