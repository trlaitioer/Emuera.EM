using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Loader;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.PluginSystem;
using MinorShift.Emuera.UI.Game.Image;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using trmb = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.MessageBox;
using trsl = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.SystemLine;

namespace MinorShift.Emuera.GameProc;

internal sealed partial class Process(EmueraConsole view)
{
	public LogicalLine? getCurrentLine => state.CurrentLine;

	/// <summary>
	/// @~~と$~~を集めたもの。CALL命令などで使う
	/// 実行順序はLogicalLine自身が保持する。
	/// </summary>
	private LabelDictionary labelDic = null!;

	public LabelDictionary LabelDictionary => labelDic;

	/// <summary>
	/// 変数全部。スクリプト中で必要になる変数は（ユーザーが直接触れないものも含め）この中にいれる
	/// </summary>
	private VariableEvaluator vEvaluator = null!;

	public VariableEvaluator VEvaluator => vEvaluator;
	private ExpressionMediator exm = null!;
	private GameBase gamebase = null!;
	public GameBase gameBase => gamebase;
	private readonly EmueraConsole console = view;
	private IdentifierDictionary idDic = null!;
	private ProcessState state = null!;
	private ProcessState originalState = null!; //リセットする時のために

	private bool noError;

	//色々あって復活させてみる
	private bool initialiing;
	public bool inInitializeing => initialiing;

