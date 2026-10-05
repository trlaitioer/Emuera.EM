using System.Text;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// 存档读取器(dotnet-modernization-02):net9 起 <see cref="BinaryReader.ReadString"/> 对畸形 UTF-16
/// 不再抛异常而是静默返回 U+FFFD,读取器改用带 <see cref="DecoderFallback.ExceptionFallback"/> 的编码后,
/// 损坏文本段应恢复「损坏即异常」,同时正常文本(含增补平面代理对)不受影响。
/// </summary>
public class EraBinaryDataReaderTests
{
	/// <summary>
	/// 最小 1808 存档头:Header + 版本 + 数据块数 0,随后是调用方给的载荷。
	/// 字节按 <see cref="BitConverter"/> 显式拼装,避免 <see cref="BinaryWriter"/> 的编码前导字节干扰夹具。
	/// </summary>
	private static MemoryStream SaveStream(params byte[] payload)
	{
		MemoryStream ms = new();
		ms.Write(BitConverter.GetBytes(EraBDConst.Header));
		ms.Write(BitConverter.GetBytes((uint)1808));
		ms.Write(BitConverter.GetBytes(0u));
		ms.Write(payload);
		ms.Seek(0, SeekOrigin.Begin);
		return ms;
	}

	/// <summary>文本段以一个 7bit 长度前缀开头(单位是字节),其后是 UTF-16LE 字节。畸形序列统一报存档错误类型
	/// <see cref="FileEE"/>,不让解码异常以内部错误的形式冒到解释器循环里。</summary>
	[Fact]
	public void ReadString_MalformedText_ThrowsFileEEInsteadOfReplacing()
	{
		// 0x02 = 长度 2 字节;00 D8 = 未配对的高代理 U+D800
		using MemoryStream save = SaveStream(0x02, 0x00, 0xD8);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		Assert.Throws<FileEE>(() => reader.ReadString());
	}

	/// <summary>反空跑(校验夹具形状,不是对 BCL 的契约):同一段字节在**非严格** UTF-16 下必须只产出替换字符,
	/// 否则这个夹具并没有覆盖「畸形文本」。断言只取「全是替换字符」这一形状,免得 .NET 改回落细节时假红。</summary>
	[Fact]
	public void ControlFixture_MalformedTextDecodesToReplacementCharactersOnly()
	{
		using MemoryStream ms = new([0x02, 0x00, 0xD8]);
		using BinaryReader plain = new(ms, Encoding.Unicode);

		string decoded = plain.ReadString();

		Assert.Contains('\uFFFD', decoded);
		Assert.Equal(new string('\uFFFD', decoded.Length), decoded);
	}

	/// <summary>字符级 EOF 检查走 <c>PeekChar</c>,同一条严格解码路径,故同一处畸形字节也必须报
	/// <see cref="FileEE"/>——否则裸的 <see cref="DecoderFallbackException"/> 会绕过存档错误处理上浮成内部错误。</summary>
	[Fact]
	public void Eof_PeeksThroughTheSameStrictDecoder_ThrowsFileEE()
	{
		// 00 D8 = 高代理 U+D800,后随 41 00 = 'A':高代理未配对,严格解码必须报错
		// (流末尾的孤立高代理不算畸形——解码器只是挂起等待低位代理,PeekChar 按「无字符」返回)
		using MemoryStream save = SaveStream(0x00, 0xD8, 0x41, 0x00);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		Assert.Throws<FileEE>(() => reader.EOF());
	}

	/// <summary>正常文本回归:含增补平面字符(合法代理对)的字符串不受严格解码影响。</summary>
	[Fact]
	public void ReadString_ValidText_RoundTrips()
	{
		// 长度 8 字节;テ U+30C6 / ス U+30B9 / 🙂 U+1F642(代理对 D83D DE42)
		using MemoryStream save = SaveStream(0x08,
			0xC6, 0x30,
			0xB9, 0x30,
			0x3D, 0xD8,
			0x42, 0xDE);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		Assert.Equal("テス🙂", reader.ReadString());
	}

	/// <summary>写→读往返(票面的「正常存档读写回归」):用生产写入器 <see cref="EraBinaryDataWriter"/> 写出
	/// 头部与一段文本,再由读取器读回同一字符串。覆盖两侧编码口径一致这一条——读取器的编码换了严格解码,
	/// 但格式与字节序必须与写入器保持兼容。压缩档(<c>Config.ZipSaveData</c>)分支不在本用例范围。</summary>
	[Fact]
	public void WriterThenReader_RoundTripsTextThroughTheSaveFormat()
	{
		TestBootstrap.Initialize();
		string path = Path.Combine(Path.GetTempPath(), $"emuera-save-roundtrip-{Guid.NewGuid():N}.dat");
		const string saved = "テキスト 🙂 保存";

		try
		{
			using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
			using (EraBinaryDataWriter writer = new(fs))
			{
				writer.WriteHeader();
				writer.WriteString(saved);
			}

			using FileStream read = new(path, FileMode.Open, FileAccess.Read);
			using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(read)!;

			Assert.Equal(saved, reader.ReadString());
		}
		finally
		{
			File.Delete(path);
		}
	}
}