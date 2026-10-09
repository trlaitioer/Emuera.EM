using System;
using System.IO;

namespace MinorShift.Emuera.Runtime.Utils;

/// <summary>
/// Writes a file so that a failure cannot damage the version already on disk: the bytes go to a temporary
/// sibling and are moved over the target only once <c>write</c> returned. <see cref="FileMode.Create"/>
/// truncates the target before the first byte is written, so a write that failed half-way (a full disk, an
/// encoding failure) destroyed the file that was there and left a truncated one behind - the very input the
/// load path reports as a damaged save.
/// </summary>
internal static class TempFileWriter
{
	/// <summary>The temporary sibling is the target's file name plus this suffix, so a stray one is recognisable
	/// as such; it sits in the same directory, which keeps the move inside one volume.</summary>
	private const string TempSuffix = ".tmp";

	/// <summary>
	/// Writes <paramref name="filepath"/> through a temporary sibling. <paramref name="write"/> owns the
	/// stream: the save writers close it (directly or through their writer), and the <c>using</c> below then
	/// disposes an already-closed stream, which is a no-op. A failure propagates with the target left as it
	/// was and the temporary file removed.
	/// </summary>
	internal static void Write(string filepath, Action<FileStream> write)
	{
		string tempPath = filepath + TempSuffix;
		try
		{
			using (FileStream stream = new(tempPath, FileMode.Create, FileAccess.Write))
			{
				write(stream);
			}

			File.Move(tempPath, filepath, true);
		}
		catch
		{
			// A leftover temporary file would show up as a stray entry in the save folder. Deleting it can
			// itself fail (the medium is gone, the file is locked), but the write failure is what the caller
			// has to report, so that one keeps propagating and this one is dropped.
			try
			{
				File.Delete(tempPath);
			}
			catch (Exception)
			{
			}

			throw;
		}
	}
}
