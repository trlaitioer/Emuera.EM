using Xunit.Sdk;
using Xunit.v3;

// Emuera 解释器核心存在大量共享静态状态(LexicalAnalyzer.UseMacro、Config、GlobalStatic 等),
// 测试类之间并行执行会相互干扰(如 UseMacro=true 时 GlobalStatic.IdentifierDictionary 为 null 导致 NRE)。
// 关闭程序集级并行化,保证测试串行执行。
// xUnit v3 4.0 起 CollectionBehavior(DisableTestParallelization) 是编译错误(CS0619),改用 Parallelization。
[assembly: Parallelization(Mode = ParallelMode.None)]
