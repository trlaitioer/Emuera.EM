// The EE / daughter-patch / Bitmap Cache / Hotkey / Outputlog tail (EE_EXISTSOUND onward).

using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

#region EE_EXISTSOUND

internal sealed class ExistSoundMethod : FunctionMethod
{
	[BuiltinMethod("EXISTSOUND")]
	private static ExistSoundMethod CreateEXISTSOUND()
	{
		return new ExistSoundMethod();
	}

	public ExistSoundMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (!Program.TryResolveSoundFile(slots.Str(exm, 0), out GamePath? soundPath))
		{
			return 0;
		}

		return File.Exists(soundPath.FullName) ? 1 : 0;
	}
}

#endregion

#region EE_EXISTFUNCTION

internal sealed class ExistFunctionMethod : FunctionMethod
{
	[BuiltinMethod("EXISTFUNCTION")]
	private static ExistFunctionMethod CreateEXISTFUNCTION()
	{
		return new ExistFunctionMethod();
	}

	public ExistFunctionMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string functionname = slots.Str(exm, 0);
		if (slots.Count == 1 || slots.Int(exm, 1) == 0)
		{
			FunctionLabelLine? func;
			if (Config.StringComparison == StringComparison.OrdinalIgnoreCase)
			{
				func = GlobalStatic.LabelDictionary.GetNonEventLabel(functionname.ToUpper());
			}
			else
			{
				func = GlobalStatic.LabelDictionary.GetNonEventLabel(functionname);
			}

			if (func == null)
			{
				return 0;
			}

			if (func.IsMethod)
			{
				if (func.MethodType == typeof(string))
				{
					return 3;
				}
				else if (func.MethodType == typeof(long))
				{
					return 2;
				}
			}

			return 1;
		}
		else
		{
			foreach (string funcname in GlobalStatic.Process.LabelDictionary.NoneventKeys)
			{
				if (funcname.Equals(functionname, StringComparison.CurrentCultureIgnoreCase))
				{
					// funcname comes from NoneventKeys, i.e. from the dictionary GetNonEventLabel reads, so the
					// lookup cannot miss.
					FunctionLabelLine func = GlobalStatic.LabelDictionary.GetNonEventLabel(funcname)!;

					if (func.IsMethod)
					{
						if (func.MethodType == typeof(string))
						{
							return 3;
						}
						else if (func.MethodType == typeof(long))
						{
							return 2;
						}
					}

					return 1;
				}
			}

			return 0;
		}
	}
}

#endregion

#region EE_GETMEMORYUSAGE

internal sealed class GetUsingMemoryMethod : FunctionMethod
{
	[BuiltinMethod("GETMEMORYUSAGE")]
	private static GetUsingMemoryMethod CreateGETMEMORYUSAGE()
	{
		return new GetUsingMemoryMethod();
	}

	public GetUsingMemoryMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		using System.Diagnostics.Process memory = System.Diagnostics.Process.GetCurrentProcess();
		return memory.WorkingSet64;
	}
}

#endregion

#region EE_CLEARMEMORY

internal sealed class ClearMemoryMethod : FunctionMethod
{
	[BuiltinMethod("CLEARMEMORY")]
	private static ClearMemoryMethod CreateCLEARMEMORY()
	{
		return new ClearMemoryMethod();
	}

	public ClearMemoryMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		using System.Diagnostics.Process destmemory = System.Diagnostics.Process.GetCurrentProcess();
		long destmemorysize = destmemory.WorkingSet64;
		GC.Collect();
		using System.Diagnostics.Process memory = System.Diagnostics.Process.GetCurrentProcess();
		return destmemorysize - memory.WorkingSet64;
	}
}

#endregion

#region EE_textbox拡張

internal sealed class GetTextBoxMethod : FunctionMethod
{
	[BuiltinMethod("GETTEXTBOX")]
	private static GetTextBoxMethod CreateGETTEXTBOX()
	{
		return new GetTextBoxMethod();
	}

	public GetTextBoxMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return exm.Console.Window.TextBox.Text;
	}
}

internal sealed class ChangeTextBoxMethod : FunctionMethod
{
	[BuiltinMethod("SETTEXTBOX")]
	private static ChangeTextBoxMethod CreateSETTEXTBOX()
	{
		return new ChangeTextBoxMethod();
	}

	public ChangeTextBoxMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		exm.Console.Window.ChangeTextBox(slots.Str(exm, 0));
		return 1;
	}
}

#endregion

#region EE_GETERDNAME

