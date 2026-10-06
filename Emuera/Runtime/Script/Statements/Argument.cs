using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.PluginSystem;
using System;
using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Script.Statements;

#region EM_私家版_INPUT系機能拡張

internal sealed class SpInputsArgument : Argument
{
	// A non-null CanSkip implies non-null Def and Mouse - the reasoning lives with the builders
	// (Runtime/Script/Statements/ArgumentBuilder.cs, SP_INPUT_ArgumentBuilder.CreateArgument and
	// SP_INPUTS_ArgumentBuilder.CreateArgument). The instruction sites in Instraction.Child.cs read
	// Def/Mouse under this invariant.
	public SpInputsArgument(AExpression? def, AExpression? mouse, AExpression? canskip)
	{
		Def = def;
		Mouse = mouse;
		CanSkip = canskip;
	}

	public readonly AExpression? Def;
	public readonly AExpression? Mouse;
	public readonly AExpression? CanSkip;
}

#endregion

#region EM_私家版_HTMLパラメータ拡張

internal sealed class MixedIntegerExprTerm
{
	public AExpression num = null!;
	public bool isPx;
}

internal sealed class SpPrintShapeArgument : Argument
{
	public SpPrintShapeArgument(MixedIntegerExprTerm[] param)
	{
		Param = param;
	}

	public readonly MixedIntegerExprTerm[] Param;
}

internal sealed class SpPrintImgArgument : Argument
{
	public SpPrintImgArgument(AExpression name, AExpression? nameb, AExpression? namem, MixedIntegerExprTerm[]? param)
	{
		Name = name;
		Nameb = nameb;
		Namem = namem;
		Param = param;
	}

	public readonly AExpression Name;
	public readonly AExpression? Nameb;
	public readonly AExpression? Namem;
	public readonly MixedIntegerExprTerm[]? Param;
}

#endregion

#region EM_DT

internal sealed class SpDtColumnOptions : Argument
{
	public enum DTOptions
	{
		Default
	};

	public SpDtColumnOptions(AExpression dt, AExpression column, DTOptions[] opts, AExpression?[] values)
	{
		Values = values;
		Options = opts;
		DT = dt;
		Column = column;
	}

	public readonly AExpression?[] Values;
	public readonly DTOptions[] Options;
	public readonly AExpression DT;
	public readonly AExpression Column;
}

#endregion

#region EM_私家版_HTML_PRINT拡張

internal sealed class SpHtmlPrint : Argument
{
	public SpHtmlPrint(AExpression str, AExpression? opt)
	{
		Str = str;
		Opt = opt;
	}

	public readonly AExpression Str;
	public readonly AExpression? Opt;
}

#endregion

internal abstract class Argument
{
	public bool IsConst;

	// Set by the ArgumentBuilder of the instruction that owns this argument and read back
	// by the matching DoInstruction, so it is never null at a read site.
	public string ConstStr = null!;
	public long ConstInt;
}

/// <summary>
/// 一般的な引数。複数の文字列式及び数式
/// </summary>
internal sealed class ExpressionsArgument : Argument
{
	public ExpressionsArgument(Type[] types, List<AExpression?> terms)
	{
		ArgumentTypeArray = types;
		ArgumentArray = terms;
	}

	/// <summary>
	/// 引数の型(ArgumentArrayよりもLengthが大きい可能性があるので見るのはArgumentArrayにすること)
	/// </summary>
	public readonly Type[] ArgumentTypeArray;

	public readonly List<AExpression?> ArgumentArray;
}

internal sealed class VoidArgument : Argument;

internal sealed class ExpressionArgument : Argument
{
	public ExpressionArgument(AExpression? termSrc)
	{
		Term = termSrc;
	}

	// Null only for the EXPRESSION_NULLABLE instructions, whose builder stores null for an omitted expression:
	// the STR_/FORM_STR_ builders, nullable variants included, substitute an empty SingleStrTerm, and every other
	// builder fails the line instead of storing null. The PRINT/DRAWLINE/SELECTCASE readers assert on that split.
	public readonly AExpression? Term;
}

internal sealed class ExpressionArrayArgument : Argument
{
	public ExpressionArrayArgument(List<AExpression?> termList)
	{
		TermList = new AExpression[termList.Count];
		termList.CopyTo(TermList);
	}

	public readonly AExpression[] TermList;
}

