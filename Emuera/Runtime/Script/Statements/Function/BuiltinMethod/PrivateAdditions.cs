// EM_私家版 additions (EM_私家版_追加関数): XML, Map, DataTable, textbox and the rest.

using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using MinorShift.Emuera.UI.Game;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal sealed class HtmlStringLenMethod : FunctionMethod
{
	[BuiltinMethod("HTML_STRINGLEN")]
	private static HtmlStringLenMethod CreateHTML_STRINGLEN()
	{
		return new HtmlStringLenMethod();
	}

	public HtmlStringLenMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		int len = HtmlManager.HtmlLength(slots.Str(exm, 0));
		if (slots.Count == 1 || slots.Int(exm, 1) == 0)
		{
			if (len >= 0)
			{
				return (2 * len / Config.FontSize) + (2 * len % Config.FontSize != 0 ? 1 : 0);
			}
			else
			{
				return (2 * len / Config.FontSize) - (2 * len % Config.FontSize != 0 ? 1 : 0);
			}
		}

		return len;
	}
}

internal sealed class XmlGetMethod : FunctionMethod
{
	[BuiltinMethod("XML_GET")]
	private static XmlGetMethod CreateXML_GET()
	{
		return new XmlGetMethod();
	}

	[BuiltinMethod("XML_GET_BYNAME")]
	private static XmlGetMethod CreateXML_GET_BYNAME()
	{
		return new XmlGetMethod(true);
	}

	public XmlGetMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.String, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 2 },
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.String, ArgType.RefString1D, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
		CanRestructure = false;
	}

	public XmlGetMethod(bool byname) : this()
	{
		byName = byname;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.String, ArgType.Int | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 2 },
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.String, ArgType.RefString1D, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
	}

	private readonly bool byName;

	private static void OutPutNode(XmlNode node, string?[] array, int i, long style)
	{
		switch (style)
		{
			case 1: array[i] = node.InnerText; break;
			case 2: array[i] = node.InnerXml; break;
			case 3: array[i] = node.OuterXml; break;
			case 4: array[i] = node.Name; break;
			default: array[i] = node.Value; break;
		}
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		XmlDocument? doc = null;
		XmlNodeList nodes = null!;
		if (slots.IsInteger(0) || (byName && slots.IsString(0)))
		{
			string idx = slots.IsString(0)
				? slots.Str(exm, 0)
				: slots.Int(exm, 0).ToString();
			Dictionary<string, XmlDocument> dict = exm.VEvaluator.VariableData.DataXmlDocument;
			if (dict.TryGetValue(idx, out XmlDocument? value))
			{
				doc = value;
			}
			else
			{
				return -1;
			}
		}
		else
		{
			doc = new XmlDocument();
			string xml = slots.Str(exm, 0);
			try
			{
				doc.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlGetError.Text, xml, e.Message));
			}
		}

		string path = slots.Str(exm, 1);
		try
		{
			nodes = doc.SelectNodes(path)!;
		}
		catch (System.Xml.XPath.XPathException e)
		{
			throw new CodeEE(string.Format(trerror.XmlGetPathError.Text, path, e.Message));
		}

		long outputStyle = slots.Count == 4 ? slots.Int(exm, 3) : 0;

		if (slots.Count >= 3)
		{
			if (slots.IsInteger(2))
			{
				// Form 1/3 takes doOutput: 0 (or omitted) means the result count only
				if (slots.Int(exm, 2) != 0)
				{
					for (int i = 0; i < Math.Min(nodes.Count, exm.VEvaluator.RESULTS_ARRAY.Length); i++)
					{
						OutPutNode(nodes[i]!, exm.VEvaluator.RESULTS_ARRAY, i, outputStyle);
					}
				}
			}
			else
			{
				// Form 2/4 takes an output array; the matched rule block declares this slot as
				// RefString1D, so the role says the slot is a variable entity
				string[] arr = (slots.Var(2).Identifier.GetArray() as string[])!;
				for (int i = 0; i < Math.Min(nodes.Count, arr.Length); i++)
				{
					OutPutNode(nodes[i]!, arr, i, outputStyle);
				}
			}
		}

		return nodes.Count;
	}
}

internal sealed class IsDefinedMethod : FunctionMethod
{
	[BuiltinMethod("ISDEFINED")]
	private static IsDefinedMethod CreateISDEFINED()
	{
		return new IsDefinedMethod();
	}

	public IsDefinedMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		return GlobalStatic.IdentifierDictionary.GetMacro(slots.Str(exm, 0)) != null ? 1 : 0;
	}
}

internal sealed class EnumNameMethod : FunctionMethod
{
	[BuiltinMethod("ENUMFUNCBEGINSWITH")]
	private static EnumNameMethod CreateENUMFUNCBEGINSWITH()
	{
		return new EnumNameMethod(EType.Function, EAction.BeginsWith);
	}

	[BuiltinMethod("ENUMFUNCENDSWITH")]
	private static EnumNameMethod CreateENUMFUNCENDSWITH()
	{
		return new EnumNameMethod(EType.Function, EAction.EndsWith);
	}

	[BuiltinMethod("ENUMFUNCWITH")]
	private static EnumNameMethod CreateENUMFUNCWITH()
	{
		return new EnumNameMethod(EType.Function, EAction.With);
	}

	[BuiltinMethod("ENUMVARBEGINSWITH")]
	private static EnumNameMethod CreateENUMVARBEGINSWITH()
	{
		return new EnumNameMethod(EType.Variable, EAction.BeginsWith);
	}

	[BuiltinMethod("ENUMVARENDSWITH")]
	private static EnumNameMethod CreateENUMVARENDSWITH()
	{
		return new EnumNameMethod(EType.Variable, EAction.EndsWith);
	}

	[BuiltinMethod("ENUMVARWITH")]
	private static EnumNameMethod CreateENUMVARWITH()
	{
		return new EnumNameMethod(EType.Variable, EAction.With);
	}

	[BuiltinMethod("ENUMMACROBEGINSWITH")]
	private static EnumNameMethod CreateENUMMACROBEGINSWITH()
	{
		return new EnumNameMethod(EType.Macro, EAction.BeginsWith);
	}

	[BuiltinMethod("ENUMMACROENDSWITH")]
	private static EnumNameMethod CreateENUMMACROENDSWITH()
	{
		return new EnumNameMethod(EType.Macro, EAction.EndsWith);
	}

	[BuiltinMethod("ENUMMACROWITH")]
	private static EnumNameMethod CreateENUMMACROWITH()
	{
		return new EnumNameMethod(EType.Macro, EAction.With);
	}

	public enum EType
	{
		Function,
		Variable,
		Macro
	}

	public enum EAction
	{
		BeginsWith,
		EndsWith,
		With
	}

	private readonly EType type;
	private readonly EAction action;

	public EnumNameMethod(EType type, EAction act)
	{
		ReturnType = typeof(long);
		CanRestructure = false;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefString1D | ArgType.NotNull }, OmitStart = 1 }
		];
		this.type = type;
		action = act;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string arg = slots.Str(exm, 0).ToUpper();
		string[]? array = null;
		switch (type)
		{
			case EType.Function:
				array = GlobalStatic.Process.LabelDictionary.NoneventKeys;
				break;
			case EType.Variable:
				array = GlobalStatic.IdentifierDictionary.VarKeys;
				break;
			case EType.Macro:
				array = GlobalStatic.IdentifierDictionary.MacroKeys;
				break;
		}

		List<string> strs = [];
		if (arg.Length > 0)
		{
			foreach (string item in array!)
			{
				if (item.Length < arg.Length)
				{
					continue;
				}

				switch (action)
				{
					case EAction.BeginsWith:
						if (item.ToUpper().StartsWith(arg, StringComparison.Ordinal))
						{
							strs.Add(item);
						}

						break;
					case EAction.EndsWith:
						if (item.ToUpper().LastIndexOf(arg, StringComparison.Ordinal) == item.Length - arg.Length)
						{
							strs.Add(item);
						}

						break;
					case EAction.With:
						if (item.ToUpper().Contains(arg, StringComparison.Ordinal))
						{
							strs.Add(item);
						}

						break;
				}
			}
		}

		// strs.Sort();
		string?[] output;
		if (slots.Count == 2)
		{
			output = (slots.Var(1).Identifier.GetArray() as string[])!;
		}
		else
		{
			output = exm.VEvaluator.RESULTS_ARRAY;
		}

		string[] ret = [.. strs];
		int outputlength = Math.Min(output.Length, ret.Length);
		Array.Copy(ret, output, outputlength);
		return outputlength;
	}
}

internal sealed class EnumFilesMethod : FunctionMethod
{
	[BuiltinMethod("ENUMFILES")]
	private static EnumFilesMethod CreateENUMFILES()
	{
		return new EnumFilesMethod();
	}

	public EnumFilesMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String | ArgType.NotNull, ArgType.Int | ArgType.NotNull, ArgType.RefString1D | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (!GamePath.TryResolve(Program.GameRoot, slots.Str(exm, 0), out GamePath? dir, out _) ||
		    !Directory.Exists(dir.FullName))
		{
			return -1;
		}

		string pattern = slots.Count > 1 ? slots.Str(exm, 1) : "*";
		if (!NamePattern.IsSingleDirectory(pattern))
		{
			return -1;
		}

		SearchOption option = slots.Count > 2
			? slots.Int(exm, 2) == 0 ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories
			: SearchOption.TopDirectoryOnly;
		string[] files;
		try
		{
			files = [.. Directory.EnumerateFiles(dir.FullName, pattern, option)];
			for (int i = 0; i < files.Length; i++)
			{
				files[i] = PurePath.From(files[i]).RelativeTo(Program.GameRoot).ToString();
			}
		}
		catch
		{
			return -1;
		}

