using MinorShift.Emuera;

namespace Emuera.Tests;

/// <summary>
/// 把 Program 的目录静态指向一个临时游戏根,等价于启动时 SetDirPaths 的结果。
/// 目录静态是进程级的,用例不得假设另一个游戏根;临时目录留在 %TEMP%\emuera-tests 下不清理,
/// 因为 Program 仍引用它。
/// </summary>
internal static class GameRootScope
{
	public static string UseTempRoot(string name)
	{
		string root = Path.Combine(Path.GetTempPath(), "emuera-tests", $"{name}-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
		Program.SetDirPaths(root);
		return root;
	}
}