internal sealed class SpPrintVArgument : Argument
{
	public SpPrintVArgument(List<AExpression?> list)
	{
		Terms = list;
	}

	public readonly List<AExpression?> Terms;
}

internal sealed class SpTimesArgument : Argument
{
	public SpTimesArgument(VariableTerm var, double d)
	{
		VariableDest = var;
		DoubleValue = d;
	}

	public readonly VariableTerm VariableDest;
	public readonly double DoubleValue;
}

internal sealed class SpBarArgument : Argument
{
	public SpBarArgument(AExpression value, AExpression max, AExpression length)
	{
		Terms[0] = value;
		Terms[1] = max;
		Terms[2] = length;
	}

	public readonly AExpression[] Terms = new AExpression[3];
}

internal sealed class SpSwapCharaArgument : Argument
{
	public SpSwapCharaArgument(AExpression x, AExpression y)
	{
		X = x;
		Y = y;
	}

	public readonly AExpression X;
	public readonly AExpression Y;
}

internal sealed class SpSwapVarArgument : Argument
{
	public SpSwapVarArgument(VariableTerm v1, VariableTerm v2)
	{
		var1 = v1;
		var2 = v2;
	}

	public readonly VariableTerm var1;
	public readonly VariableTerm var2;
}

internal sealed class SpVarsizeArgument : Argument
{
	public SpVarsizeArgument(VariableToken var)
	{
		VariableID = var;
	}

	public readonly VariableToken VariableID;
}

internal sealed class SpSaveDataArgument : Argument
{
	public SpSaveDataArgument(AExpression target, AExpression var)
	{
		Target = target;
		StrExpression = var;
	}

	public readonly AExpression Target;
	public readonly AExpression StrExpression;
}

internal sealed class SpTInputsArgument : Argument
{
	#region EM_私家版_INPUT系機能拡張

	//public SpTInputsArgument(AExpression time, AExpression def, AExpression disp, AExpression timeout)
	public SpTInputsArgument(AExpression? time, AExpression? def, AExpression? disp, AExpression? timeout, AExpression? mouse, AExpression? canskip)

		#endregion

	{
		Time = time;
		Def = def;
		Disp = disp;
		Timeout = timeout;

		#region EM_私家版_INPUT系機能拡張

		Mouse = mouse;

		#endregion

		#region EE_INPUT機能拡張

		CanSkip = canskip;

		#endregion
	}

	public readonly AExpression? Time;
	public readonly AExpression? Def;
	public readonly AExpression? Disp;
	public readonly AExpression? Timeout;

	#region EM_私家版_INPUT系機能拡張

	public readonly AExpression? Mouse;

	#endregion

	#region EE_INPUT機能拡張

	public readonly AExpression? CanSkip;

	#endregion
}

//難読化用属性。enum.ToString()やenum.Parse()を行うなら(Exclude=true)にすること。
//[global::System.Reflection.Obfuscation(Exclude = false)]
internal enum SortOrder
{
	UNDEF = 0,
	ASCENDING = 1,
	DESENDING = 2
}

internal sealed class SpSortcharaArgument : Argument
{
	public SpSortcharaArgument(VariableTerm var, SortOrder order)
	{
		SortKey = var;
		SortOrder = order;
	}

	public readonly VariableTerm SortKey;
	public readonly SortOrder SortOrder;
}

internal sealed class SpCallFArgment : Argument
{
	public SpCallFArgment(AExpression funcname, List<AExpression?> subNames, List<AExpression?> args)
	{
		FuncnameTerm = funcname;
		SubNames = subNames;
		RowArgs = args;
	}

	public readonly AExpression FuncnameTerm;
	public readonly List<AExpression?> SubNames;
	public readonly List<AExpression?> RowArgs;

	public AExpression? FuncTerm;

	// Lazily created per-callsite cache for dynamic CALLFORMF name resolution
	public CallformCache? Cache;
}

internal sealed class SpCallArgment : Argument
{
	public SpCallArgment(AExpression funcname, List<AExpression?> subNames, List<AExpression?> args)
	{
		FuncnameTerm = funcname;
		SubNames = subNames;
		RowArgs = args;
	}

	public readonly AExpression FuncnameTerm;
	public readonly List<AExpression?> SubNames;
	public readonly List<AExpression?> RowArgs;
	public UserDefinedFunctionArgument? UDFArgument;

