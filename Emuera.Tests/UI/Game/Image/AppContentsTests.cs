using MinorShift.Emuera.Runtime.Utils.PathUtils;
using MinorShift.Emuera.UI.Game.Image;
using Xunit;

namespace Emuera.Tests.UI.Game.Image;

/// <summary>
/// resources 下 csv 给出的图片名:相对该 csv 所在目录解析,但必须留在 resources\ 内。
/// 名字只做词法解析,UseTempRoot 只是为了给 ContentRoot 一个位置。
/// </summary>
public class AppContentsTests
{
	[Fact]
	public void ResolveResourcePath_AcceptsANameBelowTheCsvDirectory()
	{
		string root = GameRootScope.UseTempRoot("appcontents-below");

		string? path = AppContents.ResolveResourcePath(CsvDirectory(root), "a.png");

		Assert.Equal(Path.Combine(root, "resources", "sub", "a.png"), path);
	}

	[Fact]
	public void ResolveResourcePath_AllowsClimbingWhileStayingInsideResources()
	{
		string root = GameRootScope.UseTempRoot("appcontents-sibling");

		// resources\sub\a.csv 里的 ..\b.png 仍落在 resources\ 内,与旧行为一致。
		string? path = AppContents.ResolveResourcePath(CsvDirectory(root), @"..\b.png");

		Assert.Equal(Path.Combine(root, "resources", "b.png"), path);
	}

	[Theory]
	[InlineData(@"..\..\evil.png")]
	[InlineData(@"C:\pics\a.png")]
	[InlineData(@"\a.png")]
	[InlineData(@"\\server\share\a.png")]
	public void ResolveResourcePath_RejectsNamesLeavingResources(string imageName)
	{
		string root = GameRootScope.UseTempRoot("appcontents-outside");

		Assert.Null(AppContents.ResolveResourcePath(CsvDirectory(root), imageName));
	}

	/// <summary>resources\sub 下的 csv 所在目录。</summary>
	private static PurePath CsvDirectory(string root)
		=> PurePath.From(root).Combine("resources").Combine("sub");
}
