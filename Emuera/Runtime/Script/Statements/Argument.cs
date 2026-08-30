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
	public SpInputsArgument(AExpression def, AExpression mouse, AExpression canskip)
	{
		Def = def;
		Mouse = mouse;
		CanSkip = canskip;
	}
	readonly public AExpression Def;
	readonly public AExpression Mouse;
	readonly public AExpression CanSkip;
}
#endregion
#region EM_私家版_HTMLパラメータ拡張
internal sealed class MixedIntegerExprTerm
{
	public AExpression num;
	public bool isPx;
}
internal sealed class SpPrintShapeArgument : Argument
{
	public SpPrintShapeArgument(MixedIntegerExprTerm[] param)
	{
		Param = param;
	}
	readonly public MixedIntegerExprTerm[] Param;
}
internal sealed class SpPrintImgArgument : Argument
{
	public SpPrintImgArgument(AExpression name, AExpression nameb, AExpression namem, MixedIntegerExprTerm[] param)
	{
		Name = name;
		Nameb = nameb;
		Namem = namem;
		Param = param;
	}
	readonly public AExpression Name;
	readonly public AExpression Nameb;
	readonly public AExpression Namem;
	readonly public MixedIntegerExprTerm[] Param;
}
#endregion
#region EM_DT
internal sealed class SpDtColumnOptions : Argument
{
	public enum DTOptions
	{
		Default,
	};
	public SpDtColumnOptions(AExpression dt, AExpression column, DTOptions[] opts, AExpression[] values)
	{
		Values = values;
		Options = opts;
		DT = dt;
		Column = column;
	}
	readonly public AExpression[] Values;
	readonly public DTOptions[] Options;
	readonly public AExpression DT;
	readonly public AExpression Column;
}
#endregion
#region EM_私家版_HTML_PRINT拡張
internal sealed class SpHtmlPrint : Argument
{
	public SpHtmlPrint(AExpression str, AExpression opt)
	{
		Str = str;
		Opt = opt;
	}
	readonly public AExpression Str;
	readonly public AExpression Opt;
}
#endregion
internal abstract class Argument
{
	public bool IsConst;
	public string ConstStr;
	public long ConstInt;
}

/// <summary>
/// 一般的な引数。複数の文字列式及び数式
/// </summary>
internal sealed class ExpressionsArgument : Argument
{
	public ExpressionsArgument(Type[] types, List<AExpression> terms)
	{
		ArgumentTypeArray = types;
		ArgumentArray = terms;
	}
	/// <summary>
	/// 引数の型(ArgumentArrayよりもLengthが大きい可能性があるので見るのはArgumentArrayにすること)
	/// </summary>
	readonly public Type[] ArgumentTypeArray;
	readonly public List<AExpression> ArgumentArray;
}

internal sealed class VoidArgument : Argument
{
	public VoidArgument() { }
}

internal sealed class ExpressionArgument : Argument
{
	public ExpressionArgument(AExpression termSrc)
	{
		Term = termSrc;
	}
	readonly public AExpression Term;
}

internal sealed class ExpressionArrayArgument : Argument
{
	public ExpressionArrayArgument(List<AExpression> termList)
	{
		TermList = new AExpression[termList.Count];
		termList.CopyTo(TermList);
	}
	readonly public AExpression[] TermList;
}

internal sealed class SpPrintVArgument : Argument
{
	public SpPrintVArgument(List<AExpression> list)
	{
		Terms = list;
	}
	readonly public List<AExpression> Terms;
}

internal sealed class SpTimesArgument : Argument
{
	public SpTimesArgument(VariableTerm var, double d)
	{
		VariableDest = var;
		DoubleValue = d;
	}
	readonly public VariableTerm VariableDest;
	readonly public double DoubleValue;
}

internal sealed class SpBarArgument : Argument
{
	public SpBarArgument(AExpression value, AExpression max, AExpression length)
	{
		Terms[0] = value;
		Terms[1] = max;
		Terms[2] = length;
	}
	readonly public AExpression[] Terms = new AExpression[3];
}


internal sealed class SpSwapCharaArgument : Argument
{
	public SpSwapCharaArgument(AExpression x, AExpression y)
	{
		X = x;
		Y = y;
	}
	readonly public AExpression X;
	readonly public AExpression Y;
}

