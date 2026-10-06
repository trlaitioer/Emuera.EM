using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Loader;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Loader;

/// <summary>
/// 装载一段临时 ERB 并取回函数体的共享夹具(nullable-migration-24/27 与 ErbLoaderNestCheckTests 共用,
/// 取代各自逐字复制的拷贝)。
/// </summary>
internal static class ErbLoaderFixture
{
	/// <summary>
	/// 原样写入 <paramref name="fileLines"/> 后经 <see cref="ErbLoader.LoadErbList"/> 装载,
	/// 返回「无差错装载」标志与该 (唯一) 函数的指令行。夹具放在 Depth==0 的系统标签里:
	/// 默认 Config.IgnoreUncalledFunction 会跳过未被调用的普通函数。
	/// </summary>
	public static async Task<(bool Loaded, List<InstructionLine> Body)> LoadErb(string rootName, params string[] fileLines)
	{
		string root = GameRootScope.UseTempRoot(rootName);
		string erbDir = Path.Combine(root, "erb");
		Directory.CreateDirectory(erbDir);
		string path = Path.Combine(erbDir, "test.erb");
		File.WriteAllLines(path, fileLines, new UTF8Encoding(false));
		// EraStreamReader 走 Preload 的缓存,未预读的文件在 OpenOnCache 里直接 KeyNotFound
		await Preload.Load(path);

		// scaningLine 的占位值由 TestBootstrap 设置,装载结束时会被清掉;用例必须还原,
		// 否则后续用例的 GetScaningLine 会解引用未初始化的 Process.state。
		LogicalLine? placeholder = GlobalStatic.Process.scaningLine;
		GlobalStatic.LabelDictionary.RemoveAll();
		bool loaded;
		try
		{
			var loader = new ErbLoader(null!, GlobalStatic.EMediator, GlobalStatic.Process);
			loaded = await loader.LoadErbList([path], GlobalStatic.LabelDictionary);
		}
		finally
		{
			GlobalStatic.Process.scaningLine = placeholder;
		}

		FunctionLabelLine label = Assert.Single(GlobalStatic.LabelDictionary.GetAllLabels(false));
		return (loaded, InstructionLines(label));
	}

	/// <summary>以 <c>@SYSTEM_TITLE</c> 为函数头装载一段函数体。</summary>
	public static Task<(bool Loaded, List<InstructionLine> Body)> LoadSystemTitleBody(string rootName, params string[] bodyLines)
		=> LoadErb(rootName, ["@SYSTEM_TITLE", .. bodyLines]);

	/// <summary>
	/// 取回标签之后的全部指令行。<c>$标签</c>是 LabelLine,故不能以「链上出现非 InstructionLine」为终止条件。
	/// </summary>
	public static List<InstructionLine> InstructionLines(FunctionLabelLine label)
	{
		List<InstructionLine> body = [];
		for (LogicalLine? line = label.NextLine; line != null; line = line.NextLine)
		{
			if (line is InstructionLine instruction)
			{
				body.Add(instruction);
			}
		}

		return body;
	}
}