	public CalledFunction? CallFunc;

	// Lazily created per-callsite cache for dynamic CALLFORM/GOTOFORM name resolution
	public CallformCache? Cache;
}

internal sealed class SpCallSharpArgment : Argument
{
	public SpCallSharpArgment(AExpression funcname, List<AExpression?> subNames, List<AExpression?> args)
	{
		FuncnameTerm = funcname;
		SubNames = subNames;
		RowArgs = args;
	}

	public readonly AExpression FuncnameTerm;
	public readonly List<AExpression?> SubNames;

	public readonly List<AExpression?> RowArgs;

	// public UserDefinedFunctionArgument UDFArgument;
	public IPluginMethod? CallFunc;
}

/// <summary>
/// Cached result of one dynamic name resolution (CALLFORM / GOTOFORM / CALLFORMF family).
/// A null target field is a negative (not-found) result; resolution exceptions are never cached.
/// Only the fields of the owning instruction family are meaningful per callsite.
/// </summary>
internal sealed class CallformEntry
{
	// Built function/label name, retained so error paths on cache hits can report it
	// without rebuilding the name in long-key mode
	public string LabelName = null!;
	public CalledFunction? Call; // CALL_Instruction
	public UserDefinedFunctionArgument? Arg; // CALL_Instruction
	public LogicalLine? JumpTo; // GOTO_Instruction
	public AExpression? FuncTerm; // CALLF_Instruction family
}

/// <summary>
/// Per-callsite cache of dynamic function/label name resolution.
/// When the name is a single int interpolation ("PREFIX_{I}") the cache is keyed by the
/// evaluated term so no name string is built at all; otherwise by the built name with the
/// same comparer as LabelDictionary. Both found and not-found results are cached;
/// exceptions are not. Entries die with the InstructionLine when ERB files are reloaded,
/// and the cache is bypassed entirely while RunERBFromMemory is active (the function
/// table may then be swapped under a running game).
/// </summary>
internal sealed class CallformCache
{
	public CallformCache(AExpression funcnameTerm)
	{
		nameTerm = funcnameTerm;
		longTerm = StrForm.SingleIntFormTerm(funcnameTerm);
		if (longTerm != null)
		{
			longDic = [];
		}
		else
		{
			strDic = new Dictionary<string, CallformEntry>(Config.Config.StrComper);
		}
	}

	private readonly AExpression nameTerm;

	private readonly AExpression? longTerm;

	// The constructor creates exactly one of the two, matching longTerm; every read and
	// write below is gated on the same condition.
	private readonly Dictionary<long, CallformEntry> longDic = null!;
	private readonly Dictionary<string, CallformEntry> strDic = null!;

	/// <summary>Probe the cache; null means miss. The out params carry the probe key for a later Store.</summary>
	public CallformEntry? Lookup(ExpressionMediator exm, out long longKey, out string? strKey)
	{
		if (longTerm != null)
		{
			longKey = longTerm.GetIntValue(exm);
			strKey = null;
			return longDic.GetValueOrDefault(longKey);
		}

		longKey = 0;
		strKey = nameTerm.GetStrValue(exm);
		return strDic.GetValueOrDefault(strKey);
	}

	public void Store(long longKey, string? strKey, CallformEntry entry)
	{
		if (longTerm != null)
		{
			longDic[longKey] = entry;
		}
		else
			// Only the string-keyed path reaches here, and its Lookup always filled a non-null key.
		{
			strDic[strKey!] = entry;
		}
	}

