// Graphics and sprite builtins (画像処理系), plus the EE/EM graphics patches built around them.

using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.UI.Game.Image;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using static MinorShift.Emuera.GameData.Function.BuiltinArgReaders;
using static MinorShift.Emuera.GameData.Function.SaveDataPathBuilder;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class GraphicsStateMethod : FunctionMethod
{
	[BuiltinMethod("GCREATED")]
	private static GraphicsStateMethod CreateGCREATED()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GWIDTH")]
	private static GraphicsStateMethod CreateGWIDTH()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GHEIGHT")]
	private static GraphicsStateMethod CreateGHEIGHT()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GGETFONTSIZE")]
	private static GraphicsStateMethod CreateGGETFONTSIZE()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GGETFONTSTYLE")]
	private static GraphicsStateMethod CreateGGETFONTSTYLE()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GGETBRUSH")]
	private static GraphicsStateMethod CreateGGETBRUSH()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GGETPEN")]
	private static GraphicsStateMethod CreateGGETPEN()
	{
		return new GraphicsStateMethod();
	}

	[BuiltinMethod("GGETPENWIDTH")]
	private static GraphicsStateMethod CreateGGETPENWIDTH()
	{
		return new GraphicsStateMethod();
	}

	public GraphicsStateMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		switch (Name)
		{
			case "GCREATED":
				return 1;
			case "GWIDTH":
				return g.Width;
			case "GHEIGHT":
				return g.Height;

			#region EE_GDRAWTEXTに付随する要素

			case "GGETFONTSIZE":
				return g.Fontsize;
			case "GGETFONTSTYLE":
				return g.Fontstyle;
			case "GGETPEN":
				return g.Pen.Color.ToArgb() & 0xffffffffL;
			case "GGETPENWIDTH":
				return (long)g.Pen.Width;
			case "GGETBRUSH":
				SolidBrush b = (SolidBrush)g.Brush;
				return b.Color.ToArgb() & 0xffffffffL;

			#endregion
		}

		throw new ExeEE("GraphicsState:" + Name + ":異常な分岐");
	}
}

#region EE_GGETFONT

internal sealed class GraphicsStateStrMethod : FunctionMethod
{
	[BuiltinMethod("GGETFONT")]
	private static GraphicsStateStrMethod CreateGGETFONT()
	{
		return new GraphicsStateStrMethod();
	}

	public GraphicsStateStrMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return "";
		}

		switch (Name)
		{
			case "GGETFONT":
				return g.Fontname;
		}

		throw new ExeEE("GraphicsState:" + Name + ":Abnormal branching");
	}
}

#endregion

internal sealed class GraphicsGetColorMethod : FunctionMethod
{
	[BuiltinMethod("GGETCOLOR")]
	private static GraphicsGetColorMethod CreateGGETCOLOR()
	{
		return new GraphicsGetColorMethod();
	}

	public GraphicsGetColorMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		//失敗したら負の値を返す。他と戻り値違うけど仕方ないね
		if (!g.IsCreated)
		{
			return -1;
		}

		Point p = ReadPoint(Name, exm, slots, 1);
		if (p.X < 0 || p.X >= g.Width || p.X < 0 || p.Y >= g.Height)
		{
			return -1;
		}

		Color c = g.GGetColor(p.X, p.Y);
		//Color.ToArgb()はInt32の負の値をとることがあり、Int64にうまく変換できない?（と思ったが気のせいだった
		return c.ToArgb() & 0xFFFFFFFFL;
	}
}

internal sealed class GraphicsSetColorMethod : FunctionMethod
{
	[BuiltinMethod("GSETCOLOR")]
	private static GraphicsSetColorMethod CreateGSETCOLOR()
	{
		return new GraphicsSetColorMethod();
	}

	public GraphicsSetColorMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		Color c = ReadColor(Name, exm, slots, 1);
		Point p = ReadPoint(Name, exm, slots, 2);
		if (p.X < 0 || p.X >= g.Width || p.X < 0 || p.Y >= g.Height)
		{
			return 0;
		}

		g.GSetColor(c, p.X, p.Y);
		return 1;
	}
}

internal sealed class GraphicsSetBrushMethod : FunctionMethod
{
	[BuiltinMethod("GSETBRUSH")]
	private static GraphicsSetBrushMethod CreateGSETBRUSH()
	{
		return new GraphicsSetBrushMethod();
	}

	public GraphicsSetBrushMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		Color c = ReadColor(Name, exm, slots, 1);
		g.GSetBrush(new SolidBrush(c));
		return 1;
	}
}

#region EE_GDRAWTEXT追加に伴いGSETFONTを改良

internal sealed class GraphicsSetFontMethod : FunctionMethod
{
	[BuiltinMethod("GSETFONT")]
	private static GraphicsSetFontMethod CreateGSETFONT()
	{
		return new GraphicsSetFontMethod();
	}

	public GraphicsSetFontMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(string), typeof(Int64) };
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(string), typeof(Int64), typeof(Int64) };
		// GetIntValue reads fontsize unconditionally, so a two-argument call would index past the list:
		// only fontStyle may be omitted, which makes the minimum three.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String, ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count > 2)
	//		return null;
	//	return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		string fontname = slots.Str(exm, 1);
		long fontsize = slots.Int(exm, 2);
		FontStyle fs = FontStyle.Regular;
		if (slots.Count > 3)
		{
			long style = slots.Int(exm, 3);

			if ((style & 1) != 0)
			{
				fs |= FontStyle.Bold;
			}

			if ((style & 2) != 0)
			{
				fs |= FontStyle.Italic;
			}

			if ((style & 4) != 0)
			{
				fs |= FontStyle.Strikeout;
			}

			if ((style & 8) != 0)
			{
				fs |= FontStyle.Underline;
			}
		}

		Font styledFont;
		try
		{
			#region EE_フォントファイル対応

			foreach (FontFamily ff in GlobalStatic.Pfc.Families)
			{
				if (ff.Name == fontname)
				{
					styledFont = new Font(ff, fontsize, fs, GraphicsUnit.Pixel);
					goto foundfont;
				}
			}

			// styledFont = new Font(fontname, fontsize, FontStyle.Regular, GraphicsUnit.Pixel);
			styledFont = new Font(fontname, fontsize, fs, GraphicsUnit.Pixel);
		}
		catch
		{
			return 0;
		}

		foundfont:

		#endregion

		// g.GSetFont(styledFont);
		g.GSetFont(styledFont, fs);
		return 1;
	}
}

#endregion

internal sealed class GraphicsSetPenMethod : FunctionMethod
{
	[BuiltinMethod("GSETPEN")]
	private static GraphicsSetPenMethod CreateGSETPEN()
	{
		return new GraphicsSetPenMethod();
	}

	public GraphicsSetPenMethod()
	{
		ReturnType = typeof(long);
		// 私家版のバグだと思う
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(Int64) };
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		Color c = ReadColor(Name, exm, slots, 1);
		long width = slots.Int(exm, 2);
		g.GSetPen(new Pen(c, width));
		return 1;
	}
}

#region EE_GDASHSTYLE

internal sealed class GraphicsSetDashStyleMethod : FunctionMethod
{
	[BuiltinMethod("GDASHSTYLE")]
	private static GraphicsSetDashStyleMethod CreateGDASHSTYLE()
	{
		return new GraphicsSetDashStyleMethod();
	}

	public GraphicsSetDashStyleMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		g.GDashStyle(slots.Int(exm, 1), slots.Int(exm, 2));
		return 1;
	}
}

#endregion

