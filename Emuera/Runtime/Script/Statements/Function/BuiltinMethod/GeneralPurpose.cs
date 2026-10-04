// General-purpose builtins (汎用処理系), plus the EE font-file additions.

using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.UI.Game;
using System;
using System.Collections.Generic;
using System.Drawing;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class VarsizeMethod : FunctionMethod
{
	[BuiltinMethod("VARSIZE")]
	private static VarsizeMethod CreateVARSIZE()
	{
		return new VarsizeMethod();
	}

	public VarsizeMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 1 means dim = 0.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int }, OmitStart = 1 }
		];
		CanRestructure = true;
		//1808beta009 参照型変数の追加によりちょっと面倒になった
		HasUniqueRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 2)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!arguments[0].IsString)
	//		return name + "関数の1番目の引数が文字列ではありません";
	//	if (arguments[0] is SingleTerm)
	//	{
	//		string varName = ((SingleTerm)arguments[0]).Text;
	//		if (GlobalStatic.IdentifierDictionary.GetVariableToken(varName, null, true) == null)
	//			return name + "関数の1番目の引数が変数名ではありません";
	//	}
	//	if (arguments.Count == 1)
	//		return null;
	//	if ((arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の変数が数値ではありません";
	//	if (arguments.Count == 2)
	//		return null;
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableToken? var = GlobalStatic.IdentifierDictionary.GetVariableToken(slots.Str(exm, 0), null, true);
		if (var == null)
			// throw new CodeEE("VARSIZEの1番目の引数(\"" + arguments[0].GetStrValue(exm) + "\")が変数名ではありません");
		{
			throw new CodeEE(string.Format(trerror.NotVariableName.Text, Name, 1, slots.Str(exm, 0)));
		}

		int dim = 0;
		if (slots.Count == 2 && slots.Has(1))
		{
			dim = (int)slots.Int(exm, 1);
		}

		if (Config.VarsizeDimConfig && dim > 0)
		{
			dim--;
		}

		return var.GetLength(dim);
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		arguments.Required(0).Restructure(exm);
		if (arguments.Count > 1)
		{
			arguments.Required(1).Restructure(exm);
		}

		if (arguments[0] is SingleTerm && (arguments.Count == 1 || arguments[1] is SingleTerm))
		{
			VariableToken? var = GlobalStatic.IdentifierDictionary.GetVariableToken(arguments.Required(0).GetStrValue(exm), null, true);
			if (var == null || var.IsReference) //可変長の場合は定数化できない
			{
				return false;
			}

			return true;
		}

		return false;
	}
}

internal sealed class CheckfontMethod : FunctionMethod
{
	[BuiltinMethod("CHKFONT")]
	private static CheckfontMethod CreateCHKFONT()
	{
		return new CheckfontMethod();
	}

	public CheckfontMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = true; //起動中に変わることもそうそうないはず……
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string str = slots.Str(exm, 0);
		using System.Drawing.Text.InstalledFontCollection ifc = new();
		long isInstalled = 0;
		foreach (FontFamily ff in ifc.Families)
		{
			#region EE_フォントファイル対応

			if (ff.Name == str)
			{
				isInstalled = 1;
				break;
			}
		}

		foreach (FontFamily ff in GlobalStatic.Pfc.Families)
		{
			if (ff.Name == str)
			{
				isInstalled = 1;
				break;
			}
		}

		#endregion

		return isInstalled;
	}
}

internal sealed class CheckdataMethod : FunctionMethod
{
	[BuiltinMethod("CHKDATA")]
	private static CheckdataMethod CreateCHKDATA()
	{
		return new CheckdataMethod(EraSaveFileType.Normal);
	}

