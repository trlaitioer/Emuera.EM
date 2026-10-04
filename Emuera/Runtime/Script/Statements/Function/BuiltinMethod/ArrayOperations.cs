// Variable-operation builtins (変数操作系), plus the EE_ERD and ARRAYMSORT string-array additions.

using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class SumArrayMethod : FunctionMethod
{
	[BuiltinMethod("SUMARRAY")]
	private static SumArrayMethod CreateSUMARRAY()
	{
		return new SumArrayMethod();
	}

	[BuiltinMethod("SUMCARRAY")]
	private static SumArrayMethod CreateSUMCARRAY()
	{
		return new SumArrayMethod(true);
	}

	private readonly bool isCharaRange;

	public SumArrayMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 1/2 mean index1 = 0 and index2 = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.RefIntArray, ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		isCharaRange = false;
		CanRestructure = false;
	}

	public SumArrayMethod(bool isChara)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 1/2 mean index1 = 0 and index2 = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.CharacterData | ArgType.RefIntArray | ArgType.AllowConstRef, ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		isCharaRange = isChara;
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 3)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm))
	//		return name + "関数の1番目の引数が変数ではありません";
	//	VariableTerm varToken = (VariableTerm)arguments[0];
	//	if (varToken.IsString)
	//		return name + "関数の1番目の引数が数値変数ではありません";
	//	if (isCharaRange && !varToken.Identifier.IsCharacterData)
	//		return name + "関数の1番目の引数がキャラクタ変数ではありません";
	//	if (!isCharaRange && !varToken.Identifier.IsArray1D && !varToken.Identifier.IsArray2D && !varToken.Identifier.IsArray3D)
	//		return name + "関数の1番目の引数が配列変数ではありません";
	//	if (arguments.Count == 1)
	//		return null;
	//	if ((arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の変数が数値ではありません";
	//	if (arguments.Count == 2)
	//		return null;
	//	if ((arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の変数が数値ではありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm varTerm = slots.Var(0);
		long index1 = slots.Has(1) ? slots.Int(exm, 1) : 0;
		long index2 = slots.Count == 3 && slots.Has(2) ? slots.Int(exm, 2) :
			isCharaRange ? exm.VEvaluator.CHARANUM : varTerm.GetLastLength();

		FixedVariableTerm p = varTerm.GetFixedVariableTerm(exm);
		if (!isCharaRange)
		{
			p.IsArrayRangeValid(index1, index2, "SUMARRAY", 2L, 3L);
			return VariableEvaluator.GetArraySum(p, index1, index2);
		}
		else
		{
			long charaNum = exm.VEvaluator.CHARANUM;
			if (index1 >= charaNum || index1 < 0 || index2 > charaNum || index2 < 0)
				// throw new CodeEE("SUMCARRAY関数の範囲指定がキャラクタ配列の範囲を超えています(" + index1.ToString() + "～" + index2.ToString() + ")");
			{
				throw new CodeEE(string.Format(trerror.CharacterRangeInvalid.Text, Name, index1, index2));
			}

			return VariableEvaluator.GetArraySumChara(p, index1, index2);
		}
	}
}

internal sealed class MatchMethod : FunctionMethod
{
	[BuiltinMethod("MATCH")]
	private static MatchMethod CreateMATCH()
	{
		return new MatchMethod();
	}

	[BuiltinMethod("CMATCH")]
	private static MatchMethod CreateCMATCH()
	{
		return new MatchMethod(true);
	}

	private readonly bool isCharaRange;

