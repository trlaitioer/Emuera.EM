using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// 写入侧的存档保全契约(nullable-migration-35):写入失败时,目标文件必须与写入前逐字节相同。
/// 旧写法用 <c>FileMode.Create</c> 打开目标档,它在写入第一个字节之前就把文件截断,于是「写到一半失败」
/// 既毁掉旧存档又留下半截文件——而半截文件正是读取侧要报成损坏存档的那种输入。
/// 用例只开临时目录、不动 <c>Program</c> 的目录静态:被测类型不读游戏根。
/// </summary>
public class TempFileWriterTests
{
	[Fact]
	public void Write_WhenTheWriteFails_KeepsTheTargetAndDropsTheTemporaryFile()
	{
		string root = GameRootScope.NewTempDirectory("tempfilewriter-failure");
		string target = Path.Combine(root, "save00.sav");
		File.WriteAllText(target, "OLD SAVE");

		Assert.Throws<IOException>(() => TempFileWriter.Write(target, stream =>
		{
			// 先写进去一部分再失败:这正是「写到一半」(磁盘满、编码失败)的形状
			stream.Write("NEW SAV"u8);
			throw new IOException("disk full");
		}));

		Assert.Equal("OLD SAVE", File.ReadAllText(target));
		Assert.False(File.Exists(target + ".tmp"));
	}

	[Fact]
	public void Write_WhenTheWriteSucceeds_ReplacesTheTargetAndDropsTheTemporaryFile()
	{
		string root = GameRootScope.NewTempDirectory("tempfilewriter-success");
		string target = Path.Combine(root, "save00.sav");
		File.WriteAllText(target, "OLD SAVE");

		TempFileWriter.Write(target, stream => stream.Write("NEW SAVE"u8));

		Assert.Equal("NEW SAVE", File.ReadAllText(target));
		Assert.False(File.Exists(target + ".tmp"));
	}

	/// <summary>目标档还不存在时同样是「先临时后替换」,不产生多余的临时文件。</summary>
	[Fact]
	public void Write_WithNoExistingTarget_CreatesIt()
	{
		string root = GameRootScope.NewTempDirectory("tempfilewriter-new");
		string target = Path.Combine(root, "save00.sav");

		TempFileWriter.Write(target, stream => stream.Write("NEW SAVE"u8));

		Assert.Equal("NEW SAVE", File.ReadAllText(target));
		Assert.False(File.Exists(target + ".tmp"));
	}
}
