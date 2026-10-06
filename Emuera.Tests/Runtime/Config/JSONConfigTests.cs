using MinorShift.Emuera.Runtime.Config.JSON;
using Xunit;

// 本目录的用例统一用 Emuera.Tests.Runtime:命名空间若叫 ...Runtime.Config 会把产品类型 Config 遮蔽掉
namespace Emuera.Tests.Runtime;

/// <summary>
/// JSONConfig.Load 的坏输入路径(nullable-migration-28 第 2 处):「解析失败就返回 null」是既有契约,
/// 而 Data 被各处直接读取(静态构造也读它),故坏输入的处置必须落在 Load 内。
/// </summary>
public class JSONConfigTests
{
	public JSONConfigTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>setting.json 的内容是 JSON 字面量 null:不得让 Data 变 null 后到处空引用。</summary>
	[Fact]
	public void Load_WithNullContent_FallsBackToDefaultsAndRewritesTheFile()
	{
		string root = GameRootScope.UseTempRoot("jsonnull");
		string path = Path.Combine(root, "setting.json");
		File.WriteAllText(path, "null");

		// Data 是进程级静态,且 TestBootstrap 在其中打开了 UseScopedVariableInstruction;
		// 本用例会把整个实例换成默认值,必须还原,否则后续用例的 VARI/VARS 注册会按执行顺序失效
		JSONConfigData original = JSONConfig.Data;
		try
		{
			JSONConfig.Load();

			Assert.NotNull(JSONConfig.Data);
			Assert.NotEqual("null", File.ReadAllText(path));
		}
		finally
		{
			JSONConfig.Data = original;
		}
	}
}