	public MatchMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 2/3 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.RefAny1D | ArgType.AllowConstRef, ArgType.SameAsFirst, ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		isCharaRange = false;
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	public MatchMethod(bool isChara)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 2/3 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			//CMATCH targets a character array, so rule 1 must be a reference ({ CharacterData } is
			//checked before the ref branch, and RefAny1D already covers Any)
			new ArgTypeList { ArgTypes = { ArgType.CharacterData | ArgType.RefAny1D | ArgType.AllowConstRef, ArgType.SameAsFirst, ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		isCharaRange = isChara;
		CanRestructure = false;
		HasUniqueRestructure = true;
	}
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments.Count > 4)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm))
	//		return name + "関数の1番目の引数が変数ではありません";
	//	VariableTerm varToken = (VariableTerm)arguments[0];
	//	if (isCharaRange && !varToken.Identifier.IsCharacterData)
	//		return name + "関数の1番目の引数がキャラクタ変数ではありません";
	//	if (!isCharaRange && (varToken.Identifier.IsArray2D || varToken.Identifier.IsArray3D))
	//		return name + "関数は二重配列・三重配列には対応していません";
	//	if (!isCharaRange && !varToken.Identifier.IsArray1D)
	//		return name + "関数の1番目の引数が配列変数ではありません";
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != arguments[0].GetOperandType())
	//		return name + "関数の1番目の引数と2番目の引数の型が異なります";
	//	if ((arguments.Count >= 3) && (arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 4) && (arguments[3] != null) && (arguments[3].GetOperandType() != typeof(Int64)))
	//		return name + "関数の4番目の引数の型が正しくありません";
	//	return null;
	//}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm varTerm = slots.Var(0);
		long start = slots.Has(2) ? slots.Int(exm, 2) : 0;
		long end = slots.Has(3) ? slots.Int(exm, 3) :
			isCharaRange ? exm.VEvaluator.CHARANUM : varTerm.GetLength();

		FixedVariableTerm p = varTerm.GetFixedVariableTerm(exm);
		if (!isCharaRange)
		{
			p.IsArrayRangeValid(start, end, "MATCH", 3L, 4L);
			if (slots.IsInteger(0))
			{
				long targetValue = slots.Int(exm, 1);
				return VariableEvaluator.GetMatch(p, targetValue, start, end);
			}
			else
			{
				string targetStr = slots.Str(exm, 1);
				return VariableEvaluator.GetMatch(p, targetStr, start, end);
			}
		}
		else
		{
			long charaNum = exm.VEvaluator.CHARANUM;
			if (start >= charaNum || start < 0 || end > charaNum || end < 0)
				// throw new CodeEE("CMATCH関数の範囲指定がキャラクタ配列の範囲を超えています(" + start.ToString() + "～" + end.ToString() + ")");
			{
				throw new CodeEE(string.Format(trerror.CharacterRangeInvalid.Text, Name, start, end));
			}

			if (slots.IsInteger(0))
			{
				long targetValue = slots.Int(exm, 1);
				return VariableEvaluator.GetMatchChara(p, targetValue, start, end);
			}
			else
			{
				string targetStr = slots.Str(exm, 1);
				return VariableEvaluator.GetMatchChara(p, targetStr, start, end);
			}
		}
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		arguments.Required(0).Restructure(exm);
		for (int i = 1; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			arguments[i] = arguments.Required(i).Restructure(exm);
		}

		return false;
	}
}

internal sealed class GroupMatchMethod : FunctionMethod
{
	[BuiltinMethod("GROUPMATCH")]
	private static GroupMatchMethod CreateGROUPMATCH()
	{
		return new GroupMatchMethod();
	}

	public GroupMatchMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.VariadicSameAsFirst } }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	Type baseType = arguments[0].GetOperandType();
	//	for (int i = 1; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数は省略できません";
	//		if (arguments[i].GetOperandType() != baseType)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数の型が正しくありません";
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = 0;
		if (slots.IsInteger(0))
		{
			long baseValue = slots.Int(exm, 0);
			for (int i = 1; i < slots.Count; i++)
			{
				if (baseValue == slots.Int(exm, i))
				{
					ret += 1;
				}
			}
		}
		else
		{
			string baseString = slots.Str(exm, 0);
			for (int i = 1; i < slots.Count; i++)
			{
				if (baseString == slots.Str(exm, i))
				{
					ret += 1;
				}
			}
		}

		return ret;
	}
}

internal sealed class NosamesMethod : FunctionMethod
{
	[BuiltinMethod("NOSAMES")]
	private static NosamesMethod CreateNOSAMES()
	{
		return new NosamesMethod();
	}