	public async Task<bool> Initialize(StreamWriter? logWriter)
	{
		Stopwatch stopWatch = new();
		stopWatch.Start();
		LexicalAnalyzer.UseMacro = false;
		state = new ProcessState(console);
		originalState = state;
		initialiing = true;
		try
		{
			logWriter?.WriteLine($"Proc:Init:Start {stopWatch.ElapsedMilliseconds}ms");
			logWriter?.WriteLine($"Proc:Init:Parser:Start {stopWatch.ElapsedMilliseconds}ms");
			ParserMediator.Initialize(console);
			//コンフィグファイルに関するエラーの処理（コンフィグファイルはこの関数に入る前に読込済み）
			if (ParserMediator.HasWarning)
			{
				ParserMediator.FlushWarningList();
				if (Dialog.ShowPrompt(trmb.ConfigFileError.Text, trmb.ConfigError.Text))
				{
					console.PrintSystemLine(trsl.SelectExitConfigMB.Text);
					return false;
				}
			}

			logWriter?.WriteLine($"Proc:Init:Parser:End {stopWatch.ElapsedMilliseconds}ms");

			logWriter?.WriteLine($"Proc:Init:Image:Start {stopWatch.ElapsedMilliseconds}ms");
			//リソースフォルダ読み込み
			Exception? err = await Task.Run(() => AppContents.LoadContents(false));
			if (err != null)
			{
				ParserMediator.FlushWarningList();
				console.PrintSystemLine(trsl.ResourceReadError.Text);
				console.Print(err.ToString());
				return false;
			}

			ParserMediator.FlushWarningList();
			logWriter?.WriteLine($"Proc:Init:Image:End {stopWatch.ElapsedMilliseconds}ms");

			logWriter?.WriteLine($"Proc:Init:KeyMacro:Start {stopWatch.ElapsedMilliseconds}ms");
			//キーマクロ読み込み

			#region eee_カレントディレクトリー

			if (Config.UseKeyMacro && !Program.AnalysisMode)
			{
				if (File.Exists(KeyMacro.MacroPath.FullName))
				{
					if (Config.DisplayReport)
					{
						console.PrintSystemLine(trsl.LoadingMacro.Text);
					}

					KeyMacro.LoadMacroFile(KeyMacro.MacroPath.FullName);
				}
			}

			#endregion

			logWriter?.WriteLine($"Proc:Init:KeyMacro:End {stopWatch.ElapsedMilliseconds}ms");

			logWriter?.WriteLine($"Proc:Init:Replace:Start {stopWatch.ElapsedMilliseconds}ms");
			//_replace.csv読み込み
			if (Config.UseReplaceFile && !Program.AnalysisMode)
			{
				if (File.Exists(Program.CsvRoot.CombineName("_Replace.csv").FullName))
				{
					if (Config.DisplayReport)
					{
						console.PrintSystemLine(trsl.LoadingReplace.Text);
					}

					ConfigData.Instance.LoadReplaceFile(Program.CsvRoot.CombineName("_Replace.csv").FullName);
					if (ParserMediator.HasWarning)
					{
						ParserMediator.FlushWarningList();
						if (Dialog.ShowPrompt(trmb.ReplaceFileError.Text, trmb.ReplaceError.Text))
						{
							console.PrintSystemLine(trsl.SelectExitReplaceMB.Text);
							return false;
						}
					}
				}
			}

			logWriter?.WriteLine($"Proc:Init:Replace:End {stopWatch.ElapsedMilliseconds}ms");

			Config.SetReplace(ConfigData.Instance);
			//ここでBARを設定すれば、いいことに気づいた予感
			console.setStBar(Config.DrawLineString);

			logWriter?.WriteLine($"Proc:Init:Rename:Load:Start {stopWatch.ElapsedMilliseconds}ms");
			//_rename.csv読み込み
			if (Config.UseRenameFile)
			{
				if (File.Exists(Program.CsvRoot.CombineName("_Rename.csv").FullName))
				{
					if (Config.DisplayReport || Program.AnalysisMode)
					{
						console.PrintSystemLine(trsl.LoadingRename.Text);
					}

					ParserMediator.LoadEraExRenameFile(Program.CsvRoot.CombineName("_Rename.csv").FullName);
				}
				else
				{
					console.PrintError(trsl.MissingRename.Text);
				}
			}

			logWriter?.WriteLine($"Proc:Init:Rename:Load:End {stopWatch.ElapsedMilliseconds}ms");

			if (!Config.DisplayReport)
			{
				console.PrintSingleLine(Config.LoadLabel);
				console.RefreshStrings(true);
			}

			//gamebase.csv読み込み
			gamebase = new GameBase();
			if (!await Task.Run(() => gamebase.LoadGameBaseCsv(Program.CsvRoot.CombineName("GAMEBASE.CSV").FullName)))
			{
				ParserMediator.FlushWarningList();
				console.PrintSystemLine(trsl.GamebaseError.Text);
				return false;
			}

			console.SetWindowTitle(gamebase.ScriptWindowTitle);
			GlobalStatic.GameBaseData = gamebase;
			logWriter?.WriteLine($"Proc:Init:MainCSV:End {stopWatch.ElapsedMilliseconds}ms");

			//前記以外のcsvを全て読み込み
			ConstantData constant = new(Program.CsvRoot.FullName, console, Config.DisplayReport);
			logWriter?.WriteLine($"Proc:Init:EtcCSV:End {stopWatch.ElapsedMilliseconds}ms");

			GlobalStatic.ConstantData = constant;
			TrainName = constant.GetCsvNameList(VariableCode.TRAINNAME);
			logWriter?.WriteLine($"Proc:Init:EtcCSV:End {stopWatch.ElapsedMilliseconds}ms");

			vEvaluator = new VariableEvaluator(gamebase, constant);
			GlobalStatic.VEvaluator = vEvaluator;

			idDic = new IdentifierDictionary(vEvaluator.VariableData);
			GlobalStatic.IdentifierDictionary = idDic;

			VariableParser.Initialize();

			exm = new ExpressionMediator(this, vEvaluator, console);
			GlobalStatic.EMediator = exm;

			logWriter?.WriteLine($"Proc:Init:ERH:Start {stopWatch.ElapsedMilliseconds}ms");

			labelDic = new LabelDictionary();
			GlobalStatic.LabelDictionary = labelDic;
			ErhLoader hLoader = new(console, idDic, this);

			LexicalAnalyzer.UseMacro = false;

			PluginManager.GetInstance().SetParent(this, state, exm);
			PluginManager.GetInstance().LoadPlugins();
			if (GlobalStatic.ExistPlugin && Config.PluginAvailableWarn)
			{
				console.PrintSingleLine(trsl.PluginAvailable.Text);
			}

			//ERH読込
			if (!await Task.Run(() => hLoader.LoadHeaderFiles(Program.ErbRoot.FullName, Config.DisplayReport)))
			{
				ParserMediator.FlushWarningList();
				console.PrintSystemLine("");
				return false;
			}

			LexicalAnalyzer.UseMacro = idDic.UseMacro();
			logWriter?.WriteLine($"Proc:Init:ERH:End {stopWatch.ElapsedMilliseconds}ms");

			//TODO:ユーザー定義変数用のcsvの適用

			//ERB読込
			logWriter?.WriteLine($"Proc:Init:ERB:Start {stopWatch.ElapsedMilliseconds}ms");
			ErbLoader loader = new(console, exm, this);
			if (Program.AnalysisMode)
			{
				// Program.cs fills AnalysisFiles exactly when it turns AnalysisMode on, so it is non-null here.
				noError = await loader.LoadErbList(Program.AnalysisFiles!, labelDic);
			}
			else
			{
				noError = await loader.LoadErbDir(Program.ErbRoot.FullName, Config.DisplayReport, labelDic);
			}

			logWriter?.WriteLine($"Proc:Init:ERB:End {stopWatch.ElapsedMilliseconds}ms");

			initSystemProcess();
			initialiing = false;

			logWriter?.WriteLine($"Proc:Init:End {stopWatch.ElapsedMilliseconds}ms");
		}
		catch (Exception e)
		{
			handleException(e, null, true);
			console.PrintSystemLine(trsl.ErhLoadingError.Text);
			return false;
		}

		if (labelDic == null)
		{
			return false;
		}

		state.Begin(BeginType.TITLE);
		return true;
	}

