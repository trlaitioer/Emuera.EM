using System.Text;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// EraDataReader 的坏输入路径(nullable-migration-28 第 3 处):存档在扩展数组块内被截断时,
/// EOF 处的 null 行必须变成存档损坏错误,而不是被解引用。
/// </summary>
public class EraDataStreamTests
{
	public EraDataStreamTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>存档在扩展数组块内被截断:报 FileEE(与同函数两处外层循环同型),不抛 NullReferenceException。</summary>
	[Fact]
	public void ReadInt64Array3DExtended_WithTruncatedBlock_ThrowsFileEE()
	{
		string root = GameRootScope.UseTempRoot("truncated-save");
		string path = Path.Combine(root, "save.dat");
		File.WriteAllLines(path,
		[
			EraDataReader.EMU_1808_START,
			"KEY1",
			"{",
			"1,2",
			// 块在这里被截断(缺 "}")
		], new UTF8Encoding(false));

		using FileStream file = File.OpenRead(path);
		using EraDataReader reader = new(file);
		Assert.True(reader.SeekEmuStart()); // emu_version >= 1729,否则扩展数组段直接早退

		Assert.Throws<FileEE>(() => reader.ReadInt64Array3DExtended());
	}
}
