using System.Reflection;
using System.Text.RegularExpressions;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// 内建函数注册机制的闸门(backlog/issues/18)。注册表由 <see cref="BuiltinMethodAttribute"/> 扫描而来,
/// 四条闸门分别钉住「恰好注册一次」「无死类/无死注册」「名字不腐烂」「按名字分派的类覆盖到自己的每个名字」:
/// Gate A 管机制自洽(工厂数 = 注册数 = 名字去重数,且一名一实例——`Name` 参与行为分派,共享实例即错乱);
/// Gate B 管实现类与注册实例的全域对应(命名空间圈定,排除 OperatorMethod/StrForm 两族);
/// Gate C 是入库的黄金名单快照,任何改名/删名/加名都必须显式改快照;
/// Gate D 管 7 个 `switch (Name)` 类的 `case` 标签集合 == 该类的注册名集合(见该用例的注释)。
/// </summary>
public class BuiltinMethodRegistryTests
{
	public BuiltinMethodRegistryTests() => TestBootstrap.Initialize();

	// 266 / 183 的权威副本在 .scratch/arg-contract/builtin-contract.py(EXPECTED_NAMES / EXPECTED_CLASSES);
	// 这里各留一份是有意的:闸门要能在脚本被改坏时独立报警,而不是跟着脚本一起漂。
	private const int ExpectedNames = 266;      // == 改动前 Creator.cs 的 266 条活注册(issues/18 的 provenance)
	private const int ExpectedClasses = 183;    // 汇编里该命名空间下的具体 FunctionMethod 子类数(BuiltinMethod/ 目录内 186 处 `: FunctionMethod` 声明 - 3 处注释掉的)
	private const int ExpectedDispatchClasses = 7;   // 按运行期 `Name` 分派(`switch (Name)`)的类数
	private const int ExpectedDispatchCases = 22;    // 上述类 `case` 标签总数(权威副本在票面:7 个类、22 个 `case`)

	// ── Gate A:恰好注册一次 ────────────────────────────────────────────────────

