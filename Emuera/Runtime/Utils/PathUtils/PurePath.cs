using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace MinorShift.Emuera.Runtime.Utils.PathUtils;

/// <summary>
/// Lexical path value. Separators are unified and "." / ".." segments are collapsed without
/// touching the file system or the current directory, so the stored form may be relative or
/// absolute. Use <see cref="GamePath"/> when a path has to be proven to live inside the game
/// directory.
/// </summary>
public class PurePath : IEquatable<PurePath>
{
	private const string CurrentDirectory = ".";

	protected static readonly char Separator = Path.DirectorySeparatorChar;
	protected static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
	private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;

	/// <summary>Normalized form. Empty when the input was empty, "." when it collapsed to the current directory.</summary>
	private readonly string value;

	/// <summary>Volume prefix: "", "\", "C:\" or "\\server\share\".</summary>
	private readonly string anchor;

	/// <summary>Segments after the anchor, with "." removed and ".." collapsed.</summary>
	private readonly string[] segments;

	protected PurePath(string normalizedValue)
	{
		value = normalizedValue;
		anchor = SplitAnchor(normalizedValue, out string remainder);
		segments = remainder.Length == 0
			? []
			: remainder.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
	}

	/// <summary>Normalized lexical form; a relative path stays relative.</summary>
	public override string ToString()
	{
		return value;
	}

	/// <summary>Segments after the volume prefix, with "." removed and ".." collapsed.</summary>
	internal ReadOnlySpan<string> Segments => segments;

	public bool IsRooted => Path.IsPathRooted(value);

	/// <summary>True when the value is rooted independently of the current directory.</summary>
	public bool IsFullyQualified => Path.IsPathFullyQualified(value);

	public string Name => Path.GetFileName(value);

	public string NameWithoutExtension => Path.GetFileNameWithoutExtension(value);

	public string Extension => Path.GetExtension(value);

	/// <summary>Parent directory, or null when the value has none (a bare name, or a volume root).</summary>
	public PurePath? Directory
	{
		get
		{
			string? parent = Path.GetDirectoryName(value);
			return string.IsNullOrEmpty(parent) ? null : new PurePath(Normalize(parent));
		}
	}

	/// <summary>
	/// Replaces the extension. <paramref name="extension"/> may be given with or without the leading dot.
	/// </summary>
	public virtual PurePath ChangeExtension(string? extension)
	{
		// Path.ChangeExtension returns null only for a null path; value is the non-null normalized field.
		return new PurePath(Normalize(Path.ChangeExtension(value, extension)!));
	}

	public static PurePath From(string path)
	{
		ArgumentNullException.ThrowIfNull(path);
		return new PurePath(Normalize(path));
	}

