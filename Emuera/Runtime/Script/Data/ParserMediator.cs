#nullable enable
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MinorShift.Emuera;

//1756 新設。ParserやLexicalAnalyzerなどが知りたい情報をまとめる
//本当は引数として渡すべきなのかもしれないが全てのParserの引数を書きなおすのが面倒なのでstatic
internal partial class ParserMediator
{
	static ParserMediator()
	{
		RenameDic = [];
	}
	/// <summary>
	/// emuera.config等で発生した警告
	/// Initializeより前に発生する
	/// </summary>
	/// <param name="str"></param>
	/// <param name="?"></param>
	public static void ConfigWarn(string str, ScriptPosition? pos, int level, string? stack)
	{
		if (level <Config.DisplayWarningLevel && !Program.AnalysisMode)
			return;
		warningList.Add(new ParserWarning(str, pos, level, stack));
	}

	static EmueraConsole? console;
	public static void Initialize(EmueraConsole? console)
	{
		ParserMediator.console = console;
	}

	/// <summary>
	/// True while a parse-error notification sound must be deferred. Set only for the duration of
	/// ErbLoader.ParseScript, whose worker threads must not touch the UI. Checked from
	/// ArgumentParser.SetArgumentTo, which is also reached from runtime callers that still want the
	/// immediate sound.
	/// </summary>
	public static bool DeferParseErrorSound { get; set; }

	static int parseErrorCount;

	/// <summary>
	/// Records that a parse error occurred. Thread-safe. When <see cref="DeferParseErrorSound"/> is
	/// false the caller is on the UI thread and the sound is played right away; otherwise the count
	/// is kept for <see cref="TakeParseErrorCount"/>.
	/// </summary>
	public static void NotifyParseError()
	{
		if (DeferParseErrorSound)
		{
			System.Threading.Interlocked.Increment(ref parseErrorCount);
			return;
		}
		System.Media.SystemSounds.Hand.Play();
	}

	/// <summary>
	/// Returns the number of parse errors recorded while deferring, and resets the counter.
	/// </summary>
	public static int TakeParseErrorCount()
	{
		return System.Threading.Interlocked.Exchange(ref parseErrorCount, 0);
	}

	/// <summary>
	/// Records a resolved static CALL edge while ErbLoader.ParseScript runs. It is installed only for
	/// the duration of ParseScript. The loader reads the edges of each Depth layer back once the layer
	/// is joined, so a call site whose callee had not been parsed yet is decided the way the serial
	/// parser decided it when the callee came first. The resolved call and the call argument travel
	/// with the edge because deciding it again re-runs the argument conversion.
	/// </summary>
	// Installed only for the duration of ErbLoader.ParseScript; absent the rest of the time
	public static System.Collections.Concurrent.ConcurrentBag<
		(InstructionLine Line, SpCallArgment Arg, CalledFunction Callee)>? CallEdgeRecorder { get; set; }

	/// <summary>
	/// Reports a resolved static CALL edge to <see cref="CallEdgeRecorder"/> when one is installed.
	/// </summary>
	public static void RecordCallEdge(InstructionLine line, SpCallArgment arg, CalledFunction callee)
	{
		CallEdgeRecorder?.Add((line, arg, callee));
	}

	#region Rename
	public static Dictionary<string, string> RenameDic { get; private set; }
	//1756 Process.Load.csより移動
	public static void LoadEraExRenameFile(string filepath)
	{
		if (!File.Exists(filepath))
		{
			return;
		}
		if (RenameDic.Count > 0)
			RenameDic.Clear();

		var fileLine = File.ReadAllLines(filepath, EncodingHandler.DetectEncoding(filepath));
		ScriptPosition? pos = null;
		Regex regex = unEscapedCommaRegex();
		try
		{
			var lineNo = 0;
			foreach (var line in fileLine)
			{
				pos = new ScriptPosition(filepath, lineNo);
				if (line.StartsWith(';'))
					continue;
				var tokens = regex.Split(line);
				if (tokens.Length == 2)
				{
					//右がERB中の表記、左が変換先になる。
					string key = $"[[{tokens[1].Trim()}]]";
					string value = tokens[0].Trim();
					RenameDic[key] = value;
				}
				lineNo++;
			}
		}
		catch (Exception e)
		{
			throw new CodeEE(e.Message, pos);
		}
	}
	#endregion

