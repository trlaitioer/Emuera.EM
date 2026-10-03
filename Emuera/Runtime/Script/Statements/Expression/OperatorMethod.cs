using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using System;
using System.Collections.Generic;
using System.Text;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;

/// <summary>
/// 引数のチェック、戻り値の型チェック等は全て呼び出し元が責任を負うこと。
/// </summary>
internal abstract class OperatorMethod : FunctionMethod
{
	public OperatorMethod()
	{
		argumentTypeArray = null;
	}

	// The caller owns the type check for operators, so the checking overload is the one to refuse: overriding the
	// list-based facade instead would let CheckAndResolveSlots' virtual dispatch slip past this refusal.
	internal override string CheckArgumentType(string name, List<AExpression?> arguments, out int matchedIdx)
	{
		throw new ExeEE(trerror.TypeCheckIsCallersResponsibility.Text);
	}

	// Says the same thing to the facade, which must not ask a checker that only refuses. Needed as its own flag
	// because "has a rule table" is not the same question: `PlusStrStr` carries an argumentTypeArray as well.
	internal override bool CallerOwnsCheck => true;

	/// <summary>
	/// Caller-declared slot roles for the operand list an operator term is built with: every slot is filled, the
	/// operand type comes from the term itself, and <see cref="WritesThroughFirstArgument"/> adds REF to slot 0.
	/// </summary>
	internal SlotRole[] TrustedRoles(params AExpression[] operands)
	{
		SlotRole[] roles = new SlotRole[operands.Length];
		for (int i = 0; i < operands.Length; i++)
		{
			SlotRole role = SlotRole.Required | (operands[i].IsInteger ? SlotRole.Int : SlotRole.String);
			if (i == 0 && WritesThroughFirstArgument)
			{
				role |= SlotRole.Ref;
			}

			roles[i] = role;
		}

		return roles;
	}

	/// <summary>True when the operator writes through slot 0 (`++`/`--`): its declared role then carries REF.</summary>
	internal virtual bool WritesThroughFirstArgument => false;
}

internal static class OperatorMethodManager
{
	private static readonly Dictionary<OperatorCode, OperatorMethod> unaryDic = [];
	private static readonly Dictionary<OperatorCode, OperatorMethod> unaryAfterDic = [];
	private static readonly Dictionary<OperatorCode, OperatorMethod> binaryIntIntDic = [];
	private static readonly Dictionary<OperatorCode, OperatorMethod> binaryStrStrDic = [];
	private static readonly OperatorMethod binaryMultIntStr;
	private static readonly OperatorMethod ternaryIntIntInt;
	private static readonly OperatorMethod ternaryIntStrStr;