	public NosamesMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.VariadicSameAsFirst } }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	Type baseType = arguments[0].GetOperandType();
	//	for (int i = 1; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数は省略できません";
	//		if (arguments[i].GetOperandType() != baseType)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数の型が正しくありません";
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (slots.IsInteger(0))
		{
			long[] valueArray = new long[slots.Count];
			for (int i = 0; i < slots.Count; i++)
			{
				valueArray[i] = slots.Int(exm, i);
			}

			IEnumerable<long> resultArray = valueArray.Distinct();
			if (resultArray.Count() != slots.Count)
			{
				return 0L;
			}
		}
		else
		{
			string[] stringArray = new string[slots.Count];
			for (int i = 0; i < slots.Count; i++)
			{
				stringArray[i] = slots.Str(exm, i);
			}

			IEnumerable<string> resultArray = stringArray.Distinct();
			if (resultArray.Count() != slots.Count)
			{
				return 0L;
			}
		}

		return 1L;
	}
}

internal sealed class AllsamesMethod : FunctionMethod
{
	[BuiltinMethod("ALLSAMES")]
	private static AllsamesMethod CreateALLSAMES()
	{
		return new AllsamesMethod();
	}

	public AllsamesMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.VariadicSameAsFirst } }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	Type baseType = arguments[0].GetOperandType();
	//	for (int i = 1; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数は省略できません";
	//		if (arguments[i].GetOperandType() != baseType)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数の型が正しくありません";
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (slots.IsInteger(0))
		{
			long baseValue = slots.Int(exm, 0);
			for (int i = 1; i < slots.Count; i++)
			{
				if (baseValue != slots.Int(exm, i))
				{
					return 0L;
				}
			}
		}
		else
		{
			string baseValue = slots.Str(exm, 0);
			for (int i = 1; i < slots.Count; i++)
			{
				if (baseValue != slots.Str(exm, i))
				{
					return 0L;
				}
			}
		}

		return 1L;
	}
}

internal sealed class MaxArrayMethod : FunctionMethod
{
	[BuiltinMethod("MAXARRAY")]
	private static MaxArrayMethod CreateMAXARRAY()
	{
		return new MaxArrayMethod();
	}

	[BuiltinMethod("MAXCARRAY")]
	private static MaxArrayMethod CreateMAXCARRAY()
	{
		return new MaxArrayMethod(true);
	}

	[BuiltinMethod("MINARRAY")]
	private static MaxArrayMethod CreateMINARRAY()
	{
		return new MaxArrayMethod(false, false);
	}

	[BuiltinMethod("MINCARRAY")]
	private static MaxArrayMethod CreateMINCARRAY()
	{
		return new MaxArrayMethod(true, false);
	}

	private readonly bool isCharaRange;
	private readonly bool isMax;
	private readonly string funcName;