#region EE_GDRAWTEXT

internal sealed class GraphicsDrawStringMethod : FunctionMethod
{
	[BuiltinMethod("GDRAWTEXT")]
	private static GraphicsDrawStringMethod CreateGDRAWTEXT()
	{
		return new GraphicsDrawStringMethod();
	}

	public GraphicsDrawStringMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(string), typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
	//	if (arguments.Count > 4)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	if (arguments.Count != 2 && arguments.Count != 4)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);

	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);

	//		if (i < argumentTypeArray.Length && argumentTypeArray[i] != arguments[i].GetOperandType())
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	if (arguments.Count <= 4)
	//		return null;
	//	return null;
	//}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		string text = slots.Str(exm, 1);
		if (slots.Count == 2)
		{
			g.GDrawString(text, 0, 0);
		}
		else if (slots.Count == 4)
		{
			Point p = ReadPoint(Name, exm, slots, 2);
			g.GDrawString(text, p.X, p.Y);
		}

		//生成する画像のサイズを取得
		Bitmap bitmap = new(16, 16);
		//Graphics canvas = Graphics.FromImage(bitmap);
		Graphics graphics = Graphics.FromImage(bitmap);
		Font font = g.Fnt;
		if (font == null)
		{
			font = new Font(Config.FontName, 100, GlobalStatic.Console.StringStyle.FontStyle, GraphicsUnit.Pixel);
		}

		SizeF size = graphics.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);

		//TextRenderer
		//Size tsize = TextRenderer.MeasureText(canvas, text, g.Fnt,
		//    new Size(2000, 2000), TextFormatFlags.NoPadding);
		//test用
		long[] resultArray = exm.VEvaluator.RESULT_ARRAY;
		resultArray[1] = (long)size.Width;
		resultArray[2] = (long)size.Height;
		return 1;
	}
}

#endregion

#region EE_GGETTEXTSIZE

internal sealed class GraphicsGetTextSizeMethod : FunctionMethod
{
	[BuiltinMethod("GGETTEXTSIZE")]
	private static GraphicsGetTextSizeMethod CreateGGETTEXTSIZE()
	{
		return new GraphicsGetTextSizeMethod();
	}

	public GraphicsGetTextSizeMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(string), typeof(string), typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count > 2)
	//		return null;
	//	return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		string text = slots.Str(exm, 0);
		//生成する画像のサイズを取得
		string fontname = slots.Str(exm, 1);
		long fontsize = slots.Int(exm, 2);
		FontStyle fs = FontStyle.Regular;
		if (slots.Count > 3)
		{
			long style = slots.Int(exm, 3);
			if ((style & 1) != 0)
			{
				fs |= FontStyle.Bold;
			}

			if ((style & 2) != 0)
			{
				fs |= FontStyle.Italic;
			}

			if ((style & 4) != 0)
			{
				fs |= FontStyle.Strikeout;
			}

			if ((style & 8) != 0)
			{
				fs |= FontStyle.Underline;
			}
		}

		Font fnt = new(fontname, fontsize, fs, GraphicsUnit.Pixel);
		Bitmap bitmap = new(16, 16);
		//Graphics canvas = Graphics.FromImage(bitmap);
		Graphics graphics = Graphics.FromImage(bitmap);
		SizeF size = graphics.MeasureString(text, fnt, int.MaxValue, StringFormat.GenericTypographic);

		//TextRenderer
		//Size tsize = TextRenderer.MeasureText(canvas, text, fnt,
		//    new Size(2000, 2000), TextFormatFlags.NoPadding);
		long[] resultArray = exm.VEvaluator.RESULT_ARRAY;
		//resultArray[1] = (Int64)tsize.Width;
		resultArray[1] = (long)size.Height;
		return (long)size.Width;
	}
}

#endregion

#region EE_GDRAWGWITHROTATE

// 使われてない
//public sealed class GraphicsRotateMethod : FunctionMethod
//{
//	public GraphicsRotateMethod()
//	{
//		ReturnType = typeof(Int64);
//		argumentTypeArray = new Type[] { typeof(Int64), typeof(Int64), typeof(Int64), typeof(Int64) };
//		CanRestructure = false;
//	}
//	public override string CheckArgumentType(string name, IOperandTerm[] arguments)
//	{
//		if (arguments.Count < 2)
//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
//		if (arguments.Count > 4)
//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
//		if (arguments.Count != 2 && arguments.Count != 4)
//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);
//		return null;
//	}
//	public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] arguments)
//	{
//		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
//			throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
//		GraphicsImage g = ReadGraphics(Name, exm, arguments, 0);
//		if (!g.IsCreated)
//			return 0;
//		Int64 angle = arguments[1].GetIntValue(exm);

//		//座標省略してたらx/2,y/2で渡す
//		if (arguments.Count == 2)
//		{
//			g.GRotate(angle, g.Width / 2, g.Height / 2);
//		}
//		else
//		{
//			Point p = ReadPoint(Name, exm, arguments, 2);
//			g.GRotate(angle, p.X, p.Y);
//		}
//		return 1;
//	}
//}
internal sealed class GraphicsDrawGWithRotateMethod : FunctionMethod
{
	[BuiltinMethod("GDRAWGWITHROTATE")]
	private static GraphicsDrawGWithRotateMethod CreateGDRAWGWITHROTATE()
	{
		return new GraphicsDrawGWithRotateMethod();
	}

	public GraphicsDrawGWithRotateMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(Int64), typeof(Int64), typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int | ArgType.NotNull,
					ArgType.Int | ArgType.NotNull
				},
				OmitStart = 3
			}
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 3)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 3);
	//	if (arguments.Count > 5)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	if (arguments.Count != 3 && arguments.Count != 5)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage dest = ReadGraphics(Name, exm, slots, 0);
		if (!dest.IsCreated)
		{
			return 0;
		}

		GraphicsImage src = ReadGraphics(Name, exm, slots, 1);
		if (!src.IsCreated)
		{
			return 0;
		}

		long angle = slots.Int(exm, 2);

		//座標省略してたらx/2,y/2で渡す
		if (slots.Count == 3)
		{
			dest.GDrawGWithRotate(src, angle, src.Width / 2, src.Height / 2);
		}
		else
		{
			Point p = ReadPoint(Name, exm, slots, 3);
			dest.GDrawGWithRotate(src, angle, p.X, p.Y);
		}

		return 1;
	}
}

#endregion

#region EE_失敗作

//brushの参照がうまくいかないので保留
/**
internal sealed class GraphicsGetBrushMethod : FunctionMethod
{
	public GraphicsGetBrushMethod()
	{
		ReturnType = typeof(Int64);
		 argumentTypeArray = [typeof(Int64)];
		CanRestructure = false;
	}
	public override Int64 GetIntValue(ExpressionMediator exm, IOperandTerm[] arguments)
	{
		Color c =
		GraphicsImage g = ReadGraphics(Name, exm, arguments, 0);
		return (SolidBrush());
	}
}
**/

#endregion

#region EE_GDRAWLINE

internal sealed class GraphicsDrawLineMethod : FunctionMethod
{
	[BuiltinMethod("GDRAWLINE")]
	private static GraphicsDrawLineMethod CreateGDRAWLINE()
	{
		return new GraphicsDrawLineMethod();
	}

	public GraphicsDrawLineMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		Point fromP = ReadPoint(Name, exm, slots, 1);
		Point forP = ReadPoint(Name, exm, slots, 3);
		g.GDrawLine(fromP.X, fromP.Y, forP.X, forP.Y);
		return 1;
	}
}

