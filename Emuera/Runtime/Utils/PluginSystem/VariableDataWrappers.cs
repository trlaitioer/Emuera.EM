using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MinorShift.Emuera.Runtime.Utils.PluginSystem;

public class GlobalInt1dWrapper
{
	public long this[string key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	public long this[long key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	internal GlobalInt1dWrapper(VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public long Get(string key)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		return Get(dict![key]);
	}

	public long Get(long key)
	{
		return token.GetIntValue(exm, [key]);
	}

	public void Set(string key, long value)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		Set(dict![key], value);
	}

	public void Set(long key, long value)
	{
		token.SetValue(value, [key]);
	}

	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class GlobalString1dWrapper
{
	[DisallowNull]
	public string? this[string key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	[DisallowNull]
	public string? this[long key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	internal GlobalString1dWrapper(VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public string? Get(string key)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		return Get(dict![key]);
	}

	public string? Get(long key)
	{
		return token.GetStrValue(exm, [key]);
	}

	public void Set(string key, string value)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		Set(dict![key], value);
	}

	public void Set(long key, string value)
	{
		token.SetValue(value, [key]);
	}

	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class GlobalConstString1dWrapper
{
	public string? this[string key] => Get(key);

	public string? this[long key] => Get(key);

	internal GlobalConstString1dWrapper(VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public string? Get(string key)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		return Get(dict![key]);
	}

	public string? Get(long key)
	{
		return token.GetStrValue(exm, [key]);
	}

	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class GlobalConstInt1dWrapper
{
	public long this[string key] => Get(key);

	public long this[long key] => Get(key);

	internal GlobalConstInt1dWrapper(VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public long Get(string key)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		return Get(dict![key]);
	}

	public long Get(long key)
	{
		return token.GetIntValue(exm, [key]);
	}

	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class CharInt1dWrapper
{
	public long this[string key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	public long this[long key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	internal CharInt1dWrapper(long charId, VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.charId = charId;
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public long Get(string key)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		return Get(dict![key]);
	}

	public long Get(long key)
	{
		return token.GetIntValue(exm, [charId, key]);
	}

	public void Set(string key, long value)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		Set(dict![key], value);
	}

	public void Set(long key, long value)
	{
		token.SetValue(value, [charId, key]);
	}

	private long charId;
	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class CharInt2dWrapper
{
	public long this[string keyA, string keyB]
	{
		get => Get(keyA, keyB);
		set => Set(keyA, keyB, value);
	}

	public long this[long keyA, string keyB]
	{
		get => Get(keyA, keyB);
		set => Set(keyA, keyB, value);
	}

	public long this[string keyA, long keyB]
	{
		get => Get(keyA, keyB);
		set => Set(keyA, keyB, value);
	}

	public long this[long keyA, long keyB]
	{
		get => Get(keyA, keyB);
		set => Set(keyA, keyB, value);
	}

	internal CharInt2dWrapper(long charId, VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.charId = charId;
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public long Get(string keyA, string keyB)
	{
		string? errPos = "";
		Dictionary<string, int>? dictA = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyA);
		Dictionary<string, int>? dictB = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyB);
		return Get(dictA![keyA], dictB![keyB]);
	}

	public long Get(long keyA, string keyB)
	{
		string? errPos = "";
		Dictionary<string, int>? dictB = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyB);
		return Get(keyA, dictB![keyB]);
	}

	public long Get(string keyA, long keyB)
	{
		string? errPos = "";
		Dictionary<string, int>? dictA = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyA);
		return Get(dictA![keyA], keyB);
	}

	public long Get(long keyA, long keyB)
	{
		return token.GetIntValue(exm, [charId, keyA, keyB]);
	}

	public void Set(string keyA, string keyB, long value)
	{
		string? errPos = "";
		Dictionary<string, int>? dictA = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyA);
		Dictionary<string, int>? dictB = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyB);
		Set(dictA![keyA], dictB![keyB], value);
	}

	public void Set(long keyA, string keyB, long value)
	{
		string? errPos = "";
		Dictionary<string, int>? dictB = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyB);
		Set(keyA, dictB![keyB], value);
	}

	public void Set(string keyA, long keyB, long value)
	{
		string? errPos = "";
		Dictionary<string, int>? dictA = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, keyA);
		Set(dictA![keyA], keyB, value);
	}

	public void Set(long keyA, long keyB, long value)
	{
		token.SetValue(value, [charId, keyA, keyB]);
	}

	private long charId;
	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class CharUserdefinedInt1dWrapper
{
	public long this[string key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	public long this[string key, long idx]
	{
		get => Get(key, idx);
		set => Set(key, value, idx);
	}

	internal CharUserdefinedInt1dWrapper(long charId, Dictionary<string, VariableToken> tokens, ExpressionMediator exm, VariableCode variableCode)
	{
		this.charId = charId;
		this.tokens = tokens;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public long Get(string key, long idx = 0)
	{
		if (Config.Config.IgnoreCase)
		{
			key = key.ToUpper();
		}

		return tokens[key].GetIntValue(exm, [charId, idx]);
	}

	public void Set(string key, long value, long idx = 0)
	{
		if (Config.Config.IgnoreCase)
		{
			key = key.ToUpper();
		}

		tokens[key].SetValue(value, [charId, idx]);
	}

	private long charId;
	private Dictionary<string, VariableToken> tokens;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class CharStringWrapper
{
	internal CharStringWrapper(long charId, VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.charId = charId;
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public string? Get()
	{
		return token.GetStrValue(exm, [charId]);
	}

	public void Set(string value)
	{
		token.SetValue(value, [charId]);
	}

	private long charId;
	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}

public class CharString1dWrapper
{
	[DisallowNull]
	public string? this[string key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	[DisallowNull]
	public string? this[long key]
	{
		get => Get(key);
		set => Set(key, value);
	}

	internal CharString1dWrapper(long charId, VariableToken token, ExpressionMediator exm, VariableCode variableCode)
	{
		this.charId = charId;
		this.token = token;
		this.exm = exm;
		this.variableCode = variableCode;
	}

	public string? Get(string key)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		return Get(dict![key]);
	}

	public string? Get(long key)
	{
		return token.GetStrValue(exm, [charId, key]);
	}

	public void Set(string key, string value)
	{
		string? errPos = "";
		Dictionary<string, int>? dict = exm.VEvaluator.Constant.GetKeywordDictionary(out errPos, variableCode, 1, key);
		Set(dict![key], value);
	}

	public void Set(long key, string value)
	{
		token.SetValue(value, [charId, key]);
	}

	private long charId;
	private VariableToken token;
	private ExpressionMediator exm;
	private VariableCode variableCode;
}