	public MaxArrayMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 1/2 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.RefInt1D | ArgType.AllowConstRef, ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		isCharaRange = false;
		isMax = true;
		funcName = "MAXARRAY";
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	public MaxArrayMethod(bool isChara)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 1/2 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.CharacterData | ArgType.RefInt1D | ArgType.AllowConstRef, ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		isCharaRange = isChara;
		isMax = true;
		if (isCharaRange)
		{
			funcName = "MAXCARRAY";
		}
		else
		{
			funcName = "MAXARRAY";
		}

		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	public MaxArrayMethod(bool isChara, bool isMaxFunc)
	{
		ReturnType = typeof(long);
		// Empty slots 1/2 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx = isChara
			?
			[
				new ArgTypeList { ArgTypes = { ArgType.CharacterData | ArgType.RefInt1D | ArgType.AllowConstRef, ArgType.Int, ArgType.Int }, OmitStart = 1 }
			]
			:
			[
				new ArgTypeList { ArgTypes = { ArgType.RefInt1D | ArgType.AllowConstRef, ArgType.Int, ArgType.Int }, OmitStart = 1 }
			];
		isCharaRange = isChara;
		isMax = isMaxFunc;
		funcName = (isMax ? "MAX" : "MIN") + (isCharaRange ? "C" : "") + "ARRAY";
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 3)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm))
	//		return name + "関数の1番目の引数が変数ではありません";
	//	VariableTerm varToken = (VariableTerm)arguments[0];
	//	if (isCharaRange && !varToken.Identifier.IsCharacterData)
	//		return name + "関数の1番目の引数がキャラクタ変数ではありません";
	//	if (!varToken.IsInteger)
	//		return name + "関数の1番目の引数が数値変数ではありません";
	//	if (!isCharaRange && (varToken.Identifier.IsArray2D || varToken.Identifier.IsArray3D))
	//		return name + "関数は二重配列・三重配列には対応していません";
	//	if (!varToken.Identifier.IsArray1D)
	//		return name + "関数の1番目の引数が配列変数ではありません";
	//	if ((arguments.Count >= 2) && (arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 3) && (arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の引数の型が正しくありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm vTerm = slots.Var(0);
		long start = slots.Has(1) ? slots.Int(exm, 1) : 0;
		long end = slots.Has(2) ? slots.Int(exm, 2) :
			isCharaRange ? exm.VEvaluator.CHARANUM : vTerm.GetLength();
		FixedVariableTerm p = vTerm.GetFixedVariableTerm(exm);
		if (!isCharaRange)
		{
			p.IsArrayRangeValid(start, end, funcName, 2L, 3L);
			return VariableEvaluator.GetMaxArray(p, start, end, isMax);
		}
		else
		{
			long charaNum = exm.VEvaluator.CHARANUM;
			if (start >= charaNum || start < 0 || end > charaNum || end < 0)
			{
				throw new CodeEE(string.Format(trerror.CharacterRangeInvalid.Text, funcName, start, end));
			}

			return VariableEvaluator.GetMaxArrayChara(p, start, end, isMax);
		}
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		// The first argument is a variable slot, not a foldable value: declaring the call unique
		// keeps FunctionMethodTerm.Restructure from writing a folded SingleTerm back into the real
		// argument list, which would break the VariableTerm cast in GetIntValue. The list received
		// here is a copy, so the assignments below only carry the nested restructuring.
		arguments.Required(0).Restructure(exm);
		for (int i = 1; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			arguments[i] = arguments.Required(i).Restructure(exm);
		}

		return false;
	}
}

internal sealed class GetbitMethod : FunctionMethod
{
	[BuiltinMethod("GETBIT")]
	private static GetbitMethod CreateGETBIT()
	{
		return new GetbitMethod();
	}

	public GetbitMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	string ret = base.CheckArgumentType(name, arguments);
	//	if (ret != null)
	//		return ret;
	//	if (arguments[1] is SingleTerm)
	//	{
	//		Int64 m = ((SingleTerm)arguments[1]).Int;
	//		if (m < 0 || m > 63)
	//			return "GETBIT関数の第２引数(" + m.ToString() + ")が範囲(０～６３)を超えています";
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long n = slots.Int(exm, 0);
		long m = slots.Int(exm, 1);
		if (m is < 0 or > 63)
			// throw new CodeEE("GETBIT関数の第２引数(" + m.ToString() + ")が範囲(０～６３)を超えています");
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, 2, m, 0, 63));
		}

		int mi = (int)m;
		return (n >> mi) & 1;
	}
}

internal sealed class GetnumMethod : FunctionMethod
{
	[BuiltinMethod("GETNUM")]
	private static GetnumMethod CreateGETNUM()
	{
		return new GetnumMethod();
	}

	public GetnumMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.RefAny | ArgType.AllowConstRef, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = true;
		HasUniqueRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count != 2)
	//		return name + "関数には2つの引数が必要です";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm))
	//		return name + "関数の1番目の引数の型が正しくありません";
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != typeof(string))
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm vToken = slots.Var(0);
		VariableCode varCode = vToken.Identifier.Code;
		string varname = "";

		#region EE_ERD

		if (slots.Count > 2)
		{
			varname = vToken.Identifier.Name + "@" + slots.Int(exm, 2);
		}
		else
		{
			varname = vToken.Identifier.Name;
		}

		#endregion

		string key = slots.Str(exm, 1);

		#region EE_ERD

		// if (exm.VEvaluator.Constant.TryKeywordToInteger(out int ret, varCode, key, -1))
		if (exm.VEvaluator.Constant.TryKeywordToInteger(out int ret, varCode, key, -1, varname))

			#endregion

		{
			return ret;
		}
		else
		{
			return -1;
		}
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		arguments[1] = arguments.Required(1).Restructure(exm);
		return arguments[1] is SingleTerm;
	}
}