#endregion

internal sealed class SpriteStateMethod : FunctionMethod
{
	[BuiltinMethod("SPRITECREATED")]
	private static SpriteStateMethod CreateSPRITECREATED()
	{
		return new SpriteStateMethod();
	}

	[BuiltinMethod("SPRITEWIDTH")]
	private static SpriteStateMethod CreateSPRITEWIDTH()
	{
		return new SpriteStateMethod();
	}

	[BuiltinMethod("SPRITEHEIGHT")]
	private static SpriteStateMethod CreateSPRITEHEIGHT()
	{
		return new SpriteStateMethod();
	}

	[BuiltinMethod("SPRITEPOSX")]
	private static SpriteStateMethod CreateSPRITEPOSX()
	{
		return new SpriteStateMethod();
	}

	[BuiltinMethod("SPRITEPOSY")]
	private static SpriteStateMethod CreateSPRITEPOSY()
	{
		return new SpriteStateMethod();
	}

	public SpriteStateMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string imgname = slots.Str(exm, 0);
		ASprite? img = AppContents.GetSprite(imgname);
		if (img is not { IsCreated: true })
		{
			return 0;
		}

		switch (Name)
		{
			case "SPRITECREATED":
				return 1;
			case "SPRITEWIDTH":
				return img.DestBaseSize.Width;
			case "SPRITEHEIGHT":
				return img.DestBaseSize.Height;
			case "SPRITEPOSX":
				return img.DestBasePosition.X;
			case "SPRITEPOSY":
				return img.DestBasePosition.Y;
		}

		throw new ExeEE("SpriteStateMethod:" + Name + ":異常な分岐");
	}
}

internal sealed class SpriteSetPosMethod : FunctionMethod
{
	[BuiltinMethod("SPRITEMOVE")]
	private static SpriteSetPosMethod CreateSPRITEMOVE()
	{
		return new SpriteSetPosMethod();
	}

	[BuiltinMethod("SPRITESETPOS")]
	private static SpriteSetPosMethod CreateSPRITESETPOS()
	{
		return new SpriteSetPosMethod();
	}

	public SpriteSetPosMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string imgname = slots.Str(exm, 0);
		ASprite? img = AppContents.GetSprite(imgname);
		if (img is not { IsCreated: true })
		{
			return 0;
		}

		Point p = ReadPoint(Name, exm, slots, 1);
		switch (Name)
		{
			case "SPRITEMOVE":
				img.DestBasePosition.Offset(p);
				return 1;
			case "SPRITESETPOS":
				img.DestBasePosition = p;
				return 1;
		}

		throw new ExeEE("SpriteStateMethod:" + Name + ":異常な分岐");
	}
}

internal sealed class SpriteGetColorMethod : FunctionMethod
{
	[BuiltinMethod("SPRITEGETCOLOR")]
	private static SpriteGetColorMethod CreateSPRITEGETCOLOR()
	{
		return new SpriteGetColorMethod();
	}

	public SpriteGetColorMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string imgname = slots.Str(exm, 0);
		ASprite? img = AppContents.GetSprite(imgname);
		//他と違って失敗は0ではなく負の値
		if (img is not { IsCreated: true })
		{
			return -1;
		}

		Point p = ReadPoint(Name, exm, slots, 1);
		if (p.X < 0 || p.X >= img.DestBaseSize.Width)
		{
			return -1;
		}

		if (p.Y < 0 || p.Y >= img.DestBaseSize.Height)
		{
			return -1;
		}

		Color c = img.SpriteGetColor(p.X, p.Y);
		//Color.ToArgb()はInt32の負の値をとることがあり、Int64にうまく変換できない？（と思ったが気のせいだった
		//return ((long)c.A) << 24 + c.R << 16 + c.G << 8 + c.B;
		return c.ToArgb() & 0xFFFFFFFFL;
	}
}

internal sealed class ClientSizeMethod : FunctionMethod
{
	[BuiltinMethod("CLIENTWIDTH")]
	private static ClientSizeMethod CreateCLIENTWIDTH()
	{
		return new ClientSizeMethod();
	}

	[BuiltinMethod("CLIENTHEIGHT")]
	private static ClientSizeMethod CreateCLIENTHEIGHT()
	{
		return new ClientSizeMethod();
	}

	public ClientSizeMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		switch (Name)
		{
			case "CLIENTWIDTH":
				return exm.Console.ClientWidth;
			case "CLIENTHEIGHT":
				return exm.Console.ClientHeight;
		}

		throw new ExeEE("ClientSize:" + Name + ":異常な分岐");
	}
}

internal sealed class GraphicsCreateMethod : FunctionMethod
{
	[BuiltinMethod("GCREATE")]
	private static GraphicsCreateMethod CreateGCREATE()
	{
		return new GraphicsCreateMethod();
	}

	public GraphicsCreateMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (g.IsCreated)
		{
			return 0;
		}

		Point p = ReadPoint(Name, exm, slots, 1);
		int width = p.X;
		int height = p.Y;
		if (width <= 0) //{0}関数:GraphicsのWidthに0以下の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGWidth0, Name, width));
		{
			throw new CodeEE(string.Format(trerror.GParamIsNegative.Text, Name, "Width", width));
		}
		else if (width > AbstractImage.MAX_IMAGESIZE) //{0}関数:GraphicsのWidthに{2}以上の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGWidth1, Name, width, AbstractImage.MAX_IMAGESIZE));
		{
			throw new CodeEE(string.Format(trerror.GParamTooLarge.Text, Name, "Width", AbstractImage.MAX_IMAGESIZE, width));
		}

		if (height <= 0) //{0}関数:GraphicsのHeightに0以下の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGHeight0, Name, height));
		{
			throw new CodeEE(string.Format(trerror.GParamIsNegative.Text, Name, "Height", height));
		}
		else if (height > AbstractImage.MAX_IMAGESIZE) //{0}関数:GraphicsのHeightに{2}以上の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGHeight1, Name, height, AbstractImage.MAX_IMAGESIZE));
		{
			throw new CodeEE(string.Format(trerror.GParamTooLarge.Text, Name, "Height", AbstractImage.MAX_IMAGESIZE, height));
		}

		g.GCreate(width, height, false);
		return 1;
	}
}

internal sealed class GraphicsCreateFromFileMethod : FunctionMethod
{
	[BuiltinMethod("GCREATEFROMFILE")]
	private static GraphicsCreateFromFileMethod CreateGCREATEFROMFILE()
	{
		return new GraphicsCreateFromFileMethod();
	}

	public GraphicsCreateFromFileMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (g.IsCreated)
		{
			return 0;
		}

		string filename = slots.Str(exm, 1);
		bool isRelative = false;
		if (slots.Count > 2)
		{
			isRelative = slots.Int(exm, 2) != 0;
		}

		Bitmap? bmp = null;
		try
		{
			if (!Program.TryResolveContentFile(filename, isRelative, out GamePath? contentFile))
			{
				return 0;
			}

			string filepath = contentFile.FullName;
			if (!File.Exists(filepath))
			{
				return 0;
			}

			#region EM_私家版_webp

			// bmp = new Bitmap(filepath);
			bmp = Utils.LoadImage(filepath);
			if (bmp == null)
			{
				return 0;
			}

			#endregion

			if (bmp.Width > AbstractImage.MAX_IMAGESIZE || bmp.Height > AbstractImage.MAX_IMAGESIZE)
			{
				return 0;
			}

			g.GCreateFromF(bmp, Config.TextDrawingMode == TextDrawingMode.WINAPI);
		}
		catch (Exception e)
		{
			if (e is CodeEE)
			{
				throw;
			}
		}
		finally
		{
			bmp?.Dispose();
		}

