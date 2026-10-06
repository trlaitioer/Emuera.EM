using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements;

/// <summary>
/// Watch-list expressions of the debug window (DebugDialog.getValueString): the expression text is
/// evaluated and rendered as display text, or as the diagnostic text of whatever refused it. It lives
/// here rather than in the form so the mapping is testable without WinForms, and because
/// ReduceExpressionTerm reports "no expression here" by returning null - a whitespace-only entry used
/// to be dereferenced and shown as NullReferenceException.
/// </summary>
internal static class DebugWatchExpression
{
	/// <summary>
	/// Renders <paramref name="expression"/>: the empty string for an empty entry, otherwise the value's
	/// text or the failure's diagnostic text. A null reduction (no expression in the text) is reported as
	/// a parse error like ReduceIntegerTerm does.
	/// </summary>
	public static string GetValueString(string expression, ExpressionMediator em)
	{
		if (string.IsNullOrEmpty(expression))
		{
			return "";
		}

		try
		{
			CharStream st = new(expression);
			WordCollection wc = LexicalAnalyzer.Analyse(st, LexEndWith.EoL, LexAnalyzeFlag.None);
			AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
			if (term == null)
			{
				throw new CodeEE(trerror.CanNotInterpretedExpression.Text);
			}

			SingleTerm value = term.GetValue(em);
			//SingleLongTerm/SingleStrTerm are the only results of GetValue and both format to non-null text
			return value.ToString()!;
		}
		catch (CodeEE e)
		{
			return e.Message;
		}
		catch (Exception e)
		{
			return e.GetType() + ":" + e.Message;
		}
	}
}
