using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements.Function;

internal abstract class FunctionMethod
{
	public Type ReturnType { get; protected init; } = null!;
	protected Type[]? argumentTypeArray;
	protected string Name { get; private set; } = null!;

	#region EM_私家版_Emuera多言語化改造

	[Flags]
	protected enum ArgType
	{
		Invalid = 0,

		Any = 1,
		Int = 1 << 1,
		String = 1 << 2,
		Ref = 1 << 3,
		Array = 1 << 4,
		Array1D = 1 << 5,
		Array2D = 1 << 6,
		Array3D = 1 << 7,
		Variadic = 1 << 8,
		SameAsFirst = 1 << 9,
		CharacterData = Ref | (1 << 10),
		AllowConstRef = 1 << 11,
		// Marks a slot inside the omittable region (i >= OmitStart) that the implementation still
		// requires: omitting it by count stays legal, but leaving it empty is an error.
		NotNull = 1 << 12,

		RefInt = Ref | Int,
		RefAny = Ref | Any,
		RefString = Ref | String,
		RefAnyArray = RefAny | Array,
		RefIntArray = RefInt | Array,
		RefStringArray = RefString | Array,
		RefAny1D = RefAny | Array1D,
		RefInt1D = RefInt | Array1D,
		RefString1D = RefString | Array1D,
		RefAny2D = RefAny | Array2D,
		RefInt2D = RefInt | Array2D,
		RefString2D = RefString | Array2D,
		RefAny3D = RefAny | Array3D,
		RefInt3D = RefInt | Array3D,
		RefString3D = RefString | Array3D,

		VariadicAny = Variadic | Any,
		VariadicInt = Variadic | Int,
		VariadicString = Variadic | String,
		VariadicSameAsFirst = Variadic | SameAsFirst
	}

	/// <summary>
	/// Per-slot contract resolved at check time: what the <see cref="ArgumentSlots"/> accessors may claim about a
	/// slot. <see cref="Loose"/> is the absence of a provable claim (an unchecked variadic slot, a class without a
	/// rule table, or a caller-declared slot that guarantees nothing). Only <see cref="Required"/> carries a
	/// filled-ness claim; the type flags describe the operand type the checker matched (also for an omittable slot,
	/// where they read conditionally: "if filled, numeric"). The accessors do not assert on a role - the claim is the
	/// caller's - and <see cref="ArgumentSlots.Term"/> names the role in its failure message when a read breaks it.
	/// </summary>
	[Flags]
	internal enum SlotRole
	{
		Loose = 0,

		// The checker rejects an explicitly empty slot here (i < OmitStart, ArgType.NotNull, the SameAsFirst slot0
		// case, or a fixed-count table), so the slot is non-null whenever it is present. Presence is a separate
		// guarantee the flag does not carry: an omittable slot past the block's minimum count needs a Count guard.
		Required = 1 << 0,
		Int = 1 << 1,
		String = 1 << 2,
		Any = 1 << 3,

		// Reference slot: the argument is a variable entity rather than a value.
		Ref = 1 << 4,

		// Reference slot that admits a constant array. nullable-migration-18 then requires the class
		// to set HasUniqueRestructure, otherwise the constant array is folded into a SingleTerm and the cast fails.
		// Exempt: SumArrayMethod's character-data slot, where CONST and CHARADATA are mutually exclusive so a constant
		// array never reaches the fold (the exemption is recorded in tracker/arg-contract/arg-slots.py).
		AllowConstRef = 1 << 5,

		// Slot belongs to a variadic tail: governed by LastVariadics, not by the slot number.
		Variadic = 1 << 6,
	}

	protected sealed class _ArgType
	{
		public _ArgType(ArgType t)
		{
			type = t;
		}

