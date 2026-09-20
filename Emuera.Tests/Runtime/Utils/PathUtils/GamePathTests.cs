using MinorShift.Emuera.Runtime.Utils.PathUtils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils.PathUtils;

public class GamePathTests
{
	/// <summary>A synthetic game directory; none of these tests touch the file system.</summary>
	private static readonly PurePath GameRoot = PurePath.From(@"C:\__emuera_path_test__");

	private static GamePath Root() => GamePath.FromRoot(GameRoot);

	[Fact]
	public void FromRoot_RepresentsTheGameDirectoryItself()
	{
		var root = Root();

		Assert.Equal(GameRoot, root);
		Assert.Equal(GameRoot.ToString(), root.FullName);
		Assert.Equal(GameRoot, root.Root);
	}

	[Fact]
	public void TryResolve_AcceptsARelativePathInsideTheRoot()
	{
		Assert.True(GamePath.TryResolve(GameRoot, @"erb\a.erb", out var path, out var reason));

		Assert.Equal(PathReject.None, reason);
		Assert.Equal(@"C:\__emuera_path_test__\erb\a.erb", path!.FullName);
		Assert.Equal(GameRoot, path.Root);
	}

	[Fact]
	public void TryResolve_AllowsInnerParentTraversal()
	{
		Assert.True(GamePath.TryResolve(GameRoot, "a/../b.log", out var path, out _));

		Assert.Equal(@"C:\__emuera_path_test__\b.log", path!.FullName);
	}