internal sealed class SpSwapVarArgument : Argument
{
	public SpSwapVarArgument(VariableTerm v1, VariableTerm v2)
	{
		var1 = v1;
		var2 = v2;
	}
	readonly public VariableTerm var1;
	readonly public VariableTerm var2;
}

internal sealed class SpVarsizeArgument : Argument
{
	public SpVarsizeArgument(VariableToken var)
	{
		VariableID = var;
	}
	readonly public VariableToken VariableID;
}

internal sealed class SpSaveDataArgument : Argument
{
	public SpSaveDataArgument(AExpression target, AExpression var)
	{
		Target = target;
		StrExpression = var;
	}
	readonly public AExpression Target;
	readonly public AExpression StrExpression;
}

internal sealed class SpTInputsArgument : Argument
{
	#region EM_私家版_INPUT系機能拡張
	//public SpTInputsArgument(AExpression time, AExpression def, AExpression disp, AExpression timeout)
	public SpTInputsArgument(AExpression time, AExpression def, AExpression disp, AExpression timeout, AExpression mouse, AExpression canskip)
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
	readonly public AExpression Time;
	readonly public AExpression Def;
	readonly public AExpression Disp;
	readonly public AExpression Timeout;
	#region EM_私家版_INPUT系機能拡張
	readonly public AExpression Mouse;
	#endregion
	#region EE_INPUT機能拡張
	readonly public AExpression CanSkip;
	#endregion
}

//難読化用属性。enum.ToString()やenum.Parse()を行うなら(Exclude=true)にすること。
//[global::System.Reflection.Obfuscation(Exclude = false)]
internal enum SortOrder
{
	UNDEF = 0,
	ASCENDING = 1,
	DESENDING = 2,
}

internal sealed class SpSortcharaArgument : Argument
{
	public SpSortcharaArgument(VariableTerm var, SortOrder order)
	{
		SortKey = var;
		SortOrder = order;
	}
	readonly public VariableTerm SortKey;
	readonly public SortOrder SortOrder;
}

internal sealed class SpCallFArgment : Argument
{
	public SpCallFArgment(AExpression funcname, List<AExpression> subNames, List<AExpression> args)
	{
		FuncnameTerm = funcname;
		SubNames = subNames;
		RowArgs = args;
	}
	readonly public AExpression FuncnameTerm;
	readonly public List<AExpression> SubNames;
	readonly public List<AExpression> RowArgs;
	public AExpression FuncTerm;
	// Lazily created per-callsite cache for dynamic CALLFORMF name resolution
	public CallformCache Cache;
}

internal sealed class SpCallArgment : Argument
{
	public SpCallArgment(AExpression funcname, List<AExpression> subNames, List<AExpression> args)
	{
		FuncnameTerm = funcname;
		SubNames = subNames;
		RowArgs = args;
	}
	readonly public AExpression FuncnameTerm;
	readonly public List<AExpression> SubNames;
	readonly public List<AExpression> RowArgs;
	public UserDefinedFunctionArgument UDFArgument;
	public CalledFunction CallFunc;
	// Lazily created per-callsite cache for dynamic CALLFORM/GOTOFORM name resolution
	public CallformCache Cache;
}