	public async Task ReloadErb()
	{
		await Preload.Load(Program.ErbRoot.FullName);
		await Preload.Load(Program.CsvRoot.FullName);
		saveCurrentState(false);
		state.SystemState = SystemStateCode.System_Reloaderb;
		ErbLoader loader = new(console, exm, this);
		await loader.LoadErbDir(Program.ErbRoot.FullName, false, labelDic);
		console.ReadAnyKey();
	}

	public async Task ReloadPartialErb(List<string> paths)
	{
		saveCurrentState(false);
		state.SystemState = SystemStateCode.System_Reloaderb;
		await Preload.Load(paths);
		ErbLoader loader = new(console, exm, this);
		await loader.LoadErbList(paths, labelDic);
		console.ReadAnyKey();
	}

	public void SetCommnds(long count)
	{
		coms = new List<long>((int)count);
		isCTrain = true;
		long[] selectcom = vEvaluator.SELECTCOM_ARRAY;
		if (count >= selectcom.Length)
		{
			throw new CodeEE(trerror.CalltrainArgMoreThanSelectcom.Text);
		}

		for (int i = 0; i < (int)count; i++)
		{
			coms.Add(selectcom[i + 1]);
		}
	}

	public bool ClearCommands()
	{
		coms.Clear();
		count = 0;
		isCTrain = false;
		skipPrint = true;
		return callFunction("CALLTRAINEND", false, false);
	}

	#region EE_INPUTMOUSEKEYのボタン対応

	// public void InputResult5(int r0, int r1, int r2, int r3, int r4)
	public void InputResult5(int r0, int r1, int r2, int r3, int r4, long r5)
	{
		long[] result = vEvaluator.RESULT_ARRAY;
		result[0] = r0;
		result[1] = r1;
		result[2] = r2;
		result[3] = r3;
		result[4] = r4;
		result[5] = r5;
	}

	#endregion

	public void InputInteger(long i)
	{
		GlobalStatic.ctrlZ.Add(i.ToString());
		vEvaluator.RESULT = i;
	}

	#region EM_私家版_INPUT系機能拡張

	public void InputInteger(long idx, long i)
	{
		if (idx < vEvaluator.RESULT_ARRAY.Length)
		{
			vEvaluator.RESULT_ARRAY[idx] = i;
		}
	}

	public void InputString(long idx, string i)
	{
		if (idx < vEvaluator.RESULT_ARRAY.Length)
		{
			vEvaluator.RESULTS_ARRAY[idx] = i;
		}
	}

	#endregion

	public void InputSystemInteger(long i)
	{
		GlobalStatic.ctrlZ.Add(i.ToString());
		systemResult = i;
	}

	public void InputString(string s)
	{
		GlobalStatic.ctrlZ.Add(s);
		vEvaluator.RESULTS = s;
	}

	private readonly Stopwatch startTime = new();

	public void DoScript()
	{
		startTime.Restart();
		state.lineCount = 0;
		bool systemProcRunning = true;
		try
		{
			while (true)
			{
				methodStack = 0;
				systemProcRunning = true;
				while (state.ScriptEnd && console.IsRunning)
				{
					runSystemProc();
				}

				if (!console.IsRunning)
				{
					break;
				}

				systemProcRunning = false;
				runScriptProc();
			}
		}
		catch (Exception ec)
		{
			LogicalLine? currentLine = state.ErrorLine;
			if (currentLine is NullLine)
			{
				currentLine = null;
			}

			if (systemProcRunning)
			{
				handleExceptionInSystemProc(ec, currentLine, true);
			}
			else
			{
				handleException(ec, currentLine, true);
			}
		}
	}

	public void BeginTitle()
	{
		vEvaluator.ResetData();
		state = originalState;
		state.Begin(BeginType.TITLE);
	}

	public void UpdateCheckInfiniteLoopState()
	{
		startTime.Restart();
		state.lineCount = 0;
	}

