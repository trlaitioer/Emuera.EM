using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;

namespace Emuera.Tests;

/// <summary>
/// 运行时最小初始化夹具:等价 Process.Initialize 的解析器子集,无 UI/ERB/ERH 依赖。
/// 需要 GlobalStatic/解析器状态的测试先调用 Initialize()。
/// </summary>
internal static class TestBootstrap
{
	static bool initialized;

	public static void Initialize()
	{
		if (initialized)
			return;
		// csv fixture 用空目录:各 loader 对缺失文件直接 early-return,
		// 规避 ConstantData.LoadData 向 null console 输出的"文件存在但打不开"路径
		var csvDir = Path.Combine(Path.GetTempPath(), "emuera-tests", "csv-empty");
		Directory.CreateDirectory(csvDir);

		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		LangManager.setEncode(932);
		ParserMediator.Initialize(null);
		// 填充 PalamLvDef/ExpLvDef 等 replace 系静态默认值(VariableData.SetDefaultValue 依赖);
		// GetConfigValue 直接取 ConfigData 内建默认值,不依赖配置文件
		Config.SetReplace(ConfigData.Instance);
		// FunctionIdentifier 静态构造读取 JSONConfig.Data;Load() 依赖 Program.ExeDir(测试中未设),
		// 故直接赋默认值——等价于无 emuera.config.json 时的首次启动
		JSONConfig.Data = new JSONConfigData();

		var gamebase = new GameBase();
		gamebase.LoadGameBaseCsv(Path.Combine(csvDir, "GAMEBASE.CSV"));
		GlobalStatic.GameBaseData = gamebase;

		var constant = new ConstantData();
		constant.LoadData(csvDir, null!, false);
		GlobalStatic.ConstantData = constant;

		var vEvaluator = new VariableEvaluator(gamebase, constant);
		GlobalStatic.VEvaluator = vEvaluator;

		var idDic = new IdentifierDictionary(vEvaluator.VariableData);
		GlobalStatic.IdentifierDictionary = idDic;

		VariableParser.Initialize();

		GlobalStatic.EMediator = new ExpressionMediator(null, vEvaluator, null);

		GlobalStatic.Process = new Process(null)
		{
			// GetScaningLine 在 scaningLine==null 时会解引用未初始化的 Process.state(仅 Initialize 创建);
			// 解析期间该字段本就指向当前解析行,以占位行保持等价语义(GetVariableToken 的 private 变量路径依赖)
			scaningLine = new GotoLabelLine(new ScriptPosition("test", 1), "TEST")
		};
		GlobalStatic.LabelDictionary = new LabelDictionary();

		initialized = true;
	}
}