		//画像ファイルではなかった、などによる失敗
		if (!g.IsCreated)
		{
			return 0;
		}

		return 1;
	}
}

internal sealed class GraphicsDisposeMethod : FunctionMethod
{
	[BuiltinMethod("GDISPOSE")]
	private static GraphicsDisposeMethod CreateGDISPOSE()
	{
		return new GraphicsDisposeMethod();
	}

	public GraphicsDisposeMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		g.GDispose();
		return 1;
	}
}

/// <summary>
/// SPRITECREATE(str imgName, int gID, int x, int y, int width, int height)
/// SPRITECREATE(str imgName, int gID)
/// </summary>
internal sealed class SpriteCreateMethod : FunctionMethod
{
	[BuiltinMethod("SPRITECREATE")]
	private static SpriteCreateMethod CreateSPRITECREATE()
	{
		return new SpriteCreateMethod();
	}

	public SpriteCreateMethod()
	{
		ReturnType = typeof(long);
		//  argumentTypeArray = [typeof(string)];
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int } },
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.String,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int
				}
			}
		];
		CanRestructure = false;
	}
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{

	//	if (arguments.Count < 2)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
	//	if (arguments.Count > 6)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	if (arguments[0] == null)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, 0 + 1);
	//	if (arguments[1] == null)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, 1 + 1);
	//	if (arguments[0].GetOperandType() != typeof(string))
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, 0 + 1);
	//	if (arguments[1].GetOperandType() != typeof(Int64))
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, 1 + 1);
	//	if (arguments.Count == 2)
	//		return null;
	//	if (arguments.Count != 6)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);
	//	for (int i = 2; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);
	//		if (arguments[i].GetOperandType() != typeof(Int64))
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		string imgname = slots.Str(exm, 0);
		if (string.IsNullOrEmpty(imgname))
		{
			return 0;
		}

		ASprite? img = AppContents.GetSprite(imgname);
		if (img is { IsCreated: true })
		{
			return 0;
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 1);
		if (!g.IsCreated)
		{
			return 0;
		}

		Rectangle rect = new(0, 0, g.Width, g.Height);
		if (slots.Count == 6)
		{
			//四角形は正でも負でもよいが親画像の外を指してはいけない
			rect = ReadRectangle(Name, exm, slots, 2);

			#region EM_私家版_SPRITECREATE範囲制限緩和

			//if (rect.X + rect.Width < 0 || rect.X + rect.Width > g.Width || rect.Y + rect.Height < 0 || rect.Y + rect.Height > g.Height)
			//	throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodCIMGCreateOutOfRange0, Name));

			if (!rect.IntersectsWith(new Rectangle(0, 0, g.Width, g.Height)))
				// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodCIMGCreateOutOfRange0, Name));
			{
				throw new CodeEE(string.Format(trerror.ImgRefOutOfRange.Text, Name));
			}

			#endregion
		}

		AppContents.CreateSpriteG(imgname, g, rect);
		return 1;
	}
}

internal sealed class SpriteDisposeMethod : FunctionMethod
{
	[BuiltinMethod("SPRITEDISPOSE")]
	private static SpriteDisposeMethod CreateSPRITEDISPOSE()
	{
		return new SpriteDisposeMethod();
	}

	public SpriteDisposeMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string imgname = slots.Str(exm, 0);
		ASprite? img = AppContents.GetSprite(imgname);
		if (img is not { IsCreated: true })
		{
			return 0;
		}

		AppContents.SpriteDispose(imgname);
		return 1;
	}
}

internal sealed class SpriteDisposeAllMethod : FunctionMethod
{
	[BuiltinMethod("SPRITEDISPOSEALL")]
	private static SpriteDisposeAllMethod CreateSPRITEDISPOSEALL()
	{
		return new SpriteDisposeAllMethod();
	}

	public SpriteDisposeAllMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return AppContents.SpriteDisposeAll(slots.Int(exm, 0) != 0);
	}
}

/// <summary>
/// GCLEAR(int ID, int cARGB)
/// </summary>
internal sealed class GraphicsClearMethod : FunctionMethod
{
	[BuiltinMethod("GCLEAR")]
	private static GraphicsClearMethod CreateGCLEAR()
	{
		return new GraphicsClearMethod();
	}

	#region EM_私家版_GCLEAR拡張

	public GraphicsClearMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int } },
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int
				}
			}
		];
		argumentTypeArray = null;
		CanRestructure = false;
	}
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{

	//	if (arguments.Count != 2 && arguments.Count != 6)
	//		return string.Format("{0}関数には2つもしくは6つの引数が必要です", name);
	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);
	//		if (arguments[i].GetOperandType() != typeof(Int64))
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		Color c = ReadColor(Name, exm, slots, 1);
		if (!g.IsCreated)
		{
			return 0;
		}

		if (slots.Count == 2)
		{
			g.GClear(c);
		}
		else
		{
			g.GClear(c, (int)slots.Int(exm, 2), (int)slots.Int(exm, 3),
				(int)slots.Int(exm, 4), (int)slots.Int(exm, 5));
		}

		return 1;
	}

	#endregion
}

/// <summary>
/// GFILLRECTANGLE(int ID, int cARGB, int x, int y, int width, int height)
/// </summary>
internal sealed class GraphicsFillRectangleMethod : FunctionMethod
{
	[BuiltinMethod("GFILLRECTANGLE")]
	private static GraphicsFillRectangleMethod CreateGFILLRECTANGLE()
	{
		return new GraphicsFillRectangleMethod();
	}

	public GraphicsFillRectangleMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		Rectangle rect = ReadRectangle(Name, exm, slots, 1);
		g.GFillRectangle(rect);
		return 1;
	}
}

/// <summary>
/// GDRAWG(int ID, int srcID, int destX, int destY, int destWidth, int destHeight, int srcX, int srcY, int srcWidth, int srcHeight)
/// GDRAWG(int ID, int srcID, int destX, int destY, int destWidth, int destHeight, int srcX, int srcY, int srcWidth, int srcHeight, var CM)
/// </summary>
internal sealed class GraphicsDrawGMethod : FunctionMethod
{
	[BuiltinMethod("GDRAWG")]
	private static GraphicsDrawGMethod CreateGDRAWG()
	{
		return new GraphicsDrawGMethod();
	}

	public GraphicsDrawGMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 10 means "no color matrix" in both segments below: GetIntValue takes the
		// 10-argument form when arguments.Count == 10 or arguments[10] == null.
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.RefInt2D | ArgType.AllowConstRef
				},
				OmitStart = 10
			},
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.RefInt3D | ArgType.AllowConstRef
				},
				OmitStart = 10
			}
		];
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 10)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 10);
	//	if (arguments.Count > 11)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	for (int i = 0; i < 10; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);
	//		if (typeof(Int64) != arguments[i].GetOperandType())
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	if (arguments.Count == 10)
	//		return null;
	//	if (!(arguments[10] is VariableTerm varToken) || !varToken.IsInteger || (!varToken.Identifier.IsArray2D && !varToken.Identifier.IsArray3D))
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodGraphicsColorMatrix0, name);
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage dest = ReadGraphics(Name, exm, slots, 0);
		if (!dest.IsCreated)
		{
			return 0;
		}

		GraphicsImage src = ReadGraphics(Name, exm, slots, 1);
		if (!src.IsCreated)
		{
			return 0;
		}

		Rectangle destRect = ReadRectangle(Name, exm, slots, 2);
		Rectangle srcRect = ReadRectangle(Name, exm, slots, 6);
		// The colour matrix is an optional one: omit it or leave the slot empty for the 3-argument overload.
		if (slots.Count == 10 || slots.IsEmpty(10))
		{
			dest.GDrawG(src, destRect, srcRect);
			return 1;
		}

		float[][] cm = ReadColormatrix(Name, exm, slots, 10);
		dest.GDrawG(src, destRect, srcRect, cm);
		return 1;
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		for (int i = 0; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			//11番目の引数はColorMatrixの配列を指しているので定数にしてはいけない
			if (i == 10)
			{
				arguments.Required(i).Restructure(exm);
			}
			else
			{
				arguments[i] = arguments.Required(i).Restructure(exm);
			}
		}

		return false;
	}
}

