using MinorShift.Emuera.Runtime.Config;
using System.Drawing;
using System.Text;

namespace MinorShift.Emuera.UI.Game;

/// <summary>
/// 描画の最小単位
/// </summary>
internal abstract class AConsoleDisplayNode
{
	public bool Error { get; protected set; }

	// Declared nullable only because the base has no constructor: every concrete part assigns a
	// non-null value in its constructor (str / "" / AltText / errMes) and nothing clears it later,
	// so readers of a part that has been built may assert.
	public string? Text { get; protected set; }
	public string? AltText { get; protected set; }

	#region EM_私家版_描画拡張

	// public int PointX { get; set; }
	public virtual int PointX { get; set; }

	#endregion

	public float XsubPixel { get; set; }
	public float WidthF { get; set; }
	public int Width { get; set; }
	public virtual int Top => 0;
	public virtual int Bottom => Config.FontSize;
	public abstract bool CanDivide { get; }

	public abstract void DrawTo(
		Graphics graph,
		int pointY,
		bool isSelecting,
		bool isFocus,
		bool isBackLog,
		TextDrawingMode mode,
		bool isButton = false,
		bool scalePreview = false
	);

	/// <summary>
	/// Discards cached measurements when the conditions they were taken under, such as the font size, changed.
	/// </summary>
	public virtual void ResetLayout(StringMeasure sm)
	{ }

	public abstract void SetWidth(StringMeasure sm, float subPixel);

	public override string ToString()
	{
		if (Text == null)
		{
			return "";
		}

		return Text;
	}

	#region EM_私家版_描画拡張

	// Assigned by the print path before the part is read back
	public ConsoleButtonString Parent { get; set; } = null!;
	public int Depth { get; init; }

	public virtual StringBuilder BuildString(StringBuilder sb)
	{
		if (Text != null)
		{
			sb.Append(Text);
		}

		return sb;
	}

	#endregion

	#region EmuEra-Rikaichan

	public bool rikaichaned;

	// Built by ConsoleStyledString.SetWidth when rikaichan is enabled
	public int[] Ends = null!;

	// Linked by the line builders while a line is wrapped
	public AConsoleDisplayNode NextLine = null!;

	#endregion
}

/// <summary>
/// 色つき
/// </summary>
internal abstract class AConsoleColoredPart : AConsoleDisplayNode
{
	protected Color Color { get; set; }
	protected Color ButtonColor { get; set; }
	protected bool colorChanged;
}