		public Type Type => Int ? typeof(long) : typeof(string);
		public readonly ArgType type = ArgType.Invalid;
		public bool AllowConstRef => (type & ArgType.AllowConstRef) != 0;
		public bool NotNull => (type & ArgType.NotNull) != 0;
		public bool Ref => (type & ArgType.Ref) != 0;
		public bool Any => (type & ArgType.Any) != 0;
		public bool Int => (type & ArgType.Int) != 0;
		public bool Array => (type & ArgType.Array) != 0;
		public bool Array1D => (type & ArgType.Array1D) != 0;
		public bool Array2D => (type & ArgType.Array2D) != 0;
		public bool Array3D => (type & ArgType.Array3D) != 0;
		public bool String => (type & ArgType.String) != 0;
		public bool Variadic => (type & ArgType.Variadic) != 0;
		public bool SameAsFirst => (type & ArgType.SameAsFirst) != 0;
		public bool CharacterData => ((int)type & (1 << 10)) != 0;

		public int ArrayDims =>
			Array ? -1
			: Array1D ? 1
			: Array2D ? 2
			: Array3D ? 3 : 0;

		public static implicit operator _ArgType(ArgType value)
		{
			return new _ArgType(value);
		}
	}

	protected sealed class ArgTypeList
	{
		public List<_ArgType> ArgTypes { get; set; } = [];
		public int OmitStart { get; init; } = -1;
		public bool MatchVariadicGroup { get; init; }

		public _ArgType[]? LastVariadics
		{
			get
			{
				int count = 0;
				for (int i = ArgTypes.Count - 1; i >= 0; i--)
				{
					if (!ArgTypes[i].Variadic)
					{
						break;
					}

					count++;
				}

				if (count == 0)
				{
					return null;
				}

				_ArgType[] ret = new _ArgType[count];
				for (int i = 0; i < count; i++)
				{
					ret[i] = ArgTypes[ArgTypes.Count - count + i];
				}

				return ret;
			}
		}

		// True when any slot of the block carries ArgType.SameAsFirst, i.e. when CheckArgumentTypeEx has
		// to read slot 0 to learn the expected operand type. LastVariadics only reprises the trailing
		// ArgTypes entries, so scanning ArgTypes covers a variadic tail as well.
		public bool UsesSameAsFirst
		{
			get
			{
				foreach (_ArgType t in ArgTypes)
				{
					if (t.SameAsFirst)
					{
						return true;
					}
				}

				return false;
			}
		}
	}

	/// <summary>
	/// Slot view produced by the checker (rule-table family) or declared by a discriminating call site (operator /
	/// StrForm / TOSTR wrapper): <c>terms</c> is the check-time snapshot, <c>roles</c> the per-slot contract.
	/// The in-place fold performed after the check (<see cref="FunctionMethodTerm.Restructure"/>) does not write the
	/// snapshot back - it only replaces an element with a value-equal term (a SingleTerm for a statically evaluable
	/// identifier, a FixedVariableTerm for constant indices, which is still a VariableTerm), so a value read through
	/// this view matches the live list's. The evaluation <i>moment</i> is not part of
	/// that equivalence: a call the fold could not fold as a whole keeps the unfolded argument here, so it re-evaluates
	/// an inner call the fold already evaluated once.
	/// </summary>
	internal sealed class ArgumentSlots
	{
		private ArgumentSlots(List<AExpression?> arguments, SlotRole[]? roles)
		{
			// A short role array is not a runtime input: RoleAt would silently downgrade the tail slots to Loose,
			// which reads as "the call site claims nothing" instead of as the authoring bug it is.
			if (roles != null && roles.Length != arguments.Count)
			{
				throw new ArgumentException(
					$"slot roles ({roles.Length}) must cover every argument ({arguments.Count})", nameof(roles));
			}

			FoldTarget = arguments;
			terms = [.. arguments];
			this.roles = roles;
		}

		/// <summary>
		/// The live list the term owns and <see cref="FunctionMethodTerm.Restructure"/> rewrites in place. The
		/// accessors read the check-time snapshot instead, so reading a value here would bypass the snapshot
		/// semantics - this list exists for the fold and for handing the whole call on (<see cref="Tail"/>).
		/// </summary>
		internal List<AExpression?> FoldTarget { get; }

		private readonly AExpression?[] terms;
		private readonly SlotRole[]? roles;