	[Fact]
	public void ChangeExtension_KeepsTheInvariantsOfTheType()
	{
		Assert.True(GamePath.TryResolve(GameRoot, @"dat\memo", out var path, out _));

		var renamed = path!.ChangeExtension(".txt");

		Assert.Equal(@"C:\__emuera_path_test__\dat\memo.txt", renamed.FullName);
		Assert.Equal(GameRoot, renamed.Root);

		// An extension may not smuggle in a segment that is not a usable file name.
		Assert.Throws<ArgumentException>(() => path.ChangeExtension(@"D:\evil"));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void TryResolve_RejectsEmptyInput(string? raw)
	{
		Assert.False(GamePath.TryResolve(GameRoot, raw, out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.Empty, reason);
	}

	[Theory]
	[InlineData(@"C:\evil.log")]
	[InlineData(@"\evil.log")]
	[InlineData(@"C:evil.log")]
	[InlineData(@"\\server\share\evil.log")]
	public void TryResolve_RejectsRootedInput(string raw)
	{
		Assert.False(GamePath.TryResolve(GameRoot, raw, out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.Rooted, reason);
	}

	[Theory]
	[InlineData("..")]
	[InlineData(@"..\..\evil.log")]
	[InlineData(@"a\..\..\..\evil.log")]
	[InlineData("a/../../evil.log")]
	[InlineData(@"a\..\..\..\..\Windows\System32\evil.dll")]
	public void TryResolve_RejectsEscapingTheRoot(string raw)
	{
		Assert.False(GamePath.TryResolve(GameRoot, raw, out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.OutsideRoot, reason);
	}

	[Theory]
	[InlineData("evil:ads")]      // NTFS alternate data stream
	[InlineData("a*.log")]
	[InlineData("a?.log")]
	[InlineData("a|b.log")]
	public void TryResolve_RejectsInvalidFileNameCharacters(string raw)
	{
		Assert.False(GamePath.TryResolve(GameRoot, raw, out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.InvalidChar, reason);
	}

	[Fact]
	public void TryResolve_IsCaseInsensitiveLikeWindows()
	{
		Assert.True(GamePath.TryResolve(PurePath.From(@"C:\__EMUERA_PATH_TEST__"), @"ERB\A.ERB", out var path, out _));

		Assert.True(path!.IsRelativeTo(GameRoot));
		Assert.Equal(@"C:\__EMUERA_PATH_TEST__\ERB\A.ERB", path.FullName);
	}

	[Fact]
	public void TryResolveName_AcceptsASingleName()
	{
		Assert.True(GamePath.TryResolveName(GameRoot, "chara_01.dat", out var path, out var reason));

		Assert.Equal(PathReject.None, reason);
		Assert.Equal(@"C:\__emuera_path_test__\chara_01.dat", path!.FullName);
	}

	[Theory]
	[InlineData(@"a\b")]
	[InlineData("a/b")]
	[InlineData(@"..\x")]
	public void TryResolveName_RejectsDirectorySeparators(string raw)
	{
		Assert.False(GamePath.TryResolveName(GameRoot, raw, out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.MultiSegment, reason);
	}

	[Fact]
	public void TryResolveName_RejectsDot()
	{
		Assert.False(GamePath.TryResolveName(GameRoot, ".", out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.InvalidChar, reason);
	}

	[Fact]
	public void TryResolveName_RejectsDotDot()
	{
		Assert.False(GamePath.TryResolveName(GameRoot, "..", out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.OutsideRoot, reason);
	}

	[Fact]
	public void TryResolveName_RejectsInvalidFileNameCharacters()
	{
		Assert.False(GamePath.TryResolveName(GameRoot, "evil:ads", out _, out var reason));
		Assert.Equal(PathReject.InvalidChar, reason);
	}

	[Fact]
	public void TryResolveChild_ResolvesAgainstThisDirectoryNotTheRoot()
	{
		var root = Root();
		Assert.True(root.TryResolveChildName("sound", out var sound, out _));
		Assert.Equal(@"C:\__emuera_path_test__\sound", sound!.FullName);

		Assert.True(sound.TryResolveChildName("bgm.mp3", out var bgm, out var reason));
		Assert.Equal(PathReject.None, reason);
		Assert.Equal(@"C:\__emuera_path_test__\sound\bgm.mp3", bgm!.FullName);

		// The invariant still points at the game directory, not at "sound".
		Assert.Equal(GameRoot, bgm.Root);
	}

	[Fact]
	public void TryResolveChild_RejectsLeavingThisDirectory()
	{
		Assert.True(Root().TryResolveChildName("sound", out var sound, out _));

		Assert.False(sound!.TryResolveChild(@"..\..\evil.log", out var child, out var reason));
		Assert.Null(child);
		Assert.Equal(PathReject.OutsideRoot, reason);

		// Even stepping up to the game directory itself is not a child of "sound".
		Assert.False(sound.TryResolveChild("..", out _, out reason));
		Assert.Equal(PathReject.OutsideRoot, reason);
	}

	[Fact]
	public void CombineName_AppendsASingleNameAndKeepsTheInvariant()
	{
		var sound = Root().CombineName("sound");

		Assert.Equal(@"C:\__emuera_path_test__\sound", sound.FullName);
		Assert.Equal(GameRoot, sound.Root);
		Assert.Equal(@"C:\__emuera_path_test__\sound\bgm.mp3", sound.CombineName("bgm.mp3").FullName);
	}

	[Fact]
	public void CombineName_ThrowsWhenTheNameIsNotAUsableEntry()
	{
		var root = Root();

		Assert.Throws<ArgumentException>(() => root.CombineName(""));
		Assert.Throws<ArgumentException>(() => root.CombineName(@"sub\evil.log"));
		Assert.Throws<ArgumentException>(() => root.CombineName(".."));
	}

	[Fact]
	public void GamePath_EqualsItsPurePathValue()
	{
		Assert.True(GamePath.TryResolve(GameRoot, @"erb\a.erb", out var path, out _));

		Assert.Equal(path, PurePath.From(path!.FullName));
		Assert.True(path == PurePath.From(path.FullName));
	}

	[Fact]
	public void Factories_ThrowWhenTheRootIsNotAbsolute()
	{
		var relative = PurePath.From("relative");

		Assert.Throws<ArgumentException>(() => GamePath.FromRoot(relative));
		Assert.Throws<ArgumentException>(() => GamePath.TryResolve(relative, "a.log", out _, out _));
		Assert.Throws<ArgumentException>(() => GamePath.TryResolveName(relative, "a.log", out _, out _));
		Assert.Throws<ArgumentException>(() => GamePath.TryResolveAbsolute(relative, @"C:\a.log", out _, out _));
	}

	[Fact]
	public void TryResolveAbsolute_AcceptsAPathInsideTheRoot()
	{
		Assert.True(GamePath.TryResolveAbsolute(GameRoot, @"C:\__emuera_path_test__\sav\save01.sav", out var path, out var reason));

		Assert.Equal(PathReject.None, reason);
		Assert.Equal(@"C:\__emuera_path_test__\sav\save01.sav", path!.FullName);
	}

	[Theory]
	[InlineData(@"C:\elsewhere\save01.sav")]
	[InlineData(@"C:\__emuera_path_test__\..\evil.sav")]
	[InlineData(@"C:\__emuera_path_test__\sav\..\..\evil.sav")]
	public void TryResolveAbsolute_RejectsPathsOutsideTheRoot(string raw)
	{
		Assert.False(GamePath.TryResolveAbsolute(GameRoot, raw, out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.OutsideRoot, reason);
	}

	[Fact]
	public void TryResolveAbsolute_RejectsRelativeInput()
	{
		Assert.False(GamePath.TryResolveAbsolute(GameRoot, @"sav\save01.sav", out var path, out var reason));

		Assert.Null(path);
		Assert.Equal(PathReject.NotRooted, reason);
	}
}
