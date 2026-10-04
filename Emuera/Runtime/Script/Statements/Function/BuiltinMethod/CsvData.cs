// CSV character-data builtins (CSVデータ関係).

using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class GetcharaMethod : FunctionMethod
{
	[BuiltinMethod("GETCHARA")]
	private static GetcharaMethod CreateGETCHARA()
	{
		return new GetcharaMethod();
	}

	public GetcharaMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 1 means CheckSp = false.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	//通常２つ、１つ省略可能で１～２の引数が必要。
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 2)
	//		return name + "関数の引数が多すぎます";

	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (arguments[0].GetOperandType() != typeof(Int64))
	//		return name + "関数の1番目の引数の型が正しくありません";
	//	//2は省略可能
	//	if ((arguments.Count == 2) && (arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long integer = slots.Int(exm, 0);
		if (!Config.CompatiSPChara)
		{
			//if ((arguments.Count > 1) && (arguments[1] != null) && (arguments[1].GetIntValue(exm) != 0))
			return exm.VEvaluator.GetChara(integer);
		}

		//以下互換性用の旧処理
		bool CheckSp = false;
		if (slots.Has(1) && slots.Int(exm, 1) != 0)
		{
			CheckSp = true;
		}

		if (CheckSp)
		{
			long chara = exm.VEvaluator.GetChara_UseSp(integer, false);
			if (chara != -1)
			{
				return chara;
			}
			else
			{
				return exm.VEvaluator.GetChara_UseSp(integer, true);
			}
		}
		else
		{
			return exm.VEvaluator.GetChara_UseSp(integer, false);
		}
	}
}

internal sealed class GetspcharaMethod : FunctionMethod
{
	[BuiltinMethod("GETSPCHARA")]
	private static GetspcharaMethod CreateGETSPCHARA()
	{
		return new GetspcharaMethod();
	}

	public GetspcharaMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (!Config.CompatiSPChara)
			// throw new CodeEE("SPキャラ関係の機能は標準では使用できません(互換性オプション「SPキャラを使用する」をONにしてください)");
		{
			throw new CodeEE(trerror.SPCharacterFeatureDisabled.Text);
		}

		long integer = slots.Int(exm, 0);
		return exm.VEvaluator.GetChara_UseSp(integer, true);
	}
}

internal sealed class CsvStrDataMethod : FunctionMethod
{
	[BuiltinMethod("CSVNAME")]
	private static CsvStrDataMethod CreateCSVNAME()
	{
		return new CsvStrDataMethod(CharacterStrData.NAME);
	}

	[BuiltinMethod("CSVCALLNAME")]
	private static CsvStrDataMethod CreateCSVCALLNAME()
	{
		return new CsvStrDataMethod(CharacterStrData.CALLNAME);
	}

	[BuiltinMethod("CSVNICKNAME")]
	private static CsvStrDataMethod CreateCSVNICKNAME()
	{
		return new CsvStrDataMethod(CharacterStrData.NICKNAME);
	}

	[BuiltinMethod("CSVMASTERNAME")]
	private static CsvStrDataMethod CreateCSVMASTERNAME()
	{
		return new CsvStrDataMethod(CharacterStrData.MASTERNAME);
	}

	private readonly CharacterStrData charaStr;

	public CsvStrDataMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = null;
		charaStr = CharacterStrData.NAME;
		// An empty slot 1 means "not an SP character" (y = 0).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		CanRestructure = true;
	}

	public CsvStrDataMethod(CharacterStrData cStr)
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		// An empty slot 1 means "not an SP character" (y = 0).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		charaStr = cStr;
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 2)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!arguments[0].IsInteger)
	//		return name + "関数の1番目の引数が数値ではありません";
	//	if (arguments.Count == 1)
	//		return null;
	//	if ((arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の変数が数値ではありません";
	//	return null;
	//}
	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long x = slots.Int(exm, 0);
		long y = slots.Has(1) ? slots.Int(exm, 1) : 0;
		if (!Config.CompatiSPChara && y != 0)
			// throw new CodeEE("SPキャラ関係の機能は標準では使用できません(互換性オプション「SPキャラを使用する」をONにしてください)");
		{
			throw new CodeEE(trerror.SPCharacterFeatureDisabled.Text);
		}

		return exm.VEvaluator.GetCharacterStrfromCSVData(x, charaStr, y != 0, 0);
	}
}

internal sealed class CsvcstrMethod : FunctionMethod
{
	[BuiltinMethod("CSVCSTR")]
	private static CsvcstrMethod CreateCSVCSTR()
	{
		return new CsvcstrMethod();
	}