		string?[] output;
		if (slots.Count == 4)
		{
			output = (slots.Var(3).Identifier.GetArray() as string[])!;
		}
		else
		{
			output = exm.VEvaluator.RESULTS_ARRAY;
		}

		int ret = Math.Min(files.Length, output.Length);
		Array.Copy(files, output, ret);
		return ret;
	}
}

internal sealed class GetVarMethod : FunctionMethod
{
	[BuiltinMethod("GETVAR")]
	private static GetVarMethod CreateGETVAR()
	{
		return new GetVarMethod();
	}

	public GetVarMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(slots.Str(exm, 0)), LexEndWith.EoL, LexAnalyzeFlag.None);
		AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);

		if (term is VariableTerm var)
		{
			if (var.Identifier == null)
			{
				throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
			}

			if (!var.IsInteger)
			{
				throw new CodeEE(string.Format(trerror.IsNotInt.Text, name));
			}

			return var.GetIntValue(exm);
		}
		else
		{
			throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
		}
	}
}

internal sealed class GetVarsMethod : FunctionMethod
{
	[BuiltinMethod("GETVARS")]
	private static GetVarsMethod CreateGETVARS()
	{
		return new GetVarsMethod();
	}

	public GetVarsMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(slots.Str(exm, 0)), LexEndWith.EoL, LexAnalyzeFlag.None);
		AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);

		if (term is VariableTerm var)
		{
			if (var.Identifier == null)
			{
				throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
			}

			if (!var.IsString)
			{
				throw new CodeEE(string.Format(trerror.IsNotStr.Text, name));
			}

			return var.GetStrValue(exm);
		}
		else
		{
			throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
		}
	}
}

internal sealed class ExistVarMethod : FunctionMethod
{
	[BuiltinMethod("EXISTVAR")]
	private static ExistVarMethod CreateEXISTVAR()
	{
		return new ExistVarMethod();
	}

	public ExistVarMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = true;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		VariableToken? token = GlobalStatic.IdentifierDictionary.GetVariableToken(slots.Str(exm, 0), null, true);
		if (token != null)
		{
			long res = 0;
			if (token.IsInteger)
			{
				res |= 1;
			}

			if (token.IsString)
			{
				res |= 2;
			}

			if (token.IsConst)
			{
				res |= 4;
			}

			if (token.IsArray2D)
			{
				res |= 8;
			}

			if (token.IsArray3D)
			{
				res |= 16;
			}

			return res;
		}

		return 0;
	}
}

internal sealed class ArrayMultiSortExMethod : FunctionMethod
{
	[BuiltinMethod("ARRAYMSORTEX")]
	private static ArrayMultiSortExMethod CreateARRAYMSORTEX()
	{
		return new ArrayMultiSortExMethod();
	}

	public ArrayMultiSortExMethod()
	{
		ReturnType = typeof(long);
		// An empty slot 2 means ascending (the body reads it only when non-null).
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefString1D, ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 2 },
			new ArgTypeList { ArgTypes = { ArgType.RefInt1D, ArgType.RefString1D, ArgType.Int, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = false;
	}

	private string? CheckVariableTerm(AExpression arg, string v)
	{
		string vname = v == null ? trerror.FirstArg.Text : v;
		if (!(arg is VariableTerm varTerm) || varTerm.Identifier.IsCalc || varTerm.Identifier.IsConst)
		{
			return string.Format(trerror.NotVarFunc.Text, Name, vname);
		}

		if (v == null && !varTerm.Identifier.IsArray1D)
		{
			return string.Format(trerror.Not1DFuncArg.Text, Name, "1");
		}

		if (varTerm.Identifier.IsCharacterData)
		{
			return string.Format(trerror.IsCharaVarFunc.Text, Name, vname);
		}

		if (varTerm.Identifier is { IsArray1D: false, IsArray2D: false, IsArray3D: false })
		{
			return string.Format(trerror.NotDimVarFunc.Text, Name, vname);
		}

		return null;
	}

	private VariableTerm GetConvertedTerm(ExpressionMediator exm, string name)
	{
		WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(name), LexEndWith.EoL, LexAnalyzeFlag.None);
		AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
		string? err = CheckVariableTerm(term!, name);
		if (err != null)
		{
			throw new CodeEE(err);
		}

		// CheckVariableTerm returns null only when its argument is a VariableTerm, so this cast cannot fail.
		return (term as VariableTerm)!;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		bool isAscending = !slots.Has(2) || slots.Int(exm, 2) != 0;
		long fixedLength = slots.Count < 4 ? -1 : slots.Int(exm, 3);
		if (fixedLength == 0)
		{
			return 0;
		}

		// Slot 0 stays a type discriminant on purpose: its rule block accepts ArgType.String as well
		// (the variable name as text, see the ctor), and Var(0) would reject that form as ExeEE.
		VariableTerm varTerm = slots.Term(0) is VariableTerm argVar
			? argVar
			: GetConvertedTerm(exm, slots.Str(exm, 0));
		int[] sortedArray;
		if (varTerm.Identifier.IsInteger)
		{
			List<KeyValuePair<long, int>> sortList = [];
			long[] array = (long[])varTerm.Identifier.GetArray();
			long length = fixedLength > 0 ? Math.Min(fixedLength, array.Length) : array.Length;
			for (int i = 0; i < length; i++)
			{
				if (fixedLength == -1 && array[i] == 0)
				{
					break;
				}

				if (array[i] < long.MinValue || array[i] > long.MaxValue)
				{
					return 0;
				}

				sortList.Add(new KeyValuePair<long, int>(array[i], i));
			}

			//素ではintの範囲しか扱えないので一工夫
			sortList.Sort((a, b) => (isAscending ? 1 : -1) * Math.Sign(a.Key - b.Key));
			sortedArray = [.. sortList.Select(p => p.Value)];
		}
		else
		{
			List<KeyValuePair<string, int>> sortList = [];
			string[] array = (string[])varTerm.Identifier.GetArray();
			long length = fixedLength > 0 ? Math.Min(fixedLength, array.Length) : array.Length;
			for (int i = 0; i < length; i++)
			{
				if (fixedLength == -1 && string.IsNullOrEmpty(array[i]))
				{
					return 0;
				}

				sortList.Add(new KeyValuePair<string, int>(array[i], i));
			}

			sortList.Sort((a, b) => (isAscending ? 1 : -1) * a.Key.CompareTo(b.Key));
			sortedArray = [.. sortList.Select(p => p.Value)];
		}

		List<VariableTerm> varTerms = [];
		foreach (string nTerm in (string[])slots.Var(1).Identifier.GetArray())
		{
			varTerms.Add(GetConvertedTerm(exm, nTerm));
		}

		foreach (VariableTerm term in varTerms)
		{
			if (term.Identifier.IsArray1D)
			{
				if (term.IsInteger)
				{
					long[] array = (long[])term.Identifier.GetArray();
					long[] clone = (long[])array.Clone();
					if (array.Length < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					{
						array[i] = clone[sortedArray[i]];
					}
				}
				else
				{
					string[] array = (string[])term.Identifier.GetArray();
					string[] clone = (string[])array.Clone();
					if (array.Length < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					{
						array[i] = clone[sortedArray[i]];
					}
				}
			}
			else if (term.Identifier.IsArray2D)
			{
				if (term.IsInteger)
				{
					long[,] array = (long[,])term.Identifier.GetArray();
					long[,] clone = (long[,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					{
						array[i, x] = clone[sortedArray[i], x];
					}
				}
				else
				{
					string[,] array = (string[,])term.Identifier.GetArray();
					string[,] clone = (string[,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					{
						array[i, x] = clone[sortedArray[i], x];
					}
				}
			}
			else if (term.Identifier.IsArray3D)
			{
				if (term.IsInteger)
				{
					long[,,] array = (long[,,])term.Identifier.GetArray();
					long[,,] clone = (long[,,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					for (int y = 0; y < array.GetLength(2); y++)
					{
						array[i, x, y] = clone[sortedArray[i], x, y];
					}
				}
				else
				{
					string[,,] array = (string[,,])term.Identifier.GetArray();
					string[,,] clone = (string[,,])array.Clone();
					if (array.GetLength(0) < sortedArray.Length)
					{
						return 0;
					}

					for (int i = 0; i < sortedArray.Length; i++)
					for (int x = 0; x < array.GetLength(1); x++)
					for (int y = 0; y < array.GetLength(2); y++)
					{
						array[i, x, y] = clone[sortedArray[i], x, y];
					}
				}
			}
			else
			{
				throw new ExeEE(trerror.AbnormalArray.Text);
			}
		}

		return 1;
	}
}

internal sealed class SetVarMethod : FunctionMethod
{
	[BuiltinMethod("SETVAR")]
	private static SetVarMethod CreateSETVAR()
	{
		return new SetVarMethod();
	}

	public SetVarMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Any } }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(slots.Str(exm, 0)), LexEndWith.EoL, LexAnalyzeFlag.None);
		AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);

		if (term is VariableTerm var)
		{
			if (var.Identifier == null || var.Identifier.IsConst)
			{
				throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
			}

			if (var.IsString)
			{
				if (!slots.IsString(1))
				{
					throw new CodeEE(string.Format(trerror.IsNotInt.Text, name));
				}

				var.SetValue(slots.Str(exm, 1), exm);
			}
			else
			{
				if (!slots.IsInteger(1))
				{
					throw new CodeEE(string.Format(trerror.IsNotStr.Text, name));
				}

				var.SetValue(slots.Int(exm, 1), exm);
			}

			return 1;
		}
		else
		{
			throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
		}
	}
}

internal sealed class VarSetExMethod : FunctionMethod
{
	[BuiltinMethod("VARSETEX")]
	private static VarSetExMethod CreateVARSETEX()
	{
		return new VarSetExMethod();
	}

	public VarSetExMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.String,
					ArgType.Any,
					ArgType.Int | ArgType.NotNull,
					ArgType.Int | ArgType.NotNull,
					ArgType.Int | ArgType.NotNull
				},
				OmitStart = 2
			}
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string name = slots.Str(exm, 0);
		WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(slots.Str(exm, 0)), LexEndWith.EoL, LexAnalyzeFlag.None);
		AExpression? term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);

		if (term is VariableTerm var)
		{
			if (var.Identifier == null || var.Identifier.IsConst)
			{
				throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
			}

			int start = (int)(slots.Count >= 4 ? slots.Int(exm, 3) : 0);
			int end = (int)(slots.Count == 5 ? slots.Int(exm, 4)
				: var.Identifier.IsArray1D ? var.Identifier.GetLength()
				: var.Identifier.IsArray2D ? var.Identifier.GetLength(1)
				: var.Identifier.IsArray2D ? var.Identifier.GetLength(2) : 0);
			bool setAllDims = slots.Count < 3 || slots.Int(exm, 2) != 0;
			if (var.IsString)
			{
				string val = string.Empty;
				if (slots.Count > 1 && !slots.IsString(1))
				{
					throw new CodeEE(string.Format(trerror.SetStrToInt.Text, name));
				}

				if (slots.Count > 1)
				{
					val = slots.Str(exm, 1);
				}

				if (var.Identifier.IsArray1D)
				{
					var.Identifier.SetValueAll(val, start, end, 0);
				}
				else if (var.Identifier.IsArray2D)
				{
					string[,] array = (var.Identifier.GetArray() as string[,])!;
					long idx1 = var.GetElementInt(0, exm);
					long idx2 = var.GetElementInt(1, exm);
					for (int i = Math.Max(start, (int)idx2); i < end; i++)
					{
						array[idx1, i] = val;
					}
				}

				if (var.Identifier.IsArray3D)
				{
					long idx1 = var.GetElementInt(0, exm);
					long idx2 = var.GetElementInt(1, exm);
					long idx3 = var.GetElementInt(2, exm);
					string[,,] array = (var.Identifier.GetArray() as string[,,])!;
					for (int i = Math.Max(start, (int)idx3); i < end; i++)
					{
						array[idx2, idx1, i] = val;
					}
				}
			}
			else
			{
				long val = 0;
				if (slots.Count > 1 && !slots.IsInteger(1))
				{
					throw new CodeEE(string.Format(trerror.SetIntToStr.Text, name));
				}

				if (slots.Count > 1)
				{
					val = slots.Int(exm, 1);
				}

				if (var.Identifier.IsArray1D)
				{
					var.Identifier.SetValueAll(val, start, end, 0);
				}
				else if (var.Identifier.IsArray2D)
				{
					long[,] array = (var.Identifier.GetArray() as long[,])!;
					long idx1 = var.GetElementInt(0, exm);
					long idx2 = var.GetElementInt(1, exm);
					if (setAllDims)
					{
						for (int j = 0; j < array.GetLength(0); j++)
						for (int i = Math.Max(start, (int)idx2); i < end; i++)
						{
							array[j, i] = val;
						}
					}
					else
					{
						for (int i = Math.Max(start, (int)idx2); i < end; i++)
						{
							array[idx1, i] = val;
						}
					}
				}

				if (var.Identifier.IsArray3D)
				{
					long idx1 = var.GetElementInt(0, exm);
					long idx2 = var.GetElementInt(1, exm);
					long idx3 = var.GetElementInt(2, exm);
					long[,,] array = (var.Identifier.GetArray() as long[,,])!;
					if (setAllDims)
					{
						for (int k = 0; k < array.GetLength(0); k++)
						for (int j = 0; j < array.GetLength(1); j++)
						for (int i = Math.Max(start, (int)idx3); i < end; i++)
						{
							array[k, j, i] = val;
						}
					}
					else
					{
						for (int i = Math.Max(start, (int)idx3); i < end; i++)
						{
							array[idx2, idx1, i] = val;
						}
					}
				}
			}

			return 1;
		}
		else
		{
			throw new CodeEE(string.Format(trerror.IsNotVar.Text, name));
		}
	}
}

