using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Data;

internal sealed class StrForm
{
	private StrForm(string[] strs, AExpression[] terms)
	{
		this.strs = strs;
		this.terms = terms;
	}

	private string[] strs; //terms.Count + 1
	private AExpression[] terms;

	// Caller-declared slot roles for the terms this file builds: the formatted-string family bypasses the rule table
	// (FormattedStringMethod.CheckArgumentType throws), so the construction sites state what they guarantee. Slot 0 is
	// always the filled operand; the width / LEFT-RIGHT slots are written as explicit nulls when absent and are
	// emptiness-tested by the body, so they carry no claim.
	private static readonly FunctionMethod.SlotRole[] YenAtRoles =
	[
		FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int,
		FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String,
		FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String
	];

	private static readonly FunctionMethod.SlotRole[] IntFormatRoles =
	[
		FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.Int,
		FunctionMethod.SlotRole.Loose,
		FunctionMethod.SlotRole.Loose
	];

	private static readonly FunctionMethod.SlotRole[] StrFormatRoles =
	[
		FunctionMethod.SlotRole.Required | FunctionMethod.SlotRole.String,
		FunctionMethod.SlotRole.Loose,
		FunctionMethod.SlotRole.Loose
	];

	/// <summary>
	/// Returns the int operand of the single {…} interpolation when funcnameTerm is fixed
	/// literals plus exactly one {int-expr} ("PREFIX_{I}" or a bare "{I}"); null otherwise.
	/// The operand value fully determines the built name, so a cache can key on it without
	/// building any string. Alignment specifiers ({I,4,LEFT}) keep the name deterministic
	/// and are included in this form.
	/// </summary>
	internal static AExpression? SingleIntFormTerm(AExpression funcnameTerm)
	{
		if (funcnameTerm is StrFormTerm formTerm)
		{
			if (formTerm.StrForm.terms.Length != 1)
			{
				return null;
			}

			funcnameTerm = formTerm.StrForm.terms[0];
		}

		if (funcnameTerm is FunctionMethodTerm fmt
		    && ReferenceEquals(fmt.Method, formatCurlyBrace)
		    && fmt.Slots.TermOrNull(0) is { IsInteger: true } operand)
		{
			return operand;
		}

		return null;
	}

	#region static

	private static readonly FormattedStringMethod formatCurlyBrace;
	private static readonly FormattedStringMethod formatPercent;
	private static readonly FormattedStringMethod formatYenAt;
	private static readonly FunctionMethodTerm NameTarget; // "***"
	private static readonly FunctionMethodTerm CallnameMaster; // "+++"
	private static readonly FunctionMethodTerm CallnamePlayer; // "==="
	private static readonly FunctionMethodTerm NameAssi; // "///"
	private static readonly FunctionMethodTerm CallnameTarget; // "$$$"

	// Runs exactly once, before the first static member access or instance creation.
	// The earliest access in this codebase is FromWordToken during ERH/ERB parsing,
	// which runs after GlobalStatic.VariableData is created in Process.Initialize;
	// touching this class earlier is an initialization-order bug (a static constructor never reruns).
	static StrForm()
	{
		formatCurlyBrace = new FormatCurlyBrace();
		formatPercent = new FormatPercent();
		formatYenAt = new FormatYenAt();
		VariableToken nameID = GlobalStatic.VariableData.GetSystemVariableToken("NAME");
		VariableToken callnameID = GlobalStatic.VariableData.GetSystemVariableToken("CALLNAME");
		AExpression[] zeroArg = [new SingleLongTerm(0)];
		VariableTerm target = new(GlobalStatic.VariableData.GetSystemVariableToken("TARGET"), zeroArg);
		VariableTerm master = new(GlobalStatic.VariableData.GetSystemVariableToken("MASTER"), zeroArg);
		VariableTerm player = new(GlobalStatic.VariableData.GetSystemVariableToken("PLAYER"), zeroArg);
		VariableTerm assi = new(GlobalStatic.VariableData.GetSystemVariableToken("ASSI"), zeroArg);

		VariableTerm nametarget = new(nameID, [target]);
		VariableTerm callnamemaster = new(callnameID, [master]);
		VariableTerm callnameplayer = new(callnameID, [player]);
		VariableTerm nameassi = new(nameID, [assi]);
		VariableTerm callnametarget = new(callnameID, [target]);
		NameTarget = new FunctionMethodTerm(formatPercent,
			FunctionMethod.ArgumentSlots.Trusted([nametarget, null, null], StrFormatRoles));
		CallnameMaster = new FunctionMethodTerm(formatPercent,
			FunctionMethod.ArgumentSlots.Trusted([callnamemaster, null, null], StrFormatRoles));
		CallnamePlayer = new FunctionMethodTerm(formatPercent,
			FunctionMethod.ArgumentSlots.Trusted([callnameplayer, null, null], StrFormatRoles));
		NameAssi = new FunctionMethodTerm(formatPercent,
			FunctionMethod.ArgumentSlots.Trusted([nameassi, null, null], StrFormatRoles));
		CallnameTarget = new FunctionMethodTerm(formatPercent,
			FunctionMethod.ArgumentSlots.Trusted([callnametarget, null, null], StrFormatRoles));
	}