		/// <summary>Rule-table family: roles are projected from the rule block the checker matched.</summary>
		internal static ArgumentSlots Checked(List<AExpression?> arguments, SlotRole[] roles) => new(arguments, roles);

		/// <summary>Caller-declared family: <paramref name="roles"/> is null when the call site claims nothing.</summary>
		internal static ArgumentSlots Trusted(List<AExpression?> arguments, SlotRole[]? roles) => new(arguments, roles);

		internal int Count => terms.Length;

		/// <summary>True when the view carries a declared contract (rule-table family or a caller that stated roles).</summary>
		internal bool HasDeclaredRoles => roles != null;

		/// <summary>
		/// The contract declared for <paramref name="index"/>, or <see cref="SlotRole.Loose"/> when the view declares
		/// nothing (an index outside the declared size included: the claim describes the slot, not the caller's
		/// index). <see cref="Term"/> reads it to name the broken promise in its failure message.
		/// </summary>
		internal SlotRole RoleAt(int index) =>
			roles != null && index >= 0 && index < roles.Length ? roles[index] : SlotRole.Loose;

		/// <summary>The slot holds a value. A negative index holds nothing, like an index past the list.</summary>
		internal bool Has(int index) => (uint)index < (uint)terms.Length && terms[index] != null;

		/// <summary>The slot was written as an explicitly empty argument.</summary>
		internal bool IsEmpty(int index) => (uint)index < (uint)terms.Length && terms[index] == null;

		/// <summary>The argument list is shorter than the slot, an index before the first slot included.</summary>
		internal bool IsMissing(int index) => (uint)index >= (uint)terms.Length;

		/// <summary>
		/// Slot value. The caller owes <b>two</b> proofs, and <see cref="SlotRole.Required"/> only carries the second:
		/// the slot is <b>present</b> (guarded by <see cref="Count"/>/<see cref="IsMissing"/>, or an index below the
		/// matched block's minimum argument count) and it is <b>not explicitly empty</b> (role
		/// <see cref="SlotRole.Required"/>, or an explicit <see cref="Has"/>/<see cref="IsEmpty"/>/<see cref="IsMissing"/>
		/// test) - hence the non-null read, whose failure <see cref="Term"/> reports.
		/// </summary>
		internal long Int(ExpressionMediator exm, int index) => Term(index).GetIntValue(exm);

		/// <inheritdoc cref="Int"/>
		internal string Str(ExpressionMediator exm, int index) => Term(index).GetStrValue(exm);

		/// <inheritdoc cref="Int"/>
		internal bool IsInteger(int index) => Term(index).IsInteger;

		/// <inheritdoc cref="Int"/>
		internal bool IsString(int index) => Term(index).IsString;

		/// <summary>
		/// Variable entity behind a reference slot. Preconditions: the slot is present (as for <see cref="Int"/> - a
		/// reference slot past the block's minimum count still needs a Count guard), role <see cref="SlotRole.Ref"/>,
		/// and a non-constant argument (a rule carrying <see cref="SlotRole.AllowConstRef"/> implies the class sets
		/// HasUniqueRestructure, so the constant array is not folded into a SingleTerm).
		/// The matched rule also pins the entity's operand type, and its array rank whenever the rule names one:
		/// <see cref="CheckArgumentTypeEx"/> tested both against the argument's Identifier, which is what lets a body
		/// read Identifier.GetArray() through the array type the rule declares.
		/// </summary>
		internal VariableTerm Var(int index) => Term(index) as VariableTerm ?? throw new ExeEE(
			$"slot {index} (role {RoleAt(index)}) is not a variable entity: the slot must be a reference slot");

