#nullable enable
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
		// The Graphics above belongs to the thread that built it. Parsing folds constant expressions on
		// pool threads while this object was created with the console, so the owner has to be remembered
		// to keep those threads off it.
		ownerThreadId = Environment.CurrentManagedThreadId;
	}

	private readonly TextDrawingMode textDrawingMode;
	private Size layoutSize;

	/// <summary>
	/// Updates the measuring size after Config.FontSize/LineHeight/WindowX changed.
	/// </summary>
	public void RefreshLayout()
	{
		layoutSize = new Size(Config.WindowX * 2, Config.LineHeight);
	}

	private readonly Graphics graph;
	private readonly Bitmap bmp;
	private readonly int ownerThreadId;

	/// <summary>
	/// The Graphics to measure through: the shared one on the thread that owns it, null anywhere else so
	/// that GDI builds its own device context instead. The measurement is the advance width either way,
	/// and both paths were compared value for value; the shared Graphics must not be touched off its
	/// thread, which is what an unqualified call would do while parsing folds an expression.
	/// </summary>
	private Graphics? MeasuringGraphics => Environment.CurrentManagedThreadId == ownerThreadId ? graph : null;

	/// <summary>
	/// Measures through the given device context, or through one GDI provides for the call when it is
	/// null. TextRenderer rejects a null context, so the two overloads must not be chosen by a ternary.
	/// </summary>
	private static int MeasureText(Graphics? graph, ReadOnlySpan<char> text, Font font, Size layoutSize)
	{
		if (graph == null)
		{
			return TextRenderer.MeasureText(new string(text), font, layoutSize, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
		}

		return TextRenderer.MeasureText(graph, text, font, layoutSize, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
	}

	public int GetDisplayLength(ReadOnlySpan<char> chars, Font f)
	{
		// The string overload answers empty text before it looks at the mode, so nothing here rejects it either
		if (chars.IsEmpty)
		{
			return 0;
		}

		Graphics? measuringGraphics = MeasuringGraphics;
		if (textDrawingMode == TextDrawingMode.TEXTRENDERER)
		{
			return MeasureText(measuringGraphics, chars, f, layoutSize);
		}
		else if (textDrawingMode == TextDrawingMode.GRAPHICS)
		{
			// The span goes straight to the helper, so this path does not copy the text into a string first.
			// Tab expansion stays with the string overload, which is the one the print paths use
			return GraphicsTextRenderer.MeasureAdvance(measuringGraphics, chars, f, layoutSize);
		}
		else // if (StaticConfig.TextDrawingMode == TextDrawingMode.WINAPI)
		{
			throw new Exception("WIN32APIモードはサポートされていません");
		}
	}

	public int GetDisplayLength(string s, Font font)
	{
		if (string.IsNullOrEmpty(s))
		{
			return 0;
		}

		Graphics? measuringGraphics = MeasuringGraphics;
		if (textDrawingMode == TextDrawingMode.GRAPHICS)
		{
			if (s.Contains('\t'))
			{
				s = s.Replace("\t", "        ");
			}

			// The advance width of the text, which is the quantity the layout is built on. Drawing uses GDI+
			// without padding, so the pen it starts from is the same one the layout computed
			// (see .scratch/ui-modernization/issues/04).
			return GraphicsTextRenderer.MeasureAdvance(measuringGraphics, s, font, layoutSize);
		}
		else if (textDrawingMode == TextDrawingMode.TEXTRENDERER)
		{
			return MeasureText(measuringGraphics, s.AsSpan(), font, layoutSize);
		}
		else // if (StaticConfig.TextDrawingMode == TextDrawingMode.WINAPI)
		{
			throw new Exception("WIN32APIモードはサポートされていません");
		}
		//来るわけがない
		//else
		//    throw new ExeEE("描画モード不明");
	}


	private bool disposed;

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;
		graph.Dispose();
		bmp.Dispose();
	}
}