internal sealed class GetnumBMethod : FunctionMethod
{
	[BuiltinMethod("GETNUMB")]
	private static GetnumBMethod CreateGETNUMB()
	{
		return new GetnumBMethod();
	}

	public GetnumBMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(string)];
		CanRestructure = true;
	}

	/*
	public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	{
		string errStr = base.CheckArgumentType(name, arguments);
		if (errStr != null)
			return errStr;
		if (arguments[0] == null)
			return name + "関数の1番目の引数は省略できません";
		if (arguments[0] is SingleTerm)
		{
			string varName = ((SingleTerm)arguments.Required(0)).Text;
			if (GlobalStatic.IdentifierDictionary.GetVariableToken(varName, null, true) == null)
				return name + "関数の1番目の引数が変数名ではありません";
		}
		return null;
	}
	*/
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableToken? var = GlobalStatic.IdentifierDictionary.GetVariableToken(slots.Str(exm, 0), null, true);
		if (var == null)
		{
			throw new CodeEE(string.Format(trerror.NotVariableName.Text, Name, 1, slots.Str(exm, 0)));
		}

		string key = slots.Str(exm, 1);

		#region EE_ERD

		//GETNUMBは使ってないのでテストしていない
		// if (exm.VEvaluator.Constant.TryKeywordToInteger(out int ret, var.Code, key, -1))
		if (exm.VEvaluator.Constant.TryKeywordToInteger(out int ret, var.Code, key, -1, slots.Str(exm, 0)))

			#endregion

		{
			return ret;
		}
		else
		{
			return -1;
		}
	}
}

internal sealed class GetPalamLVMethod : FunctionMethod
{
	[BuiltinMethod("GETPALAMLV")]
	private static GetPalamLVMethod CreateGETPALAMLV()
	{
		return new GetPalamLVMethod();
	}

	public GetPalamLVMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	string errStr = base.CheckArgumentType(name, arguments);
	//	if (errStr != null)
	//		return errStr;
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long value = slots.Int(exm, 0);
		long maxLv = slots.Int(exm, 1);

		return exm.VEvaluator.getPalamLv(value, maxLv);
	}
}

internal sealed class GetExpLVMethod : FunctionMethod
{
	[BuiltinMethod("GETEXPLV")]
	private static GetExpLVMethod CreateGETEXPLV()
	{
		return new GetExpLVMethod();
	}

	public GetExpLVMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	string errStr = base.CheckArgumentType(name, arguments);
	//	if (errStr != null)
	//		return errStr;
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long value = slots.Int(exm, 0);
		long maxLv = slots.Int(exm, 1);

		return exm.VEvaluator.getExpLv(value, maxLv);
	}
}

internal sealed class FindElementMethod : FunctionMethod
{
	[BuiltinMethod("FINDELEMENT")]
	private static FindElementMethod CreateFINDELEMENT()
	{
		return new FindElementMethod(false);
	}

	[BuiltinMethod("FINDLASTELEMENT")]
	private static FindElementMethod CreateFINDLASTELEMENT()
	{
		return new FindElementMethod(true);
	}

