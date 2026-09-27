using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements.Function;

internal abstract class SuperUserDefinedMethodTerm : AExpression
{
	protected SuperUserDefinedMethodTerm(Type returnType)
		: base(returnType)
	{ }

	public abstract UserDefinedFunctionArgument Argument { get; }
	public abstract CalledFunction Call { get; }

	public override long GetIntValue(ExpressionMediator exm)
	{
		if (exm.Process.GetValue(this) is not SingleLongTerm term)
		{
			return 0;
		}

		return term.Int;
	}

	public override string GetStrValue(ExpressionMediator exm)
	{
		if (exm.Process.GetValue(this) is not SingleStrTerm term)
		{
			return "";
		}

		return term.Str;
	}

	public override SingleTerm GetValue(ExpressionMediator exm)
	{
		SingleTerm? term = exm.Process.GetValue(this);
		if (term == null)
		{
			if (IsInteger)
			{
				return new SingleLongTerm(0);
			}
			else
			{
				return new SingleStrTerm("");
			}
		}

		return term;
	}
}

internal sealed class UserDefinedMethodTerm : SuperUserDefinedMethodTerm
{
	public static bool TryCreate(
		FunctionLabelLine targetLabel,
		List<AExpression?> srcArgs,
		[NotNullWhen(false)] out string? errMes,
		[NotNullWhen(true)] out UserDefinedMethodTerm? term
	)
	{
		term = null;
		CalledFunction call = CalledFunction.CreateCalledFunctionMethod(targetLabel, targetLabel.LabelName);
		if (!call.TryConvertArg(srcArgs, out errMes, out UserDefinedFunctionArgument? argument))
		{
			return false;
		}

		term = new UserDefinedMethodTerm(argument, call.TopLabel.MethodType, call);
		return true;
	}

	private UserDefinedMethodTerm(UserDefinedFunctionArgument arg, Type returnType, CalledFunction call)
		: base(returnType)
	{
		Argument = arg;
		Call = call;
	}

	public override UserDefinedFunctionArgument Argument { get; }

	public override CalledFunction Call { get; }

	public override AExpression Restructure(ExpressionMediator exm)
	{
		Argument.Restructure(exm);
		return this;
	}
}

internal sealed class UserDefinedRefMethodTerm : SuperUserDefinedMethodTerm
{
	public UserDefinedRefMethodTerm(UserDefinedRefMethod reffunc, List<AExpression?> srcArgs)
		: base(reffunc.RetType)
	{
		this.srcArgs = srcArgs;
		this.reffunc = reffunc;
	}

	private readonly List<AExpression?> srcArgs;
	private readonly UserDefinedRefMethod reffunc;

	public override UserDefinedFunctionArgument Argument
	{
		get
		{
			if (reffunc.CalledFunction == null)
			{
				throw new CodeEE(string.Format(trerror.EmptyRefFunc.Text, reffunc.Name));
			}

			if (!reffunc.CalledFunction.TryConvertArg(srcArgs, out string? errMes, out UserDefinedFunctionArgument? argument))
			{
				throw new CodeEE(errMes);
			}

			return argument;
		}
	}

	public override CalledFunction Call
	{
		get
		{
			if (reffunc.CalledFunction == null)
			{
				throw new CodeEE(string.Format(trerror.EmptyRefFunc.Text, reffunc.Name));
			}

			return reffunc.CalledFunction;
		}
	}

	public override AExpression Restructure(ExpressionMediator exm)
	{
		// TODO: srcArgs can hold null for an omitted argument, as FunctionMethodTerm.Restructure accepts; decide whether to skip null elements instead of asserting them non-null.
		for (int i = 0; i < srcArgs.Count; i++)
		{
			if ((reffunc.ArgTypeList[i] & UserDifinedFunctionDataArgType.__Ref) == UserDifinedFunctionDataArgType.__Ref)
			{
				srcArgs.Required(i).Restructure(exm);
			}
			else
			{
				srcArgs[i] = srcArgs.Required(i).Restructure(exm);
			}
		}

		return this;
	}
}

internal sealed class UserDefinedRefMethodNoArgTerm : SuperUserDefinedMethodTerm
{
	public UserDefinedRefMethodNoArgTerm(UserDefinedRefMethod reffunc)
		: base(reffunc.RetType)
	{
		this.reffunc = reffunc;
	}

	private readonly UserDefinedRefMethod reffunc;
	public override UserDefinedFunctionArgument Argument => throw new CodeEE(string.Format(trerror.RefFuncHasNotArg.Text, reffunc.Name));

	public override CalledFunction Call => throw new CodeEE(string.Format(trerror.RefFuncHasNotArg.Text, reffunc.Name));

	public string GetRefName()
	{
		if (reffunc.CalledFunction == null)
		{
			return "";
		}

		return reffunc.CalledFunction.TopLabel.LabelName;
	}

	public override long GetIntValue(ExpressionMediator exm)
	{
		throw new CodeEE(string.Format(trerror.RefFuncHasNotArg.Text, reffunc.Name));
	}

	public override string GetStrValue(ExpressionMediator exm)
	{
		throw new CodeEE(string.Format(trerror.RefFuncHasNotArg.Text, reffunc.Name));
	}

	public override SingleTerm GetValue(ExpressionMediator exm)
	{
		throw new CodeEE(string.Format(trerror.RefFuncHasNotArg.Text, reffunc.Name));
	}

	public override AExpression Restructure(ExpressionMediator exm)
	{
		return this;
	}
}
