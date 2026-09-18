using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace MinorShift.Emuera.Runtime.Config;

internal abstract class AConfigItem(ConfigCode code, string text, string etext)
{
	#region EM_私家版_Emuera多言語化改造

	public static ConfigItem<T>? Copy<T>(ConfigItem<T>? other)
	{
		if (other == null)
		{
			return null;
		}

		ConfigItem<T> ret = new(other.Code, other.Text, other.EngText, other.Value) { Fixed = other.Fixed };
		return ret;
	}

	#endregion

	public abstract void CopyTo(AConfigItem other);
	public abstract bool TryParse(string? tokens);
	public abstract void SetValue<U>(U p);
	public abstract U GetValue<U>();
	public abstract string ValueToString();
	public readonly ConfigCode Code = code;
	public readonly string Name = EnumsNET.Enums.AsString(code);

	#region EM_私家版_Emuera多言語化改造

	public readonly string Text = text.ToUpper();
	public readonly string EngText = etext.ToUpper();

	#endregion

	public bool Fixed;
}

internal sealed class ConfigItem<T> : AConfigItem
{
	#region EM_私家版_Emuera多言語化改造

	public ConfigItem(ConfigCode code, string text, string etext, T t) : base(code, text, etext)

	#endregion

	{
		val = t;
	}

	private T val;

	public T Value
	{
		get => val;
		set
		{
			if (Fixed)
			{
				return;
			}

			val = value;
		}
	}

	public override void CopyTo(AConfigItem other)
	{
		ConfigItem<T> item = (ConfigItem<T>)other;
		item.Fixed = false;
		item.Value = Value;
		item.Fixed = Fixed;
	}

	public override void SetValue<U>(U p)
	{
		////if (this is ConfigItem<U>)
		//	((ConfigItem<U>)(AConfigItem)this).Value = p;
		////else
		////	throw new ExeEE("型が一致しない");

		((ConfigItem<U>)(AConfigItem)this).Value = p;
	}

	public override U GetValue<U>()
	{
		//////if (this is ConfigItem<U>)
		//	return ((ConfigItem<U>)(AConfigItem)this).Value;
		////throw new ExeEE("型が一致しない");

		return ((ConfigItem<U>)(AConfigItem)this).Value;
	}

	public override string ValueToString()
	{
		switch (this)
		{
			case ConfigItem<bool> boolConfigItem:
			{
				//ConfigItem<T>をConfigItem<bool>に直接キャストすることはできない
				return boolConfigItem.Value ? "YES" : "NO";
			}
			case ConfigItem<Color> colorConfigItem:
			{
				Color c = colorConfigItem.Value;
				return $"{c.R},{c.G},{c.B}";
			}

			#region EM_私家版_LoadText＆SaveText機能拡張

			case ConfigItem<List<string>> stringListConfigItem:
			{
				return string.Join(',', stringListConfigItem.Value.SkipWhile(str => str.Length == 0));
			}

			#endregion

			default:
				return val!.ToString()!;
		}
	}


	public override string ToString()
	{
		#region EM_私家版_Emuera多言語化改造

		return (Config.EnglishConfigOutput ? EngText : Text) + ":" + ValueToString();

		#endregion
	}


