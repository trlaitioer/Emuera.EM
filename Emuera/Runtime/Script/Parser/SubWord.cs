namespace MinorShift.Emuera.Runtime.Script.Parser;

/// <summary>
/// FormattedStringWTの中身用のトークン
/// </summary>
internal abstract class SubWord
{
	protected SubWord(WordCollection? w)
	{
		Words = w;
	}

	public WordCollection? Words { get; }

	public bool IsMacro;

	public virtual void SetIsMacro()
	{
		IsMacro = true;
		Words?.SetIsMacro();
	}
}

internal sealed class TripleSymbolSubWord : SubWord
{
	public TripleSymbolSubWord(char c) : base(null)
	{
		Code = c;
	}

	public char Code { get; }
}

internal sealed class CurlyBraceSubWord : SubWord
{
	public CurlyBraceSubWord(WordCollection w) : base(w)
	{ }
}

internal sealed class PercentSubWord : SubWord
{
	public PercentSubWord(WordCollection w) : base(w)
	{ }
}

internal sealed class YenAtSubWord : SubWord
{
	public YenAtSubWord(WordCollection w, StrFormWord fsLeft, StrFormWord? fsRight)
		: base(w)
	{
		Left = fsLeft;
		Right = fsRight;
	}

	public StrFormWord Left { get; }

	public StrFormWord? Right { get; }

	public override void SetIsMacro()
	{
		IsMacro = true;
		Words!.SetIsMacro();
		Left.SetIsMacro();
		Right!.SetIsMacro(); //Right is null only on the missing-# warning path, which never reaches macro expansion
	}
}