internal sealed class SpCallSharpArgment : Argument
{
	public SpCallSharpArgment(AExpression funcname, List<AExpression> subNames, List<AExpression> args)
	{
		FuncnameTerm = funcname;
		SubNames = subNames;
		RowArgs = args;
	}
	readonly public AExpression FuncnameTerm;
	readonly public List<AExpression> SubNames;
	readonly public List<AExpression> RowArgs;
	// public UserDefinedFunctionArgument UDFArgument;
	public IPluginMethod CallFunc;
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
	public string LabelName;
	public CalledFunction Call;                 // CALL_Instruction
	public UserDefinedFunctionArgument Arg;     // CALL_Instruction
	public LogicalLine JumpTo;                  // GOTO_Instruction
	public AExpression FuncTerm;                // CALLF_Instruction family
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
			longDic = [];
		else
			strDic = new Dictionary<string, CallformEntry>(Config.Config.StrComper);
	}

	readonly AExpression nameTerm;
	readonly AExpression longTerm;
	readonly Dictionary<long, CallformEntry> longDic;
	readonly Dictionary<string, CallformEntry> strDic;

	/// <summary>Probe the cache; null means miss. The out params carry the probe key for a later Store.</summary>
	public CallformEntry Lookup(ExpressionMediator exm, out long longKey, out string strKey)
	{
		if (longTerm != null)
		{
			longKey = longTerm.GetIntValue(exm);
			strKey = null;
			return longDic.TryGetValue(longKey, out CallformEntry entry) ? entry : null;
		}
		longKey = 0;
		strKey = nameTerm.GetStrValue(exm);
		return strDic.TryGetValue(strKey, out CallformEntry entry2) ? entry2 : null;
	}

	public void Store(long longKey, string strKey, CallformEntry entry)
	{
		if (longTerm != null)
			longDic[longKey] = entry;
		else
			strDic[strKey] = entry;
	}

	/// <summary>
	/// Resolve a CALLFORM-family target through the per-callsite cache.
	/// retAddress is the return line recorded in the resulting CalledFunction
	/// (the call site itself for CALL, the list line for TRYCALLLIST).
	/// Not-found results are cached as null; resolution exceptions are not.
	/// </summary>
	public static CalledFunction ResolveCall(Process process, ExpressionMediator exm, SpCallArgment spCallArg, LogicalLine retAddress, out string labelName, out UserDefinedFunctionArgument arg)
	{
		arg = null;
		if (exm.Console.RunERBFromMemory)
		{
			// ERB hot reload: the function table may be swapped under us, do not cache
			labelName = spCallArg.FuncnameTerm.GetStrValue(exm);
			return CalledFunction.CallFunction(process, labelName, retAddress);
		}
		CallformCache cache = spCallArg.Cache ??= new CallformCache(spCallArg.FuncnameTerm);
		CallformEntry entry = cache.Lookup(exm, out long longKey, out string strKey);
		if (entry != null)
		{
			labelName = entry.LabelName;
			arg = entry.Arg;
			return entry.Call;
		}
		labelName = strKey ?? spCallArg.FuncnameTerm.GetStrValue(exm);
		CalledFunction call = CalledFunction.CallFunction(process, labelName, retAddress);
		if (call != null)
		{
			string errMes;
			arg = call.ConvertArg(spCallArg.RowArgs, out errMes);
			if (arg == null)
				throw new CodeEE(errMes);
		}
		cache.Store(longKey, strKey, new CallformEntry { LabelName = labelName, Call = call, Arg = arg });
		return call;
	}

	/// <summary>Resolve a GOTOFORM-family $label through the per-callsite cache (see ResolveCall).</summary>
	public static LogicalLine ResolveGotoForm(Process process, ProcessState state, ExpressionMediator exm, SpCallArgment spCallArg, out string labelName)
	{
		if (exm.Console.RunERBFromMemory)
		{
			labelName = spCallArg.FuncnameTerm.GetStrValue(exm);
			return state.CurrentCalled.CallLabel(process, labelName);
		}
		CallformCache cache = spCallArg.Cache ??= new CallformCache(spCallArg.FuncnameTerm);
		CallformEntry entry = cache.Lookup(exm, out long longKey, out string strKey);
		if (entry != null)
		{
			labelName = entry.LabelName;
			return entry.JumpTo;
		}
		labelName = strKey ?? spCallArg.FuncnameTerm.GetStrValue(exm);
		LogicalLine jumpTo = state.CurrentCalled.CallLabel(process, labelName);
		cache.Store(longKey, strKey, new CallformEntry { LabelName = labelName, JumpTo = jumpTo });
		return jumpTo;
	}

	/// <summary>Resolve a CALLFORMF-family #FUNCTION target through the per-callsite cache (see ResolveCall).</summary>
	public static AExpression ResolveCallF(ExpressionMediator exm, SpCallFArgment spCallArg, out string labelName)
	{
		if (exm.Console.RunERBFromMemory)
		{
			labelName = spCallArg.FuncnameTerm.GetStrValue(exm);
			return GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, labelName, spCallArg.RowArgs, true);
		}
		CallformCache cache = spCallArg.Cache ??= new CallformCache(spCallArg.FuncnameTerm);
		CallformEntry entry = cache.Lookup(exm, out long longKey, out string strKey);
		if (entry != null)
		{
			labelName = entry.LabelName;
			return entry.FuncTerm;
		}
		labelName = strKey ?? spCallArg.FuncnameTerm.GetStrValue(exm);
		AExpression funcTerm = GlobalStatic.IdentifierDictionary.GetFunctionMethod(GlobalStatic.LabelDictionary, labelName, spCallArg.RowArgs, true);
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
	readonly public VariableTerm Cnt;
	readonly public AExpression Start;
	readonly public AExpression End;
	readonly public AExpression Step;
}

