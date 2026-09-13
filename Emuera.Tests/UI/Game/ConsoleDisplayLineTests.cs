using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using System.Drawing;
using Xunit;

namespace Emuera.Tests.UI.Game;

/// <summary>
/// Covers ConsoleDisplayLine.ReLayout, which rebuilds the layout after a runtime window zoom.
/// Line breaks are kept, so only widths, positions and alignment have to be reproduced.
/// </summary>
public class ConsoleDisplayLineTests
{
	/// <summary>Node whose width is proportional to the font size.</summary>
	private sealed class FakeNode : AConsoleDisplayNode
	{
		private readonly int baseWidth;

		public FakeNode(int baseWidth)
		{
			this.baseWidth = baseWidth;
		}

		public override bool CanDivide => true;

		public override void SetWidth(StringMeasure sm, float subPixel)
		{
			Width = Math.Max(1, (int)Math.Round(baseWidth * FontScale));
			XsubPixel = 0;
		}

		public override void ResetLayout(StringMeasure sm)
		{
			Width = -1;
		}

		public override void DrawTo(Graphics graph, int pointY, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false, bool scalePreview = false) { }
	}

	private const int WindowX = 760;
	private const int FontSize = 18;
	private const int LineHeight = 19;

	/// <summary>Widths multiply by this factor, emulating a larger font.</summary>
	private static float FontScale = 1.0f;

	[Fact]
	public void ReLayout_at_same_font_size_keeps_positions()
	{
		WithDefaultGeometry(() =>
		{
			AssertUnchanged(() => SingleLine(true, DisplayLineAlignment.LEFT, -1, 100, 40));
			AssertUnchanged(() => SingleLine(false, DisplayLineAlignment.LEFT, -1, 100, 40));
			AssertUnchanged(() => SingleLine(false, DisplayLineAlignment.CENTER, -1, 100, 40));
			AssertUnchanged(() => SingleLine(false, DisplayLineAlignment.RIGHT, -1, 100, 40));
			//customWidth is used by the html div sub division
			AssertUnchanged(() => SingleLine(false, DisplayLineAlignment.CENTER, 400, 100, 40));
			//lines joined by a PRINT without line end keep the alignment they were created with
			AssertUnchanged(() => Joined(
				SingleLine(true, DisplayLineAlignment.LEFT, -1, 100),
				SingleLine(true, DisplayLineAlignment.LEFT, -1, 30)));
			AssertUnchanged(() => Joined(
				SingleLine(false, DisplayLineAlignment.CENTER, -1, 100),
				SingleLine(false, DisplayLineAlignment.CENTER, -1, 30)));
			AssertUnchanged(() => Joined(
				SingleLine(false, DisplayLineAlignment.RIGHT, -1, 100),
				SingleLine(true, DisplayLineAlignment.LEFT, -1, 30)));
		});
	}

	[Fact]
	public void ReLayout_scales_positions_with_the_font_size()
	{
		WithDefaultGeometry(() =>
		{
			var line = Joined(
				SingleLine(true, DisplayLineAlignment.LEFT, -1, 100),
				SingleLine(true, DisplayLineAlignment.LEFT, -1, 30));
			int[] before = Positions(line);

			ScaleFont(2.0f, () =>
			{
				line.ReLayout(null!);
				int[] after = Positions(line);
				Assert.Equal(before.Length, after.Length);
				for (int i = 0; i < before.Length; i++)
					Assert.Equal(before[i] * 2, after[i]);
			});
		});
	}

	[Fact]
	public void ReLayout_centers_again_for_the_new_canvas_width()
	{
		WithDefaultGeometry(() =>
		{
			var line = SingleLine(false, DisplayLineAlignment.CENTER, -1, 100, 40);
			int before = line.Buttons[0].PointX;

			ScaleFont(2.0f, () =>
			{
				line.ReLayout(null!);
				int width = line.Buttons[0].Width;
				int expected = Config.DrawableWidth / 2 - width / 2;
				Assert.Equal(expected, line.Buttons[0].PointX);
				//doubling the canvas and the font doubles the margin up to integer rounding
				Assert.InRange(line.Buttons[0].PointX, before * 2 - 1, before * 2 + 1);
			});
		});
	}