	public static StrForm FromWordToken(StrFormWord wt)
	{
		AExpression[] termArray = new AExpression[wt.SubWords.Length];
		for (int i = 0; i < wt.SubWords.Length; i++)
		{
			SubWord SWT = wt.SubWords[i];
			if (SWT is TripleSymbolSubWord tSymbol)
			{
				switch (tSymbol.Code)
				{
					case '*':
						termArray[i] = NameTarget;
						continue;
					case '+':
						termArray[i] = CallnameMaster;
						continue;
					case '=':
						termArray[i] = CallnamePlayer;
						continue;
					case '/':
						termArray[i] = NameAssi;
						continue;
					case '$':
						termArray[i] = CallnameTarget;
						continue;
				}

				throw new ExeEE(trerror.StrFormUnexpected.Text);
			}

			WordCollection? wc;
			AExpression? operand;
			if (SWT is YenAtSubWord yenat)
			{
				wc = yenat.Words;
				if (wc != null)
				{
					operand = ExpressionParser.ReduceIntegerTerm(wc, TermEndWith.EoL);
					if (!wc.EOL)
					{
						throw new CodeEE(trerror.AbnormalFirstOperand.Text);
					}
				}
				else
				{
					operand = new SingleLongTerm(0);
				}

				AExpression left = new StrFormTerm(FromWordToken(yenat.Left));
				AExpression right = yenat.Right == null ? new SingleStrTerm("") : new StrFormTerm(FromWordToken(yenat.Right));

				termArray[i] = new FunctionMethodTerm(formatYenAt,
					FunctionMethod.ArgumentSlots.Trusted([operand, left, right], YenAtRoles));
				continue;
			}

			// TripleSymbolSubWord and YenAtSubWord are handled above, and their own branches do not
			// reach here; the remaining %/{} subwords are built from an analysed WordCollection.
			wc = SWT.Words!;
			operand = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.Comma);
			if (operand == null)
			{
				if (SWT is CurlyBraceSubWord)
				{
					throw new CodeEE(trerror.EmptyBrace.Text);
				}

				throw new CodeEE(trerror.EmptyPer.Text);
			}

			AExpression? second = null;
			SingleTerm? third = null;
			wc.ShiftNext();
			if (!wc.EOL)
			{
				second = ExpressionParser.ReduceIntegerTerm(wc, TermEndWith.Comma);

				wc.ShiftNext();
				if (!wc.EOL)
				{
					if (wc.Current is not IdentifierWord id)
					{
						throw new CodeEE(trerror.NotSpecifiedLR.Text);
					}

					if (string.Equals(id.Code, "LEFT", Config.Config.StringComparison)) //標準RIGHT
					{
						third = new SingleLongTerm(1);
					}
					else if (!string.Equals(id.Code, "RIGHT", Config.Config.StringComparison))
					{
						throw new CodeEE(trerror.OtherThanLR.Text);
					}

					wc.ShiftNext();
				}

				if (!wc.EOL)
				{
					throw new CodeEE(trerror.ExtraCharacterLR.Text);
				}
			}

			if (SWT is CurlyBraceSubWord)
			{
				if (!operand.IsInteger)
				{
					throw new CodeEE(trerror.IsNotNumericBrace.Text);
				}

				termArray[i] = new FunctionMethodTerm(formatCurlyBrace,
					FunctionMethod.ArgumentSlots.Trusted([operand, second, third], IntFormatRoles));
				continue;
			}

			if (!operand.IsString)
			{
				throw new CodeEE(trerror.IsNotStringPer.Text);
			}

			termArray[i] = new FunctionMethodTerm(formatPercent,
				FunctionMethod.ArgumentSlots.Trusted([operand, second, third], StrFormatRoles));
		}