	public FindElementMethod(bool last)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 2/3/4 mean start = 0, end = the array length and isExact = false.
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.RefAny1D | ArgType.AllowConstRef,
					ArgType.SameAsFirst,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int
				},
				OmitStart = 2
			}
		];
		CanRestructure = true; //すべて定数項ならできるはず
		HasUniqueRestructure = true;
		isLast = last;
		funcName = isLast ? "FINDLASTELEMENT" : "FINDELEMENT";
	}

	private readonly bool isLast;

	private readonly string funcName;
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments.Count > 5)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm varToken))
	//		return name + "関数の1番目の引数が変数ではありません";
	//	if (varToken.Identifier.IsArray2D || varToken.Identifier.IsArray3D)
	//		return name + "関数は二重配列・三重配列には対応していません";
	//	if (!varToken.Identifier.IsArray1D)
	//		return name + "関数の1番目の引数が配列変数ではありません";
	//	Type baseType = arguments[0].GetOperandType();
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != baseType)
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 3) && (arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 4) && (arguments[3] != null) && (arguments[3].GetOperandType() != typeof(Int64)))
	//		return name + "関数の4番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 5) && (arguments[4] != null) && (arguments[4].GetOperandType() != typeof(Int64)))
	//		return name + "関数の5番目の引数の型が正しくありません";
	//	return null;
	//}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		bool isExact = false;
		VariableTerm varTerm = slots.Var(0);

		long start = slots.Has(2) ? slots.Int(exm, 2) : 0;
		long end = slots.Has(3) ? slots.Int(exm, 3) : varTerm.GetLength();
		if (slots.Has(4))
		{
			isExact = slots.Int(exm, 4) != 0;
		}

		FixedVariableTerm p = varTerm.GetFixedVariableTerm(exm);
		p.IsArrayRangeValid(start, end, funcName, 3L, 4L);

		if (slots.IsInteger(0))
		{
			long targetValue = slots.Int(exm, 1);
			return VariableEvaluator.FindElement(p, targetValue, start, end, isExact, isLast);
		}
		else
		{
			Regex targetString;
			try
			{
				targetString = RegexFactory.GetRegex(slots.Str(exm, 1));
			}
			catch (ArgumentException e)
			{
				// throw new CodeEE("第2引数が正規表現として不正です");
				throw new CodeEE(string.Format(trerror.InvalidRegexArg.Text, Name, 2, e.Message));
			}

			return VariableEvaluator.FindElement(p, targetString, start, end, isExact, isLast);
		}
	}


	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		arguments.Required(0).Restructure(exm);
		VariableTerm varToken = (arguments[0] as VariableTerm)!;
		bool isConst = varToken.Identifier.IsConst;
		for (int i = 1; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			arguments[i] = arguments.Required(i).Restructure(exm);
			if (isConst && !(arguments[i] is SingleTerm))
			{
				isConst = false;
			}
		}

		return isConst;
	}
}

internal sealed class InRangeMethod : FunctionMethod
{
	[BuiltinMethod("INRANGE")]
	private static InRangeMethod CreateINRANGE()
	{
		return new InRangeMethod();
	}

	public InRangeMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long value = slots.Int(exm, 0);
		long min = slots.Int(exm, 1);
		long max = slots.Int(exm, 2);
		return value >= min && value <= max ? 1L : 0L;
	}
}

internal sealed class InRangeArrayMethod : FunctionMethod
{
	[BuiltinMethod("INRANGEARRAY")]
	private static InRangeArrayMethod CreateINRANGEARRAY()
	{
		return new InRangeArrayMethod();
	}

	[BuiltinMethod("INRANGECARRAY")]
	private static InRangeArrayMethod CreateINRANGECARRAY()
	{
		return new InRangeArrayMethod(true);
	}