/// <summary>
/// GDRAWGWITHMASK(int ID, int srcID, int maskID, int destX, int destY)
/// </summary>
internal sealed class GraphicsDrawGWithMaskMethod : FunctionMethod
{
	[BuiltinMethod("GDRAWGWITHMASK")]
	private static GraphicsDrawGWithMaskMethod CreateGDRAWGWITHMASK()
	{
		return new GraphicsDrawGWithMaskMethod();
	}

	public GraphicsDrawGWithMaskMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}


	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage dest = ReadGraphics(Name, exm, slots, 0);
		if (!dest.IsCreated)
		{
			return 0;
		}

		GraphicsImage src = ReadGraphics(Name, exm, slots, 1);
		if (!src.IsCreated)
		{
			return 0;
		}

		GraphicsImage mask = ReadGraphics(Name, exm, slots, 2);
		if (!mask.IsCreated)
		{
			return 0;
		}

		if (src.Width != mask.Width || src.Height != mask.Height)
		{
			return 0;
		}

		Point destPoint = ReadPoint(Name, exm, slots, 3);
		if (destPoint.X + src.Width > dest.Width || destPoint.Y + src.Height > dest.Height)
		{
			return 0;
		}

		dest.GDrawGWithMask(src, mask, destPoint);
		return 1;
	}
}

/// <summary>
/// GDRAWCIMG(int ID, str imgName)
/// GDRAWCIMG(int ID, str imgName, int destX, int destY)
/// GDRAWCIMG(int ID, str imgName, int destX, int destY, int destWidth, int destHeight)
/// GDRAWCIMG(int ID, str imgName, int destX, int destY, int destWidth, int destHeight, var CM)
/// </summary>
internal sealed class GraphicsDrawSpriteMethod : FunctionMethod
{
	[BuiltinMethod("GDRAWSPRITE")]
	private static GraphicsDrawSpriteMethod CreateGDRAWSPRITE()
	{
		return new GraphicsDrawSpriteMethod();
	}

	public GraphicsDrawSpriteMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(string), typeof(Int64), typeof(Int64), typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String } },
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String, ArgType.Int, ArgType.Int } },
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.String,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.RefInt2D | ArgType.AllowConstRef | ArgType.NotNull
				},
				OmitStart = 6
			},
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.String,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.RefInt3D | ArgType.AllowConstRef | ArgType.NotNull
				},
				OmitStart = 6
			}
		];
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
	//	if (arguments.Count > 7)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	if (arguments.Count != 2 && arguments.Count != 4 && arguments.Count != 6 && arguments.Count != 7)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);

	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);

	//		if (i < argumentTypeArray.Length && argumentTypeArray[i] != arguments[i].GetOperandType())
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	if (arguments.Count <= 6)
	//		return null;
	//	if (!(arguments[6] is VariableTerm varToken) || !varToken.IsInteger || (!varToken.Identifier.IsArray2D && !varToken.Identifier.IsArray3D))
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodGraphicsColorMatrix0, name);
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage dest = ReadGraphics(Name, exm, slots, 0);
		if (!dest.IsCreated)
		{
			return 0;
		}

		string imgname = slots.Str(exm, 1);
		ASprite? img = AppContents.GetSprite(imgname);
		if (img is not { IsCreated: true })
		{
			return 0;
		}

		Rectangle destRect = new(0, 0, img.DestBaseSize.Width, img.DestBaseSize.Height);
		if (slots.Count == 2)
		{
			dest.GDrawCImg(img, destRect);
			return 1;
		}

		if (slots.Count == 4)
		{
			Point p = ReadPoint(Name, exm, slots, 2);
			destRect.X = p.X;
			destRect.Y = p.Y;
			dest.GDrawCImg(img, destRect);
			return 1;
		}

		if (slots.Count == 6)
		{
			destRect = ReadRectangle(Name, exm, slots, 2);
			dest.GDrawCImg(img, destRect);
			return 1;
		}

		//if (arguments.Count == 7)
		destRect = ReadRectangle(Name, exm, slots, 2);
		float[][] cm = ReadColormatrix(Name, exm, slots, 6);
		dest.GDrawCImg(img, destRect, cm);
		return 1;
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		for (int i = 0; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			//7番目の引数はColorMatrixの配列を指しているので定数にしてはいけない
			if (i == 6)
			{
				arguments.Required(i).Restructure(exm);
			}
			else
			{
				arguments[i] = arguments.Required(i).Restructure(exm);
			}
		}

		return false;
	}
}

/// <summary>
/// int SPRITEANIMECREATE (string name, int width, int height)
/// </summary>
internal sealed class SpriteAnimeCreateMethod : FunctionMethod
{
	[BuiltinMethod("SPRITEANIMECREATE")]
	private static SpriteAnimeCreateMethod CreateSPRITEANIMECREATE()
	{
		return new SpriteAnimeCreateMethod();
	}

	public SpriteAnimeCreateMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		string imgname = slots.Str(exm, 0);
		if (string.IsNullOrEmpty(imgname))
		{
			return 0;
		}

		//リソースチェック・既に存在しているならば失敗
		ASprite? img = AppContents.GetSprite(imgname);
		if (img is { IsCreated: true })
		{
			return 0;
		}

		Point pos = ReadPoint(Name, exm, slots, 1);
		if (pos.X <= 0) //{0}関数:GraphicsのWidthに0以下の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGWidth0, Name, pos.X));
		{
			throw new CodeEE(string.Format(trerror.GParamIsNegative.Text, Name, "Width", pos.X));
		}
		else if (pos.X > AbstractImage.MAX_IMAGESIZE) //{0}関数:GraphicsのWidthに{2}以上の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGWidth1, Name, pos.X, AbstractImage.MAX_IMAGESIZE));
		{
			throw new CodeEE(string.Format(trerror.GParamTooLarge.Text, Name, "Width", AbstractImage.MAX_IMAGESIZE, pos.X));
		}

		if (pos.Y <= 0) //{0}関数:GraphicsのHeightに0以下の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGHeight0, Name, pos.Y));
		{
			throw new CodeEE(string.Format(trerror.GParamIsNegative.Text, Name, "Height", pos.Y));
		}
		else if (pos.Y > AbstractImage.MAX_IMAGESIZE) //{0}関数:GraphicsのHeightに{2}以上の値({1})が指定されました
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGHeight1, Name, pos.Y, AbstractImage.MAX_IMAGESIZE));
		{
			throw new CodeEE(string.Format(trerror.GParamTooLarge.Text, Name, "Height", AbstractImage.MAX_IMAGESIZE, pos.Y));
		}

		AppContents.CreateSpriteAnime(imgname, pos.X, pos.Y);
		return 1;
	}
}