	public CheckdataMethod(EraSaveFileType type)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
		this.type = type;
	}

	private readonly EraSaveFileType type;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long target = slots.Int(exm, 0);
		if (target < 0)
			// throw new CodeEE(Name + "の引数に負の値(" + target.ToString() + ")が指定されました");
		{
			throw new CodeEE(string.Format(trerror.ArgIsNegative.Text, Name, 1, target));
		}
		else if (target > int.MaxValue)
			// throw new CodeEE(Name + "の引数(" + target.ToString() + ")が大きすぎます");
		{
			throw new CodeEE(string.Format(trerror.ArgIsTooLarge.Text, Name, 1, target));
		}

		EraDataResult result = exm.VEvaluator.CheckData((int)target, type);
		exm.VEvaluator.RESULTS = result.DataMes;
		exm.VEvaluator.RESULT_ARRAY[1] = result.Version;
		return (long)result.State;
	}
}

/// <summary>
/// ファイル名をstringで指定する版・CHKVARDATAとCHKCHARADATAはこっちに分類
/// </summary>
internal sealed class CheckdataStrMethod : FunctionMethod
{
	[BuiltinMethod("CHKCHARADATA")]
	private static CheckdataStrMethod CreateCHKCHARADATA()
	{
		return new CheckdataStrMethod(EraSaveFileType.CharVar);
	}

	public CheckdataStrMethod(EraSaveFileType type)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
		this.type = type;
	}

	private readonly EraSaveFileType type;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string datFilename = slots.Str(exm, 0);
		EraDataResult result = exm.VEvaluator.CheckData(datFilename, type);
		exm.VEvaluator.RESULTS = result.DataMes;
		return (long)result.State;
	}
}

/// <summary>
/// ファイル探索関数
/// </summary>
internal sealed class FindFilesMethod : FunctionMethod
{
	[BuiltinMethod("FIND_CHARADATA")]
	private static FindFilesMethod CreateFIND_CHARADATA()
	{
		return new FindFilesMethod(EraSaveFileType.CharVar);
	}

	public FindFilesMethod(EraSaveFileType type)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 0 means the "*" pattern.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String }, OmitStart = 0 }
		];
		CanRestructure = false;
		this.type = type;
	}

	private readonly EraSaveFileType type;

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count > 1)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments.Count == 0 || arguments[0] == null)
	//		return null;
	//	if (!arguments[0].IsString)
	//		return name + "関数の1番目の引数が文字列ではありません";
	//	return null;
	//}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string pattern = "*";
		if (slots.Has(0))
		{
			pattern = slots.Str(exm, 0);
		}

		List<string> filepathes = VariableEvaluator.GetDatFiles(type == EraSaveFileType.CharVar, pattern);
		string?[] results = exm.VEvaluator.VariableData.DataStringArray[(int)(VariableCode.RESULTS & VariableCode.__LOWERCASE__)];
		// Element-wise copy instead of List.CopyTo: the destination elements are string? (see VariableData.DataStringArray),
		// and truncating to the destination length matches the previous if/else pair.
		int copyLength = Math.Min(filepathes.Count, results.Length);
		for (int i = 0; i < copyLength; i++)
		{
			results[i] = filepathes[i];
		}

		return filepathes.Count;
	}
}

internal sealed class IsSkipMethod : FunctionMethod
{
	[BuiltinMethod("ISSKIP")]
	private static IsSkipMethod CreateISSKIP()
	{
		return new IsSkipMethod();
	}

	public IsSkipMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return exm.Process.SkipPrint ? 1L : 0L;
	}
}

internal sealed class MesSkipMethod : FunctionMethod
{
	[BuiltinMethod("MOUSESKIP")]
	private static MesSkipMethod CreateMOUSESKIP()
	{
		return new MesSkipMethod(true);
	}

	[BuiltinMethod("MESSKIP")]
	private static MesSkipMethod CreateMESSKIP()
	{
		return new MesSkipMethod(false);
	}

	public MesSkipMethod(bool warn)
	{
		ReturnType = typeof(long);
		argumentTypeArray = null;
		CanRestructure = false;
		this.warn = warn;
	}

	private readonly bool warn;

