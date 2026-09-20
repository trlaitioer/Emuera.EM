using System.Windows.Forms;
using System.IO;
using System;

namespace MinorShift.Emuera.Runtime.Utils;

public static class AssemblyData
{
	static AssemblyData()
	{
		ExePath = Environment.ProcessPath!;
		ExeName = Path.GetFileName(ExePath);
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
