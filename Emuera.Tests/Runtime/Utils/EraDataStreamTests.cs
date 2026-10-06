using System.Text;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// EraDataReader 的坏输入路径(nullable-migration-28 第 3 处):存档在扩展数组块内被截断时,
/// EOF 处的 null 行必须变成存档损坏错误,而不是被解引用。
/// nullable-migration-30 补上「这个错误怎样被报出来」的另一半:抛出的 FileEE 文案必须是可读的
/// 存档/装载类文案,且分级为 SaveData(即不再落「予期せぬエラー + CLR 堆栈」那一档)。
/// </summary>
public class EraDataStreamTests
{
	public EraDataStreamTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>存档在扩展数组块内被截断:报 FileEE(与同函数两处外层循环同型),不抛 NullReferenceException;
	/// 文案是「予期しないセーブデータの終端」,分级为存档错误。</summary>
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

		FileEE ex = Assert.Throws<FileEE>(() => reader.ReadInt64Array3DExtended());
		Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, ex.Message);
		Assert.Equal(ErrorReportKind.SaveData, ErrorReport.Classify(ex));
	}
}