internal sealed class HtmlSubStringMethod : FunctionMethod
{
	[BuiltinMethod("HTML_SUBSTRING")]
	private static HtmlSubStringMethod CreateHTML_SUBSTRING()
	{
		return new HtmlSubStringMethod();
	}

	public HtmlSubStringMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArray = [typeof(string), typeof(long)];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string str = slots.Str(exm, 0);
		string[] strs = HtmlManager.HtmlSubString(str, (int)slots.Int(exm, 1));
		string?[] output = GlobalStatic.Process.VEvaluator.RESULTS_ARRAY;
		int outputlength = Math.Min(output.Length, strs.Length);
		Array.Copy(strs, output, outputlength);
		// HtmlManager.HtmlSubString always returns two elements, so strs.Length >= 1 and Array.Copy
		// fills output[0] with the same non-null string returned here.
		return strs[0];
	}
}

internal sealed class HtmlStringLinesMethod : FunctionMethod
{
	[BuiltinMethod("HTML_STRINGLINES")]
	private static HtmlStringLinesMethod CreateHTML_STRINGLINES()
	{
		return new HtmlStringLinesMethod();
	}

	public HtmlStringLinesMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(long)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string str = slots.Str(exm, 0);
		if (string.IsNullOrEmpty(str))
		{
			return 0;
		}

		int ret = 0;
		do
		{
			string[] strs = HtmlManager.HtmlSubString(str, (int)slots.Int(exm, 1));
			str = strs[1];
			ret++;
		} while (!string.IsNullOrEmpty(str));

		return ret;
	}
}

internal sealed class RegexpMatchMethod : FunctionMethod
{
	[BuiltinMethod("REGEXPMATCH")]
	private static RegexpMatchMethod CreateREGEXPMATCH()
	{
		return new RegexpMatchMethod();
	}

	public RegexpMatchMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 },
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.RefInt, ArgType.RefString1D } }
		];
		CanRestructure = false;
	}

	private static void Output(MatchCollection matches, Regex reg, string?[] values)
	{
		int idx = 0;
		foreach (Match match in matches)
		foreach (string name in reg.GetGroupNames())
		{
			if (idx >= values.Length)
			{
				return;
			}

			values[idx] = match.Groups[name].Value;
			idx++;
		}
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string baseString = slots.Str(exm, 0);
		Regex reg;
		try
		{
			reg = RegexFactory.GetRegex(slots.Str(exm, 1));
		}
		catch (ArgumentException e)
		{
			throw new CodeEE(string.Format(trerror.InvalidRegexArg.Text, Name, 2, e.Message));
		}

		MatchCollection matches = reg.Matches(baseString);
		int ret = matches.Count;
		if (slots.Count == 3 && slots.Int(exm, 2) != 0)
		{
			exm.VEvaluator.RESULT_ARRAY[1] = reg.GetGroupNumbers().Length;
			if (ret > 0)
			{
				Output(matches, reg, exm.VEvaluator.RESULTS_ARRAY);
			}
		}

		if (slots.Count == 4)
		{
			slots.Var(2).SetValue(reg.GetGroupNumbers().Length, exm);
			if (ret > 0)
			{
				Output(matches, reg, (slots.Var(3).Identifier.GetArray() as string[])!);
			}
		}

		return ret;
	}
}

internal sealed class XmlDocumentMethod : FunctionMethod
{
	[BuiltinMethod("XML_DOCUMENT")]
	private static XmlDocumentMethod CreateXML_DOCUMENT()
	{
		return new XmlDocumentMethod(Operation.Create);
	}

	[BuiltinMethod("XML_RELEASE")]
	private static XmlDocumentMethod CreateXML_RELEASE()
	{
		return new XmlDocumentMethod(Operation.Release);
	}

	[BuiltinMethod("XML_EXIST")]
	private static XmlDocumentMethod CreateXML_EXIST()
	{
		return new XmlDocumentMethod(Operation.Check);
	}

	public enum Operation
	{
		Create,
		Check,
		Release
	};

	public XmlDocumentMethod(Operation type)
	{
		op = type;
		ReturnType = typeof(long);
		if (op == Operation.Create)
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.String } }
			];
		}
		else
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList { ArgTypes = { ArgType.Any } }
			];
		}

		CanRestructure = false;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string idx = slots.IsString(0)
			? slots.Str(exm, 0)
			: slots.Int(exm, 0).ToString();
		Dictionary<string, XmlDocument> xmlDict = exm.VEvaluator.VariableData.DataXmlDocument;
		if (op == Operation.Create)
		{
			string xml = slots.Str(exm, 1);
			if (xmlDict.ContainsKey(idx))
			{
				return 0;
			}

			XmlDocument doc = new();
			try
			{
				doc.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlGetError.Text, xml, e.Message));
			}

			xmlDict.Add(idx, doc);
		}
		else
		{
			if (xmlDict.ContainsKey(idx))
			{
				if (op == Operation.Check)
				{
					return 1;
				}

				xmlDict.Remove(idx);
			}
			else
			{
				return 0;
			}
		}

		return 1;
	}
}

internal sealed class XmlSetMethod : FunctionMethod
{
	[BuiltinMethod("XML_SET")]
	private static XmlSetMethod CreateXML_SET()
	{
		return new XmlSetMethod();
	}

