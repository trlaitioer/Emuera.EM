#nullable enable
using System;
using System.Drawing;

namespace MinorShift.Emuera.UI.Game.Image;

internal abstract class AbstractImage : IDisposable
{
	public const int MAX_IMAGESIZE = 8192;
	public abstract Bitmap Bitmap { get; set; }

	public nint GDIhDC { get; protected set; }

	// Set by the derived types' setup paths (GCreate/CreateFrom); null means nothing drawn yet.
	protected Graphics g = null!;

	public abstract bool IsCreated { get; }

	public abstract void Dispose();
}