using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// ENUMFILES 的目录参:游戏根要用 ".";空名一直不可用——旧 Utils.GetValidPath 的
/// Path.GetPathRoot("") 在 .NET 上返回 null,空名没进过拼接分支,故这不是迁移引入的回归。
/// 返回的文件名相对游戏根。exm 用 TestBootstrap 的最小夹具(该入口只用到 VEvaluator.RESULTS_ARRAY)。
/// </summary>
public class EnumFilesMethodTests
{
	[Fact]
	public void Enumfiles_EmptyOrBlankDirectoryName_IsRejected()
	{
		string root = GameRootScope.UseTempRoot("enumfiles-empty-name");
		TestBootstrap.Initialize();
		File.WriteAllText(Path.Combine(root, "a.erb"), "");

		Assert.Equal(-1L, EnumFiles("", "*.erb"));
		Assert.Equal(-1L, EnumFiles("   ", "*.erb"));
	}

	[Fact]
	public void Enumfiles_DotDirectoryName_EnumeratesTheGameRoot()
	{
		string root = GameRootScope.UseTempRoot("enumfiles-dot-name");
		TestBootstrap.Initialize();
		File.WriteAllText(Path.Combine(root, "a.erb"), "");

		Assert.Equal(1L, EnumFiles(".", "*.erb"));
		Assert.Equal("a.erb", GlobalStatic.VEvaluator.RESULTS_ARRAY[0]);
	}

	private static long EnumFiles(string directory, string pattern)
		=> (long)Invoke(FunctionMethodCreator.GetMethodList()["ENUMFILES"],
			[new SingleStrTerm(directory), new SingleStrTerm(pattern), new SingleLongTerm(0)]);
}