	[Fact]
	public void GateA_EveryFactoryRegistersExactlyOneInstance()
	{
		List<(BuiltinMethodAttribute attr, MethodInfo factory)> definitions =
			[.. BuiltinMethodAttribute.GetDefinitions()];
		Dictionary<string, FunctionMethod> registry = FunctionMethodCreator.GetMethodList();

		// 工厂形态先查:带 attribute 的方法必须**全是**合法工厂,且这条要**先于**数量断言报错——注册层的扫描
		// 只取 static 方法(见 BuiltinMethodAttribute.FactoryFlags),「漏写 static 的工厂」在那里查不到,不查就
		// 等于静默丢弃该名字,而数量断言只能说「266 != 265」,点不出是哪个工厂。这里用 Static|Instance 全量扫一遍。
		// 可达边界:static 但形态非法的工厂在生产侧 CreateMethod 就抛 ExeEE(消息同样点名),经静态构造炸在
		// TestBootstrap 上,走不到这里;故这四条断言今天只对「漏写 static」这一形态报警,其余三条是生产侧判据的
		// 独立复核(注册层放宽判据而这里没跟上时才会红)。
		MethodInfo[] attributed = [.. typeof(FunctionMethod).Assembly.GetTypes()
			.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
				| BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			.Where(m => m.GetCustomAttributes<BuiltinMethodAttribute>(false).Any())];
		foreach (MethodInfo factory in attributed)
		{
			string where = $"{factory.DeclaringType?.Name}.{factory.Name}";
			Assert.True(factory.IsStatic, $"[BuiltinMethod] 工厂必须是 static:{where}");
			Assert.True(factory.GetParameters().Length == 0, $"[BuiltinMethod] 工厂必须无参:{where}");
			Assert.True(typeof(FunctionMethod).IsAssignableFrom(factory.ReturnType),
				$"[BuiltinMethod] 工厂必须返回 FunctionMethod 子类:{where}");
			Assert.False(factory.ReturnType.IsAbstract || factory.ReturnType.IsInterface,
				$"[BuiltinMethod] 工厂必须返回具体(非抽象/非接口)类型:{where}");
		}

		// 工厂数 == 注册数 == 名字去重数(注册层的 `seen`/`Add` 已经拦下重名,这里是全域复核)
		Assert.Equal(definitions.Count, registry.Count);
		Assert.Equal(ExpectedNames, definitions.Count);
		Assert.Equal(definitions.Count,
			definitions.Select(d => d.attr.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
		Assert.Equal(registry.Count, registry.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());

		// 属性名字集 == 注册表键集(序数排序后逐项比较)
		Assert.Equal(
			registry.Keys.OrderBy(k => k, StringComparer.Ordinal),
			definitions.Select(d => d.attr.Name).OrderBy(n => n, StringComparer.Ordinal));

		// 一名一实例:注册值两两引用不等
		Assert.Equal(registry.Count, registry.Values.Distinct().Count());

		foreach ((BuiltinMethodAttribute attr, MethodInfo factory) in definitions)
		{
			// 工厂的返回类型就是这个名字注册的实例类型,且每次调用都造新实例
			FunctionMethod registered = registry[attr.Name];
			Assert.Equal(factory.ReturnType, registered.GetType());
			Assert.NotSame(registered, factory.Invoke(null, null));
		}
	}

	// ── Gate B:无死类 / 无死注册 ───────────────────────────────────────────────

	[Fact]
	public void GateB_EveryCompiledBuiltinClassIsRegistered()
	{
		// 命名空间是天然分界:内建函数实现族在 `...GameData.Function`,而 OperatorMethod(运算符族)与
		// StrForm 的 FormattedStringMethod 族注册在别处、也不碰 `Name`,按命名空间圈定即排除。
		const string BuiltinNamespace = "MinorShift.Emuera.GameData.Function";
		List<Type> concrete = [.. typeof(FunctionMethod).Assembly.GetTypes()
			.Where(t => t.Namespace == BuiltinNamespace)
			.Where(t => t.IsClass && !t.IsAbstract && typeof(FunctionMethod).IsAssignableFrom(t))];

		// 反空跑:扫到的具体子类必须够 ExpectedClasses 这个量级——命名空间或子类判据写坏时先在这里红。
		// 为什么是 183 而 BuiltinMethod/ 目录里有 186 处 `: FunctionMethod` 声明:其中 3 处是注释掉的声明
		// (`GetRefMethod`、`GraphicsRotateMethod`、`GraphicsGetBrushMethod`,后者所在的 EE_失敗作 region 整段
		// 包在 `/** **/` 里),不进汇编(issues/18 实施结论)。故这里**不设白名单**——白名单里放一个永不出现的
		// 名字,只会掩盖真正的死类;它哪天被解注释,下面的差集断言就会红,那正是要发现的东西。
		Assert.True(concrete.Count >= ExpectedClasses,
			$"命名空间 {BuiltinNamespace} 下只扫到 {concrete.Count} 个具体 FunctionMethod 子类(期望 ≥ {ExpectedClasses})");

		HashSet<Type> instantiated = [.. FunctionMethodCreator.GetMethodList().Values.Select(m => m.GetType())];
		// 反向:每个注册实例的类型都必须落在这个集合内
		Assert.Empty(instantiated.Except(concrete));
		// 正向:无死类——全部 183 个都被注册
		Assert.Empty(concrete.Except(instantiated));
	}

	// ── Gate C:名字不腐烂 ─────────────────────────────────────────────────────

	[Fact]
	public void GateC_RegistryNamesMatchTheCommittedSnapshot()
	{
		string path = Path.Combine(AppContext.BaseDirectory,
			"Runtime", "Script", "Statements", "Function", "BuiltinMethodNames.snapshot.txt");
		Assert.True(File.Exists(path), $"黄金名单快照没随测试产物拷出来:{path}");
		string[] snapshot = [.. File.ReadAllText(path)
			.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.TrimEnd('\r'))];

		Assert.Equal(ExpectedNames, snapshot.Length);
		// 比对前排序:注册表自身的枚举序**不是**契约(它是反射扫描序,唯一可观测点是 OverWriteSystemFuncWarn 的
		// 打印序,该顺序无意义),这里只钉「键集 == 黄金名单」。
		Assert.Equal(snapshot, FunctionMethodCreator.GetMethodList().Keys.OrderBy(k => k, StringComparer.Ordinal));
	}

	// ── Gate D:按名字分派的类,`case` 标签集合 == 该类注册名集合 ────────────────────

	[Fact]
	public void GateD_NameDispatchSwitchesCoverExactlyTheRegisteredNames()
	{
		// 7 个类按运行期 `Name` 分派行为(注册名与 `switch (Name)` 在多名字类里一一对应)。名字一旦改名、
		// 或给这些类添了新的注册名而 switch 没跟上,该名字会被 switch 漏掉、落到 switch 之后的
		// `throw new ExeEE(…AbnormalBranch…)`——那是运行到该函数才炸的路径,而 Gate A/B/C 只看得到注册表,
		// 看不到 switch,故这里把两侧的集合相等钉住。源码随测试产物拷出(见 csproj 的 Content),
		// 闸门因此不依赖工作目录。
		string dir = Path.Combine(AppContext.BaseDirectory, "BuiltinMethodSources");
		Assert.True(Directory.Exists(dir), $"内建函数源码没随测试产物拷出来:{dir}");

		Dictionary<string, List<string>> caseLabels = new(StringComparer.Ordinal);
		foreach (string file in Directory.GetFiles(dir, "*.cs").OrderBy(p => p, StringComparer.Ordinal))
		{
			string text = File.ReadAllText(file);
			foreach (Match type in Regex.Matches(text, @"sealed class (\w+) : FunctionMethod\s*\{"))
			{
				string body = BracedBody(text, type.Index + type.Length - 1);
				foreach (Match dispatch in Regex.Matches(body, @"switch \(Name\)\s*\{"))
				{
					string typeName = type.Groups[1].Value;
					Assert.False(caseLabels.ContainsKey(typeName), $"{typeName} 有多个 switch (Name)");
					string switchBody = BracedBody(body, dispatch.Index + dispatch.Length - 1);
					caseLabels[typeName] = [.. Regex.Matches(switchBody, @"case ""([A-Z0-9_]+)"":")
						.Select(m => m.Groups[1].Value)];
				}
			}
		}

		// 反空跑:类声明形态或 switch 写法一变,上面的解析可能一个标签都收不到;这里先红,
		// 免得下面的集合相等在两个空集上空过。
		Assert.Equal(ExpectedDispatchClasses, caseLabels.Count);
		Assert.Equal(ExpectedDispatchCases, caseLabels.Values.Sum(labels => labels.Count));

		Dictionary<string, HashSet<string>> registered = new(StringComparer.Ordinal);
		foreach ((string name, FunctionMethod method) in FunctionMethodCreator.GetMethodList())
		{
			string typeName = method.GetType().Name;
			if (!registered.TryGetValue(typeName, out HashSet<string>? names))
			{
				registered[typeName] = names = new HashSet<string>(StringComparer.Ordinal);
			}

			names.Add(name);
		}

		foreach ((string typeName, List<string> labels) in caseLabels)
		{
			Assert.True(registered.ContainsKey(typeName), $"{typeName} 没有注册实例");
			Assert.Equal(registered[typeName].OrderBy(n => n, StringComparer.Ordinal),
				labels.Distinct().OrderBy(n => n, StringComparer.Ordinal));
		}
	}

	/// <summary>取 <paramref name="openBrace"/> 处 `{` 配对到 `}` 的整段(含两端)。</summary>
	private static string BracedBody(string text, int openBrace)
	{
		int depth = 0;
		for (int i = openBrace; i < text.Length; i++)
		{
			if (text[i] == '{')
			{
				depth++;
			}
			else if (text[i] == '}' && --depth == 0)
			{
				return text[openBrace..(i + 1)];
			}
		}

		throw new InvalidOperationException("花括号不配对,闸门的源码解析需要跟着源码形态更新");
	}
}
