# 内建函数注册表在静态构造里建完，启动期失败只能以 TypeInitializationException 呈现

Status: needs-triage

## 任务

[issues/18](18-builtin-method-registration.md) 的「一个已知边界」：`FunctionMethodCreator` 的静态构造（`Emuera/Runtime/Script/Statements/Function/Creator.cs`）里扫 `[BuiltinMethod]` 建表，任何逃出去的异常都会被 CLR 包成 `TypeInitializationException`（该一层，再经 `IdentifierDictionary.cs` 一层），而 `Process.cs` 的报错路径只打印 `type + Message + StackTrace`、**不打印 `InnerException`** ⇒ 启动期失败的可读性不能靠异常消息，目前完全依赖闸门用例在编译/测试期暴露。

完成标准（待评估后细化）：

- 建表改为显式初始化调用（例如 `FunctionMethodCreator.Initialize()`），由启动链在建表必需点之前调用一次
- 失败不再被 CLR 包装：`Process` 的 `handleException` 的 `ExeEE`/`CodeEE` 分支可用，游戏内能看到点名到 `类型名.方法名` 的可读原因
- 生产 3 处 `GetMethodList()` 调用点（`Process.CalledFunction.cs`、`IdentifierDictionary.cs`、`FunctionIdentifier.cs`）与测试侧 20 处都在「初始化已完成」之后；测试初始化（`TestBootstrap`）补上这一次调用
- 「首次查找前注册表已建完」这条不变量以新形式保住（「初始化调用先于首次查找」），并有闸门/用例兜住漏调

## 方案

待评估。会改动 18 号票「静态构造内建完」的不变量，故 18 号票当时不动它。

## 影响

- 改动面：`Creator.cs`、启动链（`Process` / `IdentifierDictionary`）、`Emuera.Tests` 的 `TestBootstrap` 与全部 `GetMethodList()` 调用点
- 与 [issues/18](18-builtin-method-registration.md) 的分工：18 号票只保证「非法工厂在测试期必红」，本票解决「游戏内能否看到原因」；若不做，就须承认启动期诊断只有测试期一条路径