	internal override string? CheckArgumentType(string name, List<AExpression?> arguments, out int matchedIdx)
	{
		// No rule table: this class checks for itself, so the slot view it resolves carries no roles.
		matchedIdx = -1;
		if (arguments.Count > 0)
			// return name + "関数の引数が多すぎます";
		{
			return string.Format(trerror.TooManyFuncArgs.Text, name);
		}

		if (warn)
			// ParserMediator.Warn("関数MOUSESKIP()は推奨されません。代わりに関数MESSKIP()を使用してください", GlobalStatic.Process.GetScaningLine(), 1, false, false, null);
		{
			ParserMediator.Warn(string.Format(trerror.FuncDeprecated.Text, name, "MESSKIP"), GlobalStatic.Process.GetScaningLine()!, 1, false,
				false, null);
		}

		return null;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return GlobalStatic.Console.MesSkip ? 1L : 0L;
	}
}

internal sealed class GetColorMethod : FunctionMethod
{
	[BuiltinMethod("GETCOLOR")]
	private static GetColorMethod CreateGETCOLOR()
	{
		return new GetColorMethod(false);
	}

	[BuiltinMethod("GETDEFCOLOR")]
	private static GetColorMethod CreateGETDEFCOLOR()
	{
		return new GetColorMethod(true);
	}

	public GetColorMethod(bool isDef)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = isDef;
		defaultColor = isDef;
	}

	private readonly bool defaultColor;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		Color color = defaultColor ? Config.ForeColor : GlobalStatic.Console.StringStyle.Color;
		return color.ToArgb() & 0xFFFFFF;
	}
}

internal sealed class GetFocusColorMethod : FunctionMethod
{
	[BuiltinMethod("GETFOCUSCOLOR")]
	private static GetFocusColorMethod CreateGETFOCUSCOLOR()
	{
		return new GetFocusColorMethod();
	}

	public GetFocusColorMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return Config.FocusColor.ToArgb() & 0xFFFFFF;
	}
}

internal sealed class GetBGColorMethod : FunctionMethod
{
	[BuiltinMethod("GETBGCOLOR")]
	private static GetBGColorMethod CreateGETBGCOLOR()
	{
		return new GetBGColorMethod(false);
	}

	[BuiltinMethod("GETDEFBGCOLOR")]
	private static GetBGColorMethod CreateGETDEFBGCOLOR()
	{
		return new GetBGColorMethod(true);
	}

	public GetBGColorMethod(bool isDef)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = isDef;
		defaultColor = isDef;
	}

	private readonly bool defaultColor;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		Color color = defaultColor ? Config.BackColor : GlobalStatic.Console.bgColor;
		return color.ToArgb() & 0xFFFFFF;
	}
}

internal sealed class GetStyleMethod : FunctionMethod
{
	[BuiltinMethod("GETSTYLE")]
	private static GetStyleMethod CreateGETSTYLE()
	{
		return new GetStyleMethod();
	}

	public GetStyleMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		FontStyle fontstyle = GlobalStatic.Console.StringStyle.FontStyle;
		long ret = 0;
		if ((fontstyle & FontStyle.Bold) == FontStyle.Bold)
		{
			ret |= 1;
		}

		if ((fontstyle & FontStyle.Italic) == FontStyle.Italic)
		{
			ret |= 2;
		}

		if ((fontstyle & FontStyle.Strikeout) == FontStyle.Strikeout)
		{
			ret |= 4;
		}

		if ((fontstyle & FontStyle.Underline) == FontStyle.Underline)
		{
			ret |= 8;
		}

		return ret;
	}
}

internal sealed class GetFontMethod : FunctionMethod
{
	[BuiltinMethod("GETFONT")]
	private static GetFontMethod CreateGETFONT()
	{
		return new GetFontMethod();
	}

	public GetFontMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return GlobalStatic.Console.StringStyle.Fontname;
	}
}

internal sealed class BarStringMethod : FunctionMethod
{
	[BuiltinMethod("BARSTR")]
	private static BarStringMethod CreateBARSTR()
	{
		return new BarStringMethod();
	}

