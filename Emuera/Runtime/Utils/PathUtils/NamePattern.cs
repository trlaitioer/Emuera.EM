using System.IO;

namespace MinorShift.Emuera.Runtime.Utils.PathUtils;

/// <summary>
/// Validation for directory search patterns. A pattern is not a path: wildcards are allowed, but a
/// separator would let it walk out of the directory it is applied to.
/// </summary>
public static class NamePattern
{
	/// <summary>True when the pattern can only match entries of a single directory.</summary>
	public static bool IsSingleDirectory(string? pattern)
	{
		if (string.IsNullOrEmpty(pattern) || pattern is "." or "..")
		{
			return false;
		}

		return pattern.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
	}
}