internal sealed class ErdNameMethod : FunctionMethod
{
	[BuiltinMethod("ERDNAME")]
	private static ErdNameMethod CreateERDNAME()
	{
		return new ErdNameMethod();
	}

	public ErdNameMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.RefAny | ArgType.AllowConstRef, ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = true;
		HasUniqueRestructure = true;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm vToken = slots.Var(0);
		string varname = "";
		if (slots.Count > 2)
		{
			varname = vToken.Identifier.Name + "@" + slots.Int(exm, 2);
		}
		else
		{
			varname = vToken.Identifier.Name;
		}

		long value = slots.Int(exm, 1);
		if (exm.VEvaluator.Constant.TryIntegerToKeyword(out string ret, value, varname))
		{
			return ret;
		}
		else
		{
			return "";
		}
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		arguments[1] = arguments.Required(1).Restructure(exm);
		return arguments[1] is SingleTerm;
	}
}

#endregion

#region EE_GETDISPLAYLINE

internal sealed class GetDisplayLineMethod : FunctionMethod
{
	[BuiltinMethod("GETDISPLAYLINE")]
	private static GetDisplayLineMethod CreateGETDISPLAYLINE()
	{
		return new GetDisplayLineMethod();
	}

	public GetDisplayLineMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int } }
		];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//修正に失敗したので差し戻す
		//long num = arguments[0].GetIntValue(exm)-exm.Console.DeletedLines;
		long num = slots.Int(exm, 0);
		if (num < 0 || num >= exm.Console.DisplayLineList.Count)
		{
			return "";
		}
		else
		{
			return exm.Console.DisplayLineList[(int)num].ToString();
		}
	}
}

#endregion

#region EE_GETDOINGFUNCTION

internal sealed class GetDoingFunctionMethod : FunctionMethod
{
	[BuiltinMethod("GETDOINGFUNCTION")]
	private static GetDoingFunctionMethod CreateGETDOINGFUNCTION()
	{
		return new GetDoingFunctionMethod();
	}

	public GetDoingFunctionMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = true;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		LogicalLine? line = exm.Process.GetScaningLine();
		if (line == null || line.ParentLabelLine == null)
		{
			return ""; //システム待機中のデバッグモードから呼び出し
		}

		return line.ParentLabelLine.LabelName;
	}
}

#endregion

#region EE_SystemInput拡張

internal sealed class FlowInputMethod : FunctionMethod
{
	[BuiltinMethod("FLOWINPUT")]
	private static FlowInputMethod CreateFLOWINPUT()
	{
		return new FlowInputMethod();
	}

	public FlowInputMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		exm.Process.flowinputDef = slots.Int(exm, 0);
		if (slots.Count > 1)
		{
			exm.Process.flowinput = slots.Int(exm, 1) != 0;
		}

		if (slots.Count > 2)
		{
			exm.Process.flowinputCanSkip = slots.Int(exm, 2) != 0;
		}

		if (slots.Count > 3)
		{
			exm.Process.flowinputForceSkip = slots.Int(exm, 3) != 0;
		}

		return 0;
	}
}

internal sealed class FlowInputsMethod : FunctionMethod
{
	[BuiltinMethod("FLOWINPUTS")]
	private static FlowInputsMethod CreateFLOWINPUTS()
	{
		return new FlowInputsMethod();
	}

	public FlowInputsMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		exm.Process.flowinputString = slots.Int(exm, 0) != 0;
		if (slots.Count > 1)
		{
			exm.Process.flowinputDefString = slots.Str(exm, 1);
		}

		return 0;
	}
}

#endregion

#region daughter-patch追加

internal sealed class GetMethMethod : FunctionMethod
{
	[BuiltinMethod("GETMETH")]
	private static GetMethMethod CreateGETMETH()
	{
		return new GetMethMethod();
	}

	public GetMethMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 1 means "no fallback": an undefined target raises CodeEE instead of a default.
		// The trailing VariadicAny is forwarded as written: a null element is interpreted downstream
		// (a UDF target substitutes that parameter's declared default, a missing target never reads
		// the slot), so it must stay nullable instead of taking ArgType.NotNull.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int, ArgType.VariadicAny }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		List<AExpression?> methArgs = slots.Tail(2);
		AExpression? term = GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, name, methArgs, true);

		if (term == null)
		{
			if (!slots.Has(1))
			{
				throw new CodeEE(string.Format(trerror.NotDefinedUserFunc.Text, name));
			}
			else
			{
				return slots.Int(exm, 1);
			}
		}
		else if (!term.IsInteger)
		{
			throw new CodeEE(string.Format(trerror.IsNotInt.Text, name));
		}
		else
		{
			return term.GetIntValue(exm);
		}
	}
}

