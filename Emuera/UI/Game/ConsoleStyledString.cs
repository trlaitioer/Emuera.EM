#nullable enable
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinorShift.Emuera.UI.Game;

/// <summary>
/// 装飾付文字列。stringとStringStyleからなる。
/// </summary>
internal sealed class ConsoleStyledString : AConsoleColoredPart
{
	private ConsoleStyledString() { }
	public ConsoleStyledString(string str, StringStyle style)
	{
		//if ((StaticConfig.TextDrawingMode != TextDrawingMode.GRAPHICS) && (str.IndexOf('\t') >= 0))
		//    str = str.Replace("\t", "");
		Text = str;
		StringStyle = style;
		Font = FontFactory.GetFont(style.Fontname, style.FontStyle);
		if (Font == null)
		{
			Error = true;
			return;
		}
		Color = style.Color;
		ButtonColor = style.ButtonColor;
		colorChanged = style.ColorChanged;
		if (!colorChanged && Color != Config.ForeColor)
			colorChanged = true;
		PointX = -1;
		Width = -1;
	}
	// Null exactly when Error is set, which every reader checks before touching the font
	public Font? Font { get; private set; }
	public StringStyle StringStyle { get; private set; }
	public override bool CanDivide
	{
		get { return true; }
	}
	//単一のボタンフラグ
	//public bool IsButton { get; set; }
	//indexの文字数の前方文字列とindex以降の後方文字列に分割
	public ConsoleStyledString? DivideAt(int index, StringMeasure sm)
	{
		//if ((index <= 0)||(index > Text.Length)||this.Error)
		//	return null;
		ConsoleStyledString? ret = DivideAt(index);
		if (ret == null)
			return null;
		SetWidth(sm, XsubPixel);
		ret.SetWidth(sm, XsubPixel);
		return ret;
	}
	public ConsoleStyledString? DivideAt(int index)
	{
		if (index <= 0 || index > Text!.Length || Error)
			return null;
		string str = Text![index..];
		Text = Text![..index];
		ConsoleStyledString ret = new()
		{
			Font = Font,
			Text = str,
			Color = Color,
			ButtonColor = ButtonColor,
			colorChanged = colorChanged,
			StringStyle = StringStyle,
			XsubPixel = XsubPixel
		};
		return ret;
	}

	public override void ResetLayout(StringMeasure sm)
	{
		Font = FontFactory.GetFont(StringStyle.Fontname, StringStyle.FontStyle);
		if (Font == null)
		{
			Error = true;
			Width = -1;
			return;
		}
		Width = -1;
		#region EmuEra-Rikaichan
		rikaichaned = false;
		// Ends is rebuilt by SetWidth; the field is declared non-null for its readers
		Ends = null!;
		#endregion
	}

	public override void SetWidth(StringMeasure sm, float subPixel)
	{
		if (Error)
		{
			Width = 0;
			return;
		}
		Width = sm.GetDisplayLength(Text!, Font!);
		XsubPixel = subPixel;

		#region EmuEra-Rikaichan
		if (!rikaichaned && Config.RikaiEnabled)
		{
			rikaichaned = true;
			int len = Text!.Length;
			Ends = new int[len];
			for (int i = 0; i < len; i++)
			{
				string temp = Text!.Substring(0, i + 1);
				Ends[i] = sm.GetDisplayLength(temp, Font!);
			}

		}
		#endregion
	}

	public override void DrawTo(Graphics graph, int pointY, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false, bool scalePreview = false)
	{
		if (Error)
			return;
		Color color = Color;
		// backdrop drawn behind the glyphs of the focused button (the GDI+ variant of DrawText's backColor)
		Color? backcolor = null;
		if (isFocus)
		{
			if (JSONConfig.Data.UseButtonFocusBackgroundColor)
			{
				if (!(Color.Yellow.R == color.R &&
					Color.Yellow.G == color.G &&
					Color.Yellow.B == color.B) &&
					!string.IsNullOrWhiteSpace(Text))
				{
					backcolor = Color.Gray;
				}
			}
			color = ButtonColor;
		}
		else if (isBackLog && !colorChanged)
		{
			color = Config.LogColor;
		}

		#region EM_私家版_描画拡張
		// The focused button paints a backdrop behind its glyphs; GDI+ mode has no DrawText backColor
		// variant, so it fills the backdrop first.
		if (isButton && !isBackLog && backcolor.HasValue)
		{
			using var backBrush = new SolidBrush(backcolor.Value);
			graph.FillRectangle(backBrush, PointX, pointY, Width, Config.FontSize);
		}
		// GDI ignores the Graphics transform, so the zoom preview and GRAPHICS mode draw text with GDI+
		if (mode == TextDrawingMode.GRAPHICS || scalePreview)
		{
			GraphicsTextRenderer.Draw(graph, Text!, Font!, color, PointX, XsubPixel, pointY);
		}
		else
		// TextRenderer.DrawText(graph, Text, Font, new Point(PointX, pointY), color, TextFormatFlags.NoPrefix);
		{
			TextFormatFlags flags = TextFormatFlags.NoPrefix;
			if (!JSONConfig.Data.UseButtonFocusBackgroundColor)
				flags |= TextFormatFlags.PreserveGraphicsClipping;
			TextRenderer.DrawText(graph, Text!.AsSpan(), Font!, new Point(PointX, pointY), color, flags);
		}

		#endregion
	}

	//Bitmap Cache
	public void DrawToBitmap(Graphics graph, bool isSelecting, bool isBackLog, TextDrawingMode mode, int xOffset, float subPixel)
	{
		if (Error)
			return;
		Color color = Color;
		if (isSelecting)
			color = ButtonColor;
		else if (isBackLog && !colorChanged)
			color = Config.LogColor;

		#region EM_私家版_描画拡張
		if (mode == TextDrawingMode.GRAPHICS)
			GraphicsTextRenderer.Draw(graph, Text!, Font!, color, xOffset, subPixel, 0);
		else
			// TextRenderer.DrawText(graph, Text, Font, new Point(PointX, pointY), color, TextFormatFlags.NoPrefix);
			TextRenderer.DrawText(graph, Text!.AsSpan(), Font!, new Point(xOffset, 0), color, TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping);
		#endregion
	}
}
