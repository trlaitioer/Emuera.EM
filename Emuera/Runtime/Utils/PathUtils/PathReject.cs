namespace MinorShift.Emuera.Runtime.Utils.PathUtils;

/// <summary>
/// Why a raw string was refused by <see cref="GamePath"/>. Callers map the value to a user facing
/// message; the enum exists so that a rejected path cannot be discarded the way a nullable error
/// string can.
/// </summary>
public enum PathReject
{
	/// <summary>The raw value was accepted.</summary>
	None,

	/// <summary>The raw value was null, empty or whitespace.</summary>
	Empty,

	/// <summary>A segment contained a character that is not valid in a file name.</summary>
	InvalidChar,

	/// <summary>The raw value was absolute or drive relative instead of relative to the root.</summary>
	Rooted,

	/// <summary>An absolute path was required, but the raw value was relative.</summary>
	NotRooted,

	/// <summary>A single name was required, but the raw value contained a directory separator.</summary>
	MultiSegment,

	/// <summary>The normalized path fell outside the directory it was resolved against.</summary>
	OutsideRoot
}