	public InRangeArrayMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 3/4 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.RefInt1D | ArgType.AllowConstRef,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int
				},
				OmitStart = 3
			}
		];
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	public InRangeArrayMethod(bool isChara)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 3/4 mean start = 0 and end = the array length (or CHARANUM).
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.CharacterData | ArgType.RefInt1D | ArgType.AllowConstRef,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int,
					ArgType.Int
				},
				OmitStart = 3
			}
		];
		isCharaRange = isChara;
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	private readonly bool isCharaRange;

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments.Count > 6)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm))
	//		return name + "関数の1番目の引数が変数ではありません";
	//	VariableTerm varToken = (VariableTerm)arguments[0];
	//	if (isCharaRange && !varToken.Identifier.IsCharacterData)
	//		return name + "関数の1番目の引数がキャラクタ変数ではありません";
	//	if (!isCharaRange && (varToken.Identifier.IsArray2D || varToken.Identifier.IsArray3D))
	//		return name + "関数は二重配列・三重配列には対応していません";
	//	if (!isCharaRange && !varToken.Identifier.IsArray1D)
	//		return name + "関数の1番目の引数が配列変数ではありません";
	//	if (!varToken.IsInteger)
	//		return name + "関数の1番目の引数が数値型変数ではありません";
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != typeof(Int64))
	//		return name + "関数の2番目の引数が数値型ではありません";
	//	if (arguments[2] == null)
	//		return name + "関数の3番目の引数は省略できません";
	//	if (arguments[2].GetOperandType() != typeof(Int64))
	//		return name + "関数の3番目の引数が数値型ではありません";
	//	if ((arguments.Count >= 4) && (arguments[3] != null) && (arguments[3].GetOperandType() != typeof(Int64)))
	//		return name + "関数の4番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 5) && (arguments[4] != null) && (arguments[4].GetOperandType() != typeof(Int64)))
	//		return name + "関数の5番目の引数の型が正しくありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long min = slots.Int(exm, 1);
		long max = slots.Int(exm, 2);

		VariableTerm varTerm = slots.Var(0);
		long start = slots.Has(3) ? slots.Int(exm, 3) : 0;
		long end = slots.Has(4) ? slots.Int(exm, 4) :
			isCharaRange ? exm.VEvaluator.CHARANUM : varTerm.GetLength();

		FixedVariableTerm p = varTerm.GetFixedVariableTerm(exm);

		if (!isCharaRange)
		{
			p.IsArrayRangeValid(start, end, "INRANGEARRAY", 4L, 5L);
			return VariableEvaluator.GetInRangeArray(p, min, max, start, end);
		}
		else
		{
			long charaNum = exm.VEvaluator.CHARANUM;
			if (start >= charaNum || start < 0 || end > charaNum || end < 0)
				// throw new CodeEE("INRANGECARRAY関数の範囲指定がキャラクタ配列の範囲を超えています(" + start.ToString() + "～" + end.ToString() + ")");
			{
				throw new CodeEE(string.Format(trerror.CharacterRangeInvalid.Text, Name, start, end));
			}

			return VariableEvaluator.GetInRangeArrayChara(p, min, max, start, end);
		}
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		// The first argument is a variable slot, not a foldable value: declaring the call unique
		// keeps FunctionMethodTerm.Restructure from writing a folded SingleTerm back into the real
		// argument list, which would break the VariableTerm read in GetIntValue. The list received
		// here is a copy, so the assignments below only carry the nested restructuring.
		arguments.Required(0).Restructure(exm);
		for (int i = 1; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			arguments[i] = arguments.Required(i).Restructure(exm);
		}

		return false;
	}
}

internal sealed class ArrayMultiSortMethod : FunctionMethod
{
	[BuiltinMethod("ARRAYMSORT")]
	private static ArrayMultiSortMethod CreateARRAYMSORT()
	{
		return new ArrayMultiSortMethod();
	}

