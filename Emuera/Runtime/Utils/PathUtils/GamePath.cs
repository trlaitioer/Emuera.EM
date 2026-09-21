using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MinorShift.Emuera.Runtime.Utils.PathUtils;

/// <summary>
/// A path that has been proven to live inside the directory it was resolved against. Instances can
/// only be produced by the factories below, so a raw string coming from an eraBasic script cannot
/// reach a file system call without passing a containment check.
/// </summary>
public sealed class GamePath : PurePath
{
	private readonly PurePath root;

	private GamePath(PurePath root, string normalizedAbsolute) : base(normalizedAbsolute)
	{
		this.root = root;
	}

	/// <summary>The game directory this path was validated against.</summary>
	public PurePath Root => root;

	/// <summary>Absolute, normalized path. Use this at file system call sites.</summary>
	public string FullName => ToString();

	/// <summary>
	/// Replaces the extension and keeps the guarantees of this type: an extension that would leave the game
	/// directory, or that brings in a segment which is not a usable file name, is refused instead of being
	/// resolved. Use this form on script input; <see cref="ChangeExtension"/> raises instead of reporting.
	/// </summary>
	public bool TryChangeExtension(string? extension, [NotNullWhen(true)] out GamePath? changed)
	{
		PurePath candidate = base.ChangeExtension(extension);
		if (!AreSegmentsValid(candidate) || !candidate.IsRelativeTo(root))
		{
			changed = null;
			return false;
		}

		changed = new GamePath(root, candidate.ToString());
		return true;
	}

	public override GamePath ChangeExtension(string? extension)
	{
		return TryChangeExtension(extension, out GamePath? changed)
			? changed
			: throw new ArgumentException($"'{base.ChangeExtension(extension)}' cannot be resolved inside '{root}'.", nameof(extension));
	}

	/// <summary>
	/// Wraps a directory that is already known to be inside the game directory. Setup only; the
	/// path is not validated, so never pass script input here.
	/// </summary>
	public static GamePath FromRoot(PurePath gameRoot)
	{
		VerifyRoot(gameRoot);
		return new GamePath(gameRoot, gameRoot.ToString());
	}

	/// <summary>Resolves a script supplied path against <paramref name="root"/>.</summary>
	public static bool TryResolve(PurePath root, string? raw, [NotNullWhen(true)] out GamePath? path, out PathReject reason)
	{
		return TryResolveCore(root, root, raw, false, out path, out reason);
	}

	/// <summary>Resolves a script supplied single name against <paramref name="root"/>.</summary>
	public static bool TryResolveName(PurePath root, string? raw, [NotNullWhen(true)] out GamePath? path, out PathReject reason)
	{
		return TryResolveCore(root, root, raw, true, out path, out reason);
	}

	/// <summary>
	/// Proves that an already absolute path lives inside <paramref name="root"/>. Used by callers
	/// that receive a path from the file system rather than from a script.
	/// </summary>
	public static bool TryResolveAbsolute(PurePath root, string? raw, [NotNullWhen(true)] out GamePath? path, out PathReject reason)
	{
		VerifyRoot(root);
		path = null;

		if (string.IsNullOrWhiteSpace(raw))
		{
			reason = PathReject.Empty;
			return false;
		}

		if (!Path.IsPathRooted(raw))
		{
			reason = PathReject.NotRooted;
			return false;
		}

		if (!AreSegmentsValid(From(raw)))
		{
			reason = PathReject.InvalidChar;
			return false;
		}

		if (!TryGetFullPath(From(raw), out string? absolute, out reason))
		{
			return false;
		}

		PurePath resolved = From(absolute);
		if (!resolved.IsRelativeTo(root))
		{
			reason = PathReject.OutsideRoot;
			return false;
		}

		reason = PathReject.None;
		path = new GamePath(root, resolved.ToString());
		return true;
	}

	/// <summary>Resolves a script supplied path against this directory.</summary>
	public bool TryResolveChild(string? raw, [NotNullWhen(true)] out GamePath? child, out PathReject reason)
	{
		return TryResolveCore(root, this, raw, false, out child, out reason);
	}

	/// <summary>Resolves a script supplied single name against this directory.</summary>
	public bool TryResolveChildName(string? raw, [NotNullWhen(true)] out GamePath? child, out PathReject reason)
	{
		return TryResolveCore(root, this, raw, true, out child, out reason);
	}

	/// <summary>
	/// Appends a single name. A rejection is a programming error here: callers pass a literal or an already
	/// formatted name, not script input, so the failure is raised instead of being handed back.
	/// </summary>
	public GamePath CombineName(string name)
	{
		if (!TryResolveChildName(name, out GamePath? child, out PathReject reason))
		{
			throw new ArgumentException($"'{name}' cannot be resolved inside '{this}' ({reason}).", nameof(name));
		}

		return child;
	}

	private static bool TryResolveCore(
		PurePath invariantRoot,
		PurePath baseDirectory,
		string? raw,
		bool singleSegment,
		[NotNullWhen(true)] out GamePath? path,
		out PathReject reason
	)
	{
		VerifyRoot(invariantRoot);
		path = null;

		if (string.IsNullOrWhiteSpace(raw))
		{
			reason = PathReject.Empty;
			return false;
		}

		if (singleSegment)
		{
			if (raw.IndexOfAny(Separators) >= 0)
			{
				reason = PathReject.MultiSegment;
				return false;
			}

			if (raw.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			{
				reason = PathReject.InvalidChar;
				return false;
			}
		}
		else
		{
			if (Path.IsPathRooted(raw))
			{
				reason = PathReject.Rooted;
				return false;
			}

			if (!AreSegmentsValid(From(raw)))
			{
				reason = PathReject.InvalidChar;
				return false;
			}
		}

		if (!TryGetFullPath(baseDirectory.Combine(raw), out string? absolute, out reason))
		{
			return false;
		}

		PurePath resolved = From(absolute);
		if (!resolved.IsRelativeTo(baseDirectory))
		{
			reason = PathReject.OutsideRoot;
			return false;
		}

		if (singleSegment && resolved.Equals(baseDirectory))
		{
			// A name has to produce an entry, not the directory it was resolved against.
			reason = PathReject.InvalidChar;
			return false;
		}

		reason = PathReject.None;
		path = new GamePath(invariantRoot, resolved.ToString());
		return true;
	}

	/// <summary>
	/// Every segment has to be a usable file name. This runs on the normalized value, so the volume
	/// prefix is not part of it and segments collapsed by ".." are gone.
	/// </summary>
	private static bool AreSegmentsValid(PurePath path)
	{
		char[] invalid = Path.GetInvalidFileNameChars();
		foreach (string segment in path.Segments)
		{
			if (segment.IndexOfAny(invalid) >= 0)
			{
				return false;
			}
		}

		return true;
	}

	private static bool TryGetFullPath(PurePath path, [NotNullWhen(true)] out string? fullName, out PathReject reason)
	{
		try
		{
			fullName = Path.GetFullPath(path.ToString());
			reason = PathReject.None;
			return true;
		}
		catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException)
		{
			fullName = null;
			reason = PathReject.InvalidChar;
			return false;
		}
	}

	private static void VerifyRoot(PurePath root)
	{
		ArgumentNullException.ThrowIfNull(root);

		if (!root.IsFullyQualified)
		{
			throw new ArgumentException("A game directory has to be a fully qualified path.", nameof(root));
		}
	}
}