	[BuiltinMethod("XML_SET_BYNAME")]
	private static XmlSetMethod CreateXML_SET_BYNAME()
	{
		return new XmlSetMethod(true);
	}

	public XmlSetMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.Int,
					ArgType.String,
					ArgType.String,
					ArgType.Int | ArgType.NotNull,
					ArgType.Int | ArgType.NotNull
				},
				OmitStart = 3
			},
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.RefString,
					ArgType.String,
					ArgType.String,
					ArgType.Int | ArgType.NotNull,
					ArgType.Int | ArgType.NotNull
				},
				OmitStart = 3
			}
		];
		CanRestructure = false;
	}

	public XmlSetMethod(bool byname) : this()
	{
		byName = byname;
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.String,
					ArgType.String,
					ArgType.String,
					ArgType.Int | ArgType.NotNull,
					ArgType.Int | ArgType.NotNull
				},
				OmitStart = 3
			}
		];
	}

	private readonly bool byName;

	private static void SetNode(XmlNode node, string val, long style)
	{
		switch (style)
		{
			case 1: node.InnerText = val; break;
			case 2: node.InnerXml = val; break;
			default: node.Value = val; break;
		}
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		XmlDocument doc;
		bool saveToArg0 = true;
		if (slots.IsInteger(0) || (byName && slots.IsString(0)))
		{
			saveToArg0 = false;
			string idx = slots.IsString(0)
				? slots.Str(exm, 0)
				: slots.Int(exm, 0).ToString();
			Dictionary<string, XmlDocument> dict = exm.VEvaluator.VariableData.DataXmlDocument;
			if (dict.TryGetValue(idx, out XmlDocument? value))
			{
				doc = value;
			}
			else
			{
				return -1;
			}
		}
		else
		{
			string xml = slots.Str(exm, 0);
			doc = new XmlDocument();
			try
			{
				doc.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
			}
		}

		string path = slots.Str(exm, 1);
		XmlNodeList nodes = null!;
		try
		{
			nodes = doc.SelectNodes(path)!;
		}
		catch (System.Xml.XPath.XPathException e)
		{
			throw new CodeEE(string.Format(trerror.XmlXPathParseError.Text, Name, path, e.Message));
		}

		bool setAllNodes = slots.Count >= 4 && slots.Int(exm, 3) != 0;
		long style = slots.Count == 5 ? slots.Int(exm, 4) : 0;
		if (style is > 2 or < 0)
		{
			style = 0;
		}

		string val = slots.Str(exm, 2);
		if (nodes.Count > 0)
		{
			if (nodes.Count != 1)
			{
				if (setAllNodes)
				{
					for (int i = 0; i < nodes.Count; i++)
					{
						SetNode(nodes[i]!, val, style);
					}
				}
			}
			else
			{
				SetNode(nodes[0]!, val, style);
			}

			if (saveToArg0)
			{
				slots.Var(0).SetValue(doc.OuterXml, exm);
			}
		}

		return nodes.Count;
	}
}

internal sealed class XmlToStrMethod : FunctionMethod
{
	[BuiltinMethod("XML_TOSTR")]
	private static XmlToStrMethod CreateXML_TOSTR()
	{
		return new XmlToStrMethod();
	}

	public XmlToStrMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any } }
		];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string idx = slots.IsString(0)
			? slots.Str(exm, 0)
			: slots.Int(exm, 0).ToString();
		Dictionary<string, XmlDocument> xmlDict = exm.VEvaluator.VariableData.DataXmlDocument;
		if (!xmlDict.TryGetValue(idx, out XmlDocument? value))
		{
			return string.Empty;
		}

		return value.OuterXml;
	}
}

internal sealed class XmlAddNodeMethod : FunctionMethod
{
	[BuiltinMethod("XML_ADDNODE")]
	private static XmlAddNodeMethod CreateXML_ADDNODE()
	{
		return new XmlAddNodeMethod(Operation.Node);
	}

	[BuiltinMethod("XML_ADDNODE_BYNAME")]
	private static XmlAddNodeMethod CreateXML_ADDNODE_BYNAME()
	{
		return new XmlAddNodeMethod(Operation.Node, true);
	}

	[BuiltinMethod("XML_ADDATTRIBUTE")]
	private static XmlAddNodeMethod CreateXML_ADDATTRIBUTE()
	{
		return new XmlAddNodeMethod(Operation.Attribute);
	}

	[BuiltinMethod("XML_ADDATTRIBUTE_BYNAME")]
	private static XmlAddNodeMethod CreateXML_ADDATTRIBUTE_BYNAME()
	{
		return new XmlAddNodeMethod(Operation.Attribute, true);
	}

	public enum Operation
	{
		Node,
		Attribute
	};

	public XmlAddNodeMethod(Operation op)
	{
		ReturnType = typeof(long);
		if (op == Operation.Node)
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.Int,
						ArgType.String,
						ArgType.String,
						ArgType.Int | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull
					},
					OmitStart = 3
				},
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.RefString,
						ArgType.String,
						ArgType.String,
						ArgType.Int | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull
					},
					OmitStart = 3
				}
			];
		}
		else
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.Int,
						ArgType.String,
						ArgType.String,
						ArgType.String | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull
					},
					OmitStart = 3
				},
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.RefString,
						ArgType.String,
						ArgType.String,
						ArgType.String | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull
					},
					OmitStart = 3
				}
			];
		}

		CanRestructure = false;
		this.op = op;
	}

	public XmlAddNodeMethod(Operation op, bool byname) : this(op)
	{
		byName = byname;
		if (op == Operation.Node)
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.String,
						ArgType.String,
						ArgType.String,
						ArgType.Int | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull
					},
					OmitStart = 3
				}
			];
		}
		else
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.String,
						ArgType.String,
						ArgType.String,
						ArgType.String | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull,
						ArgType.Int | ArgType.NotNull
					},
					OmitStart = 3
				}
			];
		}
	}

	private readonly bool byName;
	private readonly Operation op;

	private bool Insert(XmlNode node, XmlNode child, int method)
	{
		if (op == Operation.Node)
		{
			switch (method)
			{
				case 0: node.AppendChild(child); break;
				case 1:
					if (node.ParentNode == null)
					{
						return false;
					}

					node.ParentNode.InsertBefore(child, node);
					break;
				case 2:
					if (node.ParentNode == null)
					{
						return false;
					}

					node.ParentNode.InsertAfter(child, node);
					break;
			}

			return true;
		}
		else
		{
			if (child is XmlAttribute newAttr)
			{
				if (method > 0 && !(node is XmlAttribute))
				{
					return false;
				}

				XmlAttribute? attr = method == 0 ? null : node as XmlAttribute;
				switch (method)
				{
					case 0: node.Attributes!.Append(newAttr); break;
					case 1: attr!.OwnerElement!.Attributes.InsertBefore(newAttr, attr); break;
					case 2: attr!.OwnerElement!.Attributes.InsertAfter(newAttr, attr); break;
				}

				return true;
			}
		}

		return false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		XmlDocument doc;
		int methodPos = op == Operation.Node ? 4 : 5;
		int method = slots.Count >= methodPos ? (int)slots.Int(exm, methodPos - 1) : 0;
		if (method is > 2 or < 0)
		{
			method = 0;
		}

		bool saveToArg0 = true;
		if (slots.IsInteger(0) || (byName && slots.IsString(0)))
		{
			saveToArg0 = false;
			string idx = slots.IsString(0)
				? slots.Str(exm, 0)
				: slots.Int(exm, 0).ToString();
			Dictionary<string, XmlDocument> dict = exm.VEvaluator.VariableData.DataXmlDocument;
			if (dict.TryGetValue(idx, out XmlDocument? value))
			{
				doc = value;
			}
			else
			{
				return -1;
			}
		}
		else
		{
			string xml = slots.Str(exm, 0);
			doc = new XmlDocument();
			try
			{
				doc.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
			}
		}

		string path = slots.Str(exm, 1);
		XmlNodeList nodes;
		try
		{
			nodes = doc.SelectNodes(path)!;
		}
		catch (System.Xml.XPath.XPathException e)
		{
			throw new CodeEE(string.Format(trerror.XmlXPathParseError.Text, Name, path, e.Message));
		}

		if (nodes.Count > 0)
		{
			int setAllPos = op == Operation.Node ? 5 : 6;
			bool setAllNodes = slots.Count == setAllPos && slots.Int(exm, setAllPos - 1) != 0;
			XmlNode child;
			if (op == Operation.Node)
			{
				XmlDocument childNode = new();
				string xml = slots.Str(exm, 2);
				try
				{
					childNode.LoadXml(xml);
				}
				catch (XmlException e)
				{
					throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
				}

				XmlElement newNode = childNode.DocumentElement!;
				child = doc.CreateNode(newNode.NodeType, newNode.Name, newNode.NamespaceURI);
				for (int i = 0; i < newNode.Attributes.Count; i++)
				{
					XmlAttribute xattr = newNode.Attributes[i];
					XmlAttribute attr = doc.CreateAttribute(xattr.Name);
					attr.Value = xattr.Value;
					child.Attributes!.Append(attr);
				}

				child.InnerXml = newNode.InnerXml;
			}
			else
			{
				child = doc.CreateAttribute(slots.Str(exm, 2));
				if (slots.Count >= 4)
				{
					child.Value = slots.Str(exm, 3);
				}
			}

			if (nodes.Count != 1)
			{
				if (setAllNodes)
				{
					for (int i = 0; i < nodes.Count; i++)
					{
						Insert(nodes[i]!, child, method);
					}
				}
			}
			else if (!Insert(nodes[0]!, child, method) && method > 0)
			{
				return 0;
			}

			if (saveToArg0)
			{
				slots.Var(0).SetValue(doc.OuterXml, exm);
			}
		}

		return nodes.Count;
	}
}