	public ArrayMultiSortMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// GetIntValue consumes the whole list (arguments[0] plus a foreach over every argument) and has no
		// default for an empty slot, so the variadic tail carries ArgType.NotNull.
		// Slot 0 needs no flag - i < OmitStart already rejects its empty form.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.RefAny1D, ArgType.RefAnyArray | ArgType.Variadic | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
		HasUniqueRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return string.Format("{0}関数:少なくとも{1}の引数が必要です", name, 2);
	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return string.Format("{0}関数:{1}番目の引数は省略できません", name, i + 1);
	//		if (!(arguments[i] is VariableTerm varTerm) || varTerm.Identifier.IsCalc || varTerm.Identifier.IsConst)
	//			return string.Format("{0}関数:{1}番目の引数が変数ではありません", name, i + 1);
	//		if (varTerm.Identifier.IsCharacterData)
	//			return string.Format("{0}関数:{1}番目の引数がキャラクタ変数です", name, i + 1);
	//		if (i == 0 && !varTerm.Identifier.IsArray1D)
	//			return string.Format("{0}関数:{1}番目の引数が一次元配列ではありません", name, i + 1);
	//		#region EM_私家版_ARRAYMSORT_三次元配列修正
	//		//if (!varTerm.Identifier.IsArray1D && !varTerm.Identifier.IsArray2D && !varTerm.Identifier.IsArray2D)
	//		if (!varTerm.Identifier.IsArray1D && !varTerm.Identifier.IsArray2D && !varTerm.Identifier.IsArray3D)
	//			return string.Format("{0}関数:{1}番目の引数が配列変数ではありません", name, i + 1);
	//		#endregion
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm varTerm = slots.Var(0);
		int[] sortedArray;
		if (varTerm.Identifier.IsInteger)
		{
			List<KeyValuePair<long, int>> sortList = [];
			long[] array = (long[])varTerm.Identifier.GetArray();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] == 0)
				{
					break;
				}

				if (array[i] < long.MinValue || array[i] > long.MaxValue)
				{
					return 0;
				}

				sortList.Add(new KeyValuePair<long, int>(array[i], i));
			}

			//素ではintの範囲しか扱えないので一工夫
			sortList.Sort((a, b) => Math.Sign(a.Key - b.Key));
			sortedArray = new int[sortList.Count];
			for (int i = 0; i < sortedArray.Length; i++)
			{
				sortedArray[i] = sortList[i].Value;
			}
		}
		else
		{
			List<KeyValuePair<string, int>> sortList = [];
			string[] array = (string[])varTerm.Identifier.GetArray();
			for (int i = 0; i < array.Length; i++)
			{
				if (string.IsNullOrEmpty(array[i]))

					#region EM_私家版_ARRAYMSORT_文字列配列処理修正

					//return 0;
				{
					break;
				}

				#endregion

				sortList.Add(new KeyValuePair<string, int>(array[i], i));
			}

			sortList.Sort((a, b) => a.Key.CompareTo(b.Key));
			sortedArray = new int[sortList.Count];
			for (int i = 0; i < sortedArray.Length; i++)
			{
				sortedArray[i] = sortList[i].Value;
			}
		}

		for (int slot = 0; slot < slots.Count; slot++)
		{
			VariableTerm term = slots.Var(slot);
			if (term.Identifier.IsArray1D)
			{
				if (term.IsInteger)
				{
					long[] array = (long[])term.Identifier.GetArray();
					long[] clone = (long[])array.Clone();
					if (array.Length < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					{
						array[i] = clone[sortedArray[i]];
					}
				}
				else
				{
					string[] array = (string[])term.Identifier.GetArray();
					string[] clone = (string[])array.Clone();
					if (array.Length < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					{
						array[i] = clone[sortedArray[i]];
					}
				}
			}
			else if (term.Identifier.IsArray2D)
			{
				if (term.IsInteger)
				{
					long[,] array = (long[,])term.Identifier.GetArray();
					long[,] clone = (long[,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					{
						array[i, x] = clone[sortedArray[i], x];
					}
				}
				else
				{
					string[,] array = (string[,])term.Identifier.GetArray();
					string[,] clone = (string[,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					{
						array[i, x] = clone[sortedArray[i], x];
					}
				}
			}
			else if (term.Identifier.IsArray3D)
			{
				if (term.IsInteger)
				{
					long[,,] array = (long[,,])term.Identifier.GetArray();
					long[,,] clone = (long[,,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					for (int y = 0; y < array.GetLength(2); y++)
					{
						array[i, x, y] = clone[sortedArray[i], x, y];
					}
				}
				else
				{
					string[,,] array = (string[,,])term.Identifier.GetArray();
					string[,,] clone = (string[,,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					for (int y = 0; y < array.GetLength(2); y++)
					{
						array[i, x, y] = clone[sortedArray[i], x, y];
					}
				}
			}
			// else { throw new ExeEE("異常な配列"); }
			else
			{
				throw new ExeEE(trerror.AbnormalArray.Text);
			}
		}

		return 1;
	}

	public override bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		for (int i = 0; i < arguments.Count; i++)
		{
			arguments[i] = arguments.Required(i).Restructure(exm);
		}

		return false;
	}
}
