#nullable enable
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game.Image;
using System;
using System.Drawing;
using System.Text;
using static MinorShift.Emuera.Runtime.Utils.EvilMask.Utils;

namespace MinorShift.Emuera.UI.Game;

sealed class ConsoleImagePart : AConsoleDisplayNode
{
	#region EM_私家版_HTMLパラメータ拡張
	//public ConsoleImagePart(string resName, string resNameb, int raw_height, int raw_width, int raw_ypos)
	public ConsoleImagePart(string resName, string? resNameb, string? resNamem, MixedNum? raw_height, MixedNum? raw_width, MixedNum? raw_ypos)
	{
		top = 0;
		bottom = Config.FontSize;
		Text = "";
		ResourceName = resName ?? "";
		ButtonResourceName = resNameb;
		MappingGraphName = resNamem;
		rawHeight = raw_height;
		rawWidth = raw_width;
		rawYPos = raw_ypos;
		StringBuilder sb = new();
		sb.Append("<img src='").Append(ResourceName).Append('\'');
		if (ButtonResourceName != null)
			AddTagArg(sb, "srcb", ButtonResourceName);
		//{
		//	sb.Append("' srcb='");
		//	sb.Append(ButtonResourceName);
		//}
		if (!string.IsNullOrEmpty(MappingGraphName))
			AddTagArg(sb, "srcm", MappingGraphName);
		AddTagMixedNumArg(sb, "height", raw_height);
		AddTagMixedNumArg(sb, "width", raw_width);
		AddTagMixedNumArg(sb, "ypos", raw_ypos);
		//{
		//	sb.Append("' srcm='");
		//	sb.Append(MappingGraphName);
		//}
		//if(raw_height != 0)
		//	if (raw_height != null && raw_height.num != 0)
		//	{
		//	sb.Append("' height='");
		//	sb.Append(raw_height.num.ToString());
		//	if (raw_height.isPx) sb.Append("px");
		//}
		//if(raw_width != 0)
		//if (raw_width != null && raw_width.num != 0)
		//	{
		//	sb.Append("' width='");
		//	sb.Append(raw_width.num.ToString());
		//	if (raw_width.isPx) sb.Append("px");
		//}
		////if(raw_ypos != 0)
		//if(raw_ypos != null && raw_ypos.num != 0)
		//	{
		//	sb.Append("' ypos='");
		//	sb.Append(raw_ypos.num.ToString());
		//	if (raw_ypos.isPx) sb.Append("px");
		//}
		// sb.Append("'>");
		sb.Append('>');
		AltText = sb.ToString();
		cImage = AppContents.GetSprite(ResourceName);
		//if (cImage != null && !cImage.IsCreated)
		//	cImage = null;
		if (cImage == null)
		{
			Text = AltText;
			return;
		}
		computeGeometry();
		if (ButtonResourceName != null)
		{
			cImageB = AppContents.GetSprite(ButtonResourceName);
			//if (cImageB != null && !cImageB.IsCreated)
			//	cImageB = null;
		}
		if (MappingGraphName != null)
		{
			cImageM = AppContents.GetSprite(MappingGraphName);
		}
	}
	public readonly string? MappingGraphName;
	private readonly ASprite? cImageM;
	#endregion
	private readonly ASprite? cImage;
	private readonly ASprite? cImageB;
	private int top;
	private int bottom;
	private Rectangle destRect;
	// Raw tag values are kept so that the geometry can be recomputed when the font size changes
	private readonly MixedNum? rawHeight, rawWidth, rawYPos;
	//#pragma warning disable CS0649 // フィールド 'ConsoleImagePart.ia' は割り当てられません。常に既定値 null を使用します。
	//		private readonly ImageAttributes ia;
	//#pragma warning restore CS0649 // フィールド 'ConsoleImagePart.ia' は割り当てられません。常に既定値 null を使用します。
	public readonly string ResourceName;
	public readonly string? ButtonResourceName;
	public override int Top { get { return top; } }
	public override int Bottom { get { return bottom; } }

	public override bool CanDivide { get { return false; } }

