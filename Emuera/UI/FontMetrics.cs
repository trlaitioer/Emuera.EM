using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MinorShift.Emuera.UI;

/// <summary>
/// Reverse lookup of the render font size. How wide one full width character advances is a property of the
/// font, while the em baseline the scripts place their columns with is Config.FontSize, so swapping fonts
/// changes how many characters fit into the same em sized panel. Building the font at the size whose full
/// width advance matches the baseline font's keeps the columns where they were, with the em baseline and
/// therefore every em sized panel, indent and image left untouched
/// (see tracker/ui-modernization/issues/03).
/// </summary>
internal static class FontMetrics
{
	/// <summary>The font the era scripts were written against; also the built in default of ConfigData.</summary>
	public const string BaselineFontName = "ＭＳ ゴシック";

	/// <summary>
	/// The probe is a CJK full width character: the full width latin 'Ａ' advances differently in several fonts,
	/// so it is not the cell the layout is written against.
	/// </summary>
	private const char FullWidthProbe = '\u3042';

	/// <summary>A size that misses the baseline cell by more than this cannot reproduce the baseline layout.</summary>
	private const int MaxErrorPx = 1;

	/// <summary>The probe text is one character, so the proposed box only has to be wide enough not to wrap it.</summary>
	private const int ProbeBoxWidth = 4096;

	private const int ProbeBoxHeight = 64;

	/// <summary>
	/// Full width advances by (font name, size). The font name only changes when the process restarts, so the
	/// entries are never invalidated and stay bounded by the sizes the lookup enumerates.
	/// </summary>
	private static readonly Dictionary<(string FontName, int Size), int> advances = [];

	private static readonly object advancesLock = new();

	//Created together on the first measurement, before any read
	private static Bitmap probeBitmap = null!;
	private static Graphics probeGraphics = null!;

	/// <summary>
	/// The size to build <paramref name="fontName"/> at, so that its full width cell matches the baseline
	/// font's cell at <paramref name="baseFontSize"/>. The error is minimized and the narrower side wins ties;
	/// the configured size is kept when no size gets closer than <see cref="MaxErrorPx"/>, which also covers
	/// fonts that cannot be measured at all. The caller keeps the em baseline at <paramref name="baseFontSize"/>.
	/// </summary>
	public static int FindRenderSizePx(string? fontName, int baseFontSize)
	{
		if (baseFontSize <= 0)
		{
			return baseFontSize;
		}

		if (string.IsNullOrEmpty(fontName))
		{
			fontName = BaselineFontName;
		}

		int baselineCell = MeasureAdvance(BaselineFontName, baseFontSize);
		if (baselineCell <= 0)
		{
			baselineCell = baseFontSize;
		}

		//The search starts from the configured size, which is what keeps the baseline font on it: the advance
		//the layout compares against is that font's own, so the error is zero there. A tie only replaces a
		//wider candidate, which is what keeps a font that can hit the exact cell from drifting off it.
		int limit = 2 * Math.Max(baseFontSize, baselineCell);
		int best = baseFontSize;
		int bestAdvance = MeasureAdvance(fontName, baseFontSize);
		int bestError = Math.Abs(bestAdvance - baselineCell);
		for (int size = 1; size <= limit; size++)
		{
			int advance = MeasureAdvance(fontName, size);
			int error = Math.Abs(advance - baselineCell);
			if (error < bestError || (error == bestError && advance < bestAdvance))
			{
				best = size;
				bestAdvance = advance;
				bestError = error;
			}
		}

		if (bestError > MaxErrorPx)
		{
			return baseFontSize;
		}

		return best;
	}

	/// <summary>
	/// The full width advance of one character, measured the way the layout measures text: GDI advances with
	/// GDI's padding and prefix handling removed, which is what StringMeasure.GetDisplayLength measures and
	/// what both drawing modes lay out with. The font goes through FontFactory so that the font measured here
	/// is the font that gets drawn. Bold and italic are not measured: a monospaced face advances a character
	/// by the same amount in every style.
	/// </summary>
	private static int MeasureAdvance(string fontName, int size)
	{
		//A font a script asks for by name is resolved from the script thread while the window side recomputes
		//its geometry, so the curve and the probe device context are guarded.
		lock (advancesLock)
		{
			if (advances.TryGetValue((fontName, size), out int cached))
			{
				return cached;
			}

			int advance = 0;
			Font? font = FontFactory.CreateFont(fontName, size, FontStyle.Regular);
			if (font != null)
			{
				using (font)
				{
					if (probeGraphics == null)
					{
						probeBitmap = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
						probeGraphics = Graphics.FromImage(probeBitmap);
					}

					Size measured = TextRenderer.MeasureText(probeGraphics, FullWidthProbe.ToString(), font,
						new Size(ProbeBoxWidth, ProbeBoxHeight), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
					advance = measured.Width;
				}
			}

			advances[(fontName, size)] = advance;
			return advance;
		}
	}
}
