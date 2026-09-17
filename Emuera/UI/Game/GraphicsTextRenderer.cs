using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinorShift.Emuera.UI.Game;

/// <summary>
/// Text for TextDrawingMode.GRAPHICS: measured with GDI and drawn with GDI+.
/// The layout is built on pen advances and every column, line break and button width derives from them, so the
/// measurement has to be the advance width of the text and nothing else:
/// - GDI+ MeasureString cannot be used for it. Its GenericTypographic format measures trailing whitespace as
///   zero, which collapses the space only nodes html puts between buttons, and the formats that do measure
///   trailing spaces add a per call padding that a node by node layout cannot cancel out.
/// - The ink extent that MeasureCharacterRanges returns is not an advance either and cannot be converted back
///   into one without assuming a font specific cell width.
/// GDI delivers the advance directly, for whitespace too, and keeps the layout of this mode identical to
/// TextDrawingMode.TEXTRENDERER.
/// </summary>
internal static class GraphicsTextRenderer
{
	/// <summary>Origin at the line top without padding, matching TextFormatFlags.NoPadding of the GDI path.</summary>
	private readonly static StringFormat drawFormat = StringFormat.GenericTypographic;

	/// <summary>
	/// Pen advances of the whole text, which is what the layout is built on.
	/// </summary>
	/// <param name="graph">Device context to measure through, or null on a thread that does not own the
	/// one the console built. Null makes GDI create its own for the call, which is what keeps parsing
	/// from touching a Graphics across threads while it folds a constant expression.</param>
	/// <param name="layoutSize">Measuring box; GDI grows it instead of clipping, so it only has to be large
	/// enough for the widest measured node.</param>
	public static int MeasureAdvance(Graphics? graph, ReadOnlySpan<char> text, Font font, Size layoutSize)
	{
		if (text.IsEmpty)
		{
			return 0;
		}

		if (graph == null)
		{
			// GDI copies the text into its own buffer whichever overload is used, so this saves the caller's copy
			// rather than GDI's
			return TextRenderer.MeasureText(new string(text), font, layoutSize,
				TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
		}

		// GDI copies the text into its own buffer whichever overload is used, so this saves the caller's copy
		// rather than GDI's
		return TextRenderer.MeasureText(graph, text, font, layoutSize,
			TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
	}

	/// <summary>
	/// Pen advances of the whole text, which is what the layout is built on.
	/// </summary>
	/// <param name="graph">Device context to measure through, or null on a thread that does not own the
	/// one the console built.</param>
	/// <param name="layoutSize">Measuring box; GDI grows it instead of clipping, so it only has to be large
	/// enough for the widest measured node.</param>
	public static int MeasureAdvance(Graphics? graph, string text, Font font, Size layoutSize)
	{
		return MeasureAdvance(graph, text.AsSpan(), font, layoutSize);
	}

	/// <param name="subPixel">Fractional pen offset carried over from the previous node so that node by node
	/// drawing reproduces drawing the whole line in one call.</param>
	public static void Draw(Graphics graph, string text, Font font, Color color, int pointX, float subPixel, int pointY)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}

		using SolidBrush brush = new(color);
		graph.DrawString(text, font, brush, new PointF(pointX + subPixel, pointY), drawFormat);
	}
}