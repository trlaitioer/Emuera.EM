using MinorShift.Emuera.Runtime.Script.Parser;

namespace MinorShift.Emuera.Runtime.Script.Data;

internal sealed class DefineMacro
{
	public DefineMacro(string key, WordCollection wc, int argcount)
	{
		Keyword = key;
		Statement = wc;
		ArgCount = argcount;
		Statement.PointerReset();
		HasArguments = argcount != 0;
		IDWord = Statement.Collection.Count == 1 ? Statement.Current as IdentifierWord : null;
		IsNull = wc.Collection.Count == 0;
	}

	public readonly string Keyword;
	public readonly int ArgCount;

	public readonly WordCollection Statement;

	// Null unless the macro body is a single identifier word
	public readonly IdentifierWord? IDWord;
	public readonly bool HasArguments;
	public readonly bool IsNull;
}
