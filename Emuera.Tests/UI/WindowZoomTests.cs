using MinorShift.Emuera.UI;
using System.Drawing;
using Xunit;

namespace Emuera.Tests.UI;

/// <summary>
/// Covers the geometry of the runtime window zoom, including the reference frame of the drag preview:
/// the preview scales the committed layout, so it must not use the startup width.
/// </summary>
public class WindowZoomTests
{
	private const int CanvasWidth = 760;
	private const int ScrollBarWidth = 17;
	private const int FontSize = 18;
	private const int LineHeight = 19;

	private static void Compute(int clientWidth, out float scale, out int scrollBarWidth, out int fontSize, out int lineHeight)
	{
		WindowZoom.Compute(clientWidth, CanvasWidth, ScrollBarWidth, FontSize, LineHeight,
			out scale, out scrollBarWidth, out fontSize, out lineHeight);
	}

	[Fact]
	public void Compute_at_the_startup_size_changes_nothing()
	{
		Compute(CanvasWidth + ScrollBarWidth, out float scale, out int scrollBarWidth, out int fontSize, out int lineHeight);
		Assert.Equal(1.0f, scale);
		Assert.Equal(ScrollBarWidth, scrollBarWidth);
		Assert.Equal(FontSize, fontSize);
		Assert.Equal(LineHeight, lineHeight);
	}

	[Fact]
	public void Compute_scales_the_font_and_the_scroll_bar()
	{
		//40px wider canvas: the grown scroll bar shrinks it again
		Compute(CanvasWidth + ScrollBarWidth + 400, out float scale, out int scrollBarWidth, out int fontSize, out int lineHeight);
		Assert.Equal(26, scrollBarWidth);
		int canvasWidth = CanvasWidth + ScrollBarWidth + 400 - scrollBarWidth;
		Assert.Equal(canvasWidth / (float)CanvasWidth, scale);
		Assert.Equal((int)Math.Round(FontSize * scale), fontSize);
		Assert.Equal((int)Math.Round(LineHeight * scale), lineHeight);
	}

	[Fact]
	public void Compute_keeps_the_font_size_at_its_lower_bound()
	{
		Compute(200, out float scale, out _, out int fontSize, out int lineHeight);
		Assert.Equal(8, fontSize);
		Assert.True(lineHeight >= fontSize);
		Assert.True(scale < 1.0f);
	}

	[Fact]
	public void Compute_does_not_depend_on_the_previous_state()
	{
		//the geometry is always derived from the startup sizes, so two equal drags give two equal results
		Compute(1400, out float first, out int firstScrollBar, out int firstFont, out int firstLineHeight);
		Compute(1400, out float second, out int secondScrollBar, out int secondFont, out int secondLineHeight);
		Assert.Equal(first, second);
		Assert.Equal(firstScrollBar, secondScrollBar);
		Assert.Equal(firstFont, secondFont);
		Assert.Equal(firstLineHeight, secondLineHeight);
	}

	[Fact]
	public void PreviewScale_is_relative_to_the_committed_layout()
	{
		//first zoom: committed layout is still the startup one
		Assert.Equal(1151 / (float)CanvasWidth, WindowZoom.PreviewScale(1151, CanvasWidth));
		//second zoom from a committed layout of 1151: scaling by 1151/760 would overshoot the canvas
		Assert.Equal(1400 / 1151f, WindowZoom.PreviewScale(1400, 1151));
		Assert.Equal(1.0f, WindowZoom.PreviewScale(1151, 1151));
		Assert.Equal(1.0f, WindowZoom.PreviewScale(0, 1151));
	}

	[Fact]
	public void MinCanvasWidth_keeps_the_font_lower_bound_without_enlarging_the_window()
	{
		//default: shrinking is allowed down to the width where the font size reaches 8px
		Assert.Equal((int)Math.Ceiling(CanvasWidth * 8.0 / FontSize), WindowZoom.MinCanvasWidth(CanvasWidth, FontSize));
		//a narrow window can shrink to the historical lower bound, which still keeps the font above 8px
		Assert.Equal(128, WindowZoom.MinCanvasWidth(200, FontSize));
		//never above the configured width, otherwise the minimum would scale the font up
		Assert.Equal(100, WindowZoom.MinCanvasWidth(100, FontSize));
		Assert.Equal(64, WindowZoom.MinCanvasWidth(64, FontSize));
	}

	[Fact]
	public void MaximizedBounds_keeps_the_aspect_ratio_and_centers()
	{
		//limited by the height: the window keeps the ratio and sits centered horizontally
		Rectangle bounds = WindowZoom.MaximizedBounds(new Size(777, 504), new Rectangle(0, 0, 1920, 1040), 2560);
		Assert.Equal(1040, bounds.Height);
		Assert.Equal((int)Math.Round(777 * (1040 / 504.0)), bounds.Width);
		Assert.Equal((1920 - bounds.Width) / 2, bounds.Left);
		Assert.Equal(0, bounds.Top);
	}

	[Fact]
	public void MaximizedBounds_centers_vertically_when_the_height_is_capped()
	{
		//maximamY caps the height, so the window is centered in the working area instead of sticking to the top
		Rectangle bounds = WindowZoom.MaximizedBounds(new Size(800, 600), new Rectangle(0, 0, 1920, 3000), 2560);
		Assert.Equal(new Rectangle(0, 780, 1920, 1440), bounds);
	}

	[Fact]
	public void MaximizedBounds_centers_horizontally_when_the_width_is_limited()
	{
		Rectangle bounds = WindowZoom.MaximizedBounds(new Size(400, 800), new Rectangle(0, 0, 1000, 1000), 2560);
		Assert.Equal(new Rectangle(250, 0, 500, 1000), bounds);
	}

	[Fact]
	public void MaximizedBounds_keeps_the_working_area_origin()
	{
		//a secondary monitor can have a negative origin
		Rectangle bounds = WindowZoom.MaximizedBounds(new Size(777, 504), new Rectangle(-1920, 0, 1920, 1040), 2560);
		Assert.Equal(-1920 + (1920 - bounds.Width) / 2, bounds.Left);
		Assert.Equal(1040, bounds.Height);
	}
}
