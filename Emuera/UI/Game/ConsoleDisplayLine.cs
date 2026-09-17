using MinorShift.Emuera.Runtime.Config;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MinorShift.Emuera.UI.Game;

internal enum DisplayLineLastState
{
	None = 0,
	Normal = 1,
	Selected = 2,
	BackLog = 3
}

internal enum DisplayLineAlignment
{
	LEFT = 0,
	CENTER = 1,
	RIGHT = 2
}

/// <summary>
/// Unit of alignment. A line consists of several fragments because lines are joined when PRINT does not end the line.
/// </summary>
internal readonly struct LineFragment
{
	public LineFragment(int start, int count, DisplayLineAlignment align, int customWidth, bool noShift)
	{
		Start = start;
		Count = count;
		Align = align;
		CustomWidth = customWidth;
		NoShift = noShift;
	}

	public readonly int Start;
	public readonly int Count;
	public readonly DisplayLineAlignment Align;
	public readonly int CustomWidth;

	/// <summary>True when SetAlignment did not move the fragment (LEFT alignment on a logical line).</summary>
	public readonly bool NoShift;
}

/// <summary>
/// 表示行。1つ以上のボタン（ConsoleButtonString）からなる
/// </summary>
internal sealed class ConsoleDisplayLine
{
	//public ConsoleDisplayLine(EmueraConsole parentWindow, ConsoleButtonString[] buttons, bool isLogical, bool temporary)
	public ConsoleDisplayLine(ConsoleButtonString[] buttons, bool isLogical, bool temporary, bool lineEnd = true)
	{
		//parent = parentWindow;
		if (buttons == null)
		{
			//これはthis.buttonの間違い？
			buttons = [];
			return;
		}

		this.buttons = buttons;
		foreach (ConsoleButtonString button in buttons)
		{
			button.ParentLine = this;
		}

		IsLogicalLine = isLogical;
		IsTemporary = temporary;
		IsLineEnd = lineEnd;
	}

	public int LineNo = -1;

	///論理行の最初となる場合だけtrue。表示の都合で改行された2行目以降はfalse
	public readonly bool IsLogicalLine = true;

	public readonly bool IsTemporary;

	public bool IsLineEnd = true;

	//EmueraConsole parent;
	// The constructor only assigns this in its non-null branch, and ChangeStr uses null as an intermediate state
	private ConsoleButtonString[] buttons = null!;
	private DisplayLineAlignment align;
	public ConsoleButtonString[] Buttons => buttons;
	public DisplayLineAlignment Align => align;
	private bool aligned;
	private readonly List<LineFragment> fragments = [];

	/// <summary>
	/// Version of the last width measurement. A line whose value differs from EmueraConsole.layoutVersion was not measured again yet.
	/// </summary>
	public int LayoutVersion;

	//Bitmap Cache
	public bool bitmapCacheEnabled;

	public void SetAlignment(DisplayLineAlignment align, int customWidth = -1 /*, int xOffset = 0*/)
	{
		if (aligned)
		{
			return;
		}

		aligned = true;
		this.align = align;
		fragments.Add(new LineFragment(0, buttons.Length, align, customWidth, align == DisplayLineAlignment.LEFT && IsLogicalLine));
		if (buttons.Length == 0)
		{
			return;
		}

		//DisplayLineの幅
		int width = 0;
		foreach (ConsoleButtonString button in buttons)
		{
			width += button.Width;
		}

		//現在位置
		int pointX = buttons[0].PointX;

		//目標位置
		int movetoX = 0;
		if (align == DisplayLineAlignment.LEFT)
		{
			//位置固定に対応
			if (IsLogicalLine)
			{
				return;
			}

			#region EE_div各要素の修正

			movetoX = 0; // xOffsetをここに入らないで
			//movetoX = 0+xOffset;

			#endregion
		}

		#region EM_私家版_HTML_divタグ

		else if (align == DisplayLineAlignment.CENTER)
			// movetoX = Config.WindowX / 2 - width / 2;

			#region EE_div各要素の修正

		{
			movetoX = ((customWidth > 0 ? customWidth : Config.DrawableWidth) / 2) - (width / 2) /* + xOffset*/;
		}

		#endregion

		else if (align == DisplayLineAlignment.RIGHT)
			// movetoX = Config.WindowX - width;

			#region EE_div各要素の修正

		{
			movetoX = (customWidth > 0 ? customWidth : Config.DrawableWidth) - width /* + xOffset*/;
		}

		#endregion

		#endregion

		//移動距離
		int shiftX = movetoX - pointX;
		if (shiftX != 0)
		{
			ShiftPositionX(shiftX);
		}
	}

	public void ShiftPositionX(int shiftX)
	{
		foreach (ConsoleButtonString button in buttons)
		{
			button.ShiftPositionX(shiftX);
		}
	}

