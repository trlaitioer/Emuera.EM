using System.Windows.Forms;
using System.IO;
using System;

namespace MinorShift.Emuera.Runtime.Utils;

public static class AssemblyData
{
	static AssemblyData()
	{
		// Environment.ProcessPath is null only where the OS cannot report it; on Windows it names the running exe.
		ExePath = Environment.ProcessPath!;
		ExeName = Path.GetFileName(ExePath);
		// AssemblyName.Version is null only for an assembly built without a version attribute, which the SDK always emits.
		emueraVer = typeof(AssemblyData).Assembly.GetName().Version!;

		EmueraVersionText = "Emuera.NET " + Application.ProductVersion;
	}

	/// <summary>
	/// 実行ファイルのパス
	/// </summary>
	public static readonly string ExePath;

	public static readonly Version emueraVer;

	public static readonly string EmueraVersionText;

	/// <summary>
	/// 実行ファイルの名前。ディレクトリなし
	/// </summary>
	public static readonly string ExeName;

	/// <summary>
	/// 2重起動防止。既に同名exeが実行されているならばtrueを返す
	/// </summary>
	/// <returns></returns>
	public static bool PrevInstance()
	{
		string thisProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
		if (System.Diagnostics.Process.GetProcessesByName(thisProcessName).Length > 1)
		{
			return true;
		}

		return false;
	}
}
