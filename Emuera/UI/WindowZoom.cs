using System;
using System.Drawing;

namespace MinorShift.Emuera.UI;

/// <summary>
/// Geometry of the runtime window zoom.
/// Sizes are always derived from the startup sizes, so repeated zoom steps do not accumulate rounding
/// errors; the drag preview is scaled relative to the layout that is currently committed instead.
/// </summary>
internal static class WindowZoom
{
	/// <summary>
	/// Canvas width and the sizes derived from it for a client width.
	/// The scroll bar grows with the zoom and shrinks the canvas, so both are solved from a pre-scale.
	/// </summary>
	public static void Compute(
		int clientWidth,
		int baseCanvasWidth,
		int baseScrollBarWidth,
		int baseFontSize,
		int baseLineHeight,
		out float scale,
		out int scrollBarWidth,
		out int fontSize,
		out int lineHeight
	)
	{
		int baseWidth = Math.Max(1, baseCanvasWidth);
		float preScale = Math.Max(0.01f, (clientWidth - baseScrollBarWidth) / (float)baseWidth);
		scrollBarWidth = Math.Max(1, (int)Math.Round(baseScrollBarWidth * preScale));
		int canvasWidth = Math.Max(1, clientWidth - scrollBarWidth);
		scale = canvasWidth / (float)baseWidth;
		fontSize = Math.Max(8, (int)Math.Round(baseFontSize * scale));
		lineHeight = Math.Max(fontSize, (int)Math.Round(baseLineHeight * scale));
	}

	/// <summary>
	/// Scale of the drag preview. The layout that gets scaled is the committed one, so the canvas width of
	/// that layout is the reference - not the startup width, which was already applied by earlier zooms.
	/// </summary>
	public static float PreviewScale(int canvasWidth, int committedCanvasWidth)
	{
		if (canvasWidth <= 0)
		{
			return 1.0f;
		}

		return committedCanvasWidth > 0 ? canvasWidth / (float)committedCanvasWidth : 1.0f;
	}

	/// <summary>
	/// Smallest canvas width that keeps the font size at or above the lower bound.
	/// Never exceeds the configured width, otherwise the minimum would enlarge the canvas and scale the font up.
	/// </summary>
	public static int MinCanvasWidth(int baseCanvasWidth, int baseFontSize)
	{
		int lowerBound = Math.Max(128, (int)Math.Ceiling(baseCanvasWidth * 8.0 / Math.Max(1, baseFontSize)));
		return Math.Min(lowerBound, baseCanvasWidth);
	}

	/// <summary>
	/// Bounds to maximize to: the current size scaled up with its aspect ratio kept, centered in the working
	/// area, and capped by maximamY. Scaling stops as soon as one side reaches the working area.
	/// </summary>
	public static Rectangle MaximizedBounds(Size currentSize, Rectangle workingArea, int maximamY)
	{
		int maxHeight = Math.Min(workingArea.Height, maximamY);
		double scale = Math.Min(workingArea.Width / (double)currentSize.Width, maxHeight / (double)currentSize.Height);
		int width = Math.Max(1, (int)Math.Round(currentSize.Width * scale));
		int height = Math.Max(1, (int)Math.Round(currentSize.Height * scale));
		int left = workingArea.Left + ((workingArea.Width - width) / 2);
		int top = workingArea.Top + ((workingArea.Height - height) / 2);
		return new Rectangle(left, top, width, height);
	}
}
