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

		var key = (requestFontName, Config.FontSize, style);

		if (fontDic.TryGetValue(key, out var existingFont))
		{
			return existingFont;
		}

		try
		{
			FontFamily ff = GlobalStatic.Pfc.Families.FirstOrDefault(ff => ff.Name == requestFontName, null);
			if (ff != null)
			{
				return fontDic[key] = new Font(ff, Config.FontSize, style, GraphicsUnit.Pixel);
			}
			else
			{
				return fontDic[key] = new Font(requestFontName, Config.FontSize, style, GraphicsUnit.Pixel);
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
