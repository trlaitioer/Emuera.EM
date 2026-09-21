using MinorShift.Emuera;
using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.PathUtils;
using Xunit;

namespace Emuera.Tests.Runtime.Script;

/// <summary>
/// ScriptPosition.Filename 的解析规则:分析模式自带绝对路径,其余位置相对 csv\ / erb\。
/// 同一规则决定取源码行(Process.getRawTextFormFilewithLine)与打开编辑器(EmueraConsole.OpenErrorFile)的落点。
/// </summary>
public class ScriptPositionFileTests
{
	private const string FirstLine = "first";
	private const string SecondLine = "second";

	[Fact]
	public void TryResolveScriptFile_RelativeErbName_IsResolvedBelowTheErbDirectory()
	{
		string root = GameRootScope.UseTempRoot("scriptfile-erb");

		Assert.True(Program.TryResolveScriptFile(@"sub\a.erb", out PurePath? file));
		Assert.Equal(Path.Combine(root, "erb", "sub", "a.erb"), file.ToString());
	}

	[Fact]
	public void TryResolveScriptFile_RelativeCsvName_IsResolvedBelowTheCsvDirectory()
	{
		string root = GameRootScope.UseTempRoot("scriptfile-csv");

		Assert.True(Program.TryResolveScriptFile("Chara.csv", out PurePath? file));
		Assert.Equal(Path.Combine(root, "csv", "Chara.csv"), file.ToString());
	}

	[Fact]
	public void TryResolveScriptFile_AbsolutePath_IsKeptAsItIs()
	{
		GameRootScope.UseTempRoot("scriptfile-absolute");

		Assert.True(Program.TryResolveScriptFile(@"C:\outside\a.erb", out PurePath? file));
		Assert.Equal(@"C:\outside\a.erb", file.ToString());
	}

	[Fact]
	public void TryResolveScriptFile_EmptyName_IsRejected()
	{
		GameRootScope.UseTempRoot("scriptfile-empty");

		Assert.False(Program.TryResolveScriptFile("", out _));
		Assert.False(Program.TryResolveScriptFile(null, out _));
	}

	/// <summary>
	/// 该助手只负责「这个名字指向哪个文件」,不是包含性校验:越界的名字被词法折叠后照样返回,
	/// 因为两个消费方都只读该文件用于报错显示。
	/// </summary>
	[Fact]
	public void TryResolveScriptFile_EscapingName_IsNotAContainmentCheck()
	{
		string root = GameRootScope.UseTempRoot("scriptfile-escaping");

		Assert.True(Program.TryResolveScriptFile(@"..\a.erb", out PurePath? file));
		Assert.Equal(Path.Combine(root, "a.erb"), file.ToString());
	}

	[Fact]
	public void GetRawTextFormFilewithLine_RelativeErbName_ReadsTheLine()
	{
		string root = GameRootScope.UseTempRoot("rawtext-erb");
		WriteLines(Path.Combine(root, "erb", "sub", "a.erb"));

		Assert.Equal(SecondLine, Process.getRawTextFormFilewithLine(new ScriptPosition(@"sub\a.erb", 1)));
	}

	[Fact]
	public void GetRawTextFormFilewithLine_RelativeCsvName_ReadsTheLine()
	{
		string root = GameRootScope.UseTempRoot("rawtext-csv");
		WriteLines(Path.Combine(root, "csv", "Chara.csv"));

		Assert.Equal(SecondLine, Process.getRawTextFormFilewithLine(new ScriptPosition("Chara.csv", 1)));
	}

	[Fact]
	public void GetRawTextFormFilewithLine_AbsolutePath_ReadsTheLine()
	{
		string root = GameRootScope.UseTempRoot("rawtext-absolute");
		// An analyzed file carries its own absolute path, which may live outside the game root.
		string path = Path.Combine(Path.GetDirectoryName(root)!, "analyzed.erb");
		WriteLines(path);

		Assert.Equal(SecondLine, Process.getRawTextFormFilewithLine(new ScriptPosition(path, 1)));
	}

	[Fact]
	public void GetRawTextFormFilewithLine_MissingFile_IsEmpty()
	{
		GameRootScope.UseTempRoot("rawtext-missing");

		Assert.Equal("", Process.getRawTextFormFilewithLine(new ScriptPosition(@"sub\nope.erb", 0)));
	}

	[Fact]
	public void GetRawTextFormFilewithLine_OtherExtension_IsEmpty()
	{
		string root = GameRootScope.UseTempRoot("rawtext-other-extension");
		WriteLines(Path.Combine(root, "erb", "a.erh"));

		Assert.Equal("", Process.getRawTextFormFilewithLine(new ScriptPosition("a.erh", 0)));
	}

	[Fact]
	public void GetRawTextFormFilewithLine_NoPosition_IsEmpty()
	{
		GameRootScope.UseTempRoot("rawtext-no-position");

		Assert.Equal("", Process.getRawTextFormFilewithLine(null));
		Assert.Equal("", Process.getRawTextFormFilewithLine(new ScriptPosition()));
	}

	[Fact]
	public void TryResolvePositionFile_NotAnalyzing_ResolvesBelowTheErbDirectory()
	{
		string root = GameRootScope.UseTempRoot("positionfile-erb");

		Assert.True(Program.TryResolvePositionFile(new ScriptPosition(@"sub\a.erb", 1), out string? file));
		Assert.Equal(Path.Combine(root, "erb", "sub", "a.erb"), file);
	}

	[Fact]
	public void TryResolvePositionFile_AnalyzedRelativeName_KeepsTheCommandLinePath()
	{
		GameRootScope.UseTempRoot("positionfile-analyzed-relative");

		WithAnalysisMode(() =>
		{
			Assert.True(Program.TryResolvePositionFile(new ScriptPosition(@"sub\a.erb", 1), out string? file));
			Assert.Equal(@"sub\a.erb", file);
		});
	}

	[Fact]
	public void TryResolvePositionFile_AnalyzedCsvName_IsStillResolvedBelowTheCsvDirectory()
	{
		string root = GameRootScope.UseTempRoot("positionfile-analyzed-csv");

		WithAnalysisMode(() =>
		{
			Assert.True(Program.TryResolvePositionFile(new ScriptPosition("Chara.csv", 1), out string? file));
			Assert.Equal(Path.Combine(root, "csv", "Chara.csv"), file);
		});
	}

	/// <summary>
	/// 分析模式的相对名相对当前目录,不是相对 erb\:erb\ 下有同名文件时不得把它的行当成本行。
	/// </summary>
	[Fact]
	public void GetRawTextFormFilewithLine_AnalyzedRelativeName_IsNotRebasedIntoTheErbDirectory()
	{
		string root = GameRootScope.UseTempRoot("rawtext-analyzed-relative");
		WriteLines(Path.Combine(root, "erb", "sub", "a.erb"));

		WithAnalysisMode(() => Assert.Equal("", Process.getRawTextFormFilewithLine(new ScriptPosition(@"sub\a.erb", 1))));
	}

	/// <summary>Program.AnalysisMode 是进程级静态,用例结束必须复位。</summary>
	private static void WithAnalysisMode(Action body)
	{
		Program.AnalysisMode = true;
		try
		{
			body();
		}
		finally
		{
			Program.AnalysisMode = false;
		}
	}

	private static void WriteLines(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllLines(path, [FirstLine, SecondLine]);
	}
}
