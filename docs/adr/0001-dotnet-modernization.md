# 按需采用 C# 新特性和 .NET API

`LangVersion` 随目标框架使用默认值。修改具体代码时可按可读性和兼容性采用新语法。机械替换旧写法会扩大评审范围，且使与上游同步更困难。

假名与全半角转换使用 `Strings.StrConv`，以保留 Windows NLS 的日文处理行为。现有输入、计时和 WebP 接口沿用 `DllImport`，这些声明以可直接封送的类型为主，原生结构体布局仍由调用方与原生库共同约定。源生成封送的收益有限，插件动态加载和 WMP COM 当前也依赖运行时支持。

小写别名保持上游命名，以减少同步冲突。`CS8981` 警告保留。

原议题 `dotnet-modernization-04`、`dotnet-modernization-05`、`dotnet-modernization-06`、`performance-optimization-06`，见[旧 tracker 归档](../legacy-tracker.md)。