		return new StrForm(wt.Strs, termArray);
	}

	#endregion

	public bool IsConst => strs.Length == 1;

	public AExpression? GetAExpression()
	{
		return strs is [{ Length: 0 }, { Length: 0 }] ? terms[0] : null;
	}

	public void Restructure(ExpressionMediator exm)
	{
		if (strs.Length == 1)
		{
			return;
		}

		bool canRestructure = false;
		for (int i = 0; i < terms.Length; i++)
		{
			terms[i] = terms[i].Restructure(exm);
			if (terms[i] is SingleTerm)
			{
				canRestructure = true;
			}
		}

		if (!canRestructure)
		{
			return;
		}

		List<string> strList = [.. strs];
		List<AExpression> termList = [.. terms];
		for (int i = 0; i < termList.Count; i++)
		{
			if (termList[i] is not SingleTerm single)
			{
				continue;
			}

			string str = single.GetStrValue(exm);
			strList[i] = strList[i] + str + strList[i + 1];
			termList.RemoveAt(i);
			strList.RemoveAt(i + 1);
			i--;
		}

		strs = [.. strList];
		terms = [.. termList];
	}

	/// <summary>
	/// Reads a form whose parts cannot reach the context (an unfoldable one would have been handled by
	/// the caller); kept next to <see cref="GetString(ExpressionMediator)"/> for the constant-fold path.
	/// </summary>
	public string GetString()
	{
		return GetString(null!);
	}

	public string GetString(ExpressionMediator exm)
	{
		if (strs.Length == 1)
		{
			return strs[0];
		}

		DefaultInterpolatedStringHandler handler = new(strs.Length + terms.Length, 0);

		for (int i = 0; i < strs.Length - 1; i++)
		{
			handler.AppendLiteral(strs[i]);
			handler.AppendLiteral(terms[i].GetStrValue(exm));
		}

		handler.AppendLiteral(strs[^1]);
		return handler.ToString();
	}

	#region FormattedStringMethod 書式付文字列の内部

	private abstract class FormattedStringMethod : FunctionMethod
	{
		public FormattedStringMethod()
		{
			CanRestructure = true;
			ReturnType = typeof(string);
		}

		internal override string CheckArgumentType(string name, List<AExpression?> arguments, out int matchedIdx)
		{
			throw new ExeEE(trerror.TypeCheckIsCallersResponsibility.Text);
		}

		public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			throw new ExeEE(trerror.ReturnTypeMismatch.Text);
		}

		public override SingleTerm GetReturnValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return new SingleStrTerm(GetStrValue(exm, slots));
		}
	}

	private sealed class FormatCurlyBrace : FormattedStringMethod
	{
		public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			string ret = slots.Int(exm, 0).ToString();
			if (!slots.Has(1))
			{
				return ret;
			}

			// Slot 2 selects LEFT (non-empty) or RIGHT (empty/omitted).
			return slots.Has(2)
				? ret.PadRight((int)slots.Int(exm, 1), ' ') //LEFT
				: ret.PadLeft((int)slots.Int(exm, 1), ' '); //RIGHT
		}
	}

	private sealed class FormatPercent : FormattedStringMethod
	{
		public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			string ret = slots.Str(exm, 0);
			if (!slots.Has(1))
			{
				return ret;
			}

			int totalLength = (int)slots.Int(exm, 1);
			int currentLength = LangManager.GetStrlenLang(ret);
			totalLength -= currentLength - ret.Length; //全角文字の数だけマイナス。タブ文字？ゼロ幅文字？知るか！
			if (totalLength < ret.Length)
			{
				return ret; //PadLeftは0未満を送ると例外を投げる
			}

			// Slot 2 selects LEFT (non-empty) or RIGHT (empty/omitted).
			return slots.Has(2)
				? ret.PadRight(totalLength, ' ') //LEFT
				: ret.PadLeft(totalLength, ' '); //RIGHT
		}
	}

	private sealed class FormatYenAt : FormattedStringMethod
	{
		//Operator のTernaryIntStrStrとやってることは同じ
		public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
		{
			return slots.Int(exm, 0) != 0 ? slots.Str(exm, 1) : slots.Str(exm, 2);
		}
	}

	#endregion
}