	static OperatorMethodManager()
	{
		unaryDic[OperatorCode.Plus] = new PlusInt();
		unaryDic[OperatorCode.Minus] = new MinusInt();
		unaryDic[OperatorCode.Not] = new NotInt();
		unaryDic[OperatorCode.BitNot] = new BitNotInt();
		unaryDic[OperatorCode.Increment] = new IncrementInt();
		unaryDic[OperatorCode.Decrement] = new DecrementInt();

		unaryAfterDic[OperatorCode.Increment] = new IncrementAfterInt();
		unaryAfterDic[OperatorCode.Decrement] = new DecrementAfterInt();

		binaryIntIntDic[OperatorCode.Plus] = new PlusIntInt();
		binaryIntIntDic[OperatorCode.Minus] = new MinusIntInt();
		binaryIntIntDic[OperatorCode.Mult] = new MultIntInt();
		binaryIntIntDic[OperatorCode.Div] = new DivIntInt();
		binaryIntIntDic[OperatorCode.Mod] = new ModIntInt();
		binaryIntIntDic[OperatorCode.Equal] = new EqualIntInt();
		binaryIntIntDic[OperatorCode.Greater] = new GreaterIntInt();
		binaryIntIntDic[OperatorCode.Less] = new LessIntInt();
		binaryIntIntDic[OperatorCode.GreaterEqual] = new GreaterEqualIntInt();
		binaryIntIntDic[OperatorCode.LessEqual] = new LessEqualIntInt();
		binaryIntIntDic[OperatorCode.NotEqual] = new NotEqualIntInt();
		binaryIntIntDic[OperatorCode.And] = new AndIntInt();
		binaryIntIntDic[OperatorCode.Or] = new OrIntInt();
		binaryIntIntDic[OperatorCode.Xor] = new XorIntInt();
		binaryIntIntDic[OperatorCode.Nand] = new NandIntInt();
		binaryIntIntDic[OperatorCode.Nor] = new NorIntInt();
		binaryIntIntDic[OperatorCode.BitAnd] = new BitAndIntInt();
		binaryIntIntDic[OperatorCode.BitOr] = new BitOrIntInt();
		binaryIntIntDic[OperatorCode.BitXor] = new BitXorIntInt();
		binaryIntIntDic[OperatorCode.RightShift] = new RightShiftIntInt();
		binaryIntIntDic[OperatorCode.LeftShift] = new LeftShiftIntInt();

		binaryStrStrDic[OperatorCode.Plus] = new PlusStrStr();
		binaryStrStrDic[OperatorCode.Equal] = new EqualStrStr();
		binaryStrStrDic[OperatorCode.Greater] = new GreaterStrStr();
		binaryStrStrDic[OperatorCode.Less] = new LessStrStr();
		binaryStrStrDic[OperatorCode.GreaterEqual] = new GreaterEqualStrStr();
		binaryStrStrDic[OperatorCode.LessEqual] = new LessEqualStrStr();
		binaryStrStrDic[OperatorCode.NotEqual] = new NotEqualStrStr();

		binaryMultIntStr = new MultStrInt();
		ternaryIntIntInt = new TernaryIntIntInt();
		ternaryIntStrStr = new TernaryIntStrStr();
	}


	public static AExpression ReduceUnaryTerm(OperatorCode op, AExpression o1)
	{
		OperatorMethod? method = null;
		if (op is OperatorCode.Increment or OperatorCode.Decrement)
		{
			if (!(o1 is VariableTerm var))
			{
				throw new CodeEE(trerror.IncrementNonVar.Text);
			}

			if (var.Identifier.IsConst)
			{
				throw new CodeEE(trerror.IncrementConst.Text);
			}
		}

		if (o1.IsInteger)
		{
			if (op == OperatorCode.Plus)
			{
				return o1;
			}

			if (unaryDic.TryGetValue(op, out OperatorMethod? value))
			{
				method = value;
			}
		}

		if (method != null)
		{
			return new FunctionMethodTerm(method, FunctionMethod.ArgumentSlots.Trusted([o1], method.TrustedRoles(o1)));
		}

		string errMes;
		if (o1.IsInteger)
		{
			errMes = trerror.NumericType.Text;
		}
		else if (o1.IsString)
		{
			errMes = trerror.StringType.Text;
		}
		else
		{
			errMes = trerror.UnknownType.Text;
		}

		errMes = string.Format(trerror.CanNotAppliedUnaryOp.Text, errMes, OperatorManager.ToOperatorString(op));
		throw new CodeEE(errMes);
	}

	public static AExpression ReduceUnaryAfterTerm(OperatorCode op, AExpression o1)
	{
		OperatorMethod? method = null;
		if (op is OperatorCode.Increment or OperatorCode.Decrement)
		{
			if (!(o1 is VariableTerm var))
			{
				throw new CodeEE(trerror.IncrementNonVar.Text);
			}

			if (var.Identifier.IsConst)
			{
				throw new CodeEE(trerror.IncrementConst.Text);
			}
		}

		if (o1.IsInteger)
		{
			if (unaryAfterDic.TryGetValue(op, out OperatorMethod? value))
			{
				method = value;
			}
		}

		if (method != null)
		{
			return new FunctionMethodTerm(method, FunctionMethod.ArgumentSlots.Trusted([o1], method.TrustedRoles(o1)));
		}

		string errMes;
		if (o1.IsInteger)
		{
			errMes = trerror.NumericType.Text;
		}
		else if (o1.IsString)
		{
			errMes = trerror.StringType.Text;
		}
		else
		{
			errMes = trerror.UnknownType.Text;
		}

		errMes = string.Format(trerror.CanNotAppliedUnaryOp.Text, errMes, OperatorManager.ToOperatorString(op));
		throw new CodeEE(errMes);
	}

