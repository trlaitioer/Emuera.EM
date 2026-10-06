using System;
using MinorShift.Emuera.Runtime.Script.Statements;
using System.Collections.Generic;
using System.Linq;

namespace MinorShift.Emuera.Runtime.Script.Data;

//1.713 LogicalLine.csから分割
/// <summary>
/// ラベルのジャンプ先の辞書。Erbファイル読み込み時に作成
/// </summary>
internal sealed class LabelDictionary
{
	#region EM_私家版_辞書獲得

	public string[] NoneventKeys => [.. noneventLabelDic.Keys];

	#endregion

	/// <summary>
	/// 本体。全てのFunctionLabelLineを記録
	/// </summary>
	private readonly Dictionary<string, List<FunctionLabelLine>> labelAtDic = new(Config.Config.StrComper);

	private readonly List<FunctionLabelLine> invalidList = [];
	private readonly Dictionary<string, Dictionary<FunctionLabelLine, GotoLabelLine>> labelDollarList = new(Config.Config.StrComper);

	private readonly HashSet<string> loadedFileSet = [];
	private int currentFileCount;
	private int totalFileCount;

	public int Count { get; private set; }

	/// <summary>
	/// これがfalseである間は式中関数は呼べない
	/// （つまり関数宣言の初期値として式中関数は使えない）
	/// </summary>
	public bool Initialized { get; set; }

	#region Initialized 前用

	public FunctionLabelLine? GetSameNameLabel(FunctionLabelLine point)
	{
		string id = point.LabelName;
		if (!labelAtDic.TryGetValue(id, out List<FunctionLabelLine>? value))
		{
			return null;
		}

		if (point.IsError)
		{
			return null;
		}

		return value.Count <= 1 ? null : value[0];
	}


	private readonly Dictionary<string, List<FunctionLabelLine>[]> eventLabelDic = new(Config.Config.StrComper);
	private readonly Dictionary<string, FunctionLabelLine> noneventLabelDic = new(Config.Config.StrComper);

	public void SortLabels()
	{
		foreach (List<FunctionLabelLine> list in eventLabelDic.SelectMany(pair => pair.Value))
		{
			list.Clear();
		}

		eventLabelDic.Clear();
		noneventLabelDic.Clear();
		foreach ((string key, List<FunctionLabelLine> list) in labelAtDic)
		{
			if (list.Count > 1)
			{
				list.Sort();
			}

			if (!list[0].IsEvent)
			{
				noneventLabelDic.Add(key, list[0]);
				GlobalStatic.IdentifierDictionary.resizeLocalVars("ARG", list[0].LabelName, list[0].ArgLength);
				GlobalStatic.IdentifierDictionary.resizeLocalVars("ARGS", list[0].LabelName, list[0].ArgsLength);
				continue;
			}

			//1810alpha010 オプションによりイベント関数をイベント関数でないかのように呼び出すことを許可
			//eramaker仕様 - #PRI #LATER #SINGLE等を無視し、最先に定義された関数1つのみを呼び出す
			if (Config.Config.CompatiCallEvent)
			{
				noneventLabelDic.Add(key, list[0]);
			}

			List<FunctionLabelLine> onlylist = [];
			List<FunctionLabelLine> prilist = [];
			List<FunctionLabelLine> normallist = [];
			List<FunctionLabelLine> laterlist = [];
			int localMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCAL");
			int localsMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALS");
			foreach (FunctionLabelLine line in list)
			{
				localMax = Math.Max(localMax, line.LocalLength);
				localsMax = Math.Max(localsMax, line.LocalsLength);

				if (line.IsOnly)
				{
					onlylist.Add(line);
				}

				if (line.IsPri)
				{
					prilist.Add(line);
				}

				if (line.IsLater)
				{
					laterlist.Add(line); //#PRIかつ#LATERなら二重に登録する。eramakerの仕様
				}

				if (line is { IsPri: false, IsLater: false })
				{
					normallist.Add(line);
				}
			}

			List<FunctionLabelLine>[] eventLabels = [onlylist, prilist, normallist, laterlist];
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < eventLabels[i].Count; j++)
				{
					eventLabels[i][j].LocalLength = localMax;
					eventLabels[i][j].LocalsLength = localsMax;
				}
			}