	public CsvcstrMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		// An empty slot 2 means "not an SP character" (z = 0).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments.Count > 3)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!arguments[0].IsInteger)
	//		return name + "関数の1番目の引数が数値ではありません";
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != typeof(Int64))
	//		return name + "関数の2番目の変数が数値ではありません";
	//	if (arguments.Count == 2)
	//		return null;
	//	if ((arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の変数が数値ではありません";
	//	return null;
	//}
	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long x = slots.Int(exm, 0);
		long y = slots.Int(exm, 1);
		long z = slots.Count == 3 && slots.Has(2) ? slots.Int(exm, 2) : 0;
		if (!Config.CompatiSPChara && z != 0)
			// throw new CodeEE("SPキャラ関係の機能は標準では使用できません(互換性オプション「SPキャラを使用する」をONにしてください)");
		{
			throw new CodeEE(trerror.SPCharacterFeatureDisabled.Text);
		}

		return exm.VEvaluator.GetCharacterStrfromCSVData(x, CharacterStrData.CSTR, z != 0, y);
	}
}

internal sealed class CsvDataMethod : FunctionMethod
{
	[BuiltinMethod("CSVBASE")]
	private static CsvDataMethod CreateCSVBASE()
	{
		return new CsvDataMethod(CharacterIntData.BASE);
	}

	[BuiltinMethod("CSVABL")]
	private static CsvDataMethod CreateCSVABL()
	{
		return new CsvDataMethod(CharacterIntData.ABL);
	}

	[BuiltinMethod("CSVMARK")]
	private static CsvDataMethod CreateCSVMARK()
	{
		return new CsvDataMethod(CharacterIntData.MARK);
	}

	[BuiltinMethod("CSVEXP")]
	private static CsvDataMethod CreateCSVEXP()
	{
		return new CsvDataMethod(CharacterIntData.EXP);
	}

	[BuiltinMethod("CSVRELATION")]
	private static CsvDataMethod CreateCSVRELATION()
	{
		return new CsvDataMethod(CharacterIntData.RELATION);
	}

	[BuiltinMethod("CSVTALENT")]
	private static CsvDataMethod CreateCSVTALENT()
	{
		return new CsvDataMethod(CharacterIntData.TALENT);
	}

	[BuiltinMethod("CSVCFLAG")]
	private static CsvDataMethod CreateCSVCFLAG()
	{
		return new CsvDataMethod(CharacterIntData.CFLAG);
	}

	[BuiltinMethod("CSVEQUIP")]
	private static CsvDataMethod CreateCSVEQUIP()
	{
		return new CsvDataMethod(CharacterIntData.EQUIP);
	}

	[BuiltinMethod("CSVJUEL")]
	private static CsvDataMethod CreateCSVJUEL()
	{
		return new CsvDataMethod(CharacterIntData.JUEL);
	}

	private readonly CharacterIntData charaInt;

	public CsvDataMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 2 means "not an SP character" (z = 0): the body reads it only via the list pattern.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		charaInt = CharacterIntData.BASE;
		CanRestructure = true;
	}

	public CsvDataMethod(CharacterIntData cInt)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 2 means "not an SP character" (z = 0): the body reads it only via the list pattern.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		charaInt = cInt;
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments.Count > 3)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!arguments[0].IsInteger)
	//		return name + "関数の1番目の引数が数値ではありません";
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != typeof(Int64))
	//		return name + "関数の2番目の変数が数値ではありません";
	//	if (arguments.Count == 2)
	//		return null;
	//	if ((arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の変数が数値ではありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long x = slots.Int(exm, 0);
		long y = slots.Int(exm, 1);
		long z = slots.Count == 3 && slots.Has(2) ? slots.Int(exm, 2) : 0;
		if (!Config.CompatiSPChara && z != 0)
			// throw new CodeEE("SPキャラ関係の機能は標準では使用できません(互換性オプション「SPキャラを使用する」をONにしてください)");
		{
			throw new CodeEE(trerror.SPCharacterFeatureDisabled.Text);
		}

		return exm.VEvaluator.GetCharacterIntfromCSVData(x, charaInt, z != 0, y);
	}
}

internal sealed class FindcharaMethod : FunctionMethod
{
	[BuiltinMethod("FINDCHARA")]
	private static FindcharaMethod CreateFINDCHARA()
	{
		return new FindcharaMethod(false);
	}

