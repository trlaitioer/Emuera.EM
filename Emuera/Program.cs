using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Windows.Forms;
using MinorShift.Emuera.Forms;

namespace MinorShift.Emuera;

internal static class Program
{
	/*
	コードの開始地点。
	ここでMainWindowを作り、
	MainWindowがProcessを作り、
	ProcessがGameBase・ConstantData・Variableを作る。


	*.ERBの読み込み、実行、その他の処理をProcessが、
	入出力をMainWindowが、
	定数の保存をConstantDataが、
	変数の管理をVariableが行う。

	と言う予定だったが改変するうちに境界が曖昧になってしまった。

	後にEmueraConsoleを追加し、それに入出力を担当させることに。

	1750 DebugConsole追加
	 Debugを全て切り離すことはできないので一部EmueraConsoleにも担当させる

	TODO: 1819 MainWindow & Consoleの入力・表示組とProcess&Dataのデータ処理組だけでも分離したい

	*/
	/// <summary>
	/// アプリケーションのメイン エントリ ポイントです。
	/// </summary>
	[STAThread]
	private static void Main(string[] args)
	{
		// memo: Shift-JISを扱うためのおまじない
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

		RootCommand rootCommand = new("Emuera");

		#region eee_カレントディレクトリー

		Option<string> exeDirOption = new(
			"--ExeDir",
			"与えられたフォルダのEraを起動します"
		);
		exeDirOption.AddAlias("-exedir");
		exeDirOption.AddAlias("-EXEDIR");
		rootCommand.AddOption(exeDirOption);

		Option<bool> debugModeOption = new(
			"-Debug",
			"デバッグモード"
		);
		debugModeOption.AddAlias("-debug");
		debugModeOption.AddAlias("-DEBUG");
		rootCommand.AddOption(debugModeOption);

		Option<bool> genLangOption = new(
			"-GenLang",
			"言語ファイルテンプレ生成"
		);
		genLangOption.AddAlias("-genlang");
		genLangOption.AddAlias("-GENLANG");
		rootCommand.AddOption(genLangOption);

		Argument<string[]> filesArg = new(
			"解析するファイル"
		) { Arity = ArgumentArity.ZeroOrMore };
		rootCommand.AddArgument(filesArg);

		ParseResult result = rootCommand.Parse(args);

		//実行ディレクトリが引数で与えられない場合、カレントディレクトリーをゲームフォルダとする
		string? exeDir = result.GetValueForOption(exeDirOption);
		SetDirPaths(exeDir ?? Directory.GetCurrentDirectory());

		#endregion

		//エラー出力用
		//1815 .exeが東方板のNGワードに引っかかるそうなので除去
		ExeName = Path.GetFileNameWithoutExtension(AssemblyData.ExeName);


		bool debugMode = result.GetValueForOption(debugModeOption);
		DebugMode = debugMode;

		bool genLang = result.GetValueForOption(genLangOption);
		if (genLang)
		{
			Lang.GenerateDefaultLangFile();
		}

		#region EM_私家版_Emuera多言語化改造

		List<string> otherArgs = [];

		//引数の後ろにある他のフラグにマッチしなかった文字列を解析指定されたファイルとみなす
		string[] fileArgs = result.GetValueForArgument(filesArg);
		string[] analysisRequestPaths = fileArgs;
		if (analysisRequestPaths.Length > 0)
		{
			/*
			foreach (var arg in args)
			{

				//if ((args.Length > 0) && (args[0].Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase)))
				if (arg.Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase))
				{
					// argsStart = 1;//デバッグモードかつ解析モード時に最初の1っこ(-DEBUG)を飛ばす
					DebugMode = true;
				}
				else if (arg.Equals("-GENLANG", StringComparison.CurrentCultureIgnoreCase))
				{
					Lang.GenerateDefaultLangFile();
				}
				else otherArgs.Add(arg);
			}
			//if (args.Length > argsStart)
			if (otherArgs.Count > 0)
			{
			*/
			//必要なファイルのチェックにはConfig読み込みが必須なので、ここではフラグだけ立てておく
			AnalysisMode = true;
			//}
		}

		#endregion

		Application.SetCompatibleTextRenderingDefault(false);

		ProfileOptimization.SetProfileRoot(exeDir ?? GameRoot.FullName);
		ProfileOptimization.StartProfile("profile");

		//Font files are registered before the configuration is loaded: loading it derives the render font size
		//by measuring the configured font, and FontFactory gives the private font collection priority over the
		//installed families.

		#region EE_フォントファイル対応

		//フォントファイルを読み込む
		if (Directory.Exists(FontRoot.FullName))
		{
			foreach (string fontFile in Directory.GetFiles(FontRoot.FullName, "*.ttf", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
			}

			foreach (string fontFile in Directory.GetFiles(FontRoot.FullName, "*.otf", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
			}
		}

		#endregion

		ConfigData.Instance.LoadConfig();
		JSONConfig.Load();

		//WMPも終了しておく
		FunctionIdentifier.bgm.close();
		for (int i = 0; i < FunctionIdentifier.sound.Length; i++)
		{
			FunctionIdentifier.sound[i]?.close();
		}

		#region EM_私家版_Emuera多言語化改造

		Lang.LoadLanguageFiles();
		Lang.SetLanguage();

		#endregion

		#region EM_私家版_Icon指定機能

		Icon? icon = null;
		{
			Bitmap? bmp = Utils.LoadImage(Utils.GetValidPath(Config.EmueraIcon));
			if (bmp != null)
			{
				icon = Utils.MakeIconFromBmpFile(bmp);
				bmp.Dispose();
			}
		}

		#endregion

		//二重起動の禁止かつ二重起動
		if (!Config.AllowMultipleInstances && AssemblyData.PrevInstance())
		{
			//Dialog.Show("既に起動しています", "多重起動を許可する場合、emuera.configを書き換えて下さい");
			Dialog.Show(Lang.UI.MainWindow.MsgBox.InstaceExists.Text, Lang.UI.MainWindow.MsgBox.MultiInstanceInfo.Text);
			return;
		}

		if (!Directory.Exists(CsvRoot.FullName))
		{
			//Dialog.Show("フォルダなし", "csvフォルダが見つかりません");
			Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.NoCsvFolder.Text);
			return;
		}

		if (!Directory.Exists(ErbRoot.FullName))
		{
			//Dialog.Show("フォルダなし", "erbフォルダが見つかりません");
			Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.NoErbFolder.Text);
			return;
		}

		if (DebugMode)
		{
			ConfigData.Instance.LoadDebugConfig();
			if (!Directory.Exists(DebugRoot.FullName))
			{
				try
				{
					Directory.CreateDirectory(DebugRoot.FullName);
				}
				catch
				{
					Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.FailedCreateDebugFolder.Text);
					return;
				}
			}
		}

