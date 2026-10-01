using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;

/// <summary>
/// Reads of argument slots whose element type is nullable because an explicitly empty argument is
/// carried as null, while omitting trailing arguments only shortens the list (FunctionMethod
/// .CheckArgumentTypeEx and CalledFunction.ConvertArg rely on that).
/// </summary>
internal static class ArgumentListExtensions
{
	// Reading a slot through this accessor claims the slot is filled. FunctionMethod.CheckArgumentTypeEx
	// rejects an explicitly empty slot only where the rule block marks that position required - no
	// OmitStart, i < OmitStart, ArgType.NotNull on the rule governing the slot, or the SameAsFirst case
	// that requires slot 0 because it supplies the expected operand type. A variadic tail is governed by
	// whichever variadic rule its index lands on, not by the slot number. The claim therefore holds only
	// for such a slot: past OmitStart the checker lets an explicitly empty slot through, and a caller
	// that reads it anyway asserts something it cannot prove - either the rule table marks the slot
	// required, or the body has to handle the null itself.
	internal static AExpression Required(this List<AExpression?> arguments, int index)
	{
		return arguments[index]!;
	}

	internal static AExpression Required(this AExpression?[] arguments, int index)
	{
		return arguments[index]!;
	}
}
