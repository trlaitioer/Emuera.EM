#nullable enable

namespace MinorShift.Emuera.Runtime.Script.Parser;

/// <summary>
/// FormattedStringWTの中身用のトークン
/// </summary>
internal abstract class SubWord
{
	protected SubWord(WordCollection? w)
	{
		words = w;
	}

	private readonly WordCollection? words;
	public WordCollection? Words => words;
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
		code = c;
	}

	private readonly char code;
	public char Code => code;
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
		left = fsLeft;
		right = fsRight;
	}

	private readonly StrFormWord left;
	private readonly StrFormWord? right;
	public StrFormWord Left => left;
	public StrFormWord? Right => right;

	public override void SetIsMacro()
	{
		IsMacro = true;
		Words!.SetIsMacro();
		Left.SetIsMacro();
		Right!.SetIsMacro(); //Right is null only on the missing-# warning path, which never reaches macro expansion
	}
}