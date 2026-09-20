using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// GCREATEFROMFILE 的取值规则:绝对路径必须落在 resources\ 内,isRelative 的名字相对游戏根,
/// 其余名字相对 resources\。三支都不允许离开各自的基准目录。
/// </summary>
public class GraphicsCreateFromFileTests
{
	[Fact]
	public void TryResolveContentFile_AbsolutePathInsideResources_IsResolved()
	{
		string root = GameRootScope.UseTempRoot("contentfile-absolute-inside");
		string path = Path.Combine(root, "resources", "a.bmp");

		Assert.True(Program.TryResolveContentFile(path, false, out GamePath? file));
		Assert.Equal(path, file.ToString());
	}

	[Fact]
	public void TryResolveContentFile_AbsolutePathOutsideResources_IsRejected()
	{
		string root = GameRootScope.UseTempRoot("contentfile-absolute-outside");
		// 在游戏目录内但不在 resources\ 下,用于钉住两支中更严的那条基准
		string path = Path.Combine(root, "a.bmp");

		Assert.False(Program.TryResolveContentFile(path, false, out _));
	}

	[Fact]
	public void TryResolveContentFile_RelativeName_IsResolvedBelowTheGameRoot()
	{
		string root = GameRootScope.UseTempRoot("contentfile-relative");

		Assert.True(Program.TryResolveContentFile(@"resources\a.bmp", true, out GamePath? file));
		Assert.Equal(Path.Combine(root, "resources", "a.bmp"), file.ToString());
	}

	[Fact]
	public void TryResolveContentFile_NonRelativeName_IsResolvedBelowResources()
	{
		string root = GameRootScope.UseTempRoot("contentfile-non-relative");

		Assert.True(Program.TryResolveContentFile("a.bmp", false, out GamePath? file));
		Assert.Equal(Path.Combine(root, "resources", "a.bmp"), file.ToString());
	}

	[Fact]
	public void TryResolveContentFile_EscapingName_IsRejected()
	{
		GameRootScope.UseTempRoot("contentfile-escaping");

		Assert.False(Program.TryResolveContentFile(@"..\a.bmp", true, out _));
		Assert.False(Program.TryResolveContentFile(@"..\..\a.bmp", false, out _));
	}
}