	public static bool TryFrom(string? raw, [NotNullWhen(true)] out PurePath? path, out PathReject reason)
	{
		path = null;

		if (string.IsNullOrWhiteSpace(raw))
		{
			reason = PathReject.Empty;
			return false;
		}

		if (raw.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
		{
			reason = PathReject.InvalidChar;
			return false;
		}

		reason = PathReject.None;
		path = new PurePath(Normalize(raw));
		return true;
	}

	/// <summary>Appends a relative path. A rooted argument replaces this value, as Path.Combine does.</summary>
	public PurePath Combine(string relative)
	{
		return Combine(From(relative));
	}

	public PurePath Combine(PurePath relative)
	{
		ArgumentNullException.ThrowIfNull(relative);

		if (relative.IsRooted)
		{
			return relative;
		}

		if (value.Length == 0)
		{
			return relative;
		}

		if (relative.value.Length == 0)
		{
			return this;
		}

		return new PurePath(Normalize(value + Separator + relative.value));
	}

	/// <summary>True when this value is <paramref name="basePath"/> itself or a descendant of it.</summary>
	public bool IsRelativeTo(PurePath basePath)
	{
		ArgumentNullException.ThrowIfNull(basePath);

		if (!Comparer.Equals(anchor, basePath.anchor))
		{
			return false;
		}

		if (segments.Length < basePath.segments.Length)
		{
			return false;
		}

		for (int i = 0; i < basePath.segments.Length; i++)
		{
			if (!Comparer.Equals(segments[i], basePath.segments[i]))
			{
				return false;
			}
		}

		return true;
	}

	public bool TryRelativeTo(PurePath basePath, [NotNullWhen(true)] out PurePath? relative)
	{
		if (!IsRelativeTo(basePath))
		{
			relative = null;
			return false;
		}

		string[] rest = segments[basePath.segments.Length..];
		relative = new PurePath(rest.Length == 0 ? CurrentDirectory : string.Join(Separator, rest));
		return true;
	}

	public PurePath RelativeTo(PurePath basePath)
	{
		if (!TryRelativeTo(basePath, out PurePath? relative))
		{
			throw new ArgumentException($"'{value}' is not relative to '{basePath.value}'.", nameof(basePath));
		}

		return relative;
	}

	public bool Equals(PurePath? other)
	{
		return other is not null && Comparer.Equals(value, other.value);
	}

	public override bool Equals(object? obj)
	{
		return Equals(obj as PurePath);
	}

	public override int GetHashCode()
	{
		return Comparer.GetHashCode(value);
	}

	public static bool operator ==(PurePath? left, PurePath? right)
	{
		return left is null ? right is null : left.Equals(right);
	}

	public static bool operator !=(PurePath? left, PurePath? right)
	{
		return !(left == right);
	}

	/// <summary>Splits the volume prefix; <paramref name="remainder"/> is what follows it.</summary>
	private static string SplitAnchor(string path, out string remainder)
	{
		remainder = path;

		if (path.Length == 0)
		{
			return string.Empty;
		}

		if (path.Length >= 4 && path[0] == Separator && path[1] == Separator)
		{
			int serverEnd = path.IndexOf(Separator, 2);
			if (serverEnd > 2)
			{
				int shareEnd = path.IndexOf(Separator, serverEnd + 1);
				if (shareEnd > serverEnd + 1)
				{
					remainder = path[(shareEnd + 1)..];
					return path[..(shareEnd + 1)];
				}

				// "\\server\share" is itself the volume root.
				remainder = string.Empty;
				return path + Separator;
			}
		}

		if (path.Length >= 2 && path[1] == ':')
		{
			if (path.Length >= 3 && path[2] == Separator)
			{
				remainder = path[3..];
				return path[..3];
			}

			// Drive relative ("C:foo"): kept as given, see Normalize.
			remainder = path[2..];
			return path[..2];
		}

		if (path[0] == Separator)
		{
			remainder = path[1..];
			return path[..1];
		}

		return string.Empty;
	}

	private static string Normalize(string path)
	{
		if (path.Length == 0)
		{
			return string.Empty;
		}

		string unified = Separator == Path.AltDirectorySeparatorChar
			? path
			: path.Replace(Path.AltDirectorySeparatorChar, Separator);

		string anchor = SplitAnchor(unified, out string remainder);

		// A drive relative path cannot be collapsed lexically without inventing a separator.
		if (anchor.Length == 2 && anchor[1] == ':')
		{
			return unified;
		}

		List<string> parts = [];
		foreach (string segment in remainder.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
		{
			if (segment == CurrentDirectory)
			{
				continue;
			}

			if (segment == "..")
			{
				if (parts.Count > 0 && parts[^1] != "..")
				{
					parts.RemoveAt(parts.Count - 1);
				}
				else if (anchor.Length == 0)
				{
					// A relative path keeps its leading "..".
					parts.Add("..");
				}

				continue;
			}

			parts.Add(segment);
		}

		if (parts.Count == 0)
		{
			return anchor.Length > 0 ? anchor : CurrentDirectory;
		}

		return anchor + string.Join(Separator, parts);
	}
}