	/// <summary>
	/// Resolves the tag values into pixels again. Sizes given without a unit are relative to the font size and
	/// follow it, while px sizes stay absolute.
	/// </summary>
	private void computeGeometry()
	{
		int height;
		if (rawHeight == null || rawHeight.num == 0)//no height: the font size itself is the height in px
			height = Config.FontSize;
		else if (rawHeight.isPx)
			height = rawHeight.num;
		else //the height is a percentage of the font size
			height = Config.FontSize * rawHeight.num / 100;
		//without a width the aspect ratio of the image is kept; the fraction is recorded as the sub pixel
		if (rawWidth == null || rawWidth.num == 0)
		{
			//both callers of this method returned when cImage was null
			Width = cImage!.DestBaseSize.Width * height / cImage.DestBaseSize.Height;
			XsubPixel = (float)cImage.DestBaseSize.Width * height / cImage.DestBaseSize.Height - Width;
		}
		else if (rawWidth.isPx)
		{
			Width = rawWidth.num;
		}
		else
		{
			Width = Config.FontSize * rawWidth.num / 100;
			XsubPixel = (float)Config.FontSize * rawWidth.num / 100f - Width;
		}
		top = rawYPos != null ? rawYPos.isPx ? rawYPos.num : rawYPos.num * Config.FontSize / 100 : 0;
		destRect = new Rectangle(0, top, Width, height);
		//a negative size was specified: the rectangle is moved instead
		if (destRect.Width < 0)
		{
			destRect.X = -destRect.Width;
			Width = -destRect.Width;
		}
		if (destRect.Height < 0)
		{
			destRect.Y = destRect.Y - destRect.Height;
			height = -destRect.Height;
		}
		bottom = top + height;
	}

	/// <summary>
	/// Recomputes the image geometry for the new font size. px sizes stay absolute by design, sizes without a
	/// unit follow the font size.
	/// </summary>
	public override void ResetLayout(StringMeasure sm)
	{
		if (cImage == null)
		{
			//the fallback text is drawn with the current font
			Width = -1;
			top = 0;
			bottom = Config.FontSize;
			return;
		}
		computeGeometry();
	}

	public override void SetWidth(StringMeasure sm, float subPixel)
	{
		if (Error)
		{
			Width = 0;
			return;
		}
		if (cImage != null)
			return;
		Width = sm.GetDisplayLength(Text!, Config.DefaultFont);
		XsubPixel = subPixel;
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
	#region EM_私家版_imgマースク
	public long GetMappingColor(int pointX, int pointY)
	{
		if (cImageM != null && cImageM.IsCreated)
		{
			Size spriteSize;
			if (cImageM is SpriteF sf)
			{
				spriteSize = sf.DestBaseSize;
			}
			else if (cImageM is SpriteG sg)
			{
				spriteSize = sg.DestBaseSize;
			}
			else return 0;
			pointX = pointX * spriteSize.Width / destRect.Width;
			pointY = pointY * spriteSize.Height / destRect.Height;
			var c = cImageM.SpriteGetColor(pointX, pointY);
			return c.ToArgb() & 0xFFFFFF;
		}
		return 0;
	}
	#endregion
	public override void DrawTo(Graphics graph, int pointY, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false, bool scalePreview = false)
	{
		if (Error)
			return;
		ASprite? img = cImage;
		if ((isSelecting || isFocus) && cImageB != null)
			img = cImageB;

		if (img != null && img.IsCreated)
		{
			Rectangle rect = destRect;
			//PointX微調整
			rect.X = destRect.X + PointX + Config.DrawingParam_ShapePositionShift;
			rect.Y = destRect.Y + pointY;
			img.GraphicsDraw(graph, rect);
		}
		else
		{
			if (mode == TextDrawingMode.GRAPHICS)
				// AltText is assigned unconditionally in the constructor
				GraphicsTextRenderer.Draw(graph, AltText!, Config.DefaultFont, Config.ForeColor, PointX, XsubPixel, pointY);
			else
				System.Windows.Forms.TextRenderer.DrawText(graph, AltText.AsSpan(), Config.DefaultFont, new Point(PointX, pointY), Config.ForeColor, System.Windows.Forms.TextFormatFlags.NoPrefix);
		}
	}
}
