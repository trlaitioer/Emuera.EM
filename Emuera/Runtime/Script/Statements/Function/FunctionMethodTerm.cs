using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Script.Statements.Function;

internal sealed class FunctionMethodTerm : AExpression
{
	public FunctionMethodTerm(FunctionMethod meth, FunctionMethod.ArgumentSlots slots)
		: base(meth.ReturnType)
	{
		Method = meth;
		Slots = slots;
	}

	internal FunctionMethod Method { get; }

	/// <summary>Check-time slot view (roles + snapshot) the body reads through.</summary>
	internal FunctionMethod.ArgumentSlots Slots { get; }

	public override long GetIntValue(ExpressionMediator exm)
	{
		return Method.GetIntValue(exm, Slots);
	}

	public override string GetStrValue(ExpressionMediator exm)
	{
		return Method.GetStrValue(exm, Slots);
	}

	public override SingleTerm GetValue(ExpressionMediator exm)
	{
		return Method.GetReturnValue(exm, Slots);
	}

	public override AExpression Restructure(ExpressionMediator exm)
	{
		List<AExpression?> arguments = Slots.FoldTarget;
		if (Method.HasUniqueRestructure)
		{
			// UniqueRestructure receives a copy, so a fold it performs on that copy is discarded
			// (arg-contract-04).
			if (Method.UniqueRestructure(exm, [.. arguments]) && Method.CanRestructure)
			{
				return GetValue(exm);
			}

			return this;
		}

		bool argIsConst = true;
		for (int i = 0; i < arguments.Count; i++)
		{
			if (arguments[i] == null)
			{
				continue;
			}

			arguments[i] = arguments.Required(i).Restructure(exm);
			argIsConst &= arguments[i] is SingleTerm;
		}

		if (Method.CanRestructure && argIsConst)
		{
			return GetValue(exm);
		}

		return this;
	}
}