/// <summary>
/// SPRITEANIMEADDFRAME (string name, int graphID, int x, int y, int width, int height, int offsetx, int offsety, int delay)
/// </summary>
internal sealed class SpriteAnimeAddFrameMethod : FunctionMethod
{
	[BuiltinMethod("SPRITEANIMEADDFRAME")]
	private static SpriteAnimeAddFrameMethod CreateSPRITEANIMEADDFRAME()
	{
		return new SpriteAnimeAddFrameMethod();
	}

	public SpriteAnimeAddFrameMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray =
			[typeof(string), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		string imgname = slots.Str(exm, 0);
		if (string.IsNullOrEmpty(imgname))
		{
			return 0;
		}

		if (AppContents.GetSprite(imgname) == null)
		{
			return 0;
		}

		if (AppContents.GetSprite(imgname) is not SpriteAnime { IsCreated: true } img)
		{
			return 0;
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 1);
		if (!g.IsCreated)
		{
			return 0;
		}

		Rectangle rect = ReadRectangle(Name, exm, slots, 2);
		//四角形は正でなければならず、かつ親画像の外を指してはいけない
		if (rect.Width <= 0 || rect.Height <= 0 ||
		    rect.X < 0 || rect.X + rect.Width > g.Width || rect.Y < 0 || rect.Y + rect.Height > g.Height)
		{
			return 0;
		}

		//throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodCIMGCreateOutOfRange0, Name));
		Point offset = ReadPoint(Name, exm, slots, 6);
		long delay = slots.Int(exm, 8);
		if (delay is <= 0 or > int.MaxValue)
		{
			return 0;
		}

		img.AddFrame(g, rect, offset, (int)delay);
		return 1;
	}
}

/// <summary>
/// CBGCLEAR
/// </summary>
internal sealed class CBGClearMethod : FunctionMethod
{
	[BuiltinMethod("CBGCLEAR")]
	private static CBGClearMethod CreateCBGCLEAR()
	{
		return new CBGClearMethod();
	}

	public CBGClearMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
		//	throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		exm.Console.CBG_Clear();
		return 1;
	}
}

/// <summary>
/// CBGREMOVERANGE(int zmin, int zmax)
/// </summary>
internal sealed class CBGRemoveRangeMethod : FunctionMethod
{
	[BuiltinMethod("CBGREMOVERANGE")]
	private static CBGRemoveRangeMethod CreateCBGREMOVERANGE()
	{
		return new CBGRemoveRangeMethod();
	}

	public CBGRemoveRangeMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long x64 = slots.Int(exm, 0);
		long y64 = slots.Int(exm, 1);
		unchecked
		{
			exm.Console.CBG_ClearRange((int)x64, (int)y64);
		}

		return 1;
	}
}

/// <summary>
/// CBGCLEARBUTTON
/// </summary>
internal sealed class CBGClearButtonMethod : FunctionMethod
{
	[BuiltinMethod("CBGCLEARBUTTON")]
	private static CBGClearButtonMethod CreateCBGCLEARBUTTON()
	{
		return new CBGClearButtonMethod();
	}

	public CBGClearButtonMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
		//	throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		exm.Console.CBG_ClearButton();
		return 1;
	}
}

/// <summary>
/// CBGREMOVEBMAP
/// </summary>
internal sealed class CBGRemoveBMapMethod : FunctionMethod
{
	[BuiltinMethod("CBGREMOVEBMAP")]
	private static CBGRemoveBMapMethod CreateCBGREMOVEBMAP()
	{
		return new CBGRemoveBMapMethod();
	}

	public CBGRemoveBMapMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
		//	throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		exm.Console.CBG_ClearBMap();
		return 1;
	}
}

/// <summary>
/// CBGSETG(int ID, int x, int y, int zdepth)
/// </summary>
internal sealed class CBGSetGraphicsMethod : FunctionMethod
{
	[BuiltinMethod("CBGSETG")]
	private static CBGSetGraphicsMethod CreateCBGSETG()
	{
		return new CBGSetGraphicsMethod();
	}

	public CBGSetGraphicsMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated || g.Bitmap == null)
		{
			return 0;
		}

		Point p = ReadPoint(Name, exm, slots, 1);
		long z64 = slots.Int(exm, 3);
		if (z64 is < int.MinValue or > int.MaxValue or 0)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, z64, 3 + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRangeExcept.Text, Name, 4, z64, int.MinValue, int.MaxValue, 0));
		}

		exm.Console.CBG_SetGraphics(g, p.X, p.Y, (int)z64);
		return 1;
	}
}

/// <summary>
/// CBGSETBMAPG(int ID, int x, int y, int zdepth)
/// </summary>
internal sealed class CBGSetBMapGMethod : FunctionMethod
{
	[BuiltinMethod("CBGSETBMAPG")]
	private static CBGSetBMapGMethod CreateCBGSETBMAPG()
	{
		return new CBGSetBMapGMethod();
	}

	public CBGSetBMapGMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated || g.Bitmap == null)
		{
			return 0;
		}

		exm.Console.CBG_SetButtonMap(g);
		return 1;
	}
}

/// <summary>
/// CBGSETCIMG(str imgName, int x, int y, int zdepth)
/// </summary>
internal sealed class CBGSetCIMGMethod : FunctionMethod
{
	[BuiltinMethod("CBGSETSPRITE")]
	private static CBGSetCIMGMethod CreateCBGSETSPRITE()
	{
		return new CBGSetCIMGMethod();
	}

	public CBGSetCIMGMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
		//	throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));

		string imgname = slots.Str(exm, 0);
		ASprite? img = AppContents.GetSprite(imgname);
		if (img is not { IsCreated: true })
		{
			return 0;
		}

		Point p = ReadPoint(Name, exm, slots, 1);
		long z64 = slots.Int(exm, 3);
		if (z64 is < int.MinValue or > int.MaxValue or 0)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, z64, 3 + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRangeExcept.Text, Name, 4, z64, int.MinValue, int.MaxValue, 0));
		}

		if (!exm.Console.CBG_SetImage(img, p.X, p.Y, (int)z64))
		{
			return 0;
		}

		return 1;
	}
}

/// <summary>
/// CBGSETBUTTONCIMG(int button, str imgName, str imgName, int x, int y,int zdepth str tooltipmes)
/// </summary>
internal sealed class CBGSETButtonSpriteMethod : FunctionMethod
{
	[BuiltinMethod("CBGSETBUTTONSPRITE")]
	private static CBGSETButtonSpriteMethod CreateCBGSETBUTTONSPRITE()
	{
		return new CBGSETButtonSpriteMethod();
	}

	public CBGSETButtonSpriteMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(string), typeof(string), typeof(Int64), typeof(Int64), typeof(Int64), typeof(string) };
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.String,
					ArgType.String,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.String | ArgType.NotNull
				},
				OmitStart = 6
			}
		];
		CanRestructure = false;
	}
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{

	//	if (arguments.Count < 6)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 6);
	//	if (arguments.Count > 7)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	if (arguments.Count != 6 && arguments.Count != 7)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);

	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);

	//		if (i < argumentTypeArray.Length && argumentTypeArray[i] != arguments[i].GetOperandType())
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		long b64 = slots.Int(exm, 0);
		if (b64 is < 0 or > 0xFFFFFF)
		{
			return 0;
		}

		string imgnameN = slots.Str(exm, 1);
		ASprite? imgN = AppContents.GetSprite(imgnameN);
		string imgnameB = slots.Str(exm, 2);
		ASprite? imgB = AppContents.GetSprite(imgnameB);

		Point p = ReadPoint(Name, exm, slots, 3);
		long z64 = slots.Int(exm, 5);
		if (z64 is < int.MinValue or > int.MaxValue or 0)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, z64, 5 + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRangeExcept.Text, Name, 6, z64, int.MinValue, int.MaxValue, 0));
		}

		string? tooltip = null;
		if (slots.Count > 6)
		{
			tooltip = slots.Str(exm, 6);
		}

		// The two images are stored as given; CBG_SetButtonImage has never rejected null.
		if (!exm.Console.CBG_SetButtonImage((int)b64, imgN!, imgB!, p.X, p.Y, (int)z64, tooltip))
		{
			return 0;
		}

		return 1;
	}
}

internal sealed class GetKeyStateMethod : FunctionMethod
{
	[BuiltinMethod("GETKEY")]
	private static GetKeyStateMethod CreateGETKEY()
	{
		return new GetKeyStateMethod();
	}

	[BuiltinMethod("GETKEYTRIGGERED")]
	private static GetKeyStateMethod CreateGETKEYTRIGGERED()
	{
		return new GetKeyStateMethod();
	}

	private static readonly short[] keytoggle = new short[256];

	public GetKeyStateMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (!exm.Console.IsActive) //アクティブでないならスルー
		{
			return 0;
		}

		long keycode = slots.Int(exm, 0);
		if (keycode is < 0 or > 255)
		{
			return 0;
		}

		short s = WinInput.GetKeyState((int)keycode);
		short toggle = keytoggle[keycode];
		keytoggle[keycode] = (short)((s & 1) + 1); //初期値0、トグル状態に応じて1か2を代入。
		switch (Name)
		{
			case "GETKEY": return s < 0 ? 1 : 0;
			case "GETKEYTRIGGERED": return s < 0 && toggle != keytoggle[keycode] ? 1 : 0; //初回はtrue、2回目以降はトグル状態が前回と違う場合のみ1
		}

		throw new ExeEE(trerror.AbnormalBranch.Text);
	}
}

internal sealed class MousePosMethod : FunctionMethod
{
	[BuiltinMethod("MOUSEX")]
	private static MousePosMethod CreateMOUSEX()
	{
		return new MousePosMethod();
	}

	[BuiltinMethod("MOUSEY")]
	private static MousePosMethod CreateMOUSEY()
	{
		return new MousePosMethod();
	}

	public MousePosMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		switch (Name)
		{
			case "MOUSEX": return exm.Console.GetMousePosition().X;
			case "MOUSEY": return exm.Console.GetMousePosition().Y;
		}

		throw new ExeEE(trerror.AbnormalName.Text);
	}
}

#region EE_MOUSEB

internal sealed class MouseButtonMethod : FunctionMethod
{
	[BuiltinMethod("MOUSEB")]
	private static MouseButtonMethod CreateMOUSEB()
	{
		return new MouseButtonMethod();
	}

	public MouseButtonMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//if (exm.Console.SelectingButton != null)
		//	return exm.Console.SelectingButton.ToString();
		bool b = exm.Console.AlwaysRefresh;
		Point point = exm.Console.Window.MainPicBox.PointToClient(Control.MousePosition);
		exm.Console.AlwaysRefresh = true;
		if (exm.Console.Window.MainPicBox.ClientRectangle.Contains(point))
		{
			exm.Console.MoveMouse(point);
		}

		exm.Console.AlwaysRefresh = b;
		if (exm.Console.PointingSring != null)
		{
			if (!exm.Console.PointingSring.IsButton)
			{
				return "";
			}

			if (exm.Console.PointingSring.IsInteger)
			{
				return exm.Console.PointingSring.Input.ToString();
			}

			return exm.Console.PointingSring.Inputs;
		}

		return "";
	}
}

#endregion

internal sealed class IsActiveMethod : FunctionMethod
{
	[BuiltinMethod("ISACTIVE")]
	private static IsActiveMethod CreateISACTIVE()
	{
		return new IsActiveMethod();
	}

	public IsActiveMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return exm.Console.IsActive ? 1 : 0;
	}
}

internal sealed class SetAnimeTimerMethod : FunctionMethod
{
	[BuiltinMethod("SETANIMETIMER")]
	private static SetAnimeTimerMethod CreateSETANIMETIMER()
	{
		return new SetAnimeTimerMethod();
	}

	public SetAnimeTimerMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long i64 = slots.Int(exm, 0);
		if (i64 is < int.MinValue or > short.MaxValue)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, i64, 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, 1, i64, int.MinValue, int.MaxValue));
		}

		exm.Console.setRedrawTimer((int)i64);
		return 1;
	}
}

/// <summary>
/// int SAVETEXT str text, int fileNo{, int force_savdir, int force_UTF8}
/// </summary>
internal sealed class SaveTextMethod : FunctionMethod
{
	[BuiltinMethod("SAVETEXT")]
	private static SaveTextMethod CreateSAVETEXT()
	{
		return new SaveTextMethod();
	}

	public SaveTextMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = new Type[] { typeof(string) ,typeof(Int64), typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Any, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = false;
	}
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{

	//	if (arguments.Count < 2)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 2);
	//	if (arguments.Count > 4)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);
	//		#region EM_私家版_LoadText＆SaveText機能拡張
	//		if (i == 1 && arguments[i].GetOperandType() == typeof(string)) continue;
	//		#endregion
	//		if (i < argumentTypeArray.Length && argumentTypeArray[i] != arguments[i].GetOperandType())
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		#region EM_私家版_LoadText＆SaveText機能拡張

		//string savText = arguments[0].GetStrValue(exm);
		//Int64 i64 = arguments[1].GetIntValue(exm);
		//if (i64 < 0 || i64 > int.MaxValue)
		//	return 0;
		//bool forceSavdir = arguments.Count > 2 && (arguments[2].GetIntValue(exm) != 0);
		//bool forceUTF8 = arguments.Count > 3 && (arguments[3].GetIntValue(exm) != 0);
		//int fileIndex = (int)i64;
		//string filepath = forceSavdir ?
		//	GetSaveDataPathText(fileIndex, Config.ForceSavDir) :
		//	GetSaveDataPathText(fileIndex, Config.SavDir);
		//Encoding encoding = forceUTF8 ?
		//	Encoding.GetEncoding("UTF-8") :
		//	Config.SaveEncode;
		//try
		//{
		//	if (forceSavdir)
		//		Config.ForceCreateSavDir();
		//	else
		//		Config.Config.CreateSavDir();
		//	System.IO.File.WriteAllText(filepath, savText, encoding);
		//}
		//catch { return 0; }
		string savText = slots.Str(exm, 0);
		string filepath;
		PurePath? fileDirectory = null;
		long i64 = -1;
		bool forceSavdir = slots.Count > 2 && slots.Int(exm, 2) != 0;
		bool forceUTF8 = slots.Count > 3 && slots.Int(exm, 3) != 0;


		if (slots.IsInteger(1))
		{
			i64 = slots.Int(exm, 1);
			if (i64 is < 0 or > int.MaxValue)
			{
				return 0;
			}

			int fileIndex = (int)i64;
			filepath = forceSavdir ? GetSaveDataPathText(fileIndex, Config.ForceSavDir) : GetSaveDataPathText(fileIndex, Config.SavDir);
		}
		else
		{
			string rawName = slots.Str(exm, 1);
			if (!GamePath.TryResolve(Program.GameRoot, rawName, out GamePath? textFile, out _))
			{
				return 0;
			}

			string tmp = textFile.Extension.Length > 1 ? textFile.Extension[1..].ToLower() : "";
			if (!Config.ValidExtension.Contains(tmp))
			{
				// A name that folded onto the game directory itself has no file name to extend, so it is
				// refused instead of raising out of the script.
				if (!textFile.TryChangeExtension(".txt", out GamePath? withTxt))
				{
					return 0;
				}

				textFile = withTxt;
			}

			filepath = textFile.FullName;
			fileDirectory = textFile.Directory;
			forceUTF8 = true;
		}

		// Encoding encoding = forceUTF8 ?
		// 	Encoding.GetEncoding("UTF-8") :
		// 	Config.SaveEncode;
		try
		{
			if (i64 >= 0)
			{
				if (forceSavdir)
				{
					Config.ForceCreateSavDir();
				}
				else
				{
					Config.CreateSavDir();
				}
			}
			else if (fileDirectory != null)
			{
				Directory.CreateDirectory(fileDirectory.ToString());
			}

			File.WriteAllText(filepath, savText, Config.SaveEncode);
		}
		catch
		{
			return 0;
		}

		#endregion

		return 1;
	}
}