	public static AExpression ReduceBinaryTerm(OperatorCode op, AExpression left, AExpression right)
	{
		//定数同士なら型付きオーバーロードに転送する。
		if (left is SingleLongTerm leftLong && right is SingleLongTerm rightLong)
		{
			return ReduceBinaryTerm(op, leftLong.Int, rightLong.Int);
		}

		if (left is SingleStrTerm leftStr && right is SingleStrTerm rightStr)
		{
			return ReduceBinaryTerm(op, leftStr.Str, rightStr.Str);
		}

		if (left is SingleLongTerm leftLong2 && right is SingleStrTerm rightStr2)
		{
			return ReduceBinaryTerm(op, leftLong2.Int, rightStr2.Str);
		}

		if (left is SingleStrTerm leftStr3 && right is SingleLongTerm rightLong3)
		{
			return ReduceBinaryTerm(op, leftStr3.Str, rightLong3.Int);
		}

		OperatorMethod? method = null;
		if (left.IsInteger && right.IsInteger)
		{
			if (binaryIntIntDic.TryGetValue(op, out OperatorMethod? value))
			{
				method = value;
			}
		}
		else if (left.IsString && right.IsString)
		{
			if (binaryStrStrDic.TryGetValue(op, out OperatorMethod? value))
			{
				method = value;
			}
		}
		else if ((left.IsInteger && right.IsString) || (left.IsString && right.IsInteger))
		{
			if (op == OperatorCode.Mult)
			{
				method = binaryMultIntStr;
			}
		}

		if (method != null)
		{
			return new FunctionMethodTerm(method,
				FunctionMethod.ArgumentSlots.Trusted([left, right], method.TrustedRoles(left, right)));
		}

		throw CreateBinaryOpTypeError(left.GetOperandType(), right.GetOperandType(), op);
	}

	public static AExpression ReduceBinaryTerm(OperatorCode op, long left, long right)
	{
		return new SingleLongTerm(CalcBinaryIntInt(op, left, right));
	}

	public static AExpression ReduceBinaryTerm(OperatorCode op, string left, string right)
	{
		if (op == OperatorCode.Plus)
		{
			return new SingleStrTerm(left + right);
		}

		return new SingleLongTerm(CalcBinaryStrStr(op, left, right));
	}

	public static AExpression ReduceBinaryTerm(OperatorCode op, long left, string right)
	{
		if (op != OperatorCode.Mult)
		{
			throw CreateBinaryOpTypeError(typeof(long), typeof(string), op);
		}

		return new SingleStrTerm(CalcStringMultiply(left, right));
	}

	public static AExpression ReduceBinaryTerm(OperatorCode op, string left, long right)
	{
		if (op != OperatorCode.Mult)
		{
			throw CreateBinaryOpTypeError(typeof(string), typeof(long), op);
		}

		return new SingleStrTerm(CalcStringMultiply(right, left));
	}

	public static bool ReduceBinaryBool(OperatorCode op, long left, long right)
	{
		return CalcBinaryIntInt(op, left, right) != 0;
	}

	public static bool ReduceBinaryBool(OperatorCode op, string left, string right)
	{
		if (op == OperatorCode.Plus)
		{
			throw new ExeEE(trerror.ReturnTypeDifferentOrNotImpelemnt.Text);
		}

		return CalcBinaryStrStr(op, left, right) != 0;
	}

