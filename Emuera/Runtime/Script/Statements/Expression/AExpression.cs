using System;

namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;

internal abstract class AExpression
{
	public AExpression(Type t)
	{
		type = t;
	}

	public Type GetOperandType()
	{
		return type;
	}

	public virtual long GetIntValue(ExpressionMediator exm)
	{
		return 0;
	}

	public virtual string GetStrValue(ExpressionMediator exm)
	{
		return "";
	}

	public virtual SingleTerm GetValue(ExpressionMediator exm)
	{
		if (type == typeof(long))
		{
			return new SingleLongTerm(0);
		}
		else
		{
			return new SingleStrTerm("");
		}
	}

	/// <summary>
	/// Reads a term whose evaluation cannot reach the context: constant folding, and tokens whose
	/// value comes from CSV/constant tables or the character list. Naming the case keeps the
	/// "no context here" assertion in one place instead of at every call site.
	/// </summary>
	public long GetIntValue()
	{
		return GetIntValue(null!);
	}

	/// <inheritdoc cref="GetIntValue()"/>
	public string GetStrValue()
	{
		return GetStrValue(null!);
	}

	public bool IsInteger => type == typeof(long);

	public bool IsString => type == typeof(string);

	private readonly Type type;

	/// <summary>
	/// 定数を解体して可能ならSingleTerm化する
	/// defineの都合上、2回以上呼ばれる可能性がある
	/// </summary>
	public virtual AExpression Restructure(ExpressionMediator exm)
	{
		return this;
	}
}
