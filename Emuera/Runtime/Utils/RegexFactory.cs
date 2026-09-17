using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

//Regexをキャッシュする
internal static class RegexFactory
{
	private readonly static Dictionary<string, Regex> _dictionary = [];

	public static Regex GetRegex(string regex)
	{
		if (_dictionary.TryGetValue(regex, out Regex? ret))
		{
			return ret;
		}
		else
		{
			try
			{
				ret = new Regex(regex, RegexOptions.Compiled);
				_dictionary.Add(regex, ret);
			}
			catch (ArgumentException)
			{
				throw;
			}
		}

		return ret;
	}
}