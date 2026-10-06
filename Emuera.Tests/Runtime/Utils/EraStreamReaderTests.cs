using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Sub;
using Xunit;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// EraStreamReader.OpenOnCache 的坏输入路径(nullable-migration-28 第 4 处):预读失败的脚本在
/// Preload 缓存里是 null,打开应失败并保持 EOF,而不是让 ReadLine 解引用 null 数组。
/// </summary>
public class EraStreamReaderTests
{
	public EraStreamReaderTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>BOM 为 UTF-8 但含非法字节 → 严格解码失败 → 缓存为 null → 打开失败且读 EOF。</summary>
	[Fact]
	public async Task OpenOnCache_WithAnUnreadableCachedFile_ReturnsFalseAndReadsEof()
	{
		string root = GameRootScope.UseTempRoot("badcache");
		string erbDir = Path.Combine(root, "erb");
		Directory.CreateDirectory(erbDir);
		string path = Path.Combine(erbDir, "bad.erb");
		// EF BB BF = UTF-8 BOM,其后 FF FE 不是合法的 UTF-8 序列(严格解码器报错,Preload 把 null 写进缓存)
		File.WriteAllBytes(path, [0xEF, 0xBB, 0xBF, 0x41, 0xFF, 0xFE, 0x42]);
		await Preload.Load(path);
		Assert.Null(Preload.GetFileLines(path));

		using EraStreamReader reader = new(false);

		Assert.False(reader.OpenOnCache(path));
		Assert.Null(reader.ReadLine());
	}
}