		/// <summary>
		/// Raw handle. An index outside the argument list is a programmer error and throws
		/// <see cref="IndexOutOfRangeException"/>, as <c>ArgumentListExtensions.Required</c> did. An index the checker
		/// lets hold an explicitly empty slot (or a <see cref="Var"/> slot that holds no variable entity) reports
		/// itself through <see cref="ExeEE"/> rather than handing null to the caller: only the body can know whether
		/// it guarded that slot, and the role names what the call site promised.
		/// </summary>
		internal AExpression Term(int index)
		{
			if ((uint)index >= (uint)terms.Length)
			{
				throw new IndexOutOfRangeException($"slot {index} of {terms.Length} (role {RoleAt(index)})");
			}

			return terms[index] ?? throw new ExeEE(
				$"slot {index} (role {RoleAt(index)}) is explicitly empty: the body must test "
				+ "Has/IsEmpty/IsMissing before reading it");
		}

		/// <summary>Raw handle, or null when the slot is empty or omitted.</summary>
		internal AExpression? TermOrNull(int index) => (uint)index < (uint)terms.Length ? terms[index] : null;

		/// <summary>
		/// Slots from <paramref name="from"/> on, forwarded to a downstream callee that owns their contract. The
		/// snapshot is what gets forwarded, as everywhere else in this view, so the callee re-evaluates them itself
		/// instead of inheriting the fold's results (see the class summary).
		/// </summary>
		internal List<AExpression?> Tail(int from)
		{
			// A forwarded tail starts past the end whenever the caller omitted it (GETMETH with one argument).
			int start = Math.Max(0, from);
			return start >= terms.Length ? [] : [.. terms[start..]];
		}
	}

	protected ArgTypeList[]? argumentTypeArrayEx;

	// The one judgement the checker and the slot projection both ask: does this block reject an explicitly empty
	// slot? OmitStart stays -1 when a rule block omits it, and the count check then demands exactly ArgTypes.Count
	// arguments, so no slot may be omitted. Past OmitStart a slot is omittable by count only, and ArgType.NotNull
	// (or the SameAsFirst slot-0 case, which reads slot 0 to learn the expected operand type) is what rejects an
	// explicitly empty one, because the implementation reads the slot anyway.
	private static bool RejectsEmptySlot(ArgTypeList list, _ArgType rule, int index)
		=> list.OmitStart < 0 || index < list.OmitStart || rule.NotNull
			|| (index == 0 && list.OmitStart == 0 && list.UsesSameAsFirst);

	// The rule governing slot `index` of a matched block: a variadic tail repeats the block's trailing variadic
	// rules by modulo. The caller hoists LastVariadics because that property allocates on every read.
	// Precondition (both callers): index < ArgTypes.Count unless lastVariadics is non-null.
	private static _ArgType RuleForSlot(ArgTypeList list, _ArgType[]? lastVariadics, int index)
		=> lastVariadics != null && index >= list.ArgTypes.Count
			? lastVariadics[(index - list.ArgTypes.Count) % lastVariadics.Length]
			: list.ArgTypes[index];

	// Slot 0's operand type, the reference `SameAsFirst` rules compare against. Hoisted per rule block because the
	// check loops ask it once per slot; an empty slot 0 falls back to the default the SameAsFirst rules assume.
	private static Type FirstOperandType(List<AExpression?> arguments)
		=> arguments.Count > 0 && arguments[0] != null ? arguments.Required(0).GetOperandType() : typeof(long);

	// The operand type a slot is expected to hold, or null when the rule accepts either type (ArgType.Any). The
	// checker compares an argument against this and the slot projection turns it into the Int/String/Any role flag,
	// so the two judgements are one judgement instead of two copies that can drift apart.
	private static Type? ExpectedType(_ArgType rule, Type firstType)
		=> rule.Any ? null : rule.SameAsFirst ? firstType : rule.Type;