	[Fact]
	public void ApplyWindowGeometry_recomputes_derived_values()
	{
		WithDefaultGeometry(() =>
		{
			Assert.Equal(WindowX - FontSize / 6, Config.DrawableWidth);

			Config.ApplyWindowGeometry(1520, 600, 36, 20);
			Assert.Equal(1520 - 36 / 6, Config.DrawableWidth);
			//the line height never goes below the font size
			Assert.Equal(36, Config.LineHeight);

			Config.ApplyWindowGeometry(1520, 600, 30, 40);
			Assert.Equal(40, Config.LineHeight);

			//the font size has a lower bound of 8px
			Config.ApplyWindowGeometry(200, 100, 4, 3);
			Assert.Equal(8, Config.FontSize);
			Assert.Equal(8, Config.LineHeight);
			Assert.Equal(200 - 2, Config.DrawableWidth);
		});
	}

	private static void AssertUnchanged(Func<ConsoleDisplayLine> build)
	{
		ConsoleDisplayLine line = build();
		int[] before = Positions(line);
		line.ReLayout(null!);
		Assert.Equal(before, Positions(line));
	}

	private static void WithDefaultGeometry(Action action)
	{
		int windowX = Config.WindowX;
		int windowY = Config.WindowY;
		int fontSize = Config.FontSize;
		int lineHeight = Config.LineHeight;
		Config.ApplyWindowGeometry(WindowX, 480, FontSize, LineHeight);
		try
		{
			action();
		}
		finally
		{
			Config.ApplyWindowGeometry(Math.Max(1, windowX), Math.Max(1, windowY), Math.Max(8, fontSize), Math.Max(8, lineHeight));
		}
	}

	/// <summary>Doubles the widths and the canvas like a zoomed window does.</summary>
	private static void ScaleFont(float scale, Action action)
	{
		float previousScale = FontScale;
		int windowX = Config.WindowX;
		FontScale = scale;
		Config.ApplyWindowGeometry(WindowX * (int)scale, 480, FontSize * (int)scale, LineHeight * (int)scale);
		try
		{
			action();
		}
		finally
		{
			FontScale = previousScale;
			Config.ApplyWindowGeometry(windowX, 480, FontSize, LineHeight);
		}
	}

	private static ConsoleDisplayLine SingleLine(bool logical, DisplayLineAlignment align, int customWidth, params int[] widths)
	{
		ConsoleDisplayLine line = new([Button(widths)], logical, false);
		LayoutButtons(line.Buttons);
		line.SetAlignment(align, customWidth);
		return line;
	}

	/// <summary>Joins lines the same way EmueraConsole.addDisplayLine does.</summary>
	private static ConsoleDisplayLine Joined(ConsoleDisplayLine previous, ConsoleDisplayLine next)
	{
		next.ShiftPositionX(previous.Buttons[^1].PointX + previous.Buttons[^1].Width);
		next.PrependButtons(previous);
		return next;
	}

	private static ConsoleButtonString Button(params int[] widths)
	{
		AConsoleDisplayNode[] nodes = new AConsoleDisplayNode[widths.Length];
		for (int i = 0; i < widths.Length; i++)
			nodes[i] = new FakeNode(widths[i]);
		return new ConsoleButtonString(null!, nodes);
	}

	/// <summary>Mirrors PrintStringBuffer.setWidthToButtonList, which runs while printing.</summary>
	private static void LayoutButtons(ConsoleButtonString[] buttons)
	{
		int pointX = 0;
		float subPixel = 0.0f;
		foreach (ConsoleButtonString button in buttons)
		{
			button.CalcWidth(null!, subPixel);
			button.CalcPointX(pointX);
			pointX = button.PointX + button.Width;
			subPixel = button.XsubPixel;
		}
	}

	private static int[] Positions(ConsoleDisplayLine line)
	{
		List<int> positions = [];
		foreach (ConsoleButtonString button in line.Buttons)
		{
			positions.Add(button.PointX);
			foreach (AConsoleDisplayNode node in button.StrArray)
				positions.Add(node.PointX);
		}
		return [.. positions];
	}
}