	private static long CalcBinaryIntInt(OperatorCode op, long left, long right)
	{
		switch (op)
		{
			case OperatorCode.Plus:
				return left + right;
			case OperatorCode.Minus:
				return left - right;
			case OperatorCode.Mult:
				return left * right;
			case OperatorCode.Div:
				if (right == 0)
				{
					throw new CodeEE(trerror.DivideByZero.Text);
				}

				return left / right;
			case OperatorCode.Mod:
				if (right == 0)
				{
					throw new CodeEE(trerror.DivideByZero.Text);
				}

				return left % right;
			case OperatorCode.Equal:
				return left == right ? 1L : 0L;
			case OperatorCode.NotEqual:
				return left != right ? 1L : 0L;
			case OperatorCode.Greater:
				return left > right ? 1L : 0L;
			case OperatorCode.Less:
				return left < right ? 1L : 0L;
			case OperatorCode.GreaterEqual:
				return left >= right ? 1L : 0L;
			case OperatorCode.LessEqual:
				return left <= right ? 1L : 0L;
			case OperatorCode.And:
				return left != 0 && right != 0 ? 1L : 0L;
			case OperatorCode.Or:
				return left != 0 || right != 0 ? 1L : 0L;
			case OperatorCode.Xor:
				return (left == 0 && right != 0) || (left != 0 && right == 0) ? 1L : 0L;
			case OperatorCode.Nand:
				return left == 0 || right == 0 ? 1L : 0L;
			case OperatorCode.Nor:
				return left == 0 && right == 0 ? 1L : 0L;
			case OperatorCode.BitAnd:
				return left & right;
			case OperatorCode.BitOr:
				return left | right;
			case OperatorCode.BitXor:
				return left ^ right;
			case OperatorCode.RightShift:
				return left >> (int)right;
			case OperatorCode.LeftShift:
				return left << (int)right;
			default:
				throw CreateBinaryOpTypeError(typeof(long), typeof(long), op);
		}
	}

	private static long CalcBinaryStrStr(OperatorCode op, string left, string right)
	{
		int c;
		switch (op)
		{
			case OperatorCode.Equal:
				return left == right ? 1L : 0L;
			case OperatorCode.NotEqual:
				return left != right ? 1L : 0L;
			case OperatorCode.Greater:
				c = string.Compare(left, right, Config.Config.SCExpression);
				return c > 0 ? 1L : 0L;
			case OperatorCode.Less:
				c = string.Compare(left, right, Config.Config.SCExpression);
				return c < 0 ? 1L : 0L;
			case OperatorCode.GreaterEqual:
				c = string.Compare(left, right, Config.Config.SCExpression);
				return c >= 0 ? 1L : 0L;
			case OperatorCode.LessEqual:
				c = string.Compare(left, right, Config.Config.SCExpression);
				return c <= 0 ? 1L : 0L;
			default:
				throw CreateBinaryOpTypeError(typeof(string), typeof(string), op);
		}
	}

	private static string CalcStringMultiply(long value, string str)
	{
		if (value < 0)
		{
			throw new CodeEE(string.Format(trerror.MultiplyNegativeToStr.Text, value.ToString()));
		}

		if (value >= 10000)
		{
			throw new CodeEE(string.Format(trerror.Multiply10kToStr.Text, value.ToString()));
		}

		if (string.IsNullOrEmpty(str) || value == 0)
		{
			return "";
		}

		StringBuilder builder = new() { Capacity = str.Length * (int)value };
		for (int i = 0; i < value; i++)
		{
			builder.Append(str);
		}

		return builder.ToString();
	}

	private static CodeEE CreateBinaryOpTypeError(Type leftType, Type rightType, OperatorCode op)
	{
		string typeName1;
		if (leftType == typeof(long))
		{
			typeName1 = trerror.NumericType.Text;
		}
		else if (leftType == typeof(string))
		{
			typeName1 = trerror.StringType.Text;
		}
		else
		{
			typeName1 = trerror.UnknownType.Text;
		}

		string typeName2;
		if (rightType == typeof(long))
		{
			typeName2 = trerror.NumericType.Text;
		}
		else if (rightType == typeof(string))
		{
			typeName2 = trerror.StringType.Text;
		}
		else
		{
			typeName2 = trerror.UnknownType.Text;
		}

		return new CodeEE(string.Format(trerror.CanNotAppliedBinaryOp.Text, typeName1, typeName2, OperatorManager.ToOperatorString(op)));
	}

	public static AExpression ReduceTernaryTerm(AExpression o1, AExpression o2, AExpression o3)
	{
		OperatorMethod? method = null;
		if (o1.IsInteger && o2.IsInteger && o3.IsInteger)
		{
			method = ternaryIntIntInt;
		}
		else if (o1.IsInteger && o2.IsString && o3.IsString)
		{
			method = ternaryIntStrStr;
		}

		if (method != null)
		{
			return new FunctionMethodTerm(method,
				FunctionMethod.ArgumentSlots.Trusted([o1, o2, o3], method.TrustedRoles(o1, o2, o3)));
		}

		throw new CodeEE(trerror.InvalidTernaryOp.Text);
	}

