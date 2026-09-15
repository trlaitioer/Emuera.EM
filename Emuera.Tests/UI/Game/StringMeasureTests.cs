using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using System.Drawing;
using System.Reflection;
using Xunit;

namespace Emuera.Tests.UI.Game;

/// <summary>
/// Covers the GRAPHICS text drawing mode. The layout derives every column, line break and button width from
/// StringMeasure, so the measurement has to describe the same pen advances the drawing uses. Measuring the
/// ink extent and quantizing it with the font independent cell width that the earlier implementation used
/// only reproduced the advances of MS Gothic.
/// </summary>
public class StringMeasureTests
{
	private const string FullWidthSample = "あいうえおかきくけこさしすせそたちつてと";
	private const int WindowX = 760;
	private const int FontSize = 18;
	private const int LineHeight = 19;
	/// <summary>Config.ForeColor is empty unless a configuration file was loaded, so the glyph tests ink with a color of their own.</summary>
	private static readonly Color InkColor = Color.Black;

	[Fact]
	public void Graphics_measurement_is_additive_over_node_boundaries()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			int whole = sm.GetDisplayLength(FullWidthSample, font);
			int sum = 0;
			int offset = 0;
			//node boundaries: a button is a run of styled nodes and each node is measured on its own
			foreach (int length in new[] { 3, 5, 2, 4, 6 })
			{
				sum += sm.GetDisplayLength(FullWidthSample.Substring(offset, length), font);
				offset += length;
			}
			Assert.Equal(FullWidthSample.Length, offset);
			Assert.InRange(sum - whole, -1, 1);
		});
	}

	[Fact]
	public void Graphics_measurement_is_additive_over_character_boundaries()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			int whole = sm.GetDisplayLength(FullWidthSample, font);
			int sum = 0;
			foreach (char c in FullWidthSample)
				sum += sm.GetDisplayLength(c.ToString(), font);
			Assert.InRange(sum - whole, -1, 1);
		});
	}

	[Theory]
	[InlineData("MS Gothic")]
	[InlineData("JetBrains Maple Mono")]
	[InlineData("Sarasa Gothic J")]
	[InlineData("Yu Gothic")]
	public void Graphics_measurement_stays_additive_for_every_font(string family)
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			foreach (int size in new[] { 14, 18 })
			{
				using var font = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
				if (!font.Name.Equals(family, StringComparison.OrdinalIgnoreCase))
					continue;//not installed, the constructor fell back to another family
				int whole = sm.GetDisplayLength(FullWidthSample, font);
				int sum = 0;
				foreach (char c in FullWidthSample)
					sum += sm.GetDisplayLength(c.ToString(), font);
				//A font whose advances are not whole pixels drifts by the rounding of each advance, so the
				//guard allows a pixel per character. The quantization this fix removed missed by up to
				//1.5 pixels per character (JetBrains Maple Mono: +120 over 20 characters).
				Assert.InRange(Math.Abs(sum - whole), 0, FullWidthSample.Length);
			}
		});
	}

	[Fact]
	public void Graphics_measurement_keeps_the_width_of_nodes_that_hold_only_spaces()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			//html separates buttons with nodes that hold nothing but spaces. Measuring the strings with GDI+ and
			//GenericTypographic gave those nodes a zero width, which collapsed the separators
			int space = sm.GetDisplayLength("　", font);
			Assert.True(space > 0, "a full width space has to occupy room");
			Assert.Equal(2 * space, sm.GetDisplayLength("　　", font));
			Assert.Equal(5 * space, sm.GetDisplayLength("　　　　　", font));
			int halfSpace = sm.GetDisplayLength(" ", font);
			Assert.True(halfSpace > 0, "a half width space has to occupy room");
			//the half width one stays half of the full width one
			Assert.InRange(halfSpace * 2 - space, -1, 1);
			//and the spaces between two buttons add to the line instead of collapsing
			int buttons = sm.GetDisplayLength("[110] - A", font) + sm.GetDisplayLength("[111] - B", font);
			int line = sm.GetDisplayLength("[110] - A　　　　　[111] - B", font);
			Assert.InRange(line - buttons, 5 * space - 2, 5 * space + 2);
		});
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Measurement_agrees_on_a_thread_that_does_not_own_the_measure(bool graphicsMode)
	{
		//Parsing folds constant expressions such as STRLENSU(GETLINESTR("─")) on pool threads, and it
		//prints its progress on the same thread, while the StringMeasure was built with the console. The
		//shared Graphics belongs to that other thread, so measuring off it has to fall back to a device
		//context of its own and still report the same advances, or a folded #DIM initial value and the
		//header load reports would both differ from the same work done at run time. Both drawing modes
		//need this: TEXTRENDERER is the default one and its TextRenderer overload rejects a null context.
		TextDrawingMode mode = graphicsMode ? TextDrawingMode.GRAPHICS : TextDrawingMode.TEXTRENDERER;
		WithMode(mode, sm =>
		{
			Font font = DefaultFont();
			string[] samples = ["─", "あ", "あいうえお", "　　　　　", "abc def", "[110] - 投放"];
			int[] expected = [.. samples.Select(s => sm.GetDisplayLength(s, font))];

			int[]? fromOwnThread = null;
			int[]? fromPoolThread = null;
			System.Threading.Tasks.Task.Run(() =>
			{
				fromOwnThread = [.. samples.Select(s => sm.GetDisplayLength(s, font))];
			}).Wait();
			System.Threading.Tasks.Task.Run(() =>
			{
				fromPoolThread = [.. samples.Select(s => sm.GetDisplayLength(s.AsSpan(), font))];
			}).Wait();

			Assert.NotNull(fromOwnThread);
			Assert.NotNull(fromPoolThread);
			Assert.Equal(expected, fromOwnThread);
			Assert.Equal(expected, fromPoolThread);
		});
	}

	[Fact]
	public void Both_measurement_overloads_reject_the_unsupported_drawing_mode()
	{
		//WINAPI is not implemented. Loading a configuration replaces it with TEXTRENDERER, so it can only be
		//reached through the configuration object, and both overloads have to reject it the same way
		WithMode(TextDrawingMode.WINAPI, sm =>
		{
			Font font = DefaultFont();
			Assert.Throws<Exception>(() => sm.GetDisplayLength("あ", font));
			Assert.Throws<Exception>(() => sm.GetDisplayLength("あ".AsSpan(), font));
			//empty text is answered before the mode is looked at, by both overloads
			Assert.Equal(0, sm.GetDisplayLength("", font));
			Assert.Equal(0, sm.GetDisplayLength(ReadOnlySpan<char>.Empty, font));
		});
	}

	[Fact]
	public void Graphics_measurement_of_a_span_equals_the_measurement_of_the_string()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			//the print paths measure slices of the line as spans, so both overloads have to agree. The span
			//overload forwards to the string one, which owns the unsupported mode check; this pins that the two
			//cannot drift apart into separate implementations
			foreach (string sample in new[] { "あ", "あいうえお", "　　　　　", "あ　あ", FullWidthSample })
			{
				int fromString = sm.GetDisplayLength(sample, font);
				int fromSpan = sm.GetDisplayLength(sample.AsSpan(), font);
				int fromSlice = sm.GetDisplayLength((sample + "XX").AsSpan(0, sample.Length), font);
				Assert.Equal(fromString, fromSpan);
				Assert.Equal(fromString, fromSlice);
			}
		});
	}

	[Fact]
	public void Graphics_measurement_matches_the_gdi_advance_for_the_default_font()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			using var bmp = new Bitmap(16, 16);
			using var graph = Graphics.FromImage(bmp);
			foreach (string sample in new[] { "あ", "あいうえお", "こんにちは世界", FullWidthSample })
			{
				int expected = System.Windows.Forms.TextRenderer.MeasureText(graph, sample.AsSpan(), font,
					new Size(WindowX * 2, LineHeight),
					System.Windows.Forms.TextFormatFlags.NoPadding | System.Windows.Forms.TextFormatFlags.NoPrefix).Width;
				int actual = sm.GetDisplayLength(sample, font);
				//the two engines only agree exactly on fonts whose advances are the em based cell widths
				Assert.InRange(Math.Abs(actual - expected), 0, expected / 20);
			}
		});
	}

	[Fact]
	public void Graphics_glyphs_drawn_node_by_node_land_where_the_whole_line_lands()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			//the layout the engine builds: whole node widths with the fractional remainder carried forward
			int[] widths = new int[FullWidthSample.Length];
			float carry = 0;
			for (int i = 0; i < FullWidthSample.Length; i++)
			{
				float widF = carry + sm.GetDisplayLength(FullWidthSample[i].ToString(), font);
				widths[i] = (int)widF;
				carry = widF - widths[i];
			}

			using var nodeByNode = NewCanvas();
			using (var graph = Graphics.FromImage(nodeByNode))
			{
				float penX = 0;
				for (int i = 0; i < FullWidthSample.Length; i++)
				{
					GraphicsTextRenderer.Draw(graph, FullWidthSample[i].ToString(), font, InkColor,
						(int)penX, penX - (int)penX, 0);
					penX += widths[i];
				}
			}

			using var whole = NewCanvas();
			using (var graph = Graphics.FromImage(whole))
				GraphicsTextRenderer.Draw(graph, FullWidthSample, font, InkColor, 0, 0, 0);

			Assert.InRange(Math.Abs(LeftmostInk(whole) - LeftmostInk(nodeByNode)), 0, 1);
			Assert.InRange(Math.Abs(RightmostInk(whole) - RightmostInk(nodeByNode)), 0, 1);
		});
	}

	[Fact]
	public void Graphics_glyphs_after_a_space_only_node_land_where_the_whole_line_lands()
	{
		WithMode(TextDrawingMode.GRAPHICS, sm =>
		{
			Font font = DefaultFont();
			//the shape html produces around buttons: text, a node of separators, text again
			string[] nodes = ["[110] - 投放", "　　　　　", "[111] - 经营"];
			string joined = string.Concat(nodes);

			var pens = new (int pointX, float subPixel)[nodes.Length];
			int pointX = 0;
			float carry = 0;
			for (int i = 0; i < nodes.Length; i++)
			{
				pens[i] = (pointX, carry);
				float widF = carry + sm.GetDisplayLength(nodes[i], font);
				pointX += (int)widF;
				carry = widF - (int)widF;
			}

			using var split = NewCanvas();
			using (var graph = Graphics.FromImage(split))
			{
				for (int i = 0; i < nodes.Length; i++)
					GraphicsTextRenderer.Draw(graph, nodes[i], font, InkColor, pens[i].pointX, pens[i].subPixel, 0);
			}
			using var whole = NewCanvas();
			using (var graph = Graphics.FromImage(whole))
				GraphicsTextRenderer.Draw(graph, joined, font, InkColor, 0, 0, 0);

			//the separator node has to move the following button: the last button starts at the pen the widths
			//gave it, and the whole split sequence ends where drawing the joined line ends
			using var lastNode = NewCanvas();
			using (var graph = Graphics.FromImage(lastNode))
				GraphicsTextRenderer.Draw(graph, nodes[2], font, InkColor, pens[2].pointX, 0, 0);
			int lastNodeInk = LeftmostInk(lastNode);
			Assert.InRange(lastNodeInk, pens[2].pointX, pens[2].pointX + 2);
			Assert.InRange(Math.Abs(RightmostInk(split) - RightmostInk(whole)), 0, 2);
		});
	}

	[Fact]
	public void ShapePositionShift_compensates_only_the_gdi_text_engine()
	{
		WithMode(TextDrawingMode.GRAPHICS, _ => Assert.Equal(0, Config.DrawingParam_ShapePositionShift));
		WithMode(TextDrawingMode.TEXTRENDERER, _ => Assert.Equal(Math.Max(2, Config.FontSize / 6), Config.DrawingParam_ShapePositionShift));
	}

	private static Font DefaultFont()
	{
		EnsureFontName();
		Font font = Config.DefaultFont;
		Assert.NotNull(font);
		return font;
	}

	/// <summary>
	/// Gives Config the font name it would have after loading the built in defaults. Tests do not load a
	/// configuration file, and without a name Config.DefaultFont falls back to an arbitrary UI font.
	/// Config.SetConfig is not an option: it assigns unrelated statics such as IgnoreCase, which other test
	/// classes depend on. The assignment is kept for the rest of the run, like the other Config statics tests
	/// set through ApplyWindowGeometry.
	/// </summary>
	private static void EnsureFontName()
	{
		if (!string.IsNullOrEmpty(Config.FontName))
			return;
		SetFontNameForTests("ＭＳ ゴシック");
	}

	private static void SetFontNameForTests(string name)
	{
		PropertyInfo property = typeof(Config).GetProperty(nameof(Config.FontName), BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("Config.FontName not found");
		property.SetValue(null, name);
	}

	/// <summary>An all white canvas of the size the glyph tests use.</summary>
	private static Bitmap NewCanvas()
	{
		var bmp = new Bitmap(600, LineHeight * 2);
		using var graph = Graphics.FromImage(bmp);
		graph.Clear(Color.White);
		return bmp;
	}

	/// <summary>Leftmost, respectively rightmost, column that holds ink.</summary>
	private static int LeftmostInk(Bitmap bmp)
	{
		for (int x = 0; x < bmp.Width; x++)
			for (int y = 0; y < bmp.Height; y++)
				if (bmp.GetPixel(x, y).ToArgb() != Color.White.ToArgb())
					return x;
		return -1;
	}

	private static int RightmostInk(Bitmap bmp)
	{
		for (int x = bmp.Width - 1; x >= 0; x--)
			for (int y = 0; y < bmp.Height; y++)
				if (bmp.GetPixel(x, y).ToArgb() != Color.White.ToArgb())
					return x;
		return -1;
	}

	/// <summary>
	/// Runs the action with the given drawing mode. The mode is read when StringMeasure is constructed and
	/// when the derived geometry is recomputed, so the action runs with both already settled.
	/// </summary>
	private static void WithMode(TextDrawingMode mode, Action<StringMeasure> action)
	{
		EnsureFontName();
		TextDrawingMode original = Config.TextDrawingMode;
		try
		{
			SetTextDrawingMode(mode);
			Config.ApplyWindowGeometry(WindowX, 480, FontSize, LineHeight);
			Assert.Equal(mode, Config.TextDrawingMode);
			using var sm = new StringMeasure();
			action(sm);
		}
		finally
		{
			SetTextDrawingMode(original);
			Config.ApplyWindowGeometry(WindowX, 480, FontSize, LineHeight);
		}
	}

	/// <summary>
	/// Assigns the drawing mode, which only the configuration file does in the product. Applying the window
	/// geometry afterwards recomputes the derived values, as loading a configuration would.
	/// </summary>
	private static void SetTextDrawingMode(TextDrawingMode mode)
	{
		PropertyInfo property = typeof(Config).GetProperty(nameof(Config.TextDrawingMode), BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("Config.TextDrawingMode not found");
		property.SetValue(null, mode);
	}
}