internal sealed class GetMethsMethod : FunctionMethod
{
	[BuiltinMethod("GETMETHS")]
	private static GetMethsMethod CreateGETMETHS()
	{
		return new GetMethsMethod();
	}

	public GetMethsMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		// An empty slot 1 means "no fallback": an undefined target raises CodeEE instead of a default.
		// The trailing VariadicAny is forwarded as written: a null element is interpreted downstream
		// (a UDF target substitutes that parameter's declared default, a missing target never reads
		// the slot), so it must stay nullable instead of taking ArgType.NotNull.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.VariadicAny }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		List<AExpression?> methArgs = slots.Tail(2);
		AExpression? term = GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, name, methArgs, true);

		if (term == null)
		{
			if (!slots.Has(1))
			{
				throw new CodeEE(string.Format(trerror.NotDefinedUserFunc.Text, name));
			}
			else
			{
				return slots.Str(exm, 1);
			}
		}
		else if (!term.IsString)
		{
			throw new CodeEE(string.Format(trerror.IsNotStr.Text, name));
		}
		else
		{
			return term.GetStrValue(exm);
		}
	}
}

internal sealed class ExistMethMethod : FunctionMethod
{
	[BuiltinMethod("EXISTMETH")]
	private static ExistMethMethod CreateEXISTMETH()
	{
		return new ExistMethMethod();
	}

	public ExistMethMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		AExpression? term;
		try
		{
			term = GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, name, [], true);
		}
		catch (CodeEE)
		{
			return 0;
		}

		if (term == null)
		{
			return 0;
		}
		else
		{
			long res = 0;
			if (term.IsInteger)
			{
				res |= 1;
			}

			if (term.IsString)
			{
				res |= 2;
			}

			return res;
		}
	}
}

#endregion

//Bitmap Cache

#region Bitmap Cache

internal sealed class BitmapCacheEnableMethod : FunctionMethod
{
	[BuiltinMethod("BITMAP_CACHE_ENABLE")]
	private static BitmapCacheEnableMethod CreateBITMAP_CACHE_ENABLE()
	{
		return new BitmapCacheEnableMethod();
	}

	public BitmapCacheEnableMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long argument0 = slots.Int(exm, 0);
		GlobalStatic.Console.bitmapCacheEnabledForNextLine = argument0 != 0;
		return 0;
	}
}

#endregion

//HOTKEY STATE
internal sealed class HotkeyStateMethod : FunctionMethod
{
	[BuiltinMethod("HOTKEY_STATE")]
	private static HotkeyStateMethod CreateHOTKEY_STATE()
	{
		return new HotkeyStateMethod();
	}

	public HotkeyStateMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// GetIntValue writes both parameters to the hotkey state, so neither can be omitted: OmitStart is
		// set equal to the slot count (the shape HotkeyStateInitMethod already uses), which keeps both
		// slots required by count and also rejects an explicitly empty one through i < OmitStart.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long argument0 = slots.Int(exm, 0);
		long argument1 = slots.Int(exm, 1);
		GlobalStatic.Console.Window.hotkeyState.HotkeyStateSet((nint)argument0, (nint)argument1);
		return 0;
	}
}

internal sealed class HotkeyStateInitMethod : FunctionMethod
{
	[BuiltinMethod("HOTKEY_STATE_INIT")]
	private static HotkeyStateInitMethod CreateHOTKEY_STATE_INIT()
	{
		return new HotkeyStateInitMethod();
	}

	public HotkeyStateInitMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long argument0 = slots.Int(exm, 0);
		GlobalStatic.Console.Window.hotkeyState.HotkeyStateInit((nint)argument0);
		return 0;
	}
}

#region EE_OUTPUTLOG拡張

internal sealed class OutputlogMethod : FunctionMethod
{
	[BuiltinMethod("OUTPUTLOG")]
	private static OutputlogMethod CreateOUTPUTLOG()
	{
		return new OutputlogMethod();
	}

	public OutputlogMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 0 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string filename = "";
		if (slots.Count > 0)
		{
			filename = slots.Str(exm, 0);
		}

		bool hideInfo = false;
		if (slots.Count > 1)
		{
			hideInfo = slots.Int(exm, 1) == 1;
		}


		exm.Console.OutputLog(filename, hideInfo);
		return 1;
	}
}

#endregion