	#region OperatorMethod SubClasses

	private sealed class PlusIntInt : OperatorMethod
	{
		public PlusIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) + slots.Int(exm, 1);
		}
	}

	private sealed class PlusStrStr : OperatorMethod
	{
		public PlusStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(string);
			argumentTypeArray = [typeof(string), typeof(string)];
		}

		public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Str(exm, 0) + slots.Str(exm, 1);
		}
	}

	private sealed class MinusIntInt : OperatorMethod
	{
		public MinusIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) - slots.Int(exm, 1);
		}
	}

	private sealed class MultIntInt : OperatorMethod
	{
		public MultIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) * slots.Int(exm, 1);
		}
	}

	private sealed class MultStrInt : OperatorMethod
	{
		public MultStrInt()
		{
			CanRestructure = true;
			ReturnType = typeof(string);
		}

		public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			string str;
			long value;
			if (slots.IsInteger(0))
			{
				value = slots.Int(exm, 0);
				str = slots.Str(exm, 1);
			}
			else
			{
				str = slots.Str(exm, 0);
				value = slots.Int(exm, 1);
			}

			if (value < 0)
			{
				throw new CodeEE(string.Format(trerror.MultiplyNegativeToStr.Text, value.ToString()));
			}

			if (value >= 10000)
			{
				throw new CodeEE(string.Format(trerror.Multiply10kToStr.Text, value.ToString()));
			}

			if (string.IsNullOrEmpty(str) || value == 0)
			{
				return "";
			}

			StringBuilder builder = new() { Capacity = str.Length * (int)value };
			for (int i = 0; i < value; i++)
			{
				builder.Append(str);
			}

			return builder.ToString();
		}
	}

	private sealed class DivIntInt : OperatorMethod
	{
		public DivIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			long right = slots.Int(exm, 1);
			if (right == 0)
			{
				throw new CodeEE(trerror.DivideByZero.Text);
			}

			return slots.Int(exm, 0) / right;
		}
	}

	private sealed class ModIntInt : OperatorMethod
	{
		public ModIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			long right = slots.Int(exm, 1);
			if (right == 0)
			{
				throw new CodeEE(trerror.DivideByZero.Text);
			}

			return slots.Int(exm, 0) % right;
		}
	}


	private sealed class EqualIntInt : OperatorMethod
	{
		public EqualIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) == slots.Int(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class EqualStrStr : OperatorMethod
	{
		public EqualStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Str(exm, 0) == slots.Str(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class NotEqualIntInt : OperatorMethod
	{
		public NotEqualIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) != slots.Int(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class NotEqualStrStr : OperatorMethod
	{
		public NotEqualStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Str(exm, 0) != slots.Str(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class GreaterIntInt : OperatorMethod
	{
		public GreaterIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) > slots.Int(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class GreaterStrStr : OperatorMethod
	{
		public GreaterStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			int c = string.Compare(slots.Str(exm, 0), slots.Str(exm, 1), Config.Config.SCExpression);
			if (c > 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class LessIntInt : OperatorMethod
	{
		public LessIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) < slots.Int(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class LessStrStr : OperatorMethod
	{
		public LessStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			int c = string.Compare(slots.Str(exm, 0), slots.Str(exm, 1), Config.Config.SCExpression);
			if (c < 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class GreaterEqualIntInt : OperatorMethod
	{
		public GreaterEqualIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) >= slots.Int(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class GreaterEqualStrStr : OperatorMethod
	{
		public GreaterEqualStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			int c = string.Compare(slots.Str(exm, 0), slots.Str(exm, 1), Config.Config.SCExpression);
			if (c >= 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class LessEqualIntInt : OperatorMethod
	{
		public LessEqualIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) <= slots.Int(exm, 1))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class LessEqualStrStr : OperatorMethod
	{
		public LessEqualStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			int c = string.Compare(slots.Str(exm, 0), slots.Str(exm, 1), Config.Config.SCExpression);
			if (c <= 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class AndIntInt : OperatorMethod
	{
		public AndIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) != 0 && slots.Int(exm, 1) != 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class OrIntInt : OperatorMethod
	{
		public OrIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) != 0 || slots.Int(exm, 1) != 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class XorIntInt : OperatorMethod
	{
		public XorIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			long i1 = slots.Int(exm, 0);
			long i2 = slots.Int(exm, 1);
			if ((i1 == 0 && i2 != 0) || (i1 != 0 && i2 == 0))
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class NandIntInt : OperatorMethod
	{
		public NandIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) == 0 || slots.Int(exm, 1) == 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class NorIntInt : OperatorMethod
	{
		public NorIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) == 0 && slots.Int(exm, 1) == 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class BitAndIntInt : OperatorMethod
	{
		public BitAndIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) & slots.Int(exm, 1);
		}
	}

	private sealed class BitOrIntInt : OperatorMethod
	{
		public BitOrIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) | slots.Int(exm, 1);
		}
	}

	private sealed class BitXorIntInt : OperatorMethod
	{
		public BitXorIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) ^ slots.Int(exm, 1);
		}
	}

	private sealed class RightShiftIntInt : OperatorMethod
	{
		public RightShiftIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) >> (int)slots.Int(exm, 1);
		}
	}

	private sealed class LeftShiftIntInt : OperatorMethod
	{
		public LeftShiftIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) << (int)slots.Int(exm, 1);
		}
	}

	private sealed class PlusInt : OperatorMethod
	{
		public PlusInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0);
		}
	}

	private sealed class MinusInt : OperatorMethod
	{
		public MinusInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			long ret = slots.Int(exm, 0);
			if (ret == long.MinValue)
			{
				exm.Console.PrintSystemLine(string.Format(Lang.SystemLine.MinusWontWork.Text, long.MinValue));
			}

			return -ret;
		}
	}

	private sealed class NotInt : OperatorMethod
	{
		public NotInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			if (slots.Int(exm, 0) == 0)
			{
				return 1L;
			}

			return 0L;
		}
	}

	private sealed class BitNotInt : OperatorMethod
	{
		public BitNotInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return ~slots.Int(exm, 0);
		}
	}

	private sealed class IncrementInt : OperatorMethod
	{
		public IncrementInt()
		{
			CanRestructure = false;
			ReturnType = typeof(long);
		}

		internal override bool WritesThroughFirstArgument => true;

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			VariableTerm var = slots.Var(0);
			return var.ChangeValue(1L, exm);
		}
	}

	private sealed class DecrementInt : OperatorMethod
	{
		public DecrementInt()
		{
			CanRestructure = false;
			ReturnType = typeof(long);
		}

		internal override bool WritesThroughFirstArgument => true;

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			VariableTerm var = slots.Var(0);
			return var.ChangeValue(-1L, exm);
		}
	}

	private sealed class IncrementAfterInt : OperatorMethod
	{
		public IncrementAfterInt()
		{
			CanRestructure = false;
			ReturnType = typeof(long);
		}

		internal override bool WritesThroughFirstArgument => true;

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			VariableTerm var = slots.Var(0);
			return var.ChangeValue(1L, exm) - 1;
		}
	}

	private sealed class DecrementAfterInt : OperatorMethod
	{
		public DecrementAfterInt()
		{
			CanRestructure = false;
			ReturnType = typeof(long);
		}

		internal override bool WritesThroughFirstArgument => true;

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			VariableTerm var = slots.Var(0);
			return var.ChangeValue(-1L, exm) + 1;
		}
	}


	private sealed class TernaryIntIntInt : OperatorMethod
	{
		public TernaryIntIntInt()
		{
			CanRestructure = true;
			ReturnType = typeof(long);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) != 0 ? slots.Int(exm, 1) : slots.Int(exm, 2);
		}
	}

	private sealed class TernaryIntStrStr : OperatorMethod
	{
		public TernaryIntStrStr()
		{
			CanRestructure = true;
			ReturnType = typeof(string);
		}

		public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) != 0 ? slots.Str(exm, 1) : slots.Str(exm, 2);
		}
	}

	#endregion
}
