using MinorShift.Emuera;

namespace Emuera.Tests;

/// <summary>
/// 把 Program 的目录静态指向一个临时游戏根,等价于启动时 SetDirPaths 的结果。
/// 目录静态是进程级的,用例不得假设另一个游戏根;临时目录留在 %TEMP%\emuera-tests 下不清理,
/// 因为 Program 仍引用它。
/// </summary>
internal static class GameRootScope
{
	/// <summary>
	/// 只在 %TEMP%\emuera-tests 下开一个新临时目录,不动 Program 的目录静态。被测代码不读游戏根时用它,
	/// 免得把进程级静态指向一个与被测行为无关的游戏根。
	/// </summary>
	public static string NewTempDirectory(string name)
	{
		string path = Path.Combine(Path.GetTempPath(), "emuera-tests", $"{name}-{Guid.NewGuid():N}");
		Directory.CreateDirectory(path);
		return path;
	}

	public static string UseTempRoot(string name)
	{
		string root = NewTempDirectory(name);
		Program.SetDirPaths(root);
		return root;
	}
}