internal sealed class XmlRemoveNodeMethod : FunctionMethod
{
	[BuiltinMethod("XML_REMOVENODE")]
	private static XmlRemoveNodeMethod CreateXML_REMOVENODE()
	{
		return new XmlRemoveNodeMethod(Operation.Node);
	}

	[BuiltinMethod("XML_REMOVENODE_BYNAME")]
	private static XmlRemoveNodeMethod CreateXML_REMOVENODE_BYNAME()
	{
		return new XmlRemoveNodeMethod(Operation.Node, true);
	}

	[BuiltinMethod("XML_REMOVEATTRIBUTE")]
	private static XmlRemoveNodeMethod CreateXML_REMOVEATTRIBUTE()
	{
		return new XmlRemoveNodeMethod(Operation.Attribute);
	}

	[BuiltinMethod("XML_REMOVEATTRIBUTE_BYNAME")]
	private static XmlRemoveNodeMethod CreateXML_REMOVEATTRIBUTE_BYNAME()
	{
		return new XmlRemoveNodeMethod(Operation.Attribute, true);
	}

	public enum Operation
	{
		Node,
		Attribute
	};

	public XmlRemoveNodeMethod(Operation op)
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 },
			new ArgTypeList { ArgTypes = { ArgType.RefString, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
		CanRestructure = false;
		this.op = op;
	}

	public XmlRemoveNodeMethod(Operation op, bool byname) : this(op)
	{
		byName = byname;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
		];
	}

	private readonly bool byName;
	private readonly Operation op;

	private bool Remove(XmlNode node)
	{
		if (op == Operation.Attribute)
		{
			if (node is XmlAttribute attr)
			{
				attr.OwnerElement!.Attributes.Remove(attr);
				return true;
			}
		}
		else
		{
			if (node.ParentNode != null)
			{
				XmlNode parent = node.ParentNode;
				node.ParentNode.RemoveChild(node);
				return true;
			}
		}

		return false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		XmlDocument doc;
		int method = slots.Count >= 4 ? (int)slots.Int(exm, 3) : 0;
		if (method is > 2 or < 0)
		{
			method = 0;
		}

		bool saveToArg0 = true;
		if (slots.IsInteger(0) || (byName && slots.IsString(0)))
		{
			saveToArg0 = false;
			string idx = slots.IsString(0)
				? slots.Str(exm, 0)
				: slots.Int(exm, 0).ToString();
			Dictionary<string, XmlDocument> dict = exm.VEvaluator.VariableData.DataXmlDocument;
			if (dict.TryGetValue(idx, out XmlDocument? value))
			{
				doc = value;
			}
			else
			{
				return -1;
			}
		}
		else
		{
			string xml = slots.Str(exm, 0);
			doc = new XmlDocument();
			try
			{
				doc.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
			}
		}

		string path = slots.Str(exm, 1);
		XmlNodeList nodes;
		try
		{
			nodes = doc.SelectNodes(path)!;
		}
		catch (System.Xml.XPath.XPathException e)
		{
			throw new CodeEE(string.Format(trerror.XmlXPathParseError.Text, Name, path, e.Message));
		}

		if (nodes.Count > 0)
		{
			bool setAllNodes = slots.Count == 3 && slots.Int(exm, 2) != 0;
			if (nodes.Count != 1)
			{
				if (setAllNodes)
				{
					for (int i = 0; i < nodes.Count; i++)
					{
						Remove(nodes[i]!);
					}
				}
			}
			else if (!Remove(nodes[0]!))
			{
				return 0;
			}

			if (saveToArg0)
			{
				slots.Var(0).SetValue(doc.OuterXml, exm);
			}
		}

		return nodes.Count;
	}
}

internal sealed class XmlReplaceMethod : FunctionMethod
{
	[BuiltinMethod("XML_REPLACE")]
	private static XmlReplaceMethod CreateXML_REPLACE()
	{
		return new XmlReplaceMethod();
	}

	[BuiltinMethod("XML_REPLACE_BYNAME")]
	private static XmlReplaceMethod CreateXML_REPLACE_BYNAME()
	{
		return new XmlReplaceMethod(true);
	}

	public XmlReplaceMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.Any, ArgType.String } },
			new ArgTypeList { ArgTypes = { ArgType.Int, ArgType.String, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 3 },
			new ArgTypeList { ArgTypes = { ArgType.RefString, ArgType.String, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
		CanRestructure = false;
	}

	public XmlReplaceMethod(bool byname) : this()
	{
		byName = byname;
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
	}

	private readonly bool byName;

	private static bool Replace(XmlNode node, XmlNode newNode)
	{
		if (node.ParentNode != null)
		{
			node.ParentNode.ReplaceChild(newNode, node);
			return true;
		}

		return false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		XmlDocument newXml = new();
		{
			string xml = slots.Count > 2 ? slots.Str(exm, 2) : slots.Str(exm, 1);
			try
			{
				newXml.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
			}
		}
		bool saveToArg0 = true;
		XmlDocument? doc = null;
		if (slots.IsInteger(0) || (byName && slots.IsString(0)) ||
		    (slots.IsString(0) && slots.Count == 2))
		{
			saveToArg0 = false;
			string idx = slots.IsString(0)
				? slots.Str(exm, 0)
				: slots.Int(exm, 0).ToString();
			Dictionary<string, XmlDocument> dict = exm.VEvaluator.VariableData.DataXmlDocument;
			if (!dict.ContainsKey(idx))
			{
				return -1;
			}

			if (slots.Count == 2)
			{
				dict[idx] = newXml;
				return 1;
			}

			doc = dict[idx];
		}
		else
		{
			string xml = slots.Str(exm, 0);
			doc = new XmlDocument();
			try
			{
				doc.LoadXml(xml);
			}
			catch (XmlException e)
			{
				throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
			}
		}

		string path = slots.Str(exm, 1);
		XmlNodeList nodes;
		try
		{
			nodes = doc.SelectNodes(path)!;
		}
		catch (System.Xml.XPath.XPathException e)
		{
			throw new CodeEE(string.Format(trerror.XmlXPathParseError.Text, Name, path, e.Message));
		}

		if (nodes.Count > 0)
		{
			XmlElement newNode = newXml.DocumentElement!;
			XmlNode child = doc.CreateNode(newNode.NodeType, newNode.Name, newNode.NamespaceURI);
			for (int i = 0; i < newNode.Attributes.Count; i++)
			{
				XmlAttribute xattr = newNode.Attributes[i];
				XmlAttribute attr = doc.CreateAttribute(xattr.Name);
				attr.Value = xattr.Value;
				child.Attributes!.Append(attr);
			}

			child.InnerXml = newNode.InnerXml;
			bool setAllNodes = slots.Count >= 4 && slots.Int(exm, 3) != 0;
			if (nodes.Count != 1)
			{
				if (setAllNodes)
				{
					for (int i = 0; i < nodes.Count; i++)
					{
						Replace(nodes[i]!, child);
					}
				}
			}
			else if (!Replace(nodes[0]!, child))
			{
				return 0;
			}

			if (saveToArg0)
			{
				slots.Var(0).SetValue(doc.OuterXml, exm);
			}
		}

		return nodes.Count;
	}
}

internal sealed class ExistFileMethod : FunctionMethod
{
	[BuiltinMethod("EXISTFILE")]
	private static ExistFileMethod CreateEXISTFILE()
	{
		return new ExistFileMethod();
	}

	public ExistFileMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (!GamePath.TryResolve(Program.GameRoot, slots.Str(exm, 0), out GamePath? filepath, out _))
		{
			return 0;
		}

		return File.Exists(filepath.FullName) ? 1 : 0;
	}
}

internal sealed class DataTableManagementMethod : FunctionMethod
{
	[BuiltinMethod("DT_CREATE")]
	private static DataTableManagementMethod CreateDT_CREATE()
	{
		return new DataTableManagementMethod(Operation.Create);
	}

	[BuiltinMethod("DT_EXIST")]
	private static DataTableManagementMethod CreateDT_EXIST()
	{
		return new DataTableManagementMethod(Operation.Check);
	}

	[BuiltinMethod("DT_RELEASE")]
	private static DataTableManagementMethod CreateDT_RELEASE()
	{
		return new DataTableManagementMethod(Operation.Release);
	}

	[BuiltinMethod("DT_NOCASE")]
	private static DataTableManagementMethod CreateDT_NOCASE()
	{
		return new DataTableManagementMethod(Operation.Case);
	}

	[BuiltinMethod("DT_CLEAR")]
	private static DataTableManagementMethod CreateDT_CLEAR()
	{
		return new DataTableManagementMethod(Operation.Clear);
	}

	public enum Operation
	{
		Create,
		Check,
		Release,
		Clear,
		Case
	};

	public DataTableManagementMethod(Operation type)
	{
		ReturnType = typeof(long);
		if (type == Operation.Case)
		{
			argumentTypeArray = [typeof(string), typeof(long)];
		}
		else
		{
			argumentTypeArray = [typeof(string)];
		}

		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		bool contains = dict.ContainsKey(key);
		switch (op)
		{
			case Operation.Clear:
			{
				if (contains)
				{
					dict[key].Clear();
					return 1;
				}

				return -1;
			}
			case Operation.Case:
			{
				if (contains)
				{
					dict[key].CaseSensitive = slots.Int(exm, 1) == 0;
					return 1;
				}

				return -1;
			}
			case Operation.Check:
			{
				return contains ? 1 : 0;
			}
			case Operation.Release:
			{
				if (contains)
				{
					dict.Remove(key);
				}

				return 1;
			}
		}

		if (contains)
		{
			return 0;
		}

		DataTable dt = new(key) { CaseSensitive = true };
		DataColumn c = dt.Columns.Add("id", typeof(long));
		c.AllowDBNull = false;
		c.Unique = true;
		dict[key] = dt;
		dt.PrimaryKey = [c];
		return 1;
	}
}

