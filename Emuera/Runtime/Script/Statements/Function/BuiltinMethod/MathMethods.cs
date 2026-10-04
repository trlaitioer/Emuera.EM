// Math builtins (数学関数).

using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Utils;
using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class RandMethod : FunctionMethod
{
	public RandMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	//通常2つ、1つ省略可能で1～2の引数が必要。
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	if (arguments.Count > 2)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments.Count == 1)
	//	{
	//		if (arguments[0] == null)
	//			return name + "関数には少なくとも1つの引数が必要です";
	//		if ((arguments[0].GetOperandType() != typeof(Int64)))
	//			return name + "関数の1番目の引数の型が正しくありません";
	//		return null;
	//	}
	//	//1番目は省略可能
	//	if ((arguments[0] != null) && (arguments[0].GetOperandType() != typeof(Int64)))
	//		return name + "関数の1番目の引数の型が正しくありません";
	//	if ((arguments[1] != null) && (arguments[1].GetOperandType() != typeof(Int64)))
	//		return name + "関数の2番目の引数の型が正しくありません";
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long min = 0;
		long max;
		if (slots.Count == 1)
		{
			max = slots.Int(exm, 0);
		}
		else
		{
			if (slots.Has(0))
			{
				min = slots.Int(exm, 0);
			}

			max = slots.Int(exm, 1);
		}

		if (max <= min)
		{
			if (min == 0)
				// throw new CodeEE("RANDの最大値に0以下の値(" + max.ToString() + ")が指定されました");
			{
				throw new CodeEE(string.Format(trerror.NegativeMaximum.Text, Name, max));
			}
			else
				// throw new CodeEE("RANDの最大値に最小値以下の値(" + max.ToString() + ")が指定されました");
			{
				throw new CodeEE(string.Format(trerror.MaximumLowerThanMinimum.Text, Name, max));
			}
		}

		return exm.VEvaluator.GetNextRand(max - min) + min;
	}
}

internal sealed class MaxMethod : FunctionMethod
{
	private readonly bool isMax;

	public MaxMethod()
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// The variadic tail is a consumed one: GetIntValue reads every slot in a loop and has no default
		// for an empty one, so the tail carries ArgType.NotNull (omitting trailing values by count stays
		// legal). Slot 0 needs no flag - i < OmitStart already rejects its empty form.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.VariadicInt | ArgType.NotNull }, OmitStart = 1 }
		];
		isMax = true;
		CanRestructure = true;
	}

	public MaxMethod(bool max)
	{
		ReturnType = typeof(long);
		// argumentTypeArray = null;
		// The variadic tail is a consumed one: GetIntValue reads every slot in a loop and has no default
		// for an empty one, so the tail carries ArgType.NotNull (omitting trailing values by count stays
		// legal). Slot 0 needs no flag - i < OmitStart already rejects its empty form.
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.VariadicInt | ArgType.NotNull }, OmitStart = 1 }
		];
		isMax = max;
		CanRestructure = true;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	if (arguments.Count < 1)
	//		return name + "関数には少なくとも1つの引数が必要です";
	//	for (int i = 0; i < arguments.Count; i++)
	//	{
	//		if (arguments[i] == null)
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数は省略できません";
	//		if (arguments[i].GetOperandType() != typeof(Int64))
	//			return name + "関数の" + (i + 1).ToString() + "番目の引数の型が正しくありません";
	//	}
	//	return null;
	//}
	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		// The variadic tail is a consumed one: the rule carries ArgType.NotNull, so every slot is filled.
		long ret = slots.Int(exm, 0);

		for (int i = 1; i < slots.Count; i++)
		{
			long newRet = slots.Int(exm, i);
			if (isMax)
			{
				if (ret < newRet)
				{
					ret = newRet;
				}
			}
			else
			{
				if (ret > newRet)
				{
					ret = newRet;
				}
			}
		}

		return ret;
	}
}

internal sealed class AbsMethod : FunctionMethod
{
	public AbsMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = slots.Int(exm, 0);
		//普通は使わない値なので例外として投げてしまう方向性で
		if (ret == long.MinValue)
		{
			throw new CodeEE(string.Format(trerror.MinInt64CanNotApplyABS.Text, Name, long.MinValue));
		}

