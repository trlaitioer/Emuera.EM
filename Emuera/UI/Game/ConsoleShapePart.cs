using MinorShift.Emuera.Runtime.Config;
using System;
using System.Drawing;
using System.Text;
using static MinorShift.Emuera.Runtime.Utils.EvilMask.Utils;

namespace MinorShift.Emuera.UI.Game;

abstract class ConsoleShapePart : AConsoleColoredPart
{
	#region EM_私家版_HTMLパラメータ拡張
	static public ConsoleShapePart CreateShape(string shapeType, MixedNum[] param, Color color, Color bcolor, bool colorchanged)
	// static public ConsoleShapePart CreateShape(string shapeType, int[] param, Color color, Color bcolor, bool colorchanged)
	{
		string type = shapeType.ToLower();
		colorchanged = colorchanged || color != Config.ForeColor;
		StringBuilder sb = new();
		sb.Append("<shape type='");
		sb.Append(type);
		sb.Append("' param='");
		for (int i = 0; i < param.Length; i++)
		{
			// sb.Append(param[i].ToString());
			sb.Append(param[i].num);
			if (param[i].isPx) sb.Append("px");
			if (i < param.Length - 1)
				sb.Append(", ");
		}
		sb.Append('\'');
		if (colorchanged)
		{
			sb.Append(" color='");
			sb.Append(HtmlManager.GetColorToString(color));
			sb.Append('\'');
		}
		if (bcolor != Config.FocusColor)
		{
			sb.Append(" bcolor='");
			sb.Append(HtmlManager.GetColorToString(bcolor));
			sb.Append('\'');
		}
		sb.Append('>');
		ConsoleShapePart ret = null;

		switch (type)
		{
			case "space":
				#region EM_私家版_space制限解除
				// if (paramPixel.Length == 1 && paramPixel[0] >= 0)
				#endregion
				if (param.Length == 1)
					ret = new ConsoleSpacePart(param[0]);
				break;
			case "rect":
				// if (paramPixel.Length == 1 && paramPixel[0] >= 0)
				if (param.Length == 1 && param[0].num > 0)
					ret = new ConsoleRectangleShapePart(param[0]);
				// else if (paramPixel.Length == 4)
				else if (param.Length == 4)
				{
					//rectF = new RectangleF(paramPixel[0], paramPixel[1], paramPixel[2], paramPixel[3]);
					var rectF = new RectangleF(MixedNum.ToPixelf(param[0]), MixedNum.ToPixelf(param[1]), MixedNum.ToPixelf(param[2]), MixedNum.ToPixelf(param[3]));
					//1820a12 サイズ上限撤廃
					if (rectF.X >= 0 && rectF.Width > 0 && rectF.Height > 0)
					//	rectF.Y >= 0 && (rectF.Y + rectF.Height) <= lineHeight)
					{
						ret = new ConsoleRectangleShapePart(param);
					}
				}
				break;
			case "polygon":
				break;
		}
		if (ret == null)
		{
			ret = new ConsoleErrorShapePart(sb.ToString());
		}
		ret.AltText = sb.ToString();
		ret.Color = color;
		ret.ButtonColor = bcolor;
		ret.colorChanged = colorchanged;
		return ret;
	}
	#endregion

	public override bool CanDivide
	{
		get { return false; }
	}

	public override string ToString()
	{
		if (AltText == null)
			return "";
		return AltText;
	}
	#region EM_私家版_描画拡張
	public override StringBuilder BuildString(StringBuilder sb)
	{
		if (AltText != null) sb.Append(AltText);
		return sb;
	}
	#endregion
}

internal sealed class ConsoleRectangleShapePart : ConsoleShapePart
{
	/// <summary>One element: width of a rectangle that spans one line. Four elements: xpos, ypos, width, height.</summary>
	public ConsoleRectangleShapePart(MixedNum rawWidth)
	{
		Text = "";
		rawParam = [rawWidth];
		computeGeometry();
	}

	public ConsoleRectangleShapePart(MixedNum[] rawParam)
	{
		Text = "";
		this.rawParam = rawParam;
		computeGeometry();
	}

	// Raw tag values are kept so that the geometry can be recomputed when the font size changes
	private readonly MixedNum[] rawParam;

