using MinorShift.Emuera.Runtime.Utils.PathUtils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils.PathUtils;

public class NamePatternTests
{
	[Theory]
	[InlineData("*")]
	[InlineData("*.dat")]
	[InlineData("chara_*.dat")]
	[InlineData("?a.dat")]
	public void IsSingleDirectory_AllowsWildcards(string pattern)
		=> Assert.True(NamePattern.IsSingleDirectory(pattern));

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(".")]
	[InlineData("..")]
	[InlineData(@"..\*.dat")]
	[InlineData("../*.dat")]
	[InlineData(@"sub\*.dat")]
	[InlineData(@"..\..\*.sln")]
	public void IsSingleDirectory_RejectsTraversalAndSeparators(string? pattern)
		=> Assert.False(NamePattern.IsSingleDirectory(pattern));
}
