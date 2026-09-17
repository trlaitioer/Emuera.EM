#nullable enable
using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;

/// <summary>
/// Reads of argument slots whose element type is nullable because an omitted argument is carried
/// as null (FunctionMethod.CheckArgumentTypeEx and CalledFunction.ConvertArg rely on that).
/// </summary>
internal static class ArgumentListExtensions
{
	// Reading a slot through this accessor claims the slot is filled. FunctionMethod
	// .CheckArgumentType* rejects an omitted slot only where the argument list marks the position
	// as required, so the claim belongs to the caller - the same claim `arguments[index]!` made.
	internal static AExpression Required(this List<AExpression?> arguments, int index) => arguments[index]!;

	internal static AExpression Required(this AExpression?[] arguments, int index) => arguments[index]!;
}