	/// <summary>
	/// Resolve a CALLFORM-family target through the per-callsite cache.
	/// retAddress is the return line recorded in the resulting CalledFunction
	/// (the call site itself for CALL, the list line for TRYCALLLIST).
	/// Not-found results are cached as null; resolution exceptions are not.
	/// </summary>
	public static CalledFunction? ResolveCall(
		Process process,
		ExpressionMediator exm,
		SpCallArgment spCallArg,
		LogicalLine retAddress,
		out string labelName,
		out UserDefinedFunctionArgument? arg
	)
	{
		arg = null;
		if (exm.Console.RunERBFromMemory)
		{
			// ERB hot reload: the function table may be swapped under us, do not cache
			labelName = spCallArg.FuncnameTerm.GetStrValue(exm);
			return CalledFunction.CallFunction(process, labelName, retAddress);
		}

		CallformCache cache = spCallArg.Cache ??= new CallformCache(spCallArg.FuncnameTerm);
		CallformEntry? entry = cache.Lookup(exm, out long longKey, out string? strKey);
		if (entry != null)
		{
			labelName = entry.LabelName;
			arg = entry.Arg;
			return entry.Call;
		}

		labelName = strKey ?? spCallArg.FuncnameTerm.GetStrValue(exm);
		CalledFunction? call = CalledFunction.CallFunction(process, labelName, retAddress);
		if (call != null)
		{
			arg = call.ConvertArg(spCallArg.RowArgs);
		}

		cache.Store(longKey, strKey, new CallformEntry { LabelName = labelName, Call = call, Arg = arg });
		return call;
	}

	/// <summary>Resolve a GOTOFORM-family $label through the per-callsite cache (see ResolveCall).</summary>
	public static LogicalLine? ResolveGotoForm(
		Process process,
		ProcessState state,
		ExpressionMediator exm,
		SpCallArgment spCallArg,
		out string labelName
	)
	{
		if (exm.Console.RunERBFromMemory)
		{
			labelName = spCallArg.FuncnameTerm.GetStrValue(exm);
			return state.CurrentCalled.CallLabel(process, labelName);
		}

		CallformCache cache = spCallArg.Cache ??= new CallformCache(spCallArg.FuncnameTerm);
		CallformEntry? entry = cache.Lookup(exm, out long longKey, out string? strKey);
		if (entry != null)
		{
			labelName = entry.LabelName;
			return entry.JumpTo;
		}

		labelName = strKey ?? spCallArg.FuncnameTerm.GetStrValue(exm);
		LogicalLine? jumpTo = state.CurrentCalled.CallLabel(process, labelName);
		cache.Store(longKey, strKey, new CallformEntry { LabelName = labelName, JumpTo = jumpTo });
		return jumpTo;
	}

	/// <summary>Resolve a CALLFORMF-family #FUNCTION target through the per-callsite cache (see ResolveCall).</summary>
	public static AExpression? ResolveCallF(ExpressionMediator exm, SpCallFArgment spCallArg, out string labelName)
	{
		if (exm.Console.RunERBFromMemory)
		{
			labelName = spCallArg.FuncnameTerm.GetStrValue(exm);
			return GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, labelName, spCallArg.RowArgs, true);
		}

		CallformCache cache = spCallArg.Cache ??= new CallformCache(spCallArg.FuncnameTerm);
		CallformEntry? entry = cache.Lookup(exm, out long longKey, out string? strKey);
		if (entry != null)
		{
			labelName = entry.LabelName;
			return entry.FuncTerm;
		}

		labelName = strKey ?? spCallArg.FuncnameTerm.GetStrValue(exm);
		AExpression? funcTerm = GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, labelName, spCallArg.RowArgs, true);
		cache.Store(longKey, strKey, new CallformEntry { LabelName = labelName, FuncTerm = funcTerm });
		return funcTerm;
	}
}

internal sealed class SpForNextArgment : Argument
{
	public SpForNextArgment(VariableTerm var, AExpression start, AExpression end, AExpression step)
	{
		Cnt = var;
		Start = start;
		End = end;
		Step = step;
	}

	public readonly VariableTerm Cnt;
	public readonly AExpression Start;
	public readonly AExpression End;
	public readonly AExpression Step;
}

internal sealed class SpPowerArgument : Argument
{
	public SpPowerArgument(VariableTerm var, AExpression x, AExpression y)
	{
		VariableDest = var;
		X = x;
		Y = y;
	}

	public readonly VariableTerm VariableDest;
	public readonly AExpression X;
	public readonly AExpression Y;
}

internal sealed class CaseArgument : Argument
{
	public CaseArgument(CaseExpression[] args)
	{
		CaseExps = args;
	}

	public readonly CaseExpression[] CaseExps;
}

internal sealed class PrintDataArgument : Argument
{
	public PrintDataArgument(VariableTerm? var)
	{
		Var = var;
	}

	public readonly VariableTerm? Var;
}

internal sealed class StrDataArgument : Argument
{
	public StrDataArgument(VariableTerm var)
	{
		Var = var;
	}

	public readonly VariableTerm Var;
}

