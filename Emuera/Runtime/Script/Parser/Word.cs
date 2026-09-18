using MinorShift.Emuera.Runtime.Script.Statements.Expression;

namespace MinorShift.Emuera.Runtime.Script.Parser;

internal abstract class Word
{
	public abstract char Type { get; }
	public bool IsMacro;

	public virtual void SetIsMacro()
	{
		IsMacro = true;
	}
}

internal sealed class NullWord : Word
{
	public override char Type => '\0';

	public override string ToString()
	{
		return "/null/";
	}
}

internal sealed class IdentifierWord : Word
{
	public IdentifierWord(string s)
	{
		code = s;
	}

	private readonly string code;
	public string Code => code;
	public override char Type => 'A';

	public override string ToString()
	{
		return code;
	}
}

internal sealed class LiteralIntegerWord : Word
{
	public LiteralIntegerWord(long i)
	{
		code = i;
	}

	private readonly long code;
	public long Int => code;
	public override char Type => '0';

	public override string ToString()
	{
		return code.ToString();
	}
}

internal sealed class LiteralStringWord : Word
{
	public LiteralStringWord(string s)
	{
		code = s;
	}

	private readonly string code;
	public string Str => code;
	public override char Type => '\"';

	public override string ToString()
	{
		return "\"" + code + "\"";
	}
}

internal sealed class OperatorWord : Word
{
	public OperatorWord(OperatorCode op)
	{
		code = op;
	}

	private readonly OperatorCode code;
	public OperatorCode Code => code;
	public override char Type => '=';

	public override string ToString()
	{
		return code.ToString();
	}
}

internal sealed class SymbolWord : Word
{
	public SymbolWord(char c)
	{
		code = c;
	}

	private readonly char code;
	public override char Type => code;

	public override string ToString()
	{
		return code.ToString();
	}
}

internal sealed class StrFormWord : Word
{
	public StrFormWord(string[] s, SubWord[] SWT)
	{
		Strs = s;
		SubWords = SWT;
	}

	public string[] Strs { get; }

	public SubWord[] SubWords { get; }

	public override char Type => 'F'; //@はSymbolがつかっちゃった

	public override void SetIsMacro()
	{
		IsMacro = true;
		foreach (SubWord subword in SubWords)
		{
			subword.SetIsMacro();
		}
	}
}

internal sealed class MacroWord : Word
{
	public MacroWord(int num)
	{
		this.num = num;
	}

	private readonly int num;
	public int Number => num;
	public override char Type => 'M';

	public override string ToString()
	{
		return "Arg" + num;
	}
}