	//引数の数・型が一致するかどうかのテスト
	//正しくない場合はエラーメッセージを返す。
	//引数の数が不定である場合や引数の省略を許す場合にはoverrideすること。
	// matchedIdx = the rule block that passed (-1 when none did); the slot projection is built from that block.
	private string? CheckArgumentTypeEx(string name, List<AExpression?> arguments, out int matchedIdx)
	{
		matchedIdx = -1;
		// Only reached from CheckArgumentType after the null check on argumentTypeArrayEx.
		ArgTypeList[] rules = argumentTypeArrayEx!;
		string[] errMsg = new string[rules.Length];
		for (int idx = 0; idx < rules.Length; idx++)
		{
			ArgTypeList list = rules[idx];
			_ArgType[]? vs = list.LastVariadics;
			bool variadic = vs != null;
			bool argsNotMoreThanRule = variadic || arguments.Count <= list.ArgTypes.Count;
			bool argsNotLessThanRule = list.OmitStart > -1 ? arguments.Count >= list.OmitStart : arguments.Count >= list.ArgTypes.Count;
			if (argsNotMoreThanRule && argsNotLessThanRule)
			{
				if (list.MatchVariadicGroup && vs != null)
				{
					int variadicGroupStart = list.ArgTypes.Count - vs.Length;
					if (arguments.Count > variadicGroupStart && (arguments.Count - variadicGroupStart) % vs.Length != 0)
					{
						errMsg[idx] = string.Format(trerror.ArgsNotFitExpr.Text, name, arguments.Count, variadicGroupStart, vs.Length);
						continue;
					}
				}

				// 引数の数が有効
				Type firstType = FirstOperandType(arguments);
				for (int i = 0; i < (variadic ? arguments.Count : Math.Min(arguments.Count, list.ArgTypes.Count)); i++)
				{
					_ArgType rule = RuleForSlot(list, vs, i);
					if (arguments[i] == null)
					{
						if (RejectsEmptySlot(list, rule, i))
						{
							errMsg[idx] = string.Format(trerror.ArgCanNotBeNull.Text, name, i + 1);
							break;
						}
						else
						{
							continue;
						}
					}

					Type? expected = ExpectedType(rule, firstType);
					bool typeNotMatch = expected != null && expected != arguments.Required(i).GetOperandType();
					if (rule.Ref)
					{
						if (rule.CharacterData && (!(arguments[i] is VariableTerm cvarTerm) || !cvarTerm.Identifier.IsCharacterData))
						{
							// キャラ変数ではない
							errMsg[idx] = string.Format(trerror.ArgIsNotCharacterVar.Text, name, i + 1);
							break;
						}

						// 引数の型が違う
						bool error = false;
						string errText;
						int dims = rule.ArrayDims;
						switch (dims)
						{
							case 0:
							{
								// 普通の場合
								Lang.TranslatableString err = rule.String ? trerror.ArgIsNotStrVar
									: rule.Int ? trerror.ArgIsNotIntVar : trerror.ArgIsNotVar;
								errText = string.Format(err.Text, name, i + 1);
								break;
							}
							case -1:
							{
								// 任意配列の場合
								Lang.TranslatableString err = rule.String ? trerror.ArgIsNotStrArray
									: rule.Int ? trerror.ArgIsNotIntArray : trerror.ArgIsNotArray;
								errText = string.Format(err.Text, name, i + 1);
								break;
							}
							default:
							{
								// 1-3次元配列の場合
								Lang.TranslatableString err = rule.String ? trerror.ArgIsNotNDStrArray
									: rule.Int ? trerror.ArgIsNotNDIntArray : trerror.ArgIsNotNDArray;
								errText = string.Format(err.Text, name, i + 1, dims);
								break;
							}
						}

						// 引数が引用系
						if (arguments[i] is VariableTerm varTerm &&
						    !(varTerm.Identifier.IsCalc || (!rule.AllowConstRef && varTerm.Identifier.IsConst)))
						{
							// 変数の場合
							switch (dims)
							{
								case 0: error = typeNotMatch; break;
								case -1:
									error = varTerm.Identifier is { IsArray1D: false, IsArray2D: false, IsArray3D: false } ||
									        typeNotMatch; break;
								case 1: error = !varTerm.Identifier.IsArray1D || typeNotMatch; break;
								case 2: error = !varTerm.Identifier.IsArray2D || typeNotMatch; break;
								case 3: error = !varTerm.Identifier.IsArray3D || typeNotMatch; break;
							}
						}
						else
						{
							error = true; // 変数ではない
						}

						if (error)
						{
							errMsg[idx] = errText;
							break;
						}
					}
					else if (typeNotMatch)
					{
						Type type = ExpectedType(rule, firstType) ?? rule.Type;
						// 引数の型が違う
						errMsg[idx] = type == typeof(string)
							? string.Format(trerror.ArgIsNotStr.Text, name, i + 1)
							: string.Format(trerror.ArgIsNotInt.Text, name, i + 1);
						break;
					}
				}

				if (errMsg[idx] == null)
				{
					matchedIdx = idx;
					return null;
				}
			}
			else if (list is { OmitStart: -1, ArgTypes.Count: > 0 } && !variadic)
			{
				// 数固定の引数が必要
				if (list.ArgTypes.Count > 0)
				{
					errMsg[idx] = string.Format(trerror.ArgsCountNotMatches.Text, name, list.ArgTypes.Count, arguments.Count);
				}
				else
				{
					errMsg[idx] = string.Format(trerror.ArgsNotNeeded.Text, name);
				}
			}
			// 可変長引数
			else if (!argsNotMoreThanRule)
			{
				// 引数が多すぎる
				errMsg[idx] = string.Format(trerror.TooManyFuncArgs.Text, name);
			}
			else
			{
				// 引数が足りない
				errMsg[idx] = string.Format(trerror.NotEnoughArgs.Text, name, list.OmitStart < 0 ? list.ArgTypes.Count : list.OmitStart);
			}
		}

		if (rules.Length == 1)
		{
			return errMsg[0];
		}

		StringBuilder sb = new();
		for (int i = 0; i < errMsg.Length; i++)
		{
			sb.Append(string.Format(trerror.NotValidArgsReason.Text, i + 1, errMsg[i]));
			if (i + 1 < errMsg.Length)
			{
				sb.Append(" | ");
			}
		}

		return string.Format(trerror.NotValidArgs.Text, name, sb);
	}