internal sealed class MethodArgument : Argument
{
	public MethodArgument(AExpression method)
	{
		MethodTerm = method;
	}

	public readonly AExpression MethodTerm;
}

internal sealed class BitArgument : Argument
{
	public BitArgument(VariableTerm var, AExpression?[] termSrc)
	{
		VariableDest = var;
		Term = termSrc;
	}

	public readonly VariableTerm VariableDest;
	public readonly AExpression?[] Term;
}

internal sealed class SpVarSetArgument : Argument
{
	public SpVarSetArgument(VariableTerm var, AExpression termSrc, AExpression? start, AExpression? end)
	{
		VariableDest = var;
		Term = termSrc;
		Start = start;
		End = end;
	}

	public readonly VariableTerm VariableDest;
	public readonly AExpression Term;
	public readonly AExpression? Start;
	public readonly AExpression? End;
}

internal sealed class SpCVarSetArgument : Argument
{
	public SpCVarSetArgument(VariableTerm var, AExpression indexTerm, AExpression termSrc, AExpression? start, AExpression? end)
	{
		VariableDest = var;
		Index = indexTerm;
		Term = termSrc;
		Start = start;
		End = end;
	}

	public readonly VariableTerm VariableDest;
	public readonly AExpression Index;
	public readonly AExpression Term;
	public readonly AExpression? Start;
	public readonly AExpression? End;
}

internal sealed class SpButtonArgument : Argument
{
	public SpButtonArgument(AExpression p1, AExpression p2)
	{
		PrintStrTerm = p1;
		ButtonWord = p2;
	}

	public readonly AExpression PrintStrTerm;
	public readonly AExpression ButtonWord;
}

internal sealed class SpColorArgument : Argument
{
	public SpColorArgument(AExpression r, AExpression g, AExpression b)
	{
		R = r;
		G = g;
		B = b;
	}

	public SpColorArgument(AExpression rgb)
	{
		RGB = rgb;
	}

	// Exactly one of RGB and R/G/B is set, depending on which constructor was used.
	public readonly AExpression? R;
	public readonly AExpression? G;
	public readonly AExpression? B;
	public readonly AExpression? RGB;
}

internal sealed class SpSplitArgument : Argument
{
	public SpSplitArgument(AExpression s1, AExpression s2, VariableToken varId, VariableTerm num)
	{
		TargetStr = s1;
		Split = s2;
		Var = varId;
		Num = num;
	}

	public readonly AExpression TargetStr;
	public readonly AExpression Split;
	public readonly VariableToken Var;
	public readonly VariableTerm Num;
}

internal sealed class SpHtmlSplitArgument : Argument
{
	public SpHtmlSplitArgument(AExpression s1, VariableToken varId, VariableTerm num)
	{
		TargetStr = s1;
		Var = varId;
		Num = num;
	}

	public readonly AExpression TargetStr;
	public readonly VariableToken Var;
	public readonly VariableTerm Num;
}

internal sealed class SpGetIntArgument : Argument
{
	public SpGetIntArgument(VariableTerm var)
	{
		VarToken = var;
	}

	public readonly VariableTerm VarToken;
}

internal sealed class SpArrayControlArgument : Argument
{
	public SpArrayControlArgument(VariableTerm var, AExpression num1, AExpression num2)
	{
		VarToken = var;
		Num1 = num1;
		Num2 = num2;
	}

	public readonly VariableTerm VarToken;
	public readonly AExpression Num1;
	public readonly AExpression Num2;
}

internal sealed class SpArrayShiftArgument : Argument
{
	public SpArrayShiftArgument(VariableTerm var, AExpression num1, AExpression num2, AExpression? num3, AExpression? num4)
	{
		VarToken = var;
		Num1 = num1;
		Num2 = num2;
		Num3 = num3;
		Num4 = num4;
	}

	public readonly VariableTerm VarToken;
	public readonly AExpression Num1;
	public readonly AExpression Num2;
	public readonly AExpression? Num3;
	public readonly AExpression? Num4;
}

internal sealed class SpArraySortArgument : Argument
{
	public SpArraySortArgument(VariableTerm var, SortOrder order, AExpression? num1, AExpression? num2)
	{
		VarToken = var;
		Order = order;
		Num1 = num1;
		Num2 = num2;
	}