	private void checkInfiniteLoop()
	{
		//うまく動かない。BEEP音が鳴るのを止められないのでこの処理なかったことに（1.51）
		////フリーズ防止。処理中でも履歴を見たりできる
		//System.Windows.Forms.Application.DoEvents();
		////System.Threading.Thread.Sleep(0);

		//if (!console.Enabled)
		//{
		//    //DoEvents()の間にウインドウが閉じられたらおしまい。
		//    console.ReadAnyKey();
		//    return;
		//}
		long elapsedTime = startTime.ElapsedMilliseconds;
		if (elapsedTime < Config.InfiniteLoopAlertTime)
		{
			return;
		}

		LogicalLine? currentLine = state.CurrentLine;
		if (currentLine is null or NullLine)
		{
			return; //現在の行が特殊な状態ならスルー
		}

		if (!console.Enabled)
		{
			return; //クローズしてるとMessageBox.Showができないので。
		}

		string caption = string.Format(trmb.InfiniteLoop.Text);
		string text = string.Format(
			trmb.TooLongLoop.Text,
			currentLine.Position!.Value.Filename, currentLine.Position.Value.LineNo, state.lineCount, elapsedTime);
		if (Dialog.ShowPrompt(text, caption))
		{
			throw new CodeEE(trerror.SelectExitInfiniteLoopMB.Text);
		}
		else
		{
			state.lineCount = 0;
			startTime.Restart();
		}
	}

	private int methodStack;

	public SingleTerm? GetValue(SuperUserDefinedMethodTerm udmt)
	{
		methodStack++;
		if (methodStack > 100)
		{
			//StackOverflowExceptionはcatchできない上に再現性がないので発生前に一定数で打ち切る。
			//環境によっては100以前にStackOverflowExceptionがでるかも？
			throw new CodeEE(trerror.OverflowFuncStack.Text);
		}

		SingleTerm? ret = null;
		int temp_current = state.currentMin;
		state.currentMin = state.functionCount;
		udmt.Call.updateRetAddress(state.CurrentLine);
		try
		{
			state.IntoFunction(udmt.Call, udmt.Argument, exm);
			//do whileの中でthrow されたエラーはここではキャッチされない。
			//#functionを全て抜けてDoScriptでキャッチされる。
			runScriptProc();
			ret = state.MethodReturnValue;
		}
		finally
		{
			if (udmt.Call.TopLabel.hasPrivDynamicVar)
			{
				udmt.Call.TopLabel.ScopeOut();
			}

			//1756beta2+v3:こいつらはここにないとデバッグコンソールで式中関数が事故った時に大事故になる
			state.currentMin = temp_current;
			methodStack--;
		}

		return ret;
	}

	public void clearMethodStack()
	{
		methodStack = 0;
	}

	public int MethodStack()
	{
		return methodStack;
	}

	public ScriptPosition? GetRunningPosition()
	{
		LogicalLine? line = state.ErrorLine;
		if (line == null)
		{
			return null;
		}

		return line.Position;
	}

	/*
			private readonly string scaningScope = null;
			private string GetScaningScope()
			{
				if (scaningScope != null)
					return scaningScope;
				return state.Scope;
			}
	*/
	/// <summary>
	/// The "current line" during parsing. Because function bodies are parsed in parallel, each
	/// thread keeps its own thread-local value and reads that first. A thread that never set one
	/// (tests, runtime code) keeps reading this instance field, so paths that do not take part in
	/// the parallel parse behave exactly as before.
	/// </summary>
	public LogicalLine? scaningLine;

	[ThreadStatic] private static LogicalLine? scaningLineForThread;
	[ThreadStatic] private static bool scaningLineForThreadInitialized;

	/// <summary>
	/// Sets the "current line" for a parsing thread. Parallel workers are pooled threads, so this
	/// must always be used to be sure the thread-local side is owned rather than inherited.
	/// </summary>
	/// <remarks>Null is passed by a parsing worker that is done with the line.</remarks>
	internal void SetScaningLineForThread(LogicalLine? line)
	{
		if (!scaningLineForThreadInitialized)
		{
			//This thread has not joined the parse yet. Inherit the outer value (a test
			//placeholder, for instance) exactly once, then switch to the thread-local side.
			scaningLineForThread = scaningLine;
			scaningLineForThreadInitialized = true;
		}

		scaningLineForThread = line;
	}

	/// <summary>
	/// The line a name is resolved against. A null thread-local value does not mean "no current line":
	/// it falls through to the instance field and then to the line being executed, which is what lets a
	/// lazily built argument see its own line.
	/// </summary>
	internal LogicalLine? GetScaningLine()
	{
		if (scaningLineForThreadInitialized && scaningLineForThread != null)
		{
			return scaningLineForThread;
		}

		if (scaningLine != null)
		{
			return scaningLine;
		}

		LogicalLine? line = state.ErrorLine;
		if (line == null)
		{
			return null;
		}

		return line;
	}


