// HTML builtins (html系).

using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.UI.Game;
using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class HtmlGetPrintedStrMethod : FunctionMethod
{
	[BuiltinMethod("HTML_GETPRINTEDSTR")]
	private static HtmlGetPrintedStrMethod CreateHTML_GETPRINTEDSTR()
	{
		return new HtmlGetPrintedStrMethod();
	}

	public HtmlGetPrintedStrMethod()
	{
		ReturnType = typeof(string);
		// argumentTypeArray = null;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int | ArgType.NotNull }, OmitStart = 0 }
		];
		CanRestructure = false;
	}

	//public override string CheckArgumentType(string name, IOperandTerm[] arguments)
	//{
	//	//通常１つ。省略可能。
	//	if (arguments.Count > 1)
	//		return name + "関数の引数が多すぎます";
	//	if (arguments.Count == 0|| arguments[0] == null)
	//		return null;
	//	if (arguments[0].GetOperandType() != typeof(Int64))
	//		return name + "関数の1番目の引数の型が正しくありません";
	//	return null;
	//}
	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		long lineNo = 0;
		if (slots.Count > 0)
		{
			lineNo = slots.Int(exm, 0);
		}

		if (lineNo < 0)
			// throw new CodeEE("引数を0未満にできません");
		{
			throw new CodeEE(string.Format(trerror.ArgIsNegative.Text, Name, 1, lineNo));
		}

		ConsoleDisplayLine[]? dispLines = exm.Console.GetDisplayLines(lineNo);
		if (dispLines == null)
		{
			return "";
		}

		return HtmlManager.DisplayLine2Html(dispLines, true);
	}
}

internal sealed class HtmlPopPrintingStrMethod : FunctionMethod
{
	[BuiltinMethod("HTML_POPPRINTINGSTR")]
	private static HtmlPopPrintingStrMethod CreateHTML_POPPRINTINGSTR()
	{
		return new HtmlPopPrintingStrMethod();
	}

	public HtmlPopPrintingStrMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		ConsoleDisplayLine[]? dispLines = exm.Console.PopDisplayingLines();
		if (dispLines == null)
		{
			return "";
		}

		return HtmlManager.DisplayLine2Html(dispLines, false);
	}
}

internal sealed class HtmlToPlainTextMethod : FunctionMethod
{
	[BuiltinMethod("HTML_TOPLAINTEXT")]
	private static HtmlToPlainTextMethod CreateHTML_TOPLAINTEXT()
	{
		return new HtmlToPlainTextMethod();
	}

	public HtmlToPlainTextMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return HtmlManager.Html2PlainText(slots.Str(exm, 0));
	}
}

internal sealed class HtmlEscapeMethod : FunctionMethod
{
	[BuiltinMethod("HTML_ESCAPE")]
	private static HtmlEscapeMethod CreateHTML_ESCAPE()
	{
		return new HtmlEscapeMethod();
	}

	public HtmlEscapeMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return HtmlManager.Escape(slots.Str(exm, 0));
	}
}
