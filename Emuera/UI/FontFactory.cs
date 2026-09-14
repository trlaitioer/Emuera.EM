using MinorShift.Emuera.Runtime.Config;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MinorShift.Emuera.UI;

internal class FontFactory
{

	static readonly Dictionary<(string fontname, int fontSize, FontStyle fontStyle), Font> fontDic = [];

	public static Font GetFont(string requestFontName, FontStyle style)
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
			requestFontName = Config.FontName;

		//The configured font is built at the size the derived geometry was recomputed with. A font a script
		//named explicitly is a different face, so it is not built at the derived size of the configured font:
		//it gets a lookup of its own, which is what lands it on the baseline cell as well.
		int fontSize = requestFontName == Config.FontName
			? Config.RenderFontSizePx
			: FontMetrics.FindRenderSizePx(requestFontName, Config.FontSize);

		var key = (requestFontName, fontSize, style);

		if (fontDic.TryGetValue(key, out var existingFont))
		{
			return existingFont;
		}

		Font createdFont = CreateFont(requestFontName, fontSize, style);
		if (createdFont == null)
		{
			return null;
		}
		return fontDic[key] = createdFont;
	}

	/// <summary>
	/// Builds a font without caching it. The metrics probe builds fonts through this method, so that the font
	/// that gets measured is the font that gets drawn: families of the private font collection take priority,
	/// the family name is the fallback. Returns null when the font cannot be created.
	/// </summary>
	public static Font CreateFont(string requestFontName, int fontSize, FontStyle style)
	{
		try
		{
			FontFamily ff = GlobalStatic.Pfc.Families.FirstOrDefault(ff => ff.Name == requestFontName, null);
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
		foreach (var font in fontDic)
		{
			font.Value.Dispose();
		}
		fontDic.Clear();
	}
}
