using MinorShift.Emuera.Runtime.Utils.PathUtils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils.PathUtils;

public class PurePathTests
{
	[Theory]
	[InlineData("", "")]
	[InlineData(".", ".")]
	[InlineData("..", "..")]
	[InlineData("a", "a")]
	[InlineData("a/", "a")]
	[InlineData("a//b", @"a\b")]
	[InlineData("a/./b", @"a\b")]
	[InlineData("a/../b", "b")]
	[InlineData("a/..", ".")]
	[InlineData("../a", @"..\a")]
	[InlineData("../../a", @"..\..\a")]
	[InlineData(@"a\..\..\b", @"..\b")]
	[InlineData(@"C:\", @"C:\")]
	[InlineData(@"C:\a\", @"C:\a")]
	[InlineData(@"C:\a\..\b", @"C:\b")]
	[InlineData(@"C:\..", @"C:\")]
	[InlineData(@"C:\a\..\..\..\b", @"C:\b")]
	[InlineData(@"\a\b", @"\a\b")]
	[InlineData(@"\\server\share\a\..\b", @"\\server\share\b")]
	[InlineData(@"\\server\share", @"\\server\share\")]
	public void From_NormalizesLexically(string input, string expected)
		=> Assert.Equal(expected, PurePath.From(input).ToString());

	[Fact]
	public void From_DoesNotResolveAgainstCurrentDirectory()
	{
		// A relative value has to stay relative: resolving it would bind it to the process CWD.
		Assert.False(PurePath.From(@"erb\a.erb").IsRooted);
	}

	[Fact]
	public void IsRooted_DistinguishesRelativeFromRooted()
	{
		Assert.False(PurePath.From("a").IsRooted);
		Assert.False(PurePath.From(@"..\a").IsRooted);
		Assert.True(PurePath.From(@"C:\a").IsRooted);
		Assert.True(PurePath.From(@"\a").IsRooted);
	}

	[Fact]
	public void Name_And_Related_Parts()
	{
		var path = PurePath.From(@"C:\game\erb\foo.ERB");

		Assert.Equal("foo.ERB", path.Name);
		Assert.Equal("foo", path.NameWithoutExtension);
		Assert.Equal(".ERB", path.Extension);
		Assert.Equal(@"C:\game\erb", path.Directory!.ToString());
	}

	[Fact]
	public void Directory_IsNullWithoutParent()
	{
		Assert.Null(PurePath.From(@"C:\").Directory);
		Assert.Null(PurePath.From("foo").Directory);
		Assert.Equal("..", PurePath.From(@"..\a").Directory!.ToString());
	}

	[Fact]
	public void ChangeExtension_ReplacesOrAppendsTheExtension()
	{
		Assert.Equal(@"C:\game\erb\foo.txt", PurePath.From(@"C:\game\erb\foo.erb").ChangeExtension(".txt").ToString());
		Assert.Equal(@"C:\game\erb\foo.txt", PurePath.From(@"C:\game\erb\foo").ChangeExtension(".txt").ToString());
		Assert.Equal(@"C:\game\erb\foo.txt", PurePath.From(@"C:\game\erb\foo").ChangeExtension("txt").ToString());

		// Only the last segment carries an extension.
		Assert.Equal(@"C:\game\v1.2\foo.txt", PurePath.From(@"C:\game\v1.2\foo").ChangeExtension(".txt").ToString());
	}

	[Fact]
	public void ChangeExtension_FoldsTraversalBroughtInByTheExtension()
	{
		Assert.Equal(@"C:\game\foo", PurePath.From(@"C:\game\foo.txt").ChangeExtension(@"..\..\foo").ToString());
	}

	[Fact]
	public void Combine_AppendsRelativeValues()
	{
		Assert.Equal(@"C:\game\erb\a.erb", PurePath.From(@"C:\game\erb").Combine("a.erb").ToString());
		Assert.Equal(@"C:\game\a", PurePath.From(@"C:\game").Combine("./a").ToString());
		Assert.Equal(@"C:\game\a", PurePath.From(@"C:\game").Combine(PurePath.From("a")).ToString());
	}

	[Fact]
	public void Combine_RootedArgumentReplacesTheInstance()
	{
		Assert.Equal(@"D:\x", PurePath.From(@"C:\game").Combine(@"D:\x").ToString());
		Assert.Equal(@"D:\x", PurePath.From(@"C:\game").Combine(PurePath.From(@"D:\x")).ToString());
	}

	[Fact]
	public void IsRelativeTo_ComparesSegmentsNotStringPrefixes()
	{
		var root = PurePath.From(@"C:\game");

		Assert.True(PurePath.From(@"C:\game\erb\a.erb").IsRelativeTo(root));
		Assert.True(PurePath.From(@"C:\game").IsRelativeTo(root));
		Assert.True(PurePath.From(@"C:\GAME\ERB").IsRelativeTo(root));

		// A string prefix test would wrongly accept both of these.
		Assert.False(PurePath.From(@"C:\game2\a").IsRelativeTo(root));
		Assert.False(PurePath.From(@"C:\gam").IsRelativeTo(root));

		Assert.False(PurePath.From(@"D:\game\a").IsRelativeTo(root));
		Assert.False(PurePath.From(@"game\a").IsRelativeTo(root));
	}

	[Fact]
	public void RelativeTo_ProducesTheRelativeForm()
	{
		Assert.Equal(@"erb\a.erb", PurePath.From(@"C:\game\erb\a.erb").RelativeTo(PurePath.From(@"C:\game")).ToString());
		Assert.Equal(".", PurePath.From(@"C:\game").RelativeTo(PurePath.From(@"C:\game")).ToString());
		Assert.Equal(@"b\c", PurePath.From(@"C:\a\b\c").RelativeTo(PurePath.From(@"C:\a")).ToString());
	}

	[Fact]
	public void RelativeTo_ThrowsOutsideTheBase()
	{
		Assert.Throws<ArgumentException>(() => PurePath.From(@"C:\other").RelativeTo(PurePath.From(@"C:\game")));
		Assert.False(PurePath.From(@"C:\other").TryRelativeTo(PurePath.From(@"C:\game"), out var relative));
		Assert.Null(relative);
	}

	[Fact]
	public void Equality_IsValueBasedAndCaseInsensitive()
	{
		var a = PurePath.From(@"C:\game\a");
		var b = PurePath.From(@"C:/GAME/A");

		Assert.Equal(a, b);
		Assert.True(a == b);
		Assert.False(a != b);
		Assert.Equal(a.GetHashCode(), b.GetHashCode());
		Assert.NotEqual(a, PurePath.From(@"C:\game\b"));
	}

	[Fact]
	public void PurePath_CanBeUsedAsADictionaryKey()
	{
		var map = new Dictionary<PurePath, int> { [PurePath.From(@"C:\A\b")] = 1 };

		Assert.True(map.ContainsKey(PurePath.From(@"c:\a\B")));
	}

	[Fact]
	public void TryFrom_RejectsEmptyInput()
	{
		Assert.False(PurePath.TryFrom(null, out var path, out var reason));
		Assert.Null(path);
		Assert.Equal(PathReject.Empty, reason);

		Assert.False(PurePath.TryFrom("  ", out _, out reason));
		Assert.Equal(PathReject.Empty, reason);
	}

	[Fact]
	public void TryFrom_AcceptsRelativeAndAbsoluteValues()
	{
		Assert.True(PurePath.TryFrom(@"erb\a.erb", out var path, out var reason));
		Assert.Equal(PathReject.None, reason);
		Assert.Equal(@"erb\a.erb", path!.ToString());
	}
}
