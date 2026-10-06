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
		kind == ErrorReportKind.Unexpected ? exc.GetType() + ":" + exc.Message : exc.Message;
}