	/// <summary>
	/// The one checker pass. <paramref name="matchedIdx"/> reports the rule block that passed (-1 when none did),
	/// which the slot projection then reads: a caller resolves both the message and the roles from one call instead
	/// of asking the checker twice. Subclasses that check for themselves override this overload, so the resolution
	/// path cannot bypass them.
	/// </summary>
	internal virtual string? CheckArgumentType(string name, List<AExpression?> arguments, out int matchedIdx)
	{
		matchedIdx = -1;
		if (argumentTypeArrayEx != null)
		{
			return CheckArgumentTypeEx(name, arguments, out matchedIdx);
		}
		else if (argumentTypeArray != null)
		{
			if (arguments.Count != argumentTypeArray.Length)
				// return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNum0, name);
			{
				if (argumentTypeArray.Length > 0)
				{
					return string.Format(trerror.ArgsCountNotMatches.Text, name, argumentTypeArray.Length, arguments.Count);
				}
				else
				{
					return string.Format(trerror.ArgsNotNeeded.Text, name);
				}
			}

			for (int i = 0; i < argumentTypeArray.Length; i++)
			{
				if (arguments[i] == null)
					// return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentNotNullable0, name, i + 1);
				{
					return string.Format(trerror.ArgCanNotBeNull.Text, name, i + 1);
				}

				if (argumentTypeArray[i] != arguments.Required(i).GetOperandType())
					// return string.Format(Properties.Resources.SyntaxErrMesMethodDefaultArgumentType0, name, i + 1);
				{
					return argumentTypeArray[i] == typeof(string)
						? string.Format(trerror.ArgIsNotStr.Text, name, i + 1)
						: string.Format(trerror.ArgIsNotInt.Text, name, i + 1);
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Runs the checker and resolves the term's slot view from that same pass: the rule-table family gets the roles
	/// projected from the block that matched, anything else (a class without a rule table, or one that reports its
	/// own error) claims nothing. Callers throw <paramref name="errmes"/> when it is non-null.
	/// </summary>
	internal string? CheckAndResolveSlots(string name, List<AExpression?> arguments, out ArgumentSlots slots)
	{
		string? errmes = CheckArgumentType(name, arguments, out int matchedIdx);
		slots = ResolveSlots(arguments, matchedIdx);
		return errmes;
	}

	// The one factory behind every term construction: the roles come from the rule block the checker matched.
	private ArgumentSlots ResolveSlots(List<AExpression?> arguments, int matchedIdx)
	{
		if (argumentTypeArrayEx != null)
		{
			return matchedIdx < 0
				? ArgumentSlots.Trusted(arguments, null)
				: ArgumentSlots.Checked(arguments, ProjectRoles(argumentTypeArrayEx[matchedIdx], arguments));
		}

		if (argumentTypeArray != null)
		{
			return ArgumentSlots.Checked(arguments, ProjectFixedRoles(argumentTypeArray, arguments.Count));
		}

		return ArgumentSlots.Trusted(arguments, null);
	}

	// A fixed-count table rejects an empty slot as well as an argument count mismatch, so every slot is required.
	private static SlotRole[] ProjectFixedRoles(Type[] table, int count)
	{
		SlotRole[] roles = new SlotRole[count];
		for (int i = 0; i < roles.Length; i++)
		{
			roles[i] = SlotRole.Required | (i < table.Length && table[i] == typeof(long) ? SlotRole.Int : SlotRole.String);
		}

		return roles;
	}

	// Roles for the block the checker matched, read through the same helpers CheckArgumentTypeEx uses so the two
	// judgements cannot drift apart.
	private static SlotRole[] ProjectRoles(ArgTypeList matched, List<AExpression?> arguments)
	{
		_ArgType[]? vs = matched.LastVariadics;
		Type firstType = FirstOperandType(arguments);
		SlotRole[] roles = new SlotRole[arguments.Count];
		for (int i = 0; i < roles.Length; i++)
		{
			_ArgType rule = RuleForSlot(matched, vs, i);
			SlotRole role = RejectsEmptySlot(matched, rule, i) ? SlotRole.Required : SlotRole.Loose;
			if (rule.Variadic)
			{
				role |= SlotRole.Variadic;
			}

			if (rule.Ref)
			{
				role |= SlotRole.Ref;
			}

			if (rule.AllowConstRef)
			{
				role |= SlotRole.AllowConstRef;
			}

			Type? expected = ExpectedType(rule, firstType);
			role |= expected == null ? SlotRole.Any : expected == typeof(string) ? SlotRole.String : SlotRole.Int;
			roles[i] = role;
		}

		return roles;
	}

	#endregion

	//Argumentが全て定数の時にMethodを解体してよいかどうか。RANDやCharaを参照するものなどは不可
	public bool CanRestructure { get; protected set; }

	//FunctionMethodが固有のRestructure()を持つかどうか
	public bool HasUniqueRestructure { get; protected set; }

	/// <summary>
	/// Slot-based entry the term and the function bodies read through. The base throws for a class whose return type
	/// does not match the call.
	/// </summary>
	public virtual long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		throw new ExeEE(trerror.ReturnTypeDifferentOrNotImpelemnt.Text);
	}

	/// <inheritdoc cref="GetIntValue(ExpressionMediator, ArgumentSlots)"/>
	public virtual string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		throw new ExeEE(trerror.ReturnTypeDifferentOrNotImpelemnt.Text);
	}

	/// <inheritdoc cref="GetIntValue(ExpressionMediator, ArgumentSlots)"/>
	public virtual SingleTerm GetReturnValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (ReturnType == typeof(long))
		{
			return new SingleLongTerm(GetIntValue(exm, slots));
		}
		else
		{
			return new SingleStrTerm(GetStrValue(exm, slots));
		}
	}

	/// <summary>
	/// 戻り値は全体をRestructureできるかどうか
	/// </summary>
	/// <param name="exm"></param>
	/// <param name="arguments"></param>
	/// <returns></returns>
	public virtual bool UniqueRestructure(ExpressionMediator exm, List<AExpression?> arguments)
	{
		throw new ExeEE(trerror.NotImplement.Text);
	}


	internal void SetMethodName(string name)
	{
		Name = name;
	}
}