internal sealed class DataTableColumnManagementMethod : FunctionMethod
{
	[BuiltinMethod("DT_COLUMN_ADD")]
	private static DataTableColumnManagementMethod CreateDT_COLUMN_ADD()
	{
		return new DataTableColumnManagementMethod(Operation.Create);
	}

	[BuiltinMethod("DT_COLUMN_NAMES")]
	private static DataTableColumnManagementMethod CreateDT_COLUMN_NAMES()
	{
		return new DataTableColumnManagementMethod(Operation.Names);
	}

	[BuiltinMethod("DT_COLUMN_EXIST")]
	private static DataTableColumnManagementMethod CreateDT_COLUMN_EXIST()
	{
		return new DataTableColumnManagementMethod(Operation.Check);
	}

	[BuiltinMethod("DT_COLUMN_REMOVE")]
	private static DataTableColumnManagementMethod CreateDT_COLUMN_REMOVE()
	{
		return new DataTableColumnManagementMethod(Operation.Remove);
	}

	public enum Operation
	{
		Create,
		Check,
		Remove,
		Names
	};

	public DataTableColumnManagementMethod(Operation type)
	{
		ReturnType = typeof(long);
		if (type == Operation.Create)
		{
			// DT_COLUMN_ADD slots 2/3 are NotNull: the body reads them once its count guard passes.
			argumentTypeArrayEx =
			[
				new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.Any | ArgType.NotNull, ArgType.Int | ArgType.NotNull }, OmitStart = 2 }
			];
		}
		else if (type == Operation.Names)
		{
			// Empty slot 1 of DT_COLUMN_NAMES means "write the column names to the RESULTS array" (null is not a VariableTerm).
			argumentTypeArrayEx =
			[
				new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefString1D }, OmitStart = 1 }
			];
		}
		else
		{
			argumentTypeArray = [typeof(string), typeof(string)];
		}

		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return -1;
		}

		if (op == Operation.Names)
		{
			string?[] output;
			// The rule declares slot 1 as RefString1D, so a present slot 1 is a variable entity
			if (slots.Has(1))
			{
				output = (slots.Var(1).Identifier.GetArray() as string[])!;
			}
			else
			{
				output = exm.VEvaluator.RESULTS_ARRAY;
			}

			for (int i = 0; i < dt.Columns.Count; i++)
			{
				output[i] = dt.Columns[i].ColumnName;
			}

			return dt.Columns.Count;
		}

		string cName = slots.Str(exm, 1);
		bool contains = dt.Columns.Contains(cName);
		switch (op)
		{
			case Operation.Check:
			{
				return contains ? Utils.DataTable.TypeToInt(dt.Columns[cName]!.DataType) : 0;
			}
			case Operation.Remove:
			{
				if (contains && !cName.Equals("id", StringComparison.CurrentCultureIgnoreCase))
				{
					dt.Columns.Remove(cName);
					return 1;
				}

				return 0;
			}
		}

		if (contains)
		{
			return 0;
		}

		Type? t = null;
		if (slots.Count >= 3)
		{
			if (slots.IsString(2))
			{
				t = Utils.DataTable.NameToType(slots.Str(exm, 2));
			}
			else
			{
				t = Utils.DataTable.IntToType(slots.Int(exm, 2));
			}

			if (t == null)
			{
				throw new CodeEE(string.Format(trerror.UnsupportedType.Text, Name));
			}
		}

		bool nullable = slots.Count != 4 || slots.Int(exm, 3) != 0;
		DataColumn dc;
		if (t != null)
		{
			dc = dt.Columns.Add(cName, t);
		}
		else
		{
			dc = dt.Columns.Add(cName);
		}

		dc.AllowDBNull = nullable;
		return 1;
	}
}

internal sealed class DataTableRowSetMethod : FunctionMethod
{
	[BuiltinMethod("DT_ROW_ADD")]
	private static DataTableRowSetMethod CreateDT_ROW_ADD()
	{
		return new DataTableRowSetMethod(Operation.Add);
	}

	[BuiltinMethod("DT_ROW_SET")]
	private static DataTableRowSetMethod CreateDT_ROW_SET()
	{
		return new DataTableRowSetMethod(Operation.Set);
	}

	public enum Operation
	{
		Add,
		Set
	};

	public DataTableRowSetMethod(Operation type)
	{
		ReturnType = typeof(long);
		if (type == Operation.Add)
		{
			// An empty slot 2 means "no value" in the variadic name/value pair form: SetValue turns a null
			// value into DBNull. The 4-argument array form shares the slot, but the second segment below
			// (no OmitStart) rejects every empty slot, so the body never reads a null as an array. The slot
			// therefore stays nullable - ArgType.NotNull would reject the DBNull form.
			argumentTypeArrayEx =
			[
				new ArgTypeList { ArgTypes = { ArgType.String, ArgType.VariadicString | ArgType.NotNull, ArgType.VariadicAny }, MatchVariadicGroup = true, OmitStart = 1 },
				new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefString1D, ArgType.RefAny1D, ArgType.Int } }
			];
		}
		else
		{
			argumentTypeArrayEx =
			[
				new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int, ArgType.VariadicString, ArgType.VariadicAny }, MatchVariadicGroup = true },
				new ArgTypeList
				{
					ArgTypes =
					{
						ArgType.String,
						ArgType.Int,
						ArgType.RefString1D,
						ArgType.RefAny1D,
						ArgType.Int
					}
				}
			];
		}

		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	private void CheckName(DataTable dt, string name, string key)
	{
		if (name == "id")
		{
			throw new CodeEE(string.Format(trerror.DTCanNotEditIdColumn.Text, Name, key));
		}

		if (!dt.Columns.Contains(name))
		{
			throw new CodeEE(string.Format(trerror.DTLackOfNamedColumn.Text, Name, key, name));
		}
	}

	private void SetValue(DataRow row, DataTable dt, string name, string key, ExpressionMediator exm, AExpression? v)
	{
		CheckName(dt, name, key);
		if (v == null)
		{
			row[name] = DBNull.Value;
			return;
		}

		switch (dt.Columns[name]!.DataType == typeof(string))
		{
			case true when !v.IsString:
			case false when !v.IsInteger:
				throw new CodeEE(string.Format(trerror.DTInvalidDataType.Text, Name, key, name));
			case true:
				row[name] = v.GetStrValue(exm);
				break;
			default:
				row[name] = Utils.DataTable.ConvertInt(v.GetIntValue(exm), dt.Columns[name]!.DataType);
				break;
		}
	}

	private void SetValue(DataRow row, DataTable dt, string name, string key, string str)
	{
		CheckName(dt, name, key);
		if (dt.Columns[name]!.DataType != typeof(string))
		{
			throw new CodeEE(string.Format(trerror.DTInvalidDataType.Text, Name, key, name));
		}

		row[name] = str;
	}

	private void SetValue(DataRow row, DataTable dt, string name, string key, long v)
	{
		CheckName(dt, name, key);
		if (dt.Columns[name]!.DataType == typeof(string))
		{
			throw new CodeEE(string.Format(trerror.DTInvalidDataType.Text, Name, key, name));
		}

		row[name] = Utils.DataTable.ConvertInt(v, dt.Columns[name]!.DataType);
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		int b = op == Operation.Add ? 0 : 1;
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return -1;
		}

		long cCount = 0L;
		DataRow row;
		if (op == Operation.Set)
		{
			long idx = slots.Int(exm, 1);
			if (dt.Rows.Find(idx) is DataRow r)
			{
				row = r;
			}
			else
			{
				return -2;
			}
		}
		else
		{
			row = dt.NewRow();
			row[0] = Utils.TimePoint();
		}

		if (slots.Count == b + 4)
		{
			// The checker matched the array form: slot b+1 is RefString1D, slot b+2 RefAny1D, slot b+3 Int.
			string[] names = (slots.Var(b + 1).Identifier.GetArray() as string[])!;
			long count = Math.Min(names.Length, slots.Int(exm, b + 3));
			if (slots.IsString(b + 2))
			{
				string[] vals = (slots.Var(b + 2).Identifier.GetArray() as string[])!;
				count = Math.Min(vals.Length, count);
				for (int i = 0; i < count; i++)
				{
					SetValue(row, dt, names[i], key, vals[i]);
				}

				cCount += count;
			}
			else
			{
				long[] vals = (slots.Var(b + 2).Identifier.GetArray() as long[])!;
				count = Math.Min(vals.Length, count);
				for (int i = 0; i < count; i++)
				{
					SetValue(row, dt, names[i], key, vals[i]);
				}

				cCount += count;
			}
		}
		else
		{
			int pos = b + 1;
			while (pos < slots.Count)
			{
				string name = slots.Str(exm, pos);
				// Pair form: an explicitly empty value slot means "write DBNull" and is handed through as null.
				SetValue(row, dt, name, key, exm, slots.TermOrNull(pos + 1));
				pos += 2;
				cCount++;
			}
		}

		if (op == Operation.Add)
		{
			dt.Rows.Add(row);
			return (long)row[0];
		}

		return cCount;
	}
}

