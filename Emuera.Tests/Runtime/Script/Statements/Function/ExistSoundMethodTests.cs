using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// EXISTSOUND 的基准是游戏根的 sound 目录,名字必须落在该目录内:目录内的子路径可用,越界不可用。
/// </summary>
public class ExistSoundMethodTests
{
	[Fact]
	public void Existsound_SubdirectoryInsideTheSoundDirectory_IsFound()
	{
		string root = GameRootScope.UseTempRoot("existsound-subdirectory");
		CreateSoundFile(root, @"sub\x.wav");

		Assert.Equal(1L, ExistSound(@"sub\x.wav"));
	}

	[Fact]
	public void Existsound_NameInTheSoundDirectory_IsFound()
	{
		string root = GameRootScope.UseTempRoot("existsound-name");
		CreateSoundFile(root, "x.wav");

		Assert.Equal(1L, ExistSound("x.wav"));
	}

	[Fact]
	public void Existsound_EscapingName_IsMissing()
	{
		string root = GameRootScope.UseTempRoot("existsound-escaping-name");
		// Present one level above the sound directory, so a prefix based lookup would find it.
		File.WriteAllText(Path.Combine(root, "x.wav"), "");

		Assert.Equal(0L, ExistSound(@"..\x.wav"));
	}

	private static long ExistSound(string filename)
		=> FunctionMethodCreator.GetMethodList()["EXISTSOUND"]
			.GetIntValue(null!, [new SingleStrTerm(filename)]);

	private static void CreateSoundFile(string root, string relative)
	{
		string path = Path.Combine(root, "sound", relative);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, "");
	}
}
