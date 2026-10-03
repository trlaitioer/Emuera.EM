using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using System.Reflection;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// SAVETEXT 的落点:名字不带可用扩展名时补 .txt,补的是同名文件而不是同名目录。
/// 经 FunctionMethodCreator 取注册表实例,参数用 SingleStrTerm,不依赖 exm。
/// </summary>
public class SaveTextMethodTests
{
	[Fact]
	public void Savetext_WithoutExtension_WritesTxtNextToTheName()
	{
		string root = GameRootScope.UseTempRoot("savetext-no-extension");
		EnsureValidExtension();

		long result = SaveText("body", "memo");

		Assert.Equal(1L, result);
		Assert.Equal("body", File.ReadAllText(Path.Combine(root, "memo.txt")));
		Assert.False(Directory.Exists(Path.Combine(root, "memo")));
	}

	[Fact]
	public void Savetext_UnsupportedExtension_ReplacesItInTheSameDirectory()
	{
		string root = GameRootScope.UseTempRoot("savetext-unsupported-extension");
		EnsureValidExtension();

		long result = SaveText("body", @"sub\note.log");

		Assert.Equal(1L, result);
		Assert.Equal("body", File.ReadAllText(Path.Combine(root, "sub", "note.txt")));
		Assert.False(Directory.Exists(Path.Combine(root, "sub", "note.log")));
	}

	[Fact]
	public void Savetext_AllowedExtension_KeepsTheName()
	{
		string root = GameRootScope.UseTempRoot("savetext-allowed-extension");
		EnsureValidExtension();

		Assert.Equal(1L, SaveText("body", @"sub\note.txt"));
		Assert.Equal("body", File.ReadAllText(Path.Combine(root, "sub", "note.txt")));
	}

	[Fact]
	public void Savetext_EscapingName_WritesNothing()
	{
		string root = GameRootScope.UseTempRoot("savetext-escaping-name");
		EnsureValidExtension();

		Assert.Equal(0L, SaveText("body", @"..\evil"));
		Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(root)!, "evil.txt")));
	}

	[Fact]
	public void Savetext_NameResolvingToTheGameRoot_ReturnsZero()
	{
		string root = GameRootScope.UseTempRoot("savetext-game-root");
		EnsureValidExtension();

		// 三种写法词法折叠后都是游戏根:没有文件名可补 .txt,按失败返回,
		// 不能在 GamePath.ChangeExtension 的越界校验上抛出并中断脚本。
		Assert.Equal(0L, SaveText("body", "."));
		Assert.Equal(0L, SaveText("body", @"erb\.."));
		Assert.Equal(0L, SaveText("body", Path.Combine("..", Path.GetFileName(root))));
		Assert.Empty(Directory.GetFiles(root));
		Assert.False(File.Exists(root + ".txt"));
	}

	private static long SaveText(string text, string filename)
		=> (long)InvokeWith(FunctionMethodCreator.GetMethodList()["SAVETEXT"],
			[new SingleStrTerm(text), new SingleStrTerm(filename)], null);

	/// <summary>
	/// SAVETEXT 读 Config.ValidExtension,测试不加载配置文件,故按仓库惯例(见 StringMeasureTests)只赋这一个静态:
	/// Config.SetConfig 会连带改写 IgnoreCase 等其它测试依赖的静态。
	/// </summary>
	private static void EnsureValidExtension()
	{
		if (Config.ValidExtension != null)
		{
			return;
		}

		PropertyInfo property = typeof(Config).GetProperty(nameof(Config.ValidExtension), BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("Config.ValidExtension not found");
		property.SetValue(null, new List<string> { "txt" });
	}
}