	private void handleExceptionInSystemProc(Exception exc, LogicalLine? current, bool playSound)
	{
		console.ThrowError(playSound);
		ErrorReportKind kind = ErrorReport.Classify(exc);
		console.PrintError(string.Format(ErrorReport.SystemProcHeader(kind).Text, AssemblyData.EmueraVersionText));
		console.PrintError(ErrorReport.Message(kind, exc));
		if (ErrorReport.PrintsStackTrace(kind))
		{
			// Exception.StackTrace is null only for an exception that was never thrown; this one was caught.
			string[] stack = exc.StackTrace!.Split('\n');
			for (int i = 0; i < stack.Length; i++)
			{
				console.PrintError(stack[i]);
			}
		}
	}

	private void handleException(Exception exc, LogicalLine? current, bool playSound)
	{
		console.ThrowError(playSound);
		ScriptPosition? position = null;
		if (exc is EmueraException { Position: not null } ee)
		{
			position = ee.Position;
		}
		else if (current is { Position: not null })
		{
			position = current.Position;
		}

		string posString = "";
		// A memory line carries ScriptPosition's default value, which names no file: only a usable
		// position becomes part of the message
		if (LineLocationText.IsUsablePosition(position))
		{
			if (position.Value.LineNo >= 0)
			{
				posString = string.Format(trerror.ErrorFileAndLine.Text, position.Value.Filename, position.Value.LineNo.ToString());
			}
			else
			{
				posString = string.Format(trerror.ErrorFile.Text, position.Value.Filename);
			}
		}

		ErrorReportKind kind = ErrorReport.Classify(exc);
		if (kind == ErrorReportKind.Code)
		{
			if (position != null)
			{
				if (current is InstructionLine { FunctionCode: FunctionCode.THROW })
				{
					console.PrintErrorButton(string.Format(trerror.HasThrow.Text, posString), position);
					printRawLine(position);
					console.PrintError(string.Format(trerror.ThrowMessage.Text, exc.Message));
				}
				else
				{
					console.PrintErrorButton(string.Format(ErrorReport.Header(kind).Text, posString, AssemblyData.EmueraVersionText), position);
					printRawLine(position);
					console.PrintError(string.Format(trerror.ErrorMessage.Text, exc.Message));
				}

				// The current function cannot always be named (a line without a parent label, a plugin or
				// debug line), but the call stack below is still worth printing when it has entries
				if (LineLocationText.CurrentFunction(current) is { } currentFunction)
				{
					console.PrintError(currentFunction);
				}

				console.PrintError(trerror.FuncCallStack.Text);
				int depth = 0;
				while (state.GetReturnAddressSequensial(depth++) is { } parent)
				{
					if (LineLocationText.CallStackEntry(parent) is { } entry)
					{
						console.PrintErrorButton(entry, parent.Position);
					}
				}
			}
			else
			{
				console.PrintError(string.Format(ErrorReport.Header(kind).Text, posString, AssemblyData.EmueraVersionText));
				console.PrintError(exc.Message);
			}
		}
		else
		{
			console.PrintError(string.Format(ErrorReport.Header(kind).Text, posString, AssemblyData.EmueraVersionText));
			console.PrintError(ErrorReport.Message(kind, exc));
			if (ErrorReport.PrintsStackTrace(kind))
			{
				// Exception.StackTrace is null only for an exception that was never thrown; this one was caught.
				string[] stack = exc.StackTrace!.Split('\n');
				for (int i = 0; i < stack.Length; i++)
				{
					console.PrintError(stack[i]);
				}
			}
		}
	}

	public void printRawLine(ScriptPosition? position)
	{
		string str = getRawTextFormFilewithLine(position);
		if (!string.IsNullOrEmpty(str))
		{
			console.PrintError(str);
		}
	}

	public static string getRawTextFormFilewithLine(ScriptPosition? position)
	{
		if (position is not { } pos || pos.LineNo <= 0
		                            || !Program.TryResolvePositionFile(pos, out string? path)
		                            || !(Path.GetExtension(path).Equals(".erb", StringComparison.OrdinalIgnoreCase)
		                                 || Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase)))
		{
			return "";
		}

		if (!File.Exists(path))
		{
			return "";
		}

		return File.ReadLines(path, EncodingHandler.DetectEncoding(path)).Skip(pos.LineNo - 1).First();
	}
}
