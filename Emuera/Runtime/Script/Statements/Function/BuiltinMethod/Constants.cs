// Constant getters (定数取得).

using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Utils;
using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class MoneyStrMethod : FunctionMethod
{
	[BuiltinMethod("MONEYSTR")]
	private static MoneyStrMethod CreateMONEYSTR()
	{
		return new MoneyStrMethod();
	}

	public MoneyStrMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		// An empty slot 1 means the Config money format.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String }, OmitStart = 1 }
		];
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	//通常2つ、1つ省略可能で1～2の引数が必要。
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 2)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments[0] == null)
	//		return name + "関数の1番目の引数は省略できません";
	//	if (arguments[0].GetOperandType() != typeof(Int64))
	//		return name + "関数の1番目の引数の型が正しくありません";
	//	if ((arguments.Count >= 2) && (arguments[1] != null) && (arguments[1].GetOperandType() != typeof(string)))
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	return null;
	//}
	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long money = slots.Int(exm, 0);
		if (!slots.Has(1))
		{
			return Config.MoneyFirst ? Config.MoneyLabel + money : money + Config.MoneyLabel;
		}

		string format = slots.Str(exm, 1);
		string ret;
		try
		{
			ret = money.ToString(format);
		}
		catch (FormatException)
		{
			// throw new CodeEE("MONEYSTR関数の第2引数の書式指定が間違っています");
			throw new CodeEE(string.Format(trerror.InvalidFormat.Text, Name, 2));
		}

		return Config.MoneyFirst ? Config.MoneyLabel + ret : ret + Config.MoneyLabel;
	}
}

internal sealed class GetPrintCPerLineMethod : FunctionMethod
{
	[BuiltinMethod("PRINTCPERLINE")]
	private static GetPrintCPerLineMethod CreatePRINTCPERLINE()
	{
		return new GetPrintCPerLineMethod();
	}

	public GetPrintCPerLineMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return Config.PrintCPerLine;
	}
}

internal sealed class PrintCLengthMethod : FunctionMethod
{
	[BuiltinMethod("PRINTCLENGTH")]
	private static PrintCLengthMethod CreatePRINTCLENGTH()
	{
		return new PrintCLengthMethod();
	}

	public PrintCLengthMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return Config.PrintCLength;
	}
}

internal sealed class GetSaveNosMethod : FunctionMethod
{
	[BuiltinMethod("SAVENOS")]
	private static GetSaveNosMethod CreateSAVENOS()
	{
		return new GetSaveNosMethod();
	}

	public GetSaveNosMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return Config.SaveDataNos;
	}
}

internal sealed class GettimeMethod : FunctionMethod
{
	[BuiltinMethod("GETTIME")]
	private static GettimeMethod CreateGETTIME()
	{
		return new GettimeMethod();
	}

	public GettimeMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long date = DateTime.Now.Year;
		date = (date * 100) + DateTime.Now.Month;
		date = (date * 100) + DateTime.Now.Day;
		date = (date * 100) + DateTime.Now.Hour;
		date = (date * 100) + DateTime.Now.Minute;
		date = (date * 100) + DateTime.Now.Second;
		date = (date * 1000) + DateTime.Now.Millisecond;
		return date; //17桁。2京くらい。
	}
}

internal sealed class GettimesMethod : FunctionMethod
{
	[BuiltinMethod("GETTIMES")]
	private static GettimesMethod CreateGETTIMES()
	{
		return new GettimesMethod();
	}

	public GettimesMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
	}
}

internal sealed class GetmsMethod : FunctionMethod
{
	[BuiltinMethod("GETMILLISECOND")]
	private static GetmsMethod CreateGETMILLISECOND()
	{
		return new GetmsMethod();
	}

	public GetmsMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//西暦0001年1月1日からの経過時間をミリ秒で。
		return DateTime.Now.Ticks / 10000;
	}
}

internal sealed class GetSecondMethod : FunctionMethod
{
	[BuiltinMethod("GETSECOND")]
	private static GetSecondMethod CreateGETSECOND()
	{
		return new GetSecondMethod();
	}

	public GetSecondMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		//西暦0001年1月1日からの経過時間を秒で。
		//Ticksは100ナノ秒単位であるが実際にはそんな精度はないので無駄。
		return DateTime.Now.Ticks / 10000000;
	}
}