		if (AnalysisMode)
		{
			AnalysisFiles = [];

			#region EM_私家版_Emuera多言語化改造

			// for (int i = argsStart; i < args.Length; i++)
			foreach (string path in analysisRequestPaths)
			{
				//if (!File.Exists(args[i]) && !Directory.Exists(args[i]))
				if (!File.Exists(path) && !Directory.Exists(path))
				{
					MessageBox.Show(Lang.UI.MainWindow.MsgBox.ArgPathNotExists.Text);
					return;
				}

				//if ((File.GetAttributes(args[i]) & FileAttributes.Directory) == FileAttributes.Directory)
				if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
				{
					//List<KeyValuePair<string, string>> fnames = Config.Config.GetFiles(args[i] + "\\", "*.ERB");
					List<KeyValuePair<string, string>> fnames = Config.GetFiles(path + "\\", "*.ERB");
					for (int j = 0; j < fnames.Count; j++)
					{
						AnalysisFiles.Add(fnames[j].Value);
					}
				}
				else
				{
					//if (Path.GetExtension(args[i]).ToUpper() != ".ERB")
					if (!Path.GetExtension(path).Equals(".ERB", StringComparison.OrdinalIgnoreCase))
					{
						MessageBox.Show(Lang.UI.MainWindow.MsgBox.InvalidArg.Text);
						return;
					}

					//AnalysisFiles.Add(args[i]);
					AnalysisFiles.Add(path);
				}
			}

			#endregion
		}

		ApplicationConfiguration.Initialize();