		return Math.Abs(ret);
	}
}

internal sealed class PowerMethod : FunctionMethod
{
	public PowerMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long x = slots.Int(exm, 0);
		long y = slots.Int(exm, 1);
		double pow = Math.Pow(x, y);
		if (double.IsNaN(pow))
			// throw new CodeEE("累乗結果が非数値です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsNaN.Text, Name));
		}
		else if (double.IsInfinity(pow))
			//throw new CodeEE("累乗結果が無限大です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsInfinity.Text, Name));
		}
		else if (pow is >= long.MaxValue or <= long.MinValue)
			//throw new CodeEE("累乗結果(" + pow.ToString() + ")が64ビット符号付き整数の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsOutOfTheRangeOfInt64.Text, Name, pow));
		}

		return (long)pow;
	}
}

internal sealed class SqrtMethod : FunctionMethod
{
	public SqrtMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = slots.Int(exm, 0);
		if (ret < 0)
			// throw new CodeEE("SQRT関数の引数に負の値が指定されました");
		{
			throw new CodeEE(string.Format(trerror.ArgIsNegative.Text, Name, 1, ret));
		}

		return (long)Math.Sqrt(ret);
	}
}

internal sealed class CbrtMethod : FunctionMethod
{
	public CbrtMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = slots.Int(exm, 0);
		if (ret < 0)
			// throw new CodeEE("CBRT関数の引数に負の値が指定されました");
		{
			throw new CodeEE(string.Format(trerror.ArgIsNegative.Text, Name, 1, ret));
		}

		return (long)Math.Pow(ret, 1.0 / 3.0);
	}
}

internal sealed class LogMethod : FunctionMethod
{
	private readonly double Base;

	public LogMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		Base = Math.E;
		CanRestructure = true;
	}

	public LogMethod(double b)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		Base = b;
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = slots.Int(exm, 0);
		if (ret <= 0)
			// throw new CodeEE("対数関数の引数に0以下の値が指定されました");
		{
			throw new CodeEE(string.Format(trerror.ArgIsNotMoreThan0.Text, Name, 1, ret));
		}

		//　今の段階は発生しない
		//if (Base <= 0.0d)
		//	throw new CodeEE("対数関数の底に0以下の値が指定されました");
		double dret = ret;
		if (Base == Math.E)
		{
			dret = Math.Log(dret);
		}
		else
		{
			dret = Math.Log10(dret);
		}

		if (double.IsNaN(dret))
			// throw new CodeEE("計算値が非数値です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsNaN.Text, Name));
		}
		else if (double.IsInfinity(dret))
			// throw new CodeEE("計算値が無限大です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsInfinity.Text, Name));
		}
		else if (dret is >= long.MaxValue or <= long.MinValue)
			// throw new CodeEE("計算結果(" + dret.ToString() + ")が64ビット符号付き整数の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsOutOfTheRangeOfInt64.Text, Name, dret));
		}

		return (long)dret;
	}
}

internal sealed class ExpMethod : FunctionMethod
{
	public ExpMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = slots.Int(exm, 0);
		double dret = Math.Exp(ret);
		if (double.IsNaN(dret))
			// throw new CodeEE("計算値が非数値です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsNaN.Text, Name));
		}
		else if (double.IsInfinity(dret))
			// throw new CodeEE("計算値が無限大です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsInfinity.Text, Name));
		}
		else if (dret is >= long.MaxValue or <= long.MinValue)
			// throw new CodeEE("計算結果(" + dret.ToString() + ")が64ビット符号付き整数の範囲外です");
		{
			throw new CodeEE(string.Format(trerror.ResultIsOutOfTheRangeOfInt64.Text, Name, dret));
		}

		return (long)dret;
	}
}

internal sealed class SignMethod : FunctionMethod
{
	public SignMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long ret = slots.Int(exm, 0);
		return Math.Sign(ret);
	}
}

internal sealed class GetLimitMethod : FunctionMethod
{
	public GetLimitMethod()
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
		long ret;
		if (value < min)
		{
			ret = min;
		}
		else if (value > max)
		{
			ret = max;
		}
		else
		{
			ret = value;
		}

		return ret;
	}
}
