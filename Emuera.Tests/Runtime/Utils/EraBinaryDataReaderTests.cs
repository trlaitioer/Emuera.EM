using System.Text;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Utils;

/// <summary>
/// 存档读取器(dotnet-modernization-02):net9 起 <see cref="BinaryReader.ReadString"/> 对畸形 UTF-16
/// 不再抛异常而是静默返回 U+FFFD,读取器改用带 <see cref="DecoderFallback.ExceptionFallback"/> 的编码后,
/// 损坏文本段应恢复「损坏即异常」,同时正常文本(含增补平面代理对)不受影响。
/// nullable-migration-30 的残留:数据区被截断(读越界)是另一种坏输入,同样必须报存档错误类型
/// <see cref="FileEE"/>,而不是把 BCL 的 <see cref="EndOfStreamException"/> 漏给兜底分支。
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

	/// <summary>截断处的统一断言:抛存档错误、文案是文本档同款、分级为存档档。</summary>
	private static void AssertTruncatedSave(Action read)
	{
		FileEE ex = Assert.Throws<FileEE>(read);
		Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, ex.Message);
		Assert.Equal(ErrorReportKind.SaveData, ErrorReport.Classify(ex));
	}

	/// <summary>值级损坏的统一断言:不是「读越界」,而是同一份存档的 framing 坏了,
	/// 故文案是「存档数据已损坏」+ 细节,分级仍是存档档。</summary>
	private static void AssertCorruptSave(Action read)
	{
		FileEE ex = Assert.Throws<FileEE>(read);
		Assert.StartsWith(trerror.CorruptedSaveData.Text, ex.Message);
		Assert.Equal(ErrorReportKind.SaveData, ErrorReport.Classify(ex));
	}

	/// <summary>数据区被截断(nullable-migration-30 的残留):二进制档在「读越界」处原样抛 BCL 的
	/// <see cref="EndOfStreamException"/>,它按 ErrorReport.Classify 落 Unexpected 分支,
	/// 玩家看到的是「予期しないエラー」+ CLR 堆栈。读取器须把它收敛成存档错误,
	/// 并与文本档同文案(见 EraDataStreamTests.cs 的
	/// ReadInt64Array3DExtended_WithTruncatedBlock_ThrowsFileEE)。</summary>
	[Fact]
	public void ReadIntArray_WithTruncatedBlock_ThrowsFileEE()
	{
		// 长度 4;块内 0xD1(直后 4 字节 Int32)之后即文件末尾
		using MemoryStream save = SaveStream(0x04, 0x00, 0x00, 0x00, 0xD1);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertTruncatedSave(() => reader.ReadIntArray(new long[4], false));
	}

	/// <summary>截断在文本段正文(长度前缀声明 0x7F 字节,实到 2 字节):此前这条路径由文本解码的包装
	/// 独占,读越界仍会从它旁边漏过去,现在与其它读同一出口。</summary>
	[Fact]
	public void ReadString_WithTruncatedBody_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(0x7F, (byte)'a', (byte)'b');
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertTruncatedSave(() => reader.ReadString());
	}

	/// <summary>截断在文件类型字节之后的定长字段(真实格式里正是 unique code / 版本这两个 Int64):
	/// 类型字节读完即文件末尾。</summary>
	[Fact]
	public void ReadInt64_WithTruncatedHeaderField_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(0x00, 0x01, 0x02, 0x03); // 0x00 = EraSaveFileType.Normal,其后不足 8 字节
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		Assert.Equal(EraSaveFileType.Normal, reader.ReadFileType());
		AssertTruncatedSave(() => reader.ReadInt64());
	}

	/// <summary>长度前缀本身畸形(6 字节全是续读位):BCL 抛 <see cref="FormatException"/>。它和短读同属
	/// 「这份存档读不出来」,故走同一出口,但文案是「存档数据已损坏」+ 细节,而不是「意料之外的结尾」。</summary>
	[Fact]
	public void ReadString_WithMalformedLengthPrefix_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(0x80, 0x80, 0x80, 0x80, 0x80, 0x80);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertCorruptSave(() => reader.ReadString());
	}

	/// <summary>长度前缀的 7bit 编码本身合法、但结果是负数(第 5 字节把符号位置起):BCL 抛的是
	/// <see cref="IOException"/> 而不是 <see cref="FormatException"/>,同一处 framing 损坏的另一半。</summary>
	[Fact]
	public void ReadString_WithNegativeLengthPrefix_ThrowsFileEE()
	{
		// 0x80×4 + 0x08 = 0x80000000,按 int 即 -2147483648
		using MemoryStream save = SaveStream(0x80, 0x80, 0x80, 0x80, 0x08);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertCorruptSave(() => reader.ReadString());
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