internal sealed class DataTableLengthMethod : FunctionMethod
{
	[BuiltinMethod("DT_COLUMN_LENGTH")]
	private static DataTableLengthMethod CreateDT_COLUMN_LENGTH()
	{
		return new DataTableLengthMethod(Operation.Column);
	}

	[BuiltinMethod("DT_ROW_LENGTH")]
	private static DataTableLengthMethod CreateDT_ROW_LENGTH()
	{
		return new DataTableLengthMethod(Operation.Row);
	}

	public enum Operation
	{
		Row,
		Column
	};

	public DataTableLengthMethod(Operation type)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.ContainsKey(key))
		{
			return -1;
		}

		return op == Operation.Row ? dict[key].Rows.Count : dict[key].Columns.Count;
	}
}

internal sealed class DataTableRowRemoveMethod : FunctionMethod
{
	[BuiltinMethod("DT_ROW_REMOVE")]
	private static DataTableRowRemoveMethod CreateDT_ROW_REMOVE()
	{
		return new DataTableRowRemoveMethod();
	}

	public DataTableRowRemoveMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int } },
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefInt1D, ArgType.Int } }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return -1;
		}

		DataRow[] rows;
		if (slots.Count == 3)
		{
			StringBuilder sb = new();
			long[] array = (slots.Var(1).Identifier.GetArray() as long[])!;
			int count = Math.Min((int)slots.Int(exm, 2), array.Length);
			if (count <= 0)
			{
				return 0;
			}

			sb.Append('(');
			for (int i = 0; i < count; i++)
			{
				sb.Append(i == 0 ? array[i].ToString() : "," + array[i]);
			}

			sb.Append(')');
			rows = dt.Select("id IN " + sb);
			if (rows == null)
			{
				return 0;
			}
		}
		else if (dt.Rows.Find(slots.Int(exm, 1)) is DataRow row)
		{
			rows = [row];
		}
		else
		{
			return 0;
		}

		foreach (DataRow row in rows)
		{
			dt.Rows.Remove(row);
		}

		return rows.Length;
	}
}

internal sealed class DataTableCellGetMethod : FunctionMethod
{
	[BuiltinMethod("DT_CELL_GET")]
	private static DataTableCellGetMethod CreateDT_CELL_GET()
	{
		return new DataTableCellGetMethod(Operation.Get);
	}

	[BuiltinMethod("DT_CELL_ISNULL")]
	private static DataTableCellGetMethod CreateDT_CELL_ISNULL()
	{
		return new DataTableCellGetMethod(Operation.IsNull);
	}

	[BuiltinMethod("DT_CELL_GETS")]
	private static DataTableCellGetMethod CreateDT_CELL_GETS()
	{
		return new DataTableCellGetMethod(Operation.Gets);
	}

	public enum Operation
	{
		Get,
		IsNull,
		Gets
	};

	public DataTableCellGetMethod(Operation type)
	{
		ReturnType = type == Operation.Gets ? typeof(string) : typeof(long);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int, ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 3 }
		];
		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return op == Operation.IsNull ? -1 : 0;
		}

		bool asId = slots.Count == 4 && slots.Int(exm, 3) != 0;
		long idx = slots.Int(exm, 1);
		string name = slots.Str(exm, 2);
		if (asId)
		{
			if (dt.Rows.Find(idx) is DataRow row && dt.Columns.Contains(name))
			{
				object v = row[name];
				return op == Operation.Get ? v == DBNull.Value ? 0 : Convert.ToInt64(v) : v == DBNull.Value ? 1 : 0;
			}
		}
		else
		{
			if (0 <= idx && idx < dt.Rows.Count && dt.Columns.Contains(name))
			{
				object v = dt.Rows[(int)idx][name];
				return op == Operation.Get ? v == DBNull.Value ? 0 : Convert.ToInt64(v) : v == DBNull.Value ? 1 : 0;
			}
		}

		return op == Operation.IsNull ? -2 : 0;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return string.Empty;
		}

		bool asId = slots.Count == 4 && slots.Int(exm, 3) != 0;
		long idx = slots.Int(exm, 1);
		string name = slots.Str(exm, 2);
		if (asId)
		{
			if (dt.Rows.Find(idx) is DataRow row && dt.Columns.Contains(name))
			{
				object v = row[name];
				if (v != DBNull.Value)
				{
					return (string)v;
				}
			}
		}
		else
		{
			if (0 <= idx && idx < dt.Rows.Count && dt.Columns.Contains(name))
			{
				object v = dt.Rows[(int)idx][name];
				if (v != DBNull.Value)
				{
					return v.ToString()!;
				}
			}
		}

		return string.Empty;
	}
}

internal sealed class DataTableCellSetMethod : FunctionMethod
{
	[BuiltinMethod("DT_CELL_SET")]
	private static DataTableCellSetMethod CreateDT_CELL_SET()
	{
		return new DataTableCellSetMethod();
	}

	public DataTableCellSetMethod()
	{
		ReturnType = typeof(long);
		// An empty slot 3 means "write DBNull": GetIntValue keeps it as a null value and the DBNull branch
		// stores it, so the rule table must not reject it.
		argumentTypeArrayEx =
		[
			new ArgTypeList
			{
				ArgTypes =
				{
					ArgType.String,
					ArgType.Int,
					ArgType.String,
					ArgType.Any,
					ArgType.Int | ArgType.NotNull
				},
				OmitStart = 3
			}
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return -1;
		}

		bool asId = slots.Count == 5 && slots.Int(exm, 4) != 0;
		long idx = slots.Int(exm, 1);
		string name = slots.Str(exm, 2);
		if (name.Equals("id", StringComparison.CurrentCultureIgnoreCase))
		{
			return 0;
		}

		AExpression? v = slots.TermOrNull(3);
		DataRow? row = null;
		if (asId)
		{
			row = dt.Rows.Find(idx);
		}
		else if (idx >= 0 && idx < dt.Rows.Count)
		{
			row = dt.Rows[(int)idx];
		}

		if (row != null && dt.Columns.Contains(name))
		{
			if (v == null)
			{
				row[name] = DBNull.Value;
			}
			else
			{
				switch (dt.Columns[name]!.DataType == typeof(string))
				{
					case true when !v.IsString:
					case false when !v.IsInteger:
						return -2;
					case true:
						row[name] = v.GetStrValue(exm);
						break;
					case false:
						row[name] = Utils.DataTable.ConvertInt(v.GetIntValue(exm), dt.Columns[name]!.DataType);
						break;
				}
			}

			return 1;
		}

		return -3;
	}
}

internal sealed class DataTableSelectMethod : FunctionMethod
{
	[BuiltinMethod("DT_SELECT")]
	private static DataTableSelectMethod CreateDT_SELECT()
	{
		return new DataTableSelectMethod();
	}

	public DataTableSelectMethod()
	{
		ReturnType = typeof(long);
		// Empty slots 1/2 mean "no filter" and "no sort": GetIntValue reads them through arguments[i]?
		// and falls back to the unfiltered dt.Select().
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.String, ArgType.String, ArgType.RefInt1D | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return -1;
		}

		string? filter = slots.Has(1) ? slots.Str(exm, 1) : null;
		string? sort = slots.Has(2) ? slots.Str(exm, 2) : null;
		DataRow[] res;
		if (sort != null)
		{
			res = dt.Select(filter, sort);
		}
		else if (filter != null)
		{
			res = dt.Select(filter);
		}
		else
		{
			res = dt.Select();
		}

		bool toResult = slots.Count != 4;
		long[] output = toResult ? GlobalStatic.VEvaluator.RESULT_ARRAY : (slots.Var(3).Identifier.GetArray() as long[])!;
		if (res != null)
		{
			int count = Math.Min(res.Length, toResult ? output.Length - 1 : output.Length);
			for (int i = 0; i < count; i++)
			{
				output[toResult ? i + 1 : i] = (long)res[i][0];
			}

			if (toResult)
			{
				output[0] = res.Length;
			}

			return res.Length;
		}

		if (toResult)
		{
			output[0] = 0;
		}

		return 0;
	}
}

internal sealed class DataTableToXmlMethod : FunctionMethod
{
	[BuiltinMethod("DT_TOXML")]
	private static DataTableToXmlMethod CreateDT_TOXML()
	{
		return new DataTableToXmlMethod();
	}

	public DataTableToXmlMethod()
	{
		ReturnType = typeof(string);
		argumentTypeArrayEx =
		[
			new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefString | ArgType.NotNull }, OmitStart = 1 }
		];
		CanRestructure = false;
	}

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		if (!dict.TryGetValue(key, out DataTable? dt))
		{
			return string.Empty;
		}

		string?[] output = slots.Count > 1
			? (slots.Var(1).Identifier.GetArray() as string[])!
			: GlobalStatic.VEvaluator.RESULTS_ARRAY;
		int idx = slots.Count > 1 ? 0 : 1;

		StringBuilder sb = new();
		using StringWriter sw = new(sb);
		dt.WriteXmlSchema(sw);
		output[idx] = sb.ToString();
		sb.Clear();
		dt.WriteXml(sw);
		return sb.ToString();
	}
}

internal sealed class DataTableFromXmlMethod : FunctionMethod
{
	[BuiltinMethod("DT_FROMXML")]
	private static DataTableFromXmlMethod CreateDT_FROMXML()
	{
		return new DataTableFromXmlMethod();
	}

	public DataTableFromXmlMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(string), typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, DataTable> dict = exm.VEvaluator.VariableData.DataDataTables;
		DataTable dt;
		try
		{
			dt = new DataTable(key);
			using (StringReader reader = new(slots.Str(exm, 1)))
			{
				dt.ReadXmlSchema(reader);
			}

			using (StringReader reader = new(slots.Str(exm, 2)))
			{
				dt.ReadXml(reader);
			}
		}
		catch
		{
			return 0;
		}