	public readonly VariableTerm VarToken;
	public readonly SortOrder Order;
	public readonly AExpression? Num1;
	public readonly AExpression? Num2;
}

internal sealed class SpCopyArrayArgument : Argument
{
	public SpCopyArrayArgument(AExpression str1, AExpression str2)
	{
		VarName1 = str1;
		VarName2 = str2;
	}

	public readonly AExpression VarName1;
	public readonly AExpression VarName2;
}

internal sealed class SpSaveVarArgument : Argument
{
	public SpSaveVarArgument(AExpression term, AExpression mes, VariableToken[] varTokens)
	{
		Term = term;
		SavMes = mes;
		VarTokens = varTokens;
	}

	public readonly AExpression Term;
	public readonly AExpression SavMes;
	public readonly VariableToken[] VarTokens;
}

internal sealed class RefArgument : Argument
{
	public RefArgument(UserDefinedRefMethod udrm, UserDefinedRefMethod src)
	{
		RefMethodToken = udrm;
		SrcRefMethodToken = src;
	}

	public RefArgument(UserDefinedRefMethod udrm, CalledFunction src)
	{
		RefMethodToken = udrm;
		SrcCalledFunction = src;
	}

	public RefArgument(UserDefinedRefMethod udrm, AExpression? src)
	{
		RefMethodToken = udrm;
		SrcTerm = src;
	}

	public RefArgument(ReferenceToken vt, VariableToken src)
	{
		RefVarToken = vt;
		SrcVarToken = src;
	}

	public RefArgument(ReferenceToken vt, AExpression? src)
	{
		RefVarToken = vt;
		SrcTerm = src;
	}

	// Each constructor sets one reference/destination pair; the others stay null.
	public readonly UserDefinedRefMethod? RefMethodToken;
	public readonly UserDefinedRefMethod? SrcRefMethodToken;
	public readonly CalledFunction? SrcCalledFunction;

	public readonly ReferenceToken? RefVarToken;
	public readonly VariableToken? SrcVarToken;
	public readonly AExpression? SrcTerm;
}

#region EE

internal sealed class StrDoubleArgument : Argument
{
	public StrDoubleArgument(AExpression term, double doublevalue)
	{
		Term = term;
		DoubleValue = doublevalue;
	}

	public readonly AExpression Term;
	public readonly double DoubleValue;
}

#endregion

#region set系

internal sealed class SpSetArgument : Argument
{
	public SpSetArgument(VariableTerm var, AExpression? termSrc)
	{
		VariableDest = var;
		Term = termSrc;
	}

	public readonly VariableTerm VariableDest;
	public readonly AExpression? Term;
	public bool AddConst;
}

internal sealed class SpSetArrayArgument : Argument
{
	public SpSetArrayArgument(VariableTerm var, List<AExpression?> termList, long[] constList)
	{
		VariableDest = var;
		TermList = termList;
		ConstIntList = constList;
	}

	public SpSetArrayArgument(VariableTerm var, List<AExpression?> termList, string[] constList)
	{
		VariableDest = var;
		TermList = termList;
		ConstStrList = constList;
	}

	public readonly VariableTerm VariableDest;

	public readonly List<AExpression?> TermList;

	// Exactly one of the two constant lists is set, matching the variable's type.
	public readonly long[]? ConstIntList;
	public readonly string[]? ConstStrList;
}

#endregion

#region Emuera.NET VAR命令

internal sealed class IntAsignArgument : Argument
{
	public int[]? Lengths;

	public readonly AExpression Exp;

	public IntAsignArgument(string name, int[] lengths, AExpression exp)
	{
		ConstStr = name;
		Lengths = lengths;
		Exp = exp;
	}
}

internal sealed class StrAsignArgument : Argument
{
	public int[]? Lengths;

	// An omitted initializer is stored as the empty string by the parser, so every read sees a value.
	public readonly string Value;

	public StrAsignArgument(string str, int[] lengths, string value)
	{
		ConstStr = str;
		Lengths = lengths;
		Value = value;
	}
}

#endregion

internal sealed class HTML_PRINTArgument : Argument
{
	public HTML_PRINTArgument(AExpression termSrc, AExpression lineEnd)
	{
		Term = termSrc;
		LineEnd = lineEnd;
	}

	public readonly AExpression LineEnd;
	public readonly AExpression Term;
}
