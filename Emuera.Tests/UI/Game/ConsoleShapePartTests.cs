using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using Xunit;
using MixedNum = MinorShift.Emuera.Runtime.Utils.EvilMask.Utils.MixedNum;

namespace Emuera.Tests.UI.Game;

/// <summary>
/// Covers the shape geometry: a value given without a unit is relative to the font size and has to be resolved
/// again after a runtime zoom, while a px value stays absolute.
/// </summary>
public class ConsoleShapePartTests
{
	private const int WindowX = 760;
	private const int FontSize = 18;
	private const int LineHeight = 19;

	[Fact]
	public void Space_without_a_unit_follows_the_font_size()
	{
		WithDefaultGeometry(() =>
		{
			ConsoleShapePart space = Create("space", Num(100));
			space.SetWidth(null!, 0);
			Assert.Equal(FontSize, space.Width);

			GrowFont();
			space.ResetLayout(null!);
			space.SetWidth(null!, 0);
			Assert.Equal(FontSize * 2, space.Width);
		});
	}

	[Fact]
	public void Space_in_px_stays_absolute()
	{
		WithDefaultGeometry(() =>
		{
			ConsoleShapePart space = Create("space", Num(20, true));
			space.SetWidth(null!, 0);
			Assert.Equal(20, space.Width);

			GrowFont();
			space.ResetLayout(null!);
			space.SetWidth(null!, 0);
			Assert.Equal(20, space.Width);
		});
	}

	[Fact]
	public void Rectangle_without_a_unit_follows_the_font_size()
	{
		WithDefaultGeometry(() =>
		{
			ConsoleShapePart rect = Create("rect", Num(0), Num(0), Num(100), Num(200));
			rect.SetWidth(null!, 0);
			Assert.Equal(FontSize, rect.Width);
			Assert.Equal(FontSize * 2, rect.Bottom);

			GrowFont();
			rect.ResetLayout(null!);
			rect.SetWidth(null!, 0);
			Assert.Equal(FontSize * 2, rect.Width);
			Assert.Equal(FontSize * 4, rect.Bottom);
		});
	}

	[Fact]
	public void Rectangle_in_px_stays_absolute()
	{
		WithDefaultGeometry(() =>
		{
			ConsoleShapePart rect = Create("rect", Num(0, true), Num(0, true), Num(20, true), Num(40, true));
			rect.SetWidth(null!, 0);
			Assert.Equal(20, rect.Width);
			Assert.Equal(40, rect.Bottom);

			GrowFont();
			rect.ResetLayout(null!);
			rect.SetWidth(null!, 0);
			Assert.Equal(20, rect.Width);
			Assert.Equal(40, rect.Bottom);
		});
	}

	[Fact]
	public void Rectangle_of_one_line_follows_the_font_size()
	{
		WithDefaultGeometry(() =>
		{
			ConsoleShapePart rect = Create("rect", Num(150));
			rect.SetWidth(null!, 0);
			Assert.Equal(27, rect.Width);

			GrowFont();
			rect.ResetLayout(null!);
			rect.SetWidth(null!, 0);
			Assert.Equal(54, rect.Width);
		});
	}

	private static ConsoleShapePart Create(string type, params MixedNum[] param)
	{
		return ConsoleShapePart.CreateShape(type, param, Config.ForeColor, Config.FocusColor, false);
	}

	private static MixedNum Num(int num, bool isPx = false)
	{
		return new MixedNum { num = num, isPx = isPx };
	}

	/// <summary>Doubles the font size like a zoomed window does.</summary>
	private static void GrowFont()
	{
		Config.ApplyWindowGeometry(WindowX * 2, 600, FontSize * 2, LineHeight * 2);
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
}