	/// ジェネリック化大失敗。なんかうまい方法ないかな～
	public override bool TryParse(string? param)
	{
		if (string.IsNullOrEmpty(param))
		{
			return false;
		}

		if (Fixed)
		{
			return false;
		}

		bool ret = false;
		string str = param.Trim();
		switch (this)
		{
			case ConfigItem<bool> boolConfigItem:
			{
				bool b = false;
				ret = tryStringToBool(str, ref b);
				if (ret) //ConfigItem<T>をConfigItem<bool>に直接キャストすることはできない
				{
					boolConfigItem.Value = b;
				}

				break;
			}
			case ConfigItem<Color> colorConfigItem:
			{
				ret = tryStringsToColor(str, out Color c);
				if (!ret)
				{
					throw new CodeEE(Lang.Error.NotExistColorSpecifier.Text);
				}

				colorConfigItem.Value = c;
				break;
			}
			case ConfigItem<char> charConfigItem:
			{
				ret = char.TryParse(str, out char c);
				if (ret)
				{
					charConfigItem.Value = c;
				}

				break;
			}
			case ConfigItem<int> intConfigItem:
			{
				ret = int.TryParse(str, out int i);
				if (!ret)
				{
					throw new CodeEE(Lang.Error.ContainsNonNumericCharacters.Text);
				}

				intConfigItem.Value = i;
				break;
			}
			case ConfigItem<long> longConfigItem:
			{
				ret = long.TryParse(str, out long i);
				if (!ret)
				{
					throw new CodeEE(Lang.Error.ContainsNonNumericCharacters.Text);
				}

				longConfigItem.Value = i;
				break;
			}
			case ConfigItem<List<long>> longListConfigItem:
			{
				longListConfigItem.Value.Clear();
				foreach (string st in str.Split('/'))
				{
					ret = long.TryParse(st.Trim(), out long i);
					if (!ret)
					{
						throw new CodeEE(Lang.Error.ContainsNonNumericCharacters.Text);
					}

					longListConfigItem.Value.Add(i);
				}

				break;
			}
			case ConfigItem<string> stringConfigItem:
				ret = true;
				stringConfigItem.Value = str;
				break;
			case ConfigItem<List<string>> stringListConfigItem:
			{
				#region EM_私家版_LoadText＆SaveText機能拡張

				ret = true;
				List<string> list = stringListConfigItem.Value;
				tryStringToStringList(str, ref list);
				stringListConfigItem.Value = list;

				#endregion

				break;
			}
			case ConfigItem<TextDrawingMode> textDrawingModeConfigItem when Enum.TryParse(str, true, out TextDrawingMode result):
				textDrawingModeConfigItem.Value = result;
				break;
			case ConfigItem<ReduceArgumentOnLoadFlag> reduceArgumentOnLoadFlagConfigItem when Enum.TryParse(str, true, out ReduceArgumentOnLoadFlag result):
				reduceArgumentOnLoadFlagConfigItem.Value = result;
				break;
			case ConfigItem<DisplayWarningFlag> displayWarningFlagConfigItem when Enum.TryParse(str, true, out DisplayWarningFlag result):
				displayWarningFlagConfigItem.Value = result;
				break;
			case ConfigItem<UseLanguage> useLanguageConfigItem when Enum.TryParse(str, true, out UseLanguage result):
				useLanguageConfigItem.Value = result;
				break;
			case ConfigItem<TextEditorType> textEditorTypeConfigItem when Enum.TryParse(str, true, out TextEditorType result):
				textEditorTypeConfigItem.Value = result;
				break;
			case ConfigItem<TextDrawingMode>:
			case ConfigItem<ReduceArgumentOnLoadFlag>:
			case ConfigItem<DisplayWarningFlag>:
			case ConfigItem<UseLanguage>:
			case ConfigItem<TextEditorType>:
				throw new CodeEE(Lang.Error.InvalidSpecification.Text);
		}

		//else
		//    throw new ExeEE("型不明なコンフィグ");
		return ret;
	}


	#region EM_私家版_LoadText＆SaveText機能拡張

	private static bool tryStringToStringList(string arg, ref List<string> vs)
	{
		vs = [.. arg.Split(',').Select(token => token.Trim())];
		return true;
	}

	#endregion

	private static bool tryStringToBool(string arg, ref bool p)
	{
		if (arg == null)
		{
			return false;
		}

		string str = arg.Trim();
		if (int.TryParse(str, out int i))
		{
			p = i != 0;
			return true;
		}

		if (str.Equals("NO", StringComparison.OrdinalIgnoreCase)
		    || str.Equals("FALSE", StringComparison.OrdinalIgnoreCase)
		    || str.Equals("後", StringComparison.Ordinal)) //"単位の位置"用
		{
			p = false;
			return true;
		}

		if (str.Equals("YES", StringComparison.OrdinalIgnoreCase)
		    || str.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
		    || str.Equals("前", StringComparison.Ordinal))
		{
			p = true;
			return true;
		}

		throw new CodeEE(Lang.Error.InvalidSpecification.Text);
	}

	private static bool tryStringsToColor(string str, out Color c)
	{
		string[] tokens = str.Split(',');
		c = Color.Black;
		if (tokens.Length < 3)
		{
			return false;
		}

		if (!int.TryParse(tokens[0].Trim(), out int r) || r < 0 || r > 255)
		{
			return false;
		}

		if (!int.TryParse(tokens[1].Trim(), out int g) || g < 0 || g > 255)
		{
			return false;
		}

		if (!int.TryParse(tokens[2].Trim(), out int b) || b < 0 || b > 255)
		{
			return false;
		}

		c = Color.FromArgb(r, g, b);
		return true;
	}
}