internal sealed class SpPowerArgument : Argument
{
	public SpPowerArgument(VariableTerm var, AExpression x, AExpression y)
	{
		VariableDest = var;
		X = x;
		Y = y;
	}
	readonly public VariableTerm VariableDest;
	readonly public AExpression X;
	readonly public AExpression Y;
}

internal sealed class CaseArgument : Argument
{
	public CaseArgument(CaseExpression[] args)
	{
		CaseExps = args;
	}
	readonly public CaseExpression[] CaseExps;
}

internal sealed class PrintDataArgument : Argument
{
	public PrintDataArgument(VariableTerm var)
	{
		Var = var;
	}
	readonly public VariableTerm Var;
}

internal sealed class StrDataArgument : Argument
{
	public StrDataArgument(VariableTerm var)
	{
		Var = var;
	}
	readonly public VariableTerm Var;
}

internal sealed class MethodArgument : Argument
{
	public MethodArgument(AExpression method)
	{
		MethodTerm = method;
	}
	readonly public AExpression MethodTerm;
}

internal sealed class BitArgument : Argument
{
	public BitArgument(VariableTerm var, AExpression[] termSrc)
	{
		VariableDest = var;
		Term = termSrc;
	}
	readonly public VariableTerm VariableDest;
	readonly public AExpression[] Term;
}

internal sealed class SpVarSetArgument : Argument
{
	public SpVarSetArgument(VariableTerm var, AExpression termSrc, AExpression start, AExpression end)
	{
		VariableDest = var;
		Term = termSrc;
		Start = start;
		End = end;
	}
	readonly public VariableTerm VariableDest;
	readonly public AExpression Term;
	readonly public AExpression Start;
	readonly public AExpression End;
}

internal sealed class SpCVarSetArgument : Argument
{
	public SpCVarSetArgument(VariableTerm var, AExpression indexTerm, AExpression termSrc, AExpression start, AExpression end)
	{
		VariableDest = var;
		Index = indexTerm;
		Term = termSrc;
		Start = start;
		End = end;
	}
	readonly public VariableTerm VariableDest;
	readonly public AExpression Index;
	readonly public AExpression Term;
	readonly public AExpression Start;
	readonly public AExpression End;
}

internal sealed class SpButtonArgument : Argument
{
	public SpButtonArgument(AExpression p1, AExpression p2)
	{
		PrintStrTerm = p1;
		ButtonWord = p2;
	}
	readonly public AExpression PrintStrTerm;
	readonly public AExpression ButtonWord;
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
	readonly public AExpression R;
	readonly public AExpression G;
	readonly public AExpression B;
	readonly public AExpression RGB;
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
	readonly public AExpression TargetStr;
	readonly public AExpression Split;
	readonly public VariableToken Var;
	readonly public VariableTerm Num;
}

internal sealed class SpHtmlSplitArgument : Argument
{
	public SpHtmlSplitArgument(AExpression s1, VariableToken varId, VariableTerm num)
	{
		TargetStr = s1;
		Var = varId;
		Num = num;
	}
	readonly public AExpression TargetStr;
	readonly public VariableToken Var;
	readonly public VariableTerm Num;
}

internal sealed class SpGetIntArgument : Argument
{
	public SpGetIntArgument(VariableTerm var)
	{
		VarToken = var;
	}
	readonly public VariableTerm VarToken;
}

internal sealed class SpArrayControlArgument : Argument
{
	public SpArrayControlArgument(VariableTerm var, AExpression num1, AExpression num2)
	{
		VarToken = var;
		Num1 = num1;
		Num2 = num2;
	}
	readonly public VariableTerm VarToken;
	readonly public AExpression Num1;
	readonly public AExpression Num2;
}