	/// <summary>
	/// Re-measures the width and rebuilds positions and alignment after the font size changed. Line breaks are kept.
	/// Alignment is applied per fragment, so the fragment list is rebuilt as well.
	/// </summary>
	internal void ReLayout(StringMeasure sm)
	{
		if (buttons == null || buttons.Length == 0)
		{
			return;
		}

		float subPixel = 0.0f;
		int pointX = 0;
		foreach (ConsoleButtonString button in buttons)
		{
			if (button == null)
			{
				//forced line break flag
				pointX = 0;
				continue;
			}

			button.ResetLayout(sm);
			button.CalcWidth(sm, subPixel);
			button.CalcPointX(pointX);
			pointX = button.PointX + button.Width;
			if (button.PointXisLocked)
			{
				subPixel = 0;
			}

			subPixel = button.XsubPixel;
		}

		int end = 0;
		foreach (LineFragment fragment in fragments)
		{
			int last = Math.Min(fragment.Start + fragment.Count, buttons.Length);
			if (fragment.Start >= last)
			{
				continue;
			}

			int width = 0;
			for (int i = fragment.Start; i < last; i++)
			{
				if (buttons[i] != null)
				{
					width += buttons[i].Width;
				}
			}

			int movetoX = 0;
			if (!fragment.NoShift)
			{
				#region EM_私家版_HTML_divタグ

				int customWidth = fragment.CustomWidth > 0 ? fragment.CustomWidth : Config.DrawableWidth;

				#endregion

				if (fragment.Align == DisplayLineAlignment.CENTER)
				{
					movetoX = (customWidth / 2) - (width / 2);
				}
				else if (fragment.Align == DisplayLineAlignment.RIGHT)
				{
					movetoX = customWidth - width;
				}
			}

			int shiftX = movetoX + end - buttons[fragment.Start].PointX;
			if (shiftX != 0)
			{
				for (int i = fragment.Start; i < last; i++)
				{
					if (buttons[i] != null)
					{
						buttons[i].ShiftPositionX(shiftX);
					}
				}
			}

			end = movetoX + end + width;
		}
	}

	/// <summary>
	/// Joins the previous line that was continued (PRINT without a line end) and takes its fragments over.
	/// </summary>
	internal void PrependButtons(ConsoleDisplayLine prev)
	{
		int offset = prev.buttons.Length;
		List<LineFragment> merged = [.. prev.fragments];
		foreach (LineFragment fragment in fragments)
		{
			merged.Add(new LineFragment(fragment.Start + offset, fragment.Count, fragment.Align, fragment.CustomWidth, fragment.NoShift));
		}

		fragments.Clear();
		fragments.AddRange(merged);
		ChangeStr([.. prev.Buttons, .. buttons]);
	}

	public void ChangeStr(ConsoleButtonString[] newButtons)
	{
		// null is the intermediate state while the buttons are re-parented
		buttons = null!;
		foreach (ConsoleButtonString button in newButtons)
		{
			button.ParentLine = this;
		}

		buttons = newButtons;
	}

	public static void Clear(Brush brush, Graphics graph, int pointY)
	{
		Rectangle rect = new(0, pointY, Config.WindowX, Config.LineHeight);
		graph.FillRectangle(brush, rect);
	}

	//public ConsoleButtonString GetPointingButton(int pointX)
	//{
	//	////1815 優先順位を逆順にする
	//	////後から描画されるボタンが優先されるように
	//	for (int i = 0; i < buttons.Length; i++)
	//	{
	//		ConsoleButtonString button = buttons[buttons.Length - i - 1];
	//		if ((button.PointX <= pointX) && (button.PointX + button.Width >= pointX))
	//			return button;
	//	}
	//	//foreach (ConsoleButtonString button in buttons)
	//	//{
	//	//    if ((button.PointX <= pointX) && (button.PointX + button.Width >= pointX))
	//	//        return button;
	//	//}
	//	return null;
	//}

	public void DrawTo(Graphics graph, int pointY, bool isBackLog, bool force, TextDrawingMode mode, bool scalePreview = false)
	{
		foreach (ConsoleButtonString button in buttons)
		{
			button.DrawTo(graph, pointY, isBackLog, mode, scalePreview);
		}
	}

	private readonly static StringBuilder builder = new();

	public override string ToString()
	{
		if (buttons == null)
		{
			return "";
		}

		builder.Clear();
		foreach (ConsoleButtonString button in buttons)
		{
			builder.Append(button);
		}

		return builder.ToString();
	}

	#region EM_私家版_描画拡張

	public StringBuilder BuildString(StringBuilder builder)
	{
		if (buttons != null)
		{
			foreach (ConsoleButtonString button in buttons)
			{
				builder.Append(button);
			}
		}

		return builder;
	}

	#endregion
}