	/// <summary>
	/// Resolves the tag values into a rectangle again. Values given without a unit are relative to the font size
	/// and follow it, while px values stay absolute.
	/// </summary>
	private void computeGeometry()
	{
		int lineHeight = Config.FontSize;
		RectangleF theRect = rawParam.Length == 1
			? new RectangleF(0, 0, rawParam[0].isPx ? rawParam[0].num : (float)rawParam[0].num * lineHeight / 100f, lineHeight)
			: new RectangleF(MixedNum.ToPixelf(rawParam[0]), MixedNum.ToPixelf(rawParam[1]), MixedNum.ToPixelf(rawParam[2]), MixedNum.ToPixelf(rawParam[3]));
		originalRectF = theRect;
		WidthF = theRect.X + theRect.Width;
		rect.Y = (int)theRect.Y;
		//if (rect.Y == 0 && theRect.Y >= 0.001f)
		//	rect.Y = 1;
		rect.Height = (int)theRect.Height;
		if (rect.Height == 0 && theRect.Height >= 0.001f)
			rect.Height = 1;
		top = Math.Min(0, rect.Y);
		bottom = Math.Max(Config.FontSize, rect.Y + rect.Height);
	}

	public override void ResetLayout(StringMeasure sm)
	{
		computeGeometry();
		Width = -1;
	}

	private int top;
	private int bottom;
	public override int Top { get { return top; } }
	public override int Bottom { get { return bottom; } }
	RectangleF originalRectF;
	bool visible;
	Rectangle rect;
	public override void DrawTo(Graphics graph, int pointY, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false, bool scalePreview = false)
	{
		if (!visible)
			return;
		Rectangle targetRect = rect;
		targetRect.X = targetRect.X + PointX;
		targetRect.Y = targetRect.Y + pointY;
		Color dcolor = isSelecting ? ButtonColor : Color;
		graph.FillRectangle(new SolidBrush(dcolor), targetRect);
	}

	public override void SetWidth(StringMeasure sm, float subPixel)
	{
		float widF = subPixel + WidthF;
		Width = (int)widF;
		XsubPixel = widF - Width;
		rect.X = (int)(subPixel + originalRectF.X);
		rect.Width = Width - rect.X;
		rect.X += Config.DrawingParam_ShapePositionShift;
		visible = rect.X >= 0 && rect.Width > 0;// && rect.Y >= 0 && (rect.Y + rect.Height) <= Config.Config.FontSize);
	}
}

internal sealed class ConsoleSpacePart : ConsoleShapePart
{
	public ConsoleSpacePart(MixedNum rawWidth)
	{
		Text = "";
		this.rawWidth = rawWidth;
		computeGeometry();
	}

	// Raw tag value is kept so that the width can be recomputed when the font size changes
	private readonly MixedNum rawWidth;

	/// <summary>
	/// Resolves the tag value again. A value given without a unit is relative to the font size and follows it,
	/// while a px value stays absolute.
	/// </summary>
	private void computeGeometry()
	{
		WidthF = rawWidth.isPx ? rawWidth.num : (float)rawWidth.num * Config.FontSize / 100f;
	}

	public override void ResetLayout(StringMeasure sm)
	{
		computeGeometry();
		Width = -1;
	}

	public override void DrawTo(Graphics graph, int pointY, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false, bool scalePreview = false) { }

	public override void SetWidth(StringMeasure sm, float subPixel)
	{
		float widF = subPixel + WidthF;
		Width = (int)widF;
		XsubPixel = widF - Width;
	}
}

internal sealed class ConsoleErrorShapePart : ConsoleShapePart
{
	public ConsoleErrorShapePart(string errMes)
	{
		Text = errMes;
		AltText = errMes;
	}

	public override void DrawTo(Graphics graph, int pointY, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false, bool scalePreview = false)
	{
		if (mode == TextDrawingMode.GRAPHICS || scalePreview)
			graph.DrawString(Text, Config.DefaultFont, new SolidBrush(Config.ForeColor), new Point(PointX, pointY));
		else
			System.Windows.Forms.TextRenderer.DrawText(graph, Text.AsSpan(), Config.DefaultFont, new Point(PointX, pointY), Config.ForeColor, System.Windows.Forms.TextFormatFlags.NoPrefix);
	}
	public override void ResetLayout(StringMeasure sm)
	{
		Width = -1;
	}
	public override void SetWidth(StringMeasure sm, float subPixel)
	{
		if (Error)
		{
			Width = 0;
			return;
		}
		Width = sm.GetDisplayLength(Text, Config.DefaultFont);
		XsubPixel = subPixel;
	}
}
