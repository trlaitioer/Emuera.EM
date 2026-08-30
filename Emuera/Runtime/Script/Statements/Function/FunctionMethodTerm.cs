using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Script.Statements.Function;

internal sealed class FunctionMethodTerm : AExpression
{
	public FunctionMethodTerm(FunctionMethod meth, List<AExpression> args)
		: base(meth.ReturnType)
	{
		Method = meth;
		Arguments = args;
	}

	internal FunctionMethod Method { get; }
	internal List<AExpression> Arguments { get; }

	public override long GetIntValue(ExpressionMediator exm)
	{
		return Method.GetIntValue(exm, Arguments);
	}
	public override string GetStrValue(ExpressionMediator exm)
	{
		return Method.GetStrValue(exm, Arguments);
	}
	public override SingleTerm GetValue(ExpressionMediator exm)
	{
		return Method.GetReturnValue(exm, Arguments);
	}

	public override AExpression Restructure(ExpressionMediator exm)
	{
		if (Method.HasUniqueRestructure)
		{
			if (Method.UniqueRestructure(exm, [.. Arguments]) && Method.CanRestructure)
				return GetValue(exm);
			return this;
		}
		bool argIsConst = true;
		for (int i = 0; i < Arguments.Count; i++)
		{
			if (Arguments[i] == null)
				continue;
			Arguments[i] = Arguments[i].Restructure(exm);
			argIsConst &= Arguments[i] is SingleTerm;
		}
		if (Method.CanRestructure && argIsConst)
			return GetValue(exm);
		return this;

	}

}
