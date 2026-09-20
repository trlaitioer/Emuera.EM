using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using Xunit;

namespace Emuera.Tests.UI.Game;

/// <summary>
/// Rikaichan 词典设置:相对游戏目录解析,或指向游戏目录内的绝对路径。
/// 索引文件写在词典旁,所以目录外的路径必须被拒绝。
/// </summary>
public class RikaichanTests
{
	[Fact]
	public void TryResolveRikaiFile_ResolvesANameRelativeToTheGameDirectory()
	{
		string root = GameRootScope.UseTempRoot("rikai-relative");

		Assert.True(Rikaichan.TryResolveRikaiFile(@"dict\edict", out GamePath? file));
		Assert.Equal(Path.Combine(root, "dict", "edict"), file!.FullName);
	}

	[Fact]
	public void TryResolveRikaiFile_AcceptsAnAbsolutePathInsideTheGameDirectory()
	{
		string root = GameRootScope.UseTempRoot("rikai-absolute-inside");

		Assert.True(Rikaichan.TryResolveRikaiFile(Path.Combine(root, "dict", "edict"), out GamePath? file));
		Assert.Equal(Path.Combine(root, "dict", "edict"), file!.FullName);
	}

	[Fact]
	public void TryResolveRikaiFile_RejectsAnAbsolutePathOutsideTheGameDirectory()
	{
		string root = GameRootScope.UseTempRoot("rikai-absolute-outside");
		string outside = Path.Combine(Path.GetDirectoryName(root)!, "elsewhere", "edict");

		Assert.False(Rikaichan.TryResolveRikaiFile(outside, out GamePath? file));
		Assert.Null(file);
	}

	[Fact]
	public void TryResolveRikaiFile_RejectsAnEscapingName()
	{
		GameRootScope.UseTempRoot("rikai-escaping");

		Assert.False(Rikaichan.TryResolveRikaiFile(@"..\edict", out _));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TryResolveRikaiFile_RejectsAnEmptySetting(string? configuredPath)
	{
		GameRootScope.UseTempRoot("rikai-empty");

		Assert.False(Rikaichan.TryResolveRikaiFile(configuredPath, out GamePath? file));
		Assert.Null(file);
	}
}