		dict[key] = dt;
		return 1;
	}
}

internal sealed class MapManagementMethod : FunctionMethod
{
	[BuiltinMethod("MAP_CREATE")]
	private static MapManagementMethod CreateMAP_CREATE()
	{
		return new MapManagementMethod(Operation.Create);
	}

	[BuiltinMethod("MAP_EXIST")]
	private static MapManagementMethod CreateMAP_EXIST()
	{
		return new MapManagementMethod(Operation.Check);
	}

	[BuiltinMethod("MAP_RELEASE")]
	private static MapManagementMethod CreateMAP_RELEASE()
	{
		return new MapManagementMethod(Operation.Release);
	}

	public enum Operation
	{
		Create,
		Check,
		Release
	};

	public MapManagementMethod(Operation type)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string)];
		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string key = slots.Str(exm, 0);
		Dictionary<string, Dictionary<string, string>> dict = exm.VEvaluator.VariableData.DataStringMaps;
		bool contains = dict.ContainsKey(key);
		switch (op)
		{
			case Operation.Check:
			{
				return contains ? 1 : 0;
			}
			case Operation.Release:
			{
				if (contains)
				{
					dict.Remove(key);
				}

				return 1;
			}
		}

		if (contains)
		{
			return 0;
		}

		dict[key] = [];
		return 1;
	}
}

internal sealed class MapDataOperationMethod : FunctionMethod
{
	[BuiltinMethod("MAP_CLEAR")]
	private static MapDataOperationMethod CreateMAP_CLEAR()
	{
		return new MapDataOperationMethod(Operation.Clear);
	}

	[BuiltinMethod("MAP_SIZE")]
	private static MapDataOperationMethod CreateMAP_SIZE()
	{
		return new MapDataOperationMethod(Operation.Size);
	}

	[BuiltinMethod("MAP_HAS")]
	private static MapDataOperationMethod CreateMAP_HAS()
	{
		return new MapDataOperationMethod(Operation.Has);
	}

	[BuiltinMethod("MAP_SET")]
	private static MapDataOperationMethod CreateMAP_SET()
	{
		return new MapDataOperationMethod(Operation.Set);
	}

	[BuiltinMethod("MAP_REMOVE")]
	private static MapDataOperationMethod CreateMAP_REMOVE()
	{
		return new MapDataOperationMethod(Operation.Remove);
	}

	public enum Operation
	{
		Set,
		Has,
		Remove,
		Clear,
		Size
	};

	public MapDataOperationMethod(Operation type)
	{
		ReturnType = typeof(long);
		switch (type)
		{
			case Operation.Set:
				argumentTypeArray = [typeof(string), typeof(string), typeof(string)]; break;
			case Operation.Has:
			case Operation.Remove:
				argumentTypeArray = [typeof(string), typeof(string)]; break;
			default:
				argumentTypeArray = [typeof(string)]; break;
		}

		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string map = slots.Str(exm, 0);
		Dictionary<string, Dictionary<string, string>> dict = exm.VEvaluator.VariableData.DataStringMaps;
		if (!dict.TryGetValue(map, out Dictionary<string, string>? sMap))
		{
			return -1;
		}

		if (op == Operation.Clear)
		{
			sMap.Clear();
		}
		else if (op == Operation.Size)
		{
			return sMap.Count;
		}
		else
		{
			string key = slots.Str(exm, 1);
			bool contains = sMap.ContainsKey(key);
			if (op == Operation.Has)
			{
				return contains ? 1 : 0;
			}

			if (op == Operation.Remove)
			{
				sMap.Remove(key);
			}
			else
			{
				sMap[key] = slots.Str(exm, 2);
			}
		}

		return 1;
	}
}

internal sealed class MapGetStrMethod : FunctionMethod
{
	[BuiltinMethod("MAP_GET")]
	private static MapGetStrMethod CreateMAP_GET()
	{
		return new MapGetStrMethod(Operation.Get);
	}

	[BuiltinMethod("MAP_GETKEYS")]
	private static MapGetStrMethod CreateMAP_GETKEYS()
	{
		return new MapGetStrMethod(Operation.GetKeys);
	}

	[BuiltinMethod("MAP_TOXML")]
	private static MapGetStrMethod CreateMAP_TOXML()
	{
		return new MapGetStrMethod(Operation.ToXml);
	}

	public enum Operation
	{
		Get,
		ToXml,
		GetKeys
	};

	public MapGetStrMethod(Operation type)
	{
		ReturnType = typeof(string);
		switch (type)
		{
			case Operation.Get:
				argumentTypeArray = [typeof(string), typeof(string)]; break;
			case Operation.ToXml:
				argumentTypeArray = [typeof(string)]; break;
			case Operation.GetKeys:
				argumentTypeArrayEx =
				[
					new ArgTypeList { ArgTypes = { ArgType.String, ArgType.Int | ArgType.NotNull }, OmitStart = 1 },
					new ArgTypeList { ArgTypes = { ArgType.String, ArgType.RefString1D, ArgType.Int } }
				]; break;
		}

		CanRestructure = false;
		op = type;
	}

	private readonly Operation op;

	public override string GetStrValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		Dictionary<string, Dictionary<string, string>> dict = exm.VEvaluator.VariableData.DataStringMaps;
		string map = slots.Str(exm, 0);
		if (!dict.TryGetValue(map, out Dictionary<string, string>? sMap))
		{
			return "";
		}

		if (op == Operation.Get)
		{
			string key = slots.Str(exm, 1);
			return sMap.GetValueOrDefault(key, "");
		}
		else if (op == Operation.GetKeys && slots.Count > 1)
		{
			int count = 0;
			string?[] array;
			if (slots.Count == 3) // to array
			{
				VariableTerm Term = slots.Var(1);
				if (slots.Int(exm, 2) == 0)
				{
					return "";
				}

				array = (Term.Identifier.GetArray() as string[])!;
			}
			else if (slots.Count == 2) // to RESULTS array
			{
				if (slots.Int(exm, 1) == 0)
				{
					return "";
				}

				array = exm.VEvaluator.RESULTS_ARRAY;
			}
			else
			{
				return "";
			}

			foreach (string k in sMap.Keys)
			{
				if (count >= array.Length)
				{
					break;
				}

				array[count] = k;
				count++;
			}

			exm.VEvaluator.RESULT = sMap.Keys.Count;
			return slots.Count == 2 ? exm.VEvaluator.RESULTS : "";
		}

		StringBuilder sb = new();
		if (op == Operation.GetKeys)
		{
			bool isNotEmpty = false;
			foreach (string k in sMap.Keys)
			{
				if (isNotEmpty)
				{
					sb.Append(',').Append(k);
				}
				else
				{
					isNotEmpty = true;
					sb.Append(k);
				}
			}
		}
		else
		{
			sb.Append("<map>");
			foreach (KeyValuePair<string, string> p in sMap)
			{
				sb.Append($"<p><k>{p.Key}</k><v>{p.Value}</v></p>");
			}

			sb.Append("</map>");
		}

		return sb.ToString();
	}
}

internal sealed class MapFromXmlMethod : FunctionMethod
{
	[BuiltinMethod("MAP_FROMXML")]
	private static MapFromXmlMethod CreateMAP_FROMXML()
	{
		return new MapFromXmlMethod();
	}

	public MapFromXmlMethod()
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(string), typeof(string)];
		CanRestructure = false;
	}

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		string map = slots.Str(exm, 0);
		Dictionary<string, Dictionary<string, string>> dict = exm.VEvaluator.VariableData.DataStringMaps;
		if (!dict.TryGetValue(map, out Dictionary<string, string>? sMap))
		{
			return 0;
		}

		string xml = slots.Str(exm, 1);
		XmlDocument doc = new();
		XmlNodeList nodes;
		try
		{
			doc.LoadXml(xml);
			nodes = doc.SelectNodes("/map/p")!;
		}
		catch (XmlException e)
		{
			throw new CodeEE(string.Format(trerror.XmlParseError.Text, Name, xml, e.Message));
		}

		for (int i = 0; i < nodes.Count; i++)
		{
			XmlNode node = nodes[i]!;
			XmlNodeList key = node.SelectNodes("./k")!;
			XmlNodeList val = node.SelectNodes("./v")!;
			if (key.Count != 1 || val.Count != 1)
			{
				continue;
			}

			sMap[key[0]!.InnerText] = val[0]!.InnerXml;
		}

		return 1;
	}
}

internal sealed class MoveTextBoxMethod : FunctionMethod
{
	[BuiltinMethod("MOVETEXTBOX")]
	private static MoveTextBoxMethod CreateMOVETEXTBOX()
	{
		return new MoveTextBoxMethod();
	}

	[BuiltinMethod("RESUMETEXTBOX")]
	private static MoveTextBoxMethod CreateRESUMETEXTBOX()
	{
		return new MoveTextBoxMethod(true);
	}

	public MoveTextBoxMethod(bool b = false)
	{
		ReturnType = typeof(long);
		argumentTypeArray = [typeof(long), typeof(long), typeof(long)];
		CanRestructure = false;
		resume = b;
	}

	private readonly bool resume;

	public override long GetIntValue(ExpressionMediator exm, ArgumentSlots slots)
	{
		if (resume)
		{
			exm.Console.Window.ResetTextBoxPos();
		}
		else
		{
			exm.Console.Window.SetTextBoxPos(
				(int)slots.Int(exm, 0),
				(int)slots.Int(exm, 1),
				(int)slots.Int(exm, 2));
		}

		return 1;
	}
}
