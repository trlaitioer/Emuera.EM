#nullable enable
using MinorShift.Emuera.GameData.Variable;
using System.Collections.Generic;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements.Variable;

internal delegate LocalVariableToken CreateLocalVariableToken(VariableCode varCode, string subKey, int size);
internal sealed class VariableLocal
{
	public VariableLocal(VariableCode varCode, int size, CreateLocalVariableToken creater)
	{
		this.size = size;
		this.varCode = varCode;
		this.creater = creater;
	}
	readonly int size;
	public bool IsForbid { get { return size == 0; } }
	VariableCode varCode;
	//VariableData varData;
	CreateLocalVariableToken creater;
	Dictionary<string, LocalVariableToken> localVarTokens = [];
	//While function bodies are parsed in parallel, several threads create tokens for different
	//functions at once. Writes are serialized to keep the Dictionary intact. A lock is used rather
	//than GetOrAdd because creater must run exactly once per subKey: calling it twice would leave a
	//function referring to the array that never made it into the dictionary.
	readonly object localVarTokensLock = new();

	public int GetDefaultSize()
	{
		return size;
	}

	private LocalVariableToken GetNewLocalVariableToken(string subKey, FunctionLabelLine func)
	{
		LocalVariableToken? ret = null;
		int newSize = 0;
		if (varCode == VariableCode.LOCAL)
			newSize = func.LocalLength;
		else if (varCode == VariableCode.LOCALS)
			newSize = func.LocalsLength;
		else if (varCode == VariableCode.ARG)
			newSize = func.ArgLength;
		else if (varCode == VariableCode.ARGS)
			newSize = func.ArgsLength;
		if (newSize > 0)
		{
			if (newSize < size && (varCode == VariableCode.ARG || varCode == VariableCode.ARGS))
				newSize = size;
			ret = creater(varCode, subKey, newSize);
		}
		else if (newSize == 0)
			ret = creater(varCode, subKey, size);
		else
		{
			ret = creater(varCode, subKey, size);
			LogicalLine? line = GlobalStatic.Process.GetScaningLine();
			if (line != null)
			{
				if (!func.IsSystem)
					ParserMediator.Warn(string.Format(trerror.UseArgVarInHasNotArgFunc.Text, varCode), line, 1, false, false);
				else
					ParserMediator.Warn(string.Format(trerror.UseArgVarInSystemFunc.Text, func.LabelName, varCode), line, 1, false, false);
			}
			//throw new CodeEE("この関数に引数変数\"" + varCode + "\"は定義されていません");
		}
		lock (localVarTokensLock)
		{
			localVarTokens.Add(subKey, ret);
		}
		return ret;
	}

	/// <summary>
	/// Returns the existing token, or creates and registers one when absent. The lookup and the
	/// creation share a single critical section, so the same subKey cannot be created twice while
	/// parsing runs in parallel.
	/// </summary>
	public LocalVariableToken GetNewVariableTokenIfAbsent(string subKey, FunctionLabelLine func)
	{
		lock (localVarTokensLock)
		{
			if (localVarTokens.TryGetValue(subKey, out LocalVariableToken? exist))
				return exist;
			return GetNewLocalVariableToken(subKey, func);
		}
	}

	public void ResizeLocalVariableToken(string subKey, int newSize)
	{
		lock (localVarTokensLock)
		{
			if (localVarTokens.TryGetValue(subKey, out LocalVariableToken? ret))
			{
				if (size < newSize)
					ret.resize(newSize);
				else
					ret.resize(size);
			}
			else
			{
				if (newSize > size)
					ret = creater(varCode, subKey, newSize);
				else if (newSize == 0)
					ret = creater(varCode, subKey, size);
				else
					return;
				localVarTokens.Add(subKey, ret);
			}
		}
	}

	public void Clear()
	{
		lock (localVarTokensLock)
		{
			localVarTokens.Clear();
		}
	}

	public void SetDefault()
	{
		foreach (KeyValuePair<string, LocalVariableToken> pair in localVarTokens)
			pair.Value.SetDefault();
	}
}
