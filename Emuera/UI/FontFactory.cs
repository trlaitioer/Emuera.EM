using MinorShift.Emuera.Runtime.Config;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MinorShift.Emuera.UI;

internal class FontFactory
{
	private static readonly Dictionary<(string fontname, int fontSize, FontStyle fontStyle), Font> fontDic = [];

	public static Font? GetFont(string requestFontName, FontStyle style)
	{
		/*
		string fontname = requestFontName;
		if (string.IsNullOrEmpty(requestFontName))
			fontname = Config.FontName;
		if (!fontDic.ContainsKey((fontname, Config.FontSize, style)))
		{
			var font = new Font(fontname, Config.FontSize, style, GraphicsUnit.Pixel);
			if (font == null)
			{
				return null;
			}
			else
			{
				fontDic.Add((fontname, Config.FontSize, style), font);
			}

		}
		#region EE_フォントファイル対応
		int fontsize = Config.FontSize;
		Font styledFont;
		foreach (FontFamily ff in GlobalStatic.Pfc.Families)
		{
			if (ff.Name == fontname)
			{
				styledFont = new Font(ff, fontsize, style, GraphicsUnit.Pixel);
				break;
			}
		}
		#endregion
		return fontDic[(fontname, Config.FontSize, style)];
		*/

		if (string.IsNullOrEmpty(requestFontName))
		{
			requestFontName = Config.FontName;
		}

		//The configured font is built at the size the derived geometry was recomputed with. A font a script
		//named explicitly is a different face, so it is not built at the derived size of the configured font:
		//it gets a lookup of its own, which is what lands it on the baseline cell as well.
		int fontSize = requestFontName == Config.FontName
			? Config.RenderFontSizePx
			: FontMetrics.FindRenderSizePx(requestFontName, Config.FontSize);

		(string requestFontName, int fontSize, FontStyle style) key = (requestFontName, fontSize, style);

		if (fontDic.TryGetValue(key, out Font? existingFont))
		{
			return existingFont;
		}

		Font? createdFont = CreateFont(requestFontName, fontSize, style);
		if (createdFont == null)
		{
			return null;
		}

		return fontDic[key] = createdFont;
	}

	/// <summary>
	/// The default font, which must exist: every drawing path reads it without a null check, and the configured
	/// family can be unusable - GetFont reports that by returning null.
	/// </summary>
	public static Font GetDefaultFont()
		=> GetFont("", FontStyle.Regular) ?? GetFallbackFont(Config.RenderFontSizePx);

	/// <summary>
	/// The font used when the configured one cannot be built: the generic family at <paramref name="fontSize"/>,
	/// clamped positive because an illegal size is one of the ways a font fails to build. Cached per size, since
	/// the default font is read on every frame - building one per read would leak GDI handles - and the cache's
	/// disposer (ClearFont) then owns it like any other font.
	/// </summary>
	public static Font GetFallbackFont(int fontSize)
	{
		(string fontname, int fontSize, FontStyle fontStyle) key =
			(FontFamily.GenericSansSerif.Name, Math.Max(1, fontSize), FontStyle.Regular);
		if (fontDic.TryGetValue(key, out Font? cachedFont))
		{
			return cachedFont;
		}

		// CreateFont reports "cannot be built" as null; the FontFamily overload skips name resolution and the
		// size is clamped positive, so this floor cannot fail.
		return fontDic[key] = CreateFont(key.fontname, key.fontSize, key.fontStyle)
			?? new Font(FontFamily.GenericSansSerif, key.fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
	}

	/// <summary>
	/// Builds a font without caching it. The metrics probe builds fonts through this method, so that the font
	/// that gets measured is the font that gets drawn: families of the private font collection take priority,
	/// the family name is the fallback. Returns null when the font cannot be created.
	/// </summary>
	public static Font? CreateFont(string requestFontName, int fontSize, FontStyle style)
	{
		try
		{
			//The null default makes the element type nullable; the predicate only sees the collection's elements
			FontFamily? ff = GlobalStatic.Pfc.Families.FirstOrDefault(ff => ff!.Name == requestFontName, null);
			if (ff != null)
			{
				return new Font(ff, fontSize, style, GraphicsUnit.Pixel);
			}
			else
			{
				return new Font(requestFontName, fontSize, style, GraphicsUnit.Pixel);
			}
		}
		catch
		{
			return null;
		}
	}

	public static void ClearFont()
	{
		foreach (KeyValuePair<(string fontname, int fontSize, FontStyle fontStyle), Font> font in fontDic)
		{
			font.Value.Dispose();
		}

		fontDic.Clear();
	}
}
