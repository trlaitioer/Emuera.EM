using MinorShift.Emuera.Runtime.Config;
using Xunit;

// 命名空间不能取 Emuera.Tests.Runtime.Config:那会让同项目其它文件里的 Config 解析成命名空间而非类型。
namespace Emuera.Tests.Runtime;

/// <summary>
/// GetFiles 的 Key 是「相对搜索根的目录 + 文件名」。根的判定必须按整段比较：rootdir 不带尾分隔符时,
/// 字符串前缀会把兄弟目录 erb2 当成 erb 的子目录并截错。
/// </summary>
public class ConfigGetFilesTests
{
	[Fact]
	public void Getfiles_SubdirectoryBelowTheRoot_PrefixesTheKeyWithTheRelativeDirectory()
	{
		string root = GameRootScope.UseTempRoot("getfiles-subdirectory");
		string erb = Path.Combine(root, "erb");
		string sub = Path.Combine(erb, "sub");
		Directory.CreateDirectory(sub);
		File.WriteAllText(Path.Combine(sub, "a.erb"), "");

		KeyValuePair<string, string> file = Assert.Single(Config.GetFiles(sub, erb, "*.erb"));

		Assert.Equal(@"sub\a.erb", file.Key);
		Assert.Equal(Path.Combine(sub, "a.erb"), file.Value);
	}

	[Fact]
	public void Getfiles_SearchRootItself_KeepsTheBareName()
	{
		string root = GameRootScope.UseTempRoot("getfiles-root");
		string erb = Path.Combine(root, "erb");
		Directory.CreateDirectory(erb);
		File.WriteAllText(Path.Combine(erb, "a.erb"), "");

		Assert.Equal("a.erb", Assert.Single(Config.GetFiles(erb, "*.erb")).Key);
	}

	[Fact]
	public void Getfiles_DirectoryDifferingOnlyInCase_IsStillBelowTheRoot()
	{
		string root = GameRootScope.UseTempRoot("getfiles-case");
		string erb = Path.Combine(root, "erb");
		string sub = Path.Combine(erb, "sub");
		Directory.CreateDirectory(sub);
		File.WriteAllText(Path.Combine(sub, "a.erb"), "");

		Assert.Equal(@"sub\a.erb", Assert.Single(Config.GetFiles(sub, erb.ToUpperInvariant(), "*.erb")).Key);
	}

	[Fact]
	public void Getfiles_DirectoryOutsideTheSearchRoot_KeepsItsOwnPathAsThePrefix()
	{
		string root = GameRootScope.UseTempRoot("getfiles-outside-root");
		string erb = Path.Combine(root, "erb");
		// Shares the "erb" string prefix with the root, so a string based root test would slice it.
		string sibling = Path.Combine(root, "erb2");
		Directory.CreateDirectory(sibling);
		File.WriteAllText(Path.Combine(sibling, "a.erb"), "");

		KeyValuePair<string, string> file = Assert.Single(Config.GetFiles(sibling, erb, "*.erb"));

		Assert.Equal(Path.Combine(sibling, "a.erb"), file.Key);
	}
}