internal sealed class SpArrayShiftArgument : Argument
{
	public SpArrayShiftArgument(VariableTerm var, AExpression num1, AExpression num2, AExpression num3, AExpression num4)
	{
		VarToken = var;
		Num1 = num1;
		Num2 = num2;
		Num3 = num3;
		Num4 = num4;
	}
	readonly public VariableTerm VarToken;
	readonly public AExpression Num1;
	readonly public AExpression Num2;
	readonly public AExpression Num3;
	readonly public AExpression Num4;
}

internal sealed class SpArraySortArgument : Argument
{
	public SpArraySortArgument(VariableTerm var, SortOrder order, AExpression num1, AExpression num2)
	{
		VarToken = var;
		Order = order;
		Num1 = num1;
		Num2 = num2;
	}
	readonly public VariableTerm VarToken;
	readonly public SortOrder Order;
	readonly public AExpression Num1;
	readonly public AExpression Num2;
}

internal sealed class SpCopyArrayArgument : Argument
{
	public SpCopyArrayArgument(AExpression str1, AExpression str2)
	{
		VarName1 = str1;
		VarName2 = str2;
	}
	readonly public AExpression VarName1;
	readonly public AExpression VarName2;
}

internal sealed class SpSaveVarArgument : Argument
{
	public SpSaveVarArgument(AExpression term, AExpression mes, VariableToken[] varTokens)
	{
		Term = term;
		SavMes = mes;
		VarTokens = varTokens;
	}
	readonly public AExpression Term;
	readonly public AExpression SavMes;
	readonly public VariableToken[] VarTokens;
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
	public RefArgument(UserDefinedRefMethod udrm, AExpression src)
	{
		RefMethodToken = udrm;
		SrcTerm = src;
	}

	public RefArgument(ReferenceToken vt, VariableToken src)
	{
		RefVarToken = vt;
		SrcVarToken = src;
	}
	public RefArgument(ReferenceToken vt, AExpression src)
	{
		RefVarToken = vt;
		SrcTerm = src;
	}
	readonly public UserDefinedRefMethod RefMethodToken;
	readonly public UserDefinedRefMethod SrcRefMethodToken;
	readonly public CalledFunction SrcCalledFunction;

	readonly public ReferenceToken RefVarToken;
	readonly public VariableToken SrcVarToken;
	readonly public AExpression SrcTerm;
}
#region EE
internal sealed class StrDoubleArgument : Argument
{
	public StrDoubleArgument(AExpression term, double doublevalue)
	{
		Term = term;
		DoubleValue = doublevalue;
	}
	readonly public AExpression Term;
	readonly public double DoubleValue;
}
#endregion

#region set系
internal sealed class SpSetArgument : Argument
{
	public SpSetArgument(VariableTerm var, AExpression termSrc)
	{
		VariableDest = var;
		Term = termSrc;
	}
	readonly public VariableTerm VariableDest;
	readonly public AExpression Term;
	public bool AddConst;
}

internal sealed class SpSetArrayArgument : Argument
{
	public SpSetArrayArgument(VariableTerm var, List<AExpression> termList, long[] constList)
	{
		VariableDest = var;
		TermList = termList;
		ConstIntList = constList;
	}
	public SpSetArrayArgument(VariableTerm var, List<AExpression> termList, string[] constList)
	{
		VariableDest = var;
		TermList = termList;
		ConstStrList = constList;
	}
	readonly public VariableTerm VariableDest;
	readonly public List<AExpression> TermList;
	readonly public long[] ConstIntList;
	readonly public string[] ConstStrList;
}
#endregion

#region Emuera.NET VAR命令
internal sealed class IntAsignArgument : Argument
{
	public int[] Lengths;
	public IntAsignArgument(string str, int[] lengths)
	{
		ConstStr = str;
	}
	public AExpression Exp;
	public IntAsignArgument(string name, int[] lengths, AExpression exp)
	{
		ConstStr = name;
		Lengths = lengths;
		Exp = exp;
	}
}

internal sealed class StrAsignArgument : Argument
{
	public int[] Lengths;
	public StrAsignArgument(string str)
	{
		ConstStr = str;
	}
	public string Value;
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
	readonly public AExpression LineEnd;
	readonly public AExpression Term;
}
