using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.UI.Game;
using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using Xunit;

namespace Emuera.Tests.UI.Game;

/// <summary>
/// Covers the bar string builder behind CUSTOMDRAWLINE and GETLINESTR.
/// <para>
/// The optimized form starts from an estimated repetition count, so these pin that it still returns
/// exactly what growing the string one repetition at a time returns: the character by character
/// reference below is the original implementation and the two must agree byte for byte. Widths at the
/// boundary where the two forms differ are asserted on their own rather than folded into that loop.
/// </para>
/// <para>
/// BuildStBar is only usable while one repetition has a positive measured width: both of its width
/// loops advance on a width that a narrower repetition cannot grow. Plain empty and null input is
/// rejected before the builder by printCustomBar and GETLINESTR, and the configured DRAWLINE
/// character falls back to a printable default, so the non-terminating combinations are not reachable
/// from the product and are not invoked here. See the getStBar entry of
/// tracker/performance-optimization/issues/04-parse-parallel.md for the residual case.
/// </para>
/// </summary>
public class StBarTests
{
	private const int WindowX = 760;
	private const int WindowY = 480;
	private const int FontSize = 18;
	private const int LineHeight = 19;

	/// <summary>A representative set of bar strings: single full width glyphs, multi character runs and mixes.</summary>
	private static readonly string[] Bars =
	[
		"─",
		"ー",
		"＝",
		"・",
		"-",
		"ab",
		"あい",
		"─=",
		"=─",
		"─・=",
	];

	/// <summary>
	/// Target widths around the interesting boundaries of the samples above. Only positive widths are
	/// listed: the equivalence below holds for those, and zero is asserted separately because the two
	/// forms differ there.
	/// </summary>
	private static readonly int[] Targets =
	[
		1, 2, 3, 8, 17, 18, 19, 35, 36, 37, 100, 199, 361, 720, 758, 760,
	];

	[Fact]
	public void BuildStBar_matches_growing_one_repetition_at_a_time()
	{
		WithMeasure((measure, font) =>
		{
			foreach (string bar in Bars)
			{
				foreach (int target in Targets)
				{
					string expected = GrowOneAtATime(measure, bar, target, font);
					string actual = EmueraConsole.BuildStBar(measure, bar, target, font);
					Assert.Equal(expected, actual);
				}
			}
		});
	}

	/// <summary>
	/// The premise BuildStBar's loops need, and the reason an empty bar cannot be passed to it: a
	/// repetition that measures zero leaves both loops without a value to advance on.
	/// </summary>
	[Fact]
	public void GetDisplayLength_is_zero_for_text_that_cannot_advance_the_loops()
	{
		WithMeasure((measure, font) =>
		{
			Assert.Equal(0, measure.GetDisplayLength("", font));
			//Keeps the null through the string overload rather than the span one, which is the path a
			//reflection-free caller takes. The measurement answers null before it looks at anything
			//else, so the argument being unguarded here is deliberate.
			string? noBar = null;
			Assert.Equal(0, measure.GetDisplayLength(noBar!, font));
			//The width is what the loops check, so input that is not empty is no safer: a zero width
			//character cannot bound the repetition count either.
			Assert.Equal(0, measure.GetDisplayLength("\u200B", font));
			Assert.True(measure.GetDisplayLength("abc", font) > 0);
		});
	}

	/// <summary>
	/// Pins the one input where the builder and the algorithm it replaces part ways. The optimized
	/// form only appends while below the target, so a target of zero yields nothing, while the original
	/// seeded a repetition before its first measurement and therefore returned that repetition. The
	/// builder is fed Config.DrawableWidth in the product, which is never zero, and a bar of zero width
	/// is not printable, so this is a boundary the caller owns rather than the builder.
	/// </summary>
	[Fact]
	public void BuildStBar_seeds_nothing_at_a_zero_target_where_the_original_seeded_a_repetition()
	{
		WithMeasure((measure, font) =>
		{
			Assert.Equal("─", GrowOneAtATime(measure, "─", 0, font));
			Assert.Equal("", EmueraConsole.BuildStBar(measure, "─", 0, font));
		});
	}

	/// <summary>
	/// getStBar is the entry point the product reaches without passing a width, and it uses
	/// Config.DrawableWidth. That width therefore has to keep the builder usable: a value that let a
	/// repetition measure as nothing would leave the loops below without a value to advance on.
	/// </summary>
	[Fact]
	public void Configured_drawable_width_allows_a_usable_bar()
	{
		WithMeasure((measure, font) =>
		{
			Assert.True(Config.DrawableWidth > 0, "the configured drawable width must allow a repetition");
			Assert.True(measure.GetDisplayLength("─", font) > 0);
		});
	}

	/// <summary>
	/// The original implementation: seed one repetition, then grow until the width reaches the target,
	/// then drop single characters until it no longer exceeds it.
	/// </summary>
	private static string GrowOneAtATime(StringMeasure measure, string barStr, int targetWidth, Font font)
	{
		var builder = new StringBuilder();
		builder.Append(barStr);
		int width = 0;
		while (width < targetWidth)
		{
			builder.Append(barStr);
			width = measure.GetDisplayLength(builder.ToString(), font);
		}
		while (width > targetWidth)
		{
			builder.Remove(builder.Length - 1, 1);
			width = measure.GetDisplayLength(builder.ToString(), font);
		}
		return builder.ToString();
	}

	/// <summary>
	/// Runs the body with the GRAPHICS drawing mode, the font name set and the window geometry applied,
	/// then restores the mode. Config.DrawableWidth depends on the mode (the shape position shift is
	/// zero only in GRAPHICS), so pinning it keeps the widths these tests use independent of the order
	/// the test classes happen to run in.
	/// </summary>
	private static void WithMeasure(Action<StringMeasure, Font> body)
	{
		EnsureFontName();
		TextDrawingMode original = Config.TextDrawingMode;
		try
		{
			SetTextDrawingMode(TextDrawingMode.GRAPHICS);
			Config.ApplyWindowGeometry(WindowX, WindowY, FontSize, LineHeight);
			//The samples are compared through the targetWidth parameter, but getStBar feeds it
			//Config.DrawableWidth. Keep them equal so the two entry points cannot drift apart unnoticed.
			Assert.Equal(WindowX, Config.DrawableWidth);
			using var measure = new StringMeasure();
			body(measure, Config.DefaultFont);
		}
		finally
		{
			SetTextDrawingMode(original);
			Config.ApplyWindowGeometry(WindowX, WindowY, FontSize, LineHeight);
		}
	}

	/// <summary>
	/// Assigns the drawing mode, which only the configuration file does in the product. Applying the
	/// window geometry afterwards recomputes the derived values, as loading a configuration would.
	/// </summary>
	private static void SetTextDrawingMode(TextDrawingMode mode)
	{
		PropertyInfo property = typeof(Config).GetProperty(nameof(Config.TextDrawingMode), BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("Config.TextDrawingMode not found");
		property.SetValue(null, mode);
	}

	/// <summary>
	/// Config.FontName is empty unless a configuration file was loaded, and StringMeasure needs a real
	/// font. Kept identical to StringMeasureTests so both classes measure with the same face.
	/// </summary>
	private static void EnsureFontName()
	{
		if (!string.IsNullOrEmpty(Config.FontName))
			return;
		PropertyInfo property = typeof(Config).GetProperty(nameof(Config.FontName), BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("Config.FontName not found");
		property.SetValue(null, "ＭＳ ゴシック");
	}
}