	public BarStringMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = true;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long var = slots.Int(exm, 0);
		long max = slots.Int(exm, 1);
		long length = slots.Int(exm, 2);
		return ExpressionMediator.CreateBar(var, max, length);
	}
}

internal sealed class CurrentAlignMethod : FunctionMethod
{
	[BuiltinMethod("CURRENTALIGN")]
	private static CurrentAlignMethod CreateCURRENTALIGN()
	{
		return new CurrentAlignMethod();
	}

	public CurrentAlignMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (exm.Console.Alignment == DisplayLineAlignment.LEFT)
		{
			return "LEFT";
		}
		else if (exm.Console.Alignment == DisplayLineAlignment.CENTER)
		{
			return "CENTER";
		}
		else
		{
			return "RIGHT";
		}
	}
}

internal sealed class CurrentRedrawMethod : FunctionMethod
{
	[BuiltinMethod("CURRENTREDRAW")]
	private static CurrentRedrawMethod CreateCURRENTREDRAW()
	{
		return new CurrentRedrawMethod();
	}

	public CurrentRedrawMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return exm.Console.Redraw == GameView.ConsoleRedraw.None ? 0L : 1L;
	}
}

internal sealed class ColorFromNameMethod : FunctionMethod
{
	[BuiltinMethod("COLOR_FROMNAME")]
	private static ColorFromNameMethod CreateCOLOR_FROMNAME()
	{
		return new ColorFromNameMethod();
	}

	public ColorFromNameMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string colorName = slots.Str(exm, 0);
		Color color = Color.FromName(colorName);
		int i;
		if (color.A > 0)
		{
			i = (color.R << 16) + (color.G << 8) + color.B;
		}
		else
		{
			if (colorName.Equals("transparent", StringComparison.OrdinalIgnoreCase))
				// throw new CodeEE("無色透明(Transparent)は色として指定できません");
			{
				throw new CodeEE(trerror.TransparentUnsupported.Text);
			}

			//throw new CodeEE("指定された色名\"" + colorName + "\"は無効な色名です");
			i = -1;
		}

		return i;
	}
}

internal sealed class ColorFromRGBMethod : FunctionMethod
{
	[BuiltinMethod("COLOR_FROMRGB")]
	private static ColorFromRGBMethod CreateCOLOR_FROMRGB()
	{
		return new ColorFromRGBMethod();
	}

	public ColorFromRGBMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long r = slots.Int(exm, 0);
		if (r is < 0 or > 255)
			// throw new CodeEE("第１引数が0から255の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, 1, r, 0, 255));
		}

		long g = slots.Int(exm, 1);
		if (g is < 0 or > 255)
			// throw new CodeEE("第２引数が0から255の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, 2, g, 0, 255));
		}

		long b = slots.Int(exm, 2);
		if (b is < 0 or > 255)
			// throw new CodeEE("第３引数が0から255の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, 3, b, 0, 255));
		}

		return (r << 16) + (g << 8) + b;
	}
}

/// <summary>
/// 1810 作ったけど保留
/// </summary>
// 使われてない
//private sealed class GetRefMethod : FunctionMethod
//{
//	public GetRefMethod()
//	{
//		ReturnType = typeof(string);
//		argumentTypeArray = null;
//		CanRestructure = false;
//	}
//	public override string CheckArgumentType(string name, IOperandTerm[] arguments)
//	{
//		if (arguments.Count < 1)
//			return name + "関数には少なくとも1つの引数が必要です";
//		if (arguments.Count > 1)
//			return name + "関数の引数が多すぎます";
//		if (arguments[0] == null)
//			return name + "関数の1番目の引数は省略できません";
//		if (!(arguments[0] is UserDefinedRefMethodNoArgTerm))
//			return name + "関数の1番目の引数が関数参照ではありません";
//		return null;
//	}
//	public override string GetStrValue(ExpressionMediator exm, IOperandTerm[] arguments)
//	{
//		return ((UserDefinedRefMethodNoArgTerm)arguments[0]).GetRefName();
//	}
//}
