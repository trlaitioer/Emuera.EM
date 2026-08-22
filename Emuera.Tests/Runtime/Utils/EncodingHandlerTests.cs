using System.Text;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// 编码检测(EncodingHandler)测试。无需手动注册 CodePagesEncodingProvider,
/// EncodingHandler 静态初始化(GetEncoding(932))内部会注册。
/// </summary>
public class EncodingHandlerTests
{
	[Fact]
	public void DetectEncoding_Utf8Bom()
	{
		byte[] data = [0xEF, 0xBB, 0xBF, (byte)'a', (byte)'b', (byte)'c'];
		using var ms = new MemoryStream(data);

		var enc = EncodingHandler.DetectEncoding(ms);

		Assert.Equal("utf-8", enc.WebName);
		Assert.True(enc.GetPreamble().Length == 3, "应检测到 UTF-8 BOM");
	}

	[Fact]
	public void DetectEncoding_PlainUtf8()
	{
		byte[] data = [(byte)'a', (byte)'b', (byte)'c'];
		using var ms = new MemoryStream(data);

		var enc = EncodingHandler.DetectEncoding(ms);

		Assert.Equal("utf-8", enc.WebName);
		Assert.True(enc.GetPreamble().Length == 0, "无 BOM 的 UTF-8 不应带前导字节");
	}

	[Fact]
	public void DetectEncoding_ShiftJis()
	{
		// Shift-JIS 编码的 "あ" = 0x82 0xA0,不是合法 UTF-8 序列
		byte[] data = [0x82, 0xA0];
		using var ms = new MemoryStream(data);

		var enc = EncodingHandler.DetectEncoding(ms);

		Assert.Equal("shift_jis", enc.WebName);
	}

	[Fact]
	public void GetEncoding_932_IsShiftJis()
	{
		Assert.Equal("shift_jis", EncodingHandler.GetEncoding(932).WebName);
	}
}
