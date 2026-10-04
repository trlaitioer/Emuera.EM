using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Xunit;

namespace Emuera.Tests.UI;

/// <summary>
/// The reverse lookup is what keeps a font swap from changing the column layout, so what has to hold is that
/// the baseline font stays where it is and that every other font lands on the baseline font's cell. Advances
/// are quantized snapshots of the fonts installed on the machine, so the assertions are those invariants and
/// never a concrete derived size (see ui-modernization-03).
/// </summary>
public class FontMetricsTests
{
	private const int BaseFontSize = 18;
	private const int WindowX = 760;
	private const int LineHeight = 19;

	/// <summary>Baseline first; the rest are the faces the ticket measured. Absent ones are skipped.</summary>
	public static TheoryData<string> Families =>
	[
		FontMetrics.BaselineFontName,
		"JetBrains Maple Mono",
		"Sarasa Gothic J",
		"Yu Gothic",
		"MS PGothic",
		"MS UI Gothic",
	];

	[Fact]
	public void Baseline_font_keeps_the_configured_size()
	{
		//The baseline font is the font the cell is compared against, so its own error is zero at every size;
		//sizes that advance the way another size does (an advance only takes discrete values) must not drag it
		//off that size either
		for (int size = 8; size <= 30; size++)
		{
			Assert.Equal(size, FontMetrics.FindRenderSizePx(FontMetrics.BaselineFontName, size));
			Assert.Equal(size, FontMetrics.FindRenderSizePx(null, size));
			Assert.Equal(size, FontMetrics.FindRenderSizePx("", size));
		}
	}

	[Theory]
	[MemberData(nameof(Families))]
	public void Lookup_lands_on_the_baseline_cell_or_one_pixel_next_to_it(string family)
	{
		if (!IsInstalled(family))
			return;
		for (int size = 8; size <= 30; size++)
		{
			int baselineCell = MeasureAdvance(FontMetrics.BaselineFontName, size);
			int cell = MeasureAdvance(family, FontMetrics.FindRenderSizePx(family, size));
			Assert.InRange(Math.Abs(cell - baselineCell), 0, 1);
		}
	}

	[Theory]
	[MemberData(nameof(Families))]
	public void Lookup_is_never_worse_than_leaving_the_size_alone(string family)
	{
		if (!IsInstalled(family))
			return;
		for (int size = 8; size <= 30; size++)
		{
			int baselineCell = MeasureAdvance(FontMetrics.BaselineFontName, size);
			int untouched = Math.Abs(MeasureAdvance(family, size) - baselineCell);
			int derived = Math.Abs(MeasureAdvance(family, FontMetrics.FindRenderSizePx(family, size)) - baselineCell);
			Assert.True(derived <= untouched, $"{family} at {size}: derived error {derived} exceeds {untouched}");
		}
	}

	[Theory]
	[MemberData(nameof(Families))]
	public void Fonts_are_built_at_the_derived_size(string family)
	{
		if (!IsInstalled(family))
			return;
		EnsureFontName();
		Config.ApplyWindowGeometry(WindowX, 480, BaseFontSize, LineHeight);
		//the font the product builds is the font the layout measures, for the configured family and for a
		//family a script asks for by name alike
		Font? font = FontFactory.GetFont(family, FontStyle.Regular);
		Assert.NotNull(font);
		int baselineCell = MeasureAdvance(FontMetrics.BaselineFontName, BaseFontSize);
		Assert.InRange(Math.Abs(MeasureAdvance(font) - baselineCell), 0, 1);
	}

	[Fact]
	public void Config_derives_the_render_size_from_the_baseline_font()
	{
		EnsureFontName();
		Config.ApplyWindowGeometry(WindowX, 480, BaseFontSize, LineHeight);
		//with the baseline font configured the derived size is the configured one, so the layout of an
		//existing configuration does not move at all, and the em baseline stays what the panels use
		Assert.Equal(BaseFontSize, Config.RenderFontSizePx);
		Assert.Equal(BaseFontSize, Config.FontSize);
	}

	/// <summary>Advances measured here, independently of the lookup's own cache.</summary>
	private static int MeasureAdvance(string family, int size)
	{
		using var font = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
		return MeasureAdvance(font);
	}

	/// <summary>Advances of a font the caller owns: it is not disposed here.</summary>
	private static int MeasureAdvance(Font font)
	{
		using var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using var graph = Graphics.FromImage(bmp);
		return TextRenderer.MeasureText(graph, "あ", font, new Size(4096, 64),
			TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
	}

	/// <summary>
	/// A missing family makes the Font constructor fall back to another one, which would measure a font the
	/// test does not name; comparing the resolved name is how the other font tests detect that.
	/// </summary>
	private static bool IsInstalled(string family)
	{
		using var font = new Font(family, BaseFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
		return font.Name.Equals(family, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Gives Config the font name it would have after loading the built in defaults, the same way the other
	/// font tests do: tests do not load a configuration file, and Config.SetConfig assigns unrelated statics.
	/// </summary>
	private static void EnsureFontName()
	{
		if (!string.IsNullOrEmpty(Config.FontName))
			return;
		PropertyInfo property = typeof(Config).GetProperty(nameof(Config.FontName), BindingFlags.Public | BindingFlags.Static)
			?? throw new InvalidOperationException("Config.FontName not found");
		property.SetValue(null, FontMetrics.BaselineFontName);
	}
}
