using MinorShift.Emuera.Runtime.Config;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinorShift.Emuera.UI.Game;

/// <summary>
/// テキスト長計測装置
/// 1819 必要になるたびにCreateGraphicsする方式をやめてあらかじめGraphicsを用意しておくことにする
/// </summary>
internal sealed class StringMeasure : IDisposable
{
	public StringMeasure()
	{
		textDrawingMode = Config.TextDrawingMode;
		RefreshLayout();
		bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		graph = Graphics.FromImage(bmp);
	}

	readonly TextDrawingMode textDrawingMode;
	private Size layoutSize;

	/// <summary>
	/// Updates the measuring size after Config.FontSize/LineHeight/WindowX changed.
	/// </summary>
	public void RefreshLayout()
	{
		layoutSize = new Size(Config.WindowX * 2, Config.LineHeight);
	}

	readonly Graphics graph;
	readonly Bitmap bmp;

	public int GetDisplayLength(ReadOnlySpan<char> chars, Font f)
	{
		// The string overload answers empty text before it looks at the mode, so nothing here rejects it either
		if (chars.IsEmpty)
			return 0;
		if (textDrawingMode == TextDrawingMode.TEXTRENDERER)
		{
			var size = TextRenderer.MeasureText(graph, chars, f, layoutSize, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
			return size.Width;
		}
		else if (textDrawingMode == TextDrawingMode.GRAPHICS)
		{
			// The span goes straight to the helper, so this path does not copy the text into a string first.
			// Tab expansion stays with the string overload, which is the one the print paths use
			return GraphicsTextRenderer.MeasureAdvance(graph, chars, f, layoutSize);
		}
		else// if (StaticConfig.TextDrawingMode == TextDrawingMode.WINAPI)
		{
			throw new Exception("WIN32APIモードはサポートされていません");
		}
	}

	public int GetDisplayLength(string s, Font font)
	{
		if (string.IsNullOrEmpty(s))
			return 0;
		if (textDrawingMode == TextDrawingMode.GRAPHICS)
		{
			if (s.Contains('\t'))
				s = s.Replace("\t", "        ");
			// The advance width of the text, which is the quantity the layout is built on. Drawing uses GDI+
			// without padding, so the pen it starts from is the same one the layout computed
			// (see .scratch/ui-modernization/issues/04).
			return GraphicsTextRenderer.MeasureAdvance(graph, s, font, layoutSize);
		}
		else if (textDrawingMode == TextDrawingMode.TEXTRENDERER)
		{
			Size size = TextRenderer.MeasureText(graph, s.AsSpan(), font, layoutSize, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
			//Size size = TextRenderer.MeasureText(g, s, StaticConfig.Font);
			return size.Width;
		}
		else// if (StaticConfig.TextDrawingMode == TextDrawingMode.WINAPI)
		{
			throw new Exception("WIN32APIモードはサポートされていません");
		}
		//来るわけがない
		//else
		//    throw new ExeEE("描画モード不明");
	}


	bool disposed;
	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
		graph.Dispose();
		bmp.Dispose();
	}
}
