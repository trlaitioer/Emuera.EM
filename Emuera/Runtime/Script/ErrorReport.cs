using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script;

/// <summary>
/// How an exception is reported: the header text it gets and whether the CLR stack is printed.
/// </summary>
internal enum ErrorReportKind
{
	/// <summary>A script-side error (<see cref="CodeEE"/> and its subclasses): a readable message with file/line.</summary>
	Code,

	/// <summary>An Emuera error (<see cref="ExeEE"/>): a readable message, no stack.</summary>
	Emuera,

	/// <summary>A save/load file error (<see cref="FileEE"/>): a readable message, no stack.</summary>
	SaveData,

	/// <summary>Anything else (a programming fault): reported as an unexpected error, with the CLR stack.</summary>
	Unexpected
}

/// <summary>
/// The reporting policy for one <see cref="ErrorReportKind"/>: <see cref="Classify"/> picks the kind, the two
/// header accessors give the text the handlers print, and <see cref="PrintsStackTrace"/> says whether the CLR
/// stack is worth printing. Keeping the policy here rather than inline in each handler is what makes "a save error
/// is not an unexpected error" a single decision instead of one per call site.
/// </summary>
internal static class ErrorReport
{
	/// <summary>
	/// <see cref="FileEE"/> derives from <see cref="EmueraException"/> and is neither a <see cref="CodeEE"/> nor an
	/// <see cref="ExeEE"/>: missing it reports a corrupted save that the pre-load check cannot see (a truncated data
	/// section, say) as an unexpected error, whose readable message is then buried under a CLR stack.
	/// </summary>
	internal static ErrorReportKind Classify(Exception exc) =>
		exc switch
		{
			CodeEE => ErrorReportKind.Code,
			ExeEE => ErrorReportKind.Emuera,
			FileEE => ErrorReportKind.SaveData,
			_ => ErrorReportKind.Unexpected
		};

	/// <summary>Header of an error raised while running a script line.</summary>
	internal static Lang.TranslatableString Header(ErrorReportKind kind) =>
		kind switch
		{
			ErrorReportKind.Code => trerror.HasError,
			ErrorReportKind.Emuera => trerror.HasEmueraError,
			ErrorReportKind.SaveData => trerror.HasSaveDataError,
			_ => trerror.HasUnexpectedError
		};

	/// <summary>Header of an error that ends a system flow (the function-termination path).</summary>
	internal static Lang.TranslatableString SystemProcHeader(ErrorReportKind kind) =>
		kind switch
		{
			ErrorReportKind.Code => trerror.FuncEndError,
			ErrorReportKind.Emuera => trerror.FuncEndEmueraError,
			ErrorReportKind.SaveData => trerror.FuncEndSaveDataError,
			_ => trerror.FuncEndUnexpectedError
		};

	/// <summary>
	/// Only an unexpected error is a programming fault, and its CLR stack is the only thing that locates it.
	/// The other kinds carry a message written for the player.
	/// </summary>
	internal static bool PrintsStackTrace(ErrorReportKind kind) => kind == ErrorReportKind.Unexpected;

	/// <summary>The line under the header: an unexpected error names its CLR type, since nothing else does.</summary>
	internal static string Message(ErrorReportKind kind, Exception exc) =>
		kind == ErrorReportKind.Unexpected ? WithTypeName(exc, exc.GetType().ToString(), ":") : exc.Message;

	/// <summary>
	/// The detail for a save/load failure, wherever it is reported: an <see cref="EmueraException"/> already
	/// carries a message written for the player, while anything else - a BCL I/O failure from the medium -
	/// names its CLR type, which is what tells the two apart because these lines are printed without a stack.
	/// Used both where the caller prints the line itself (the <c>SAVEDATA</c> family and the
	/// <c>LOADGLOBAL</c>/<c>LOADCHARA</c> paths, which keep their return values) and where the failure is
	/// graded through the handlers (<see cref="AsSaveDataError"/>, and <c>SaveGlobal</c>'s own message, which
	/// keeps its header text in front of the reason).
	/// </summary>
	internal static string Reason(Exception exc) =>
		exc is EmueraException ? exc.Message : WithTypeName(exc, exc.GetType().Name, ": ");

	/// <summary>
	/// Grades a failure as a save/load error carrying its reason (<see cref="Reason"/>), for the writers that
	/// report through the handlers instead of through a return value: a failed write is a save/load error, not
	/// an unexpected one that prints a CLR stack.
	/// </summary>
	internal static FileEE AsSaveDataError(Exception exc) => new(Reason(exc));

	/// <summary>
	/// One "<c>type:message</c>" line, for the reports that print no stack and so have nothing else to say what
	/// went wrong. Both spellings belong to the caller: the handler path has always printed the full name tight
	/// against the message - pinned by <c>Message_NamesTheClrTypeOnlyForUnexpected</c> in
	/// <c>Emuera.Tests/Runtime/Script/ErrorReportTests.cs</c> - while the save line prints the short name.
	/// </summary>
	private static string WithTypeName(Exception exc, string typeName, string separator) =>
		typeName + separator + exc.Message;
}