	static object warningListLock = new();

	public static void Warn(string str, ScriptPosition? pos, int level)
	{
		Warn(str, pos, level, null);
	}

	public static void Warn(string str, ScriptPosition? pos, int level, string? stack)
	{
		if (level < Config.DisplayWarningLevel && !Program.AnalysisMode)
			return;
		if (console != null && !console.RunERBFromMemory)
		{
			lock (warningListLock)
			{
				warningList.Add(new ParserWarning(str, pos, level, stack));
			}
		}
	}

	/// <summary>
	/// Parser中での警告出力
	/// </summary>
	/// <param name="str"></param>
	/// <param name="line"></param>
	/// <param name="level">警告レベル.0:軽微なミス.1:無視できる行.2:行が実行されなければ無害.3:致命的</param>
	public static void Warn(string str, LogicalLine line, int level, bool isError, bool isBackComp)
	{
		Warn(str, line, level, isError, isBackComp, null);
	}

	public static void Warn(string str, LogicalLine line, int level, bool isError, bool isBackComp, string? stack)
	{
		if (isError)
		{
			//Also written from the depth-layer workers of ErbLoader.ParseScript. Each line has a
			//single owning worker, but the console may flush from the UI thread concurrently.
			lock (warningListLock)
			{
				line.IsError = true;
				line.ErrMes = str;
			}
		}
		if (level < Config.DisplayWarningLevel && !Program.AnalysisMode)
			return;
		if (isBackComp && !Config.WarnBackCompatibility)
			return;
		if (console != null && !console.RunERBFromMemory)
		{
			lock (warningListLock)
			{
				warningList.Add(new ParserWarning(str, line.Position, level, stack));
			}
		}
		//				console.PrintWarning(str, line.Position, level);
	}

	private static List<ParserWarning> warningList = [];

	public static bool HasWarning
	{
		get
		{
			lock (warningListLock)
			{
				return warningList.Count > 0;
			}
		}
	}
	public static void ClearWarningList()
	{
		lock (warningListLock)
		{
			warningList.Clear();
		}
	}

	public static void FlushWarningList()
	{
		ParserWarning[] pending;
		lock (warningListLock)
		{
			pending = [.. warningList];
			warningList.Clear();
		}
		for (int i = 0; i < pending.Length; i++)
		{
			ParserWarning warning = pending[i];
			// console is non-null whenever warnings reached the list, and the dereference is kept as-is
			console!.PrintWarning(warning.WarningMes, warning.WarningPos, warning.WarningLevel);
			if (warning.StackTrace != null)
			{
				string[] stacks = warning.StackTrace.Split('\n');
				for (int j = 0; j < stacks.Length; j++)
				{
					console!.PrintSystemLine(stacks[j]);
				}
			}
		}
	}

	private sealed class ParserWarning
	{
		public ParserWarning(string mes, ScriptPosition? pos, int level, string? stackTrace)
		{
			WarningMes = mes;
			WarningPos = pos;
			WarningLevel = level;
			StackTrace = stackTrace;
		}
		public string WarningMes;
		public ScriptPosition? WarningPos;
		public int WarningLevel;
		public string? StackTrace;
	}

	[GeneratedRegex(@"(?<!\\),")]
	private static partial Regex unEscapedCommaRegex();

	public static void SingleLine(string str)
	{
		// Same as above: the console is present whenever this is called
		console!.PrintSingleLine(str);
	}

}
