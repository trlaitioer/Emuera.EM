---
id: dotnet-modernization-01
status: implemented
---

# UPDATECHECK 换用 HttpClient

## 任务

`UPDATECHECK_Instruction.DoInstruction`(`Statements/Instraction.Child.cs`,经 `FunctionIdentifier` 以 `FunctionCode.UPDATECHECK` 注册)用 `WebClient.OpenRead` 读取更新检查 URL 的前两行文本(新版本号、链接)。问题:

- WebClient 属 SYSLIB0014 弃用 API(Framework 时代网络栈);
- 同步网络 I/O 执行于脚本主线程(UI 线程),网络慢时冻结窗口;
- 失败与提前返回的部分路径未 `Dispose` WebClient 与 Stream。

完成标准:构建通过 + UPDATECHECK 冒烟(无网络、URL 为空、版本一致/不一致)。

## 方案

- 换 `HttpClient`:每次调用新建,`using` 保证释放(与原 WebClient 生命周期一致,URL 为空的提前返回路径不创建任何对象);
- 保持指令同步语义不变(阻塞等待与现状等价);async 化需指令层配合,超出本票范围;
- 行为保持:仅读取响应的前两行文本,任一为空/缺失则 RESULT=3(不跳过空行);RESULT 码与确认弹窗逻辑不变;
- HttpClient 不像 WebClient.OpenRead 那样对非 2xx 响应抛异常,需显式 `EnsureSuccessStatusCode()`,否则错误页正文会被当作版本号读入并可能误弹"新版本"确认框。

## 曾考虑的替代方案

1. 静态共享 `HttpClient` 实例(含 `field ??=` 惰性初始化变体):UPDATECHECK 低频且各次调用间隔远超空闲连接存活时间(约 1 分钟自动关闭),连接复用无实际收益;共享实例所防御的 socket 耗尽仅在高频请求下成立,低频且及时释放的场景不存在,反而引入常驻状态与"脚本单线程、指令不重入"的隐含前提。

## 影响

UPDATECHECK 指令外部行为不变;网络栈从 WebClient 切至按次创建的 `HttpClient`。