/// <summary>
/// str LOADTEXT int fileNo{, int force_savdir, int force_UTF8}
/// </summary>
internal sealed class LoadTextMethod : FunctionMethod
{
	[BuiltinMethod("LOADTEXT")]
	private static LoadTextMethod CreateLOADTEXT()
	{
		return new LoadTextMethod();
	}

	public LoadTextMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = new Type[] { typeof(Int64), typeof(Int64), typeof(Int64) };
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{

	//	if (arguments.Count < 1)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum1, name, 1);
	//	if (arguments.Count > 3)
	//		return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum2, name);
	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);
	//		#region EM_私家版_LoadText＆SaveText機能拡張
	//		if (i == 0 && arguments[i].GetOperandType() == typeof(string)) continue;
	//		#endregion
	//		if (i < argumentTypeArray.Length && argumentTypeArray[i] != arguments[i].GetOperandType())
	//			return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
	//	}
	//	return null;
	//}
	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		#region EM_私家版_LoadText＆SaveText機能拡張

		//Int64 i64 = arguments[0].GetIntValue(exm);
		//if (i64 < 0 || i64 > int.MaxValue)
		//	return "";
		//bool forceSavdir = arguments.Count > 1 && (arguments[1].GetIntValue(exm) != 0);
		//bool forceUTF8 = arguments.Count > 2 && (arguments[2].GetIntValue(exm) != 0);
		//int fileIndex = (int)i64;
		//string filepath = forceSavdir ?
		//	GetSaveDataPathText(fileIndex, Config.ForceSavDir) :
		//	GetSaveDataPathText(fileIndex, Config.SavDir);
		//Encoding encoding = forceUTF8 ?
		//	Encoding.GetEncoding("UTF-8") :
		//	Config.SaveEncode;
		//if (!System.IO.File.Exists(filepath))
		//	return "";
		//string ret;
		//try
		//{
		//	ret = System.IO.File.ReadAllText(filepath, encoding);
		//}
		//catch { return ""; }
		//return ret;
		string ret = "";
		string filepath;
		long i64 = -1;
		bool forceSavdir = slots.Count > 1 && slots.Int(exm, 1) != 0;
		bool forceUTF8 = slots.Count > 2 && slots.Int(exm, 2) != 0;
		if (slots.IsInteger(0))
		{
			i64 = slots.Int(exm, 0);
			if (i64 is < 0 or > int.MaxValue)
			{
				return "";
			}

			int fileIndex = (int)i64;
			filepath = forceSavdir ? GetSaveDataPathText(fileIndex, Config.ForceSavDir) : GetSaveDataPathText(fileIndex, Config.SavDir);
		}
		else
		{
			if (!GamePath.TryResolve(Program.GameRoot, slots.Str(exm, 0), out GamePath? textFile, out _))
			{
				return string.Empty;
			}

			string tmp = textFile.Extension.Length > 1 ? textFile.Extension[1..].ToLower() : "";
			if (!Config.ValidExtension.Contains(tmp))
			{
				return "";
			}

			filepath = textFile.FullName;
		}

		if (!File.Exists(filepath))
		{
			return "";
		}

		try
		{
			ret = File.ReadAllText(filepath, EncodingHandler.DetectEncoding(filepath));
		}
		catch
		{
			return "";
		}

		//一貫性の観点で\rには死んでもらう
		return ret.Replace("\r", "");

		#endregion
	}
}

/// <summary>
/// int GSAVE int ID, int fileNo
/// </summary>
internal sealed class GraphicsSaveMethod : FunctionMethod
{
	[BuiltinMethod("GSAVE")]
	private static GraphicsSaveMethod CreateGSAVE()
	{
		return new GraphicsSaveMethod();
	}

	public GraphicsSaveMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (!g.IsCreated)
		{
			return 0;
		}

		long i64 = slots.Int(exm, 1);
		if (i64 is < 0 or > int.MaxValue)
		{
			return 0;
		}

		string filepath = GetSaveDataPathGraphics((int)i64);
		try
		{
			Config.CreateSavDir();
			g.Bitmap.Save(filepath);
		}
		catch
		{
			return 0;
		}

		return 1;
	}
}

/// <summary>
/// int GLOAD int ID, int fileNo
/// </summary>
internal sealed class GraphicsLoadMethod : FunctionMethod
{
	[BuiltinMethod("GLOAD")]
	private static GraphicsLoadMethod CreateGLOAD()
	{
		return new GraphicsLoadMethod();
	}

	public GraphicsLoadMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGDIPLUSOnly, Name));
		{
			throw new CodeEE(string.Format(trerror.GDIPlusOnly.Text, Name));
		}

		GraphicsImage g = ReadGraphics(Name, exm, slots, 0);
		if (g.IsCreated)
		{
			return 0;
		}

		long i64 = slots.Int(exm, 1);
		if (i64 is < 0 or > int.MaxValue)
		{
			return 0;
		}

		string filepath = GetSaveDataPathGraphics((int)i64);
		Bitmap? bmp = null;
		try
		{
			if (!File.Exists(filepath))
			{
				return 0;
			}

			#region EM_私家版_webp

			// bmp = new Bitmap(filepath);
			bmp = Utils.LoadImage(filepath);
			if (bmp == null)
			{
				return 0;
			}

			#endregion

			if (bmp.Width > AbstractImage.MAX_IMAGESIZE || bmp.Height > AbstractImage.MAX_IMAGESIZE)
			{
				return 0;
			}

			g.GCreateFromF(bmp, Config.TextDrawingMode == TextDrawingMode.WINAPI);
		}
		catch (Exception e)
		{
			if (e is CodeEE)
			{
				throw;
			}
		}
		finally
		{
			bmp?.Dispose();
		}

		if (!g.IsCreated)
		{
			return 0;
		}

		return 1;
	}
}

// Save-path builders for SAVETEXT / LOADTEXT / GSAVE / GLOAD; file-local because only this file's classes
// call them.
file static class SaveDataPathBuilder
{
	internal static string GetSaveDataPathText(int index, GamePath dir)
	{
		return dir.CombineName($"txt{index:00}.txt").FullName;
	}

	internal static string GetSaveDataPathGraphics(int index)
	{
		return Config.SavDir.CombineName($"img{index:0000}.png").FullName;
	}
}