	[BuiltinMethod("FINDLASTCHARA")]
	private static FindcharaMethod CreateFINDLASTCHARA()
	{
		return new FindcharaMethod(true);
	}

	public FindcharaMethod(bool last)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// Empty slots 2/3 mean startindex = 0 and lastindex = CHARANUM.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.CharacterData | ArgType.Any, ArgType.SameAsFirst, ArgType.Int, ArgType.Int }, OmitStart = 2 }
		];
		CanRestructure = false;
		isLast = last;
	}

	private readonly bool isLast;
	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	//通常3つ、1つ省略可能で2～3の引数が必要。
	//	if (arguments.Count < 2)
	//		return name + "関数には少なくとも2つの引数が必要です";
	//	if (arguments.Count > 4)
	//		return name + "関数の引数が多すぎます";

	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!(arguments[0] is VariableTerm))
	//		return name + "関数の1番目の引数の型が正しくありません";
	//	if (!(((VariableTerm)arguments[0]).Identifier.IsCharacterData))
	//		return name + "関数の1番目の引数の変数がキャラクタ変数ではありません";
	//	if (arguments[1] == null)
	//		return name + "関数の2番目の引数は省略できません";
	//	if (arguments[1].GetOperandType() != arguments[0].GetOperandType())
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	//3番目は省略可能
	//	if ((arguments.Count >= 3) && (arguments[2] != null) && (arguments[2].GetOperandType() != typeof(Int64)))
	//		return name + "関数の3番目の引数の型が正しくありません";
	//	//4番目は省略可能
	//	if ((arguments.Count >= 4) && (arguments[3] != null) && (arguments[3].GetOperandType() != typeof(Int64)))
	//		return name + "関数の4番目の引数の型が正しくありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableTerm vTerm = slots.Var(0);
		VariableToken varID = vTerm.Identifier;

		long elem = 0;
		if (vTerm.Identifier.IsArray1D)
		{
			elem = vTerm.GetElementInt(1, exm);
		}
		else if (vTerm.Identifier.IsArray2D)
		{
			elem = vTerm.GetElementInt(1, exm) << 32;
			elem += vTerm.GetElementInt(2, exm);
		}

		long startindex = 0;
		long lastindex = exm.VEvaluator.CHARANUM;
		if (slots.Has(2))
		{
			startindex = slots.Int(exm, 2);
		}

		if (slots.Has(3))
		{
			lastindex = slots.Int(exm, 3);
		}

		if (startindex < 0 || startindex >= exm.VEvaluator.CHARANUM)
			// throw new CodeEE((isLast ? "" : "") + "関数の第3引数(" + startindex.ToString() + ")はキャラクタ位置の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.CharacterIndexOutOfRange.Text, Name, 3, startindex));
		}

		if (lastindex < 0 || lastindex > exm.VEvaluator.CHARANUM)
			// throw new CodeEE((isLast ? "" : "") + "関数の第4引数(" + lastindex.ToString() + ")はキャラクタ位置の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.CharacterIndexOutOfRange.Text, Name, 4, lastindex));
		}

		long ret;
		if (varID.IsString)
		{
			string word = slots.Str(exm, 1);
			ret = VariableEvaluator.FindChara(varID, elem, word, startindex, lastindex, isLast);
		}
		else
		{
			long word = slots.Int(exm, 1);
			ret = VariableEvaluator.FindChara(varID, elem, word, startindex, lastindex, isLast);
		}

		return ret;
	}
}

internal sealed class ExistCsvMethod : FunctionMethod
{
	[BuiltinMethod("EXISTCSV")]
	private static ExistCsvMethod CreateEXISTCSV()
	{
		return new ExistCsvMethod();
	}

	public ExistCsvMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// An empty slot 1 means isSp = false.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int }, OmitStart = 1 }
		];
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 2)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (!arguments[0].IsInteger)
	//		return name + "関数の1番目の引数が数値ではありません";
	//	if (arguments.Count == 1)
	//		return null;
	//	if ((arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の変数が数値ではありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long no = slots.Int(exm, 0);
		bool isSp = slots.Count == 2 && slots.Has(1) && slots.Int(exm, 1) != 0;
		if (!Config.CompatiSPChara && isSp)
			// throw new CodeEE("SPキャラ関係の機能は標準では使用できません(互換性オプション「SPキャラを使用する」をONにしてください)");
		{
			throw new CodeEE(trerror.SPCharacterFeatureDisabled.Text);
		}

		return exm.VEvaluator.ExistCsv(no, isSp);
	}
}