		using MainWindow win = new(args);
		{
			#region EM_私家版_Emuera多言語化改造

			win.TranslateUI();

			#endregion

			#region EM_私家版_Icon指定機能

			if (icon != null)
			{
				win.SetupIcon(icon);
			}

			#endregion

			Application.Run(win);
			/* VVII版マージ前の起動処理
			MainWindow win = null;
			StartTime = WinmmTimer.TickCount;
			using (win = new MainWindow())
			{
				#region EM_私家版_Emuera多言語化改造
				win.TranslateUI();
				#endregion
				#region EM_私家版_Icon指定機能
				if (icon != null)
					win.SetupIcon(icon);
				#endregion
				Application.Run(win);
				Content.AppContents.UnloadContents();
				if (!Reboot)
					break;
				RebootWinState = win.WindowState;
				if (win.WindowState == FormWindowState.Normal)
				{
					RebootClientY = win.ClientSize.Height;
					RebootLocation = win.Location;
				}
				else
				{
					RebootClientY = 0;
					RebootLocation = new Point();
				}
			}
			//条件次第ではParserMediatorが空でない状態で再起動になる場合がある
			ParserMediator.ClearWarningList();
			ParserMediator.Initialize(null);
			GlobalStatic.Reset();
			//GC.Collect();
			#region EE_メモリリークの解決
			ConfigData.Instance.ReLoadConfig();

			break;
			*/
		}
		/*
		if (rebootFlag)
			Application.Restart();
		#endregion
		*/
	}

	/// <summary>
	/// Points every game directory at <paramref name="gameDir"/>. This is the single place a game directory
	/// is normalized: "--ExeDir" may be relative, so it is resolved by the file system against the current
	/// directory rather than lexically.
	/// </summary>
	internal static void SetDirPaths(string gameDir)
	{
		GamePath root = GamePath.FromRoot(PurePath.From(Path.GetFullPath(new DirectoryInfo(gameDir).FullName)));

		gameRoot = root;
		csvRoot = root.CombineName("csv");
		erbRoot = root.CombineName("erb");
		debugRoot = root.CombineName("debug");
		datRoot = root.CombineName("dat");
		contentRoot = root.CombineName("resources");
		soundRoot = root.CombineName("sound");
		fontRoot = root.CombineName("font");
		defaultLogFile = root.CombineName("emuera.log");
	}

	#region eee_カレントディレクトリー

	private static GamePath? gameRoot;
	private static GamePath? csvRoot;
	private static GamePath? erbRoot;
	private static GamePath? debugRoot;
	private static GamePath? datRoot;
	private static GamePath? contentRoot;
	private static GamePath? soundRoot;
	private static GamePath? fontRoot;
	private static GamePath? defaultLogFile;

	/// <summary>The game directory. Every other directory below is one of its entries.</summary>
	public static GamePath GameRoot => gameRoot ?? throw NotSetUpException();

	public static GamePath CsvRoot => csvRoot ?? throw NotSetUpException();
	public static GamePath ErbRoot => erbRoot ?? throw NotSetUpException();
	public static GamePath DebugRoot => debugRoot ?? throw NotSetUpException();
	public static GamePath DatRoot => datRoot ?? throw NotSetUpException();
	public static GamePath ContentRoot => contentRoot ?? throw NotSetUpException();
	public static GamePath SoundRoot => soundRoot ?? throw NotSetUpException();
	public static GamePath FontRoot => fontRoot ?? throw NotSetUpException();

	/// <summary>Log file written when OUTPUTLOG is called without a name.</summary>
	public static GamePath DefaultLogFile => defaultLogFile ?? throw NotSetUpException();

	private static InvalidOperationException NotSetUpException()
	{
		return new InvalidOperationException($"{nameof(SetDirPaths)} has to run before the game directories are read.");
	}

	#endregion

	public static string? ExeName { get; private set; }

	/// <summary>Resolves a script supplied sound name inside the sound directory.</summary>
	public static bool TryResolveSoundFile(string? name, [NotNullWhen(true)] out GamePath? file)
	{
		return SoundRoot.TryResolveChild(name, out file, out _);
	}

	/// <summary>
	/// Resolves <see cref="ScriptPosition.Filename"/> to the file it names: a rooted value is the file itself,
	/// otherwise the name is relative to csv\ or erb\, chosen by the extension. Callers that must keep an
	/// analyzed position as it was given on the command line do not call this.
	/// </summary>
	public static bool TryResolveScriptFile(string? filename, [NotNullWhen(true)] out PurePath? file)
	{
		file = null;
		if (!PurePath.TryFrom(filename, out PurePath? path, out _))
		{
			return false;
		}

		if (!path.IsRooted)
		{
			PurePath baseDirectory = path.Extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ? CsvRoot : ErbRoot;
			path = baseDirectory.Combine(path);
		}

		file = path;
		return true;
	}


	public static bool rebootFlag;

	//public static int RebootClientX = 0;
	//public static int RebootClientY = 0;
	public static FormWindowState RebootWinState = FormWindowState.Normal;
	//public static Point RebootLocation;

	public static bool AnalysisMode;
	public static List<string>? AnalysisFiles;

	//public static bool debugMode = false;
	//public static bool DebugMode { get { return debugMode; } }
	public static bool DebugMode { get; private set; }
}