			eventLabelDic.Add(key, eventLabels);
		}
	}

	public void RemoveAll()
	{
		Initialized = false;
		Count = 0;
		foreach ((string _, List<FunctionLabelLine>[] array) in eventLabelDic)
		foreach (List<FunctionLabelLine> list in array)
		{
			list.Clear();
		}

		eventLabelDic.Clear();
		noneventLabelDic.Clear();

		foreach ((string _, List<FunctionLabelLine> value) in labelAtDic)
		{
			value.Clear();
		}

		labelAtDic.Clear();
		foreach ((string _, Dictionary<FunctionLabelLine, GotoLabelLine> value) in labelDollarList)
		{
			value.Clear();
		}

		labelDollarList.Clear();
		loadedFileSet.Clear();
		invalidList.Clear();
		currentFileCount = 0;
		totalFileCount = 0;
	}

	//ファイル名に基づき、そのファイルに紐づくラベルを削除する
	public void RemoveLabelWithPath(string fname)
	{
		// FunctionLabelLine is built by LogicalLineParser.ParseLabelLine with a position
		static bool IsMatch(string fname, FunctionLabelLine line) => string.Equals(line.Position!.Value.Filename, fname, Config.Config.SCIgnoreCase);

		List<string> removeFunctions = [];
		foreach ((string functionName, List<FunctionLabelLine> functions) in labelAtDic)
		{
			int removeCount = functions.RemoveAll(line => IsMatch(fname, line));

			Count -= removeCount;

			if (functions.Count == 0)
			{
				removeFunctions.Add(functionName);
			}
		}

		foreach (string rKey in removeFunctions)
		{
			labelAtDic.Remove(rKey);
		}

		invalidList.RemoveAll(line => IsMatch(fname, line));
	}


	/// <summary>
	/// ファイルの重複をチェックし、重複していたらすでにあるそのファイルに関連するラベルを消去する
	/// </summary>
	public void IfFileLoadClearLabelWithPath(string filename)
	{
		if (loadedFileSet.Contains(filename))
		{
			currentFileCount = loadedFileSet.Count;
			RemoveLabelWithPath(filename);
			return;
		}

		totalFileCount++;
		currentFileCount = totalFileCount;
		loadedFileSet.Add(filename);
	}

	public void AddLabel(FunctionLabelLine point)
	{
		point.Index = Count;
		point.FileIndex = currentFileCount;
		Count++;
		string id = point.LabelName;
		if (labelAtDic.TryGetValue(id, out List<FunctionLabelLine>? labelList))
		{
			labelList.Add(point);
		}
		else
		{
			labelAtDic.TryAdd(id, [point]);
		}
	}

	public bool AddLabelDollar(GotoLabelLine point)
	{
		string id = point.LabelName;
		if (labelDollarList.TryGetValue(id, out Dictionary<FunctionLabelLine, GotoLabelLine>? label))
		{
			return label.TryAdd(point.ParentLabelLine!, point);
		}

		labelDollarList.TryAdd(id, new Dictionary<FunctionLabelLine, GotoLabelLine> { { point.ParentLabelLine!, point } });
		return true;
	}

	#endregion


	public List<FunctionLabelLine>[]? GetEventLabels(string key)
	{
		return eventLabelDic.GetValueOrDefault(key);
	}

	public FunctionLabelLine? GetNonEventLabel(string key)
	{
		return noneventLabelDic.GetValueOrDefault(key);
	}

	public List<FunctionLabelLine> GetAllLabels(bool getInvalidList)
	{
		List<FunctionLabelLine> ret = [];
		foreach (List<FunctionLabelLine> list in labelAtDic.Values)
		{
			ret.AddRange(list);
		}

		if (getInvalidList)
		{
			ret.AddRange(invalidList);
		}

		return ret;
	}

	public GotoLabelLine? GetLabelDollar(string key, FunctionLabelLine labelAtLine)
	{
		return labelDollarList.TryGetValue(key, out Dictionary<FunctionLabelLine, GotoLabelLine>? labels) ? labels.GetValueOrDefault(labelAtLine) : null;
	}

	internal void AddInvalidLabel(FunctionLabelLine invalidLabelLine)
	{
		invalidList.Add(invalidLabelLine);
	}
}
