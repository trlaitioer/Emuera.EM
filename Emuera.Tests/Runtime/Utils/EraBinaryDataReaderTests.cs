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

	/// <summary>按存档格式写一段文本:7bit 长度前缀(单位是字节;这里都短于 128,故前缀是一字节)+ UTF-16LE 字节。</summary>
	private static byte[] Text(string value)
	{
		byte[] body = Encoding.Unicode.GetBytes(value);
		return [.. new[] { (byte)body.Length }, .. body];
	}

	/// <summary>结构性损坏的统一断言(nullable-migration-34 第 3 组):声明形状或游标越过数组这类可判定的坏输入,
	/// 报读取器既有的「異常なバイナリデータ」,分级仍是存档档——而不是把 BCL 的
	/// <see cref="IndexOutOfRangeException"/>/<see cref="OverflowException"/> 漏给兜底分支。</summary>
	private static void AssertAbnormalBinaryData(Action read)
	{
		FileEE ex = Assert.Throws<FileEE>(read);
		Assert.Equal(trerror.AbnormalBinaryData.Text, ex.Message);
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

	/// <summary>任意头部 + 任意数据块数的二进制档:用于 <c>CreateReader</c> 自身的坏输入路径。</summary>
	private static MemoryStream BinarySave(ulong header, uint version, uint dataCount, params byte[] payload)
	{
		MemoryStream ms = new();
		ms.Write(BitConverter.GetBytes(header));
		ms.Write(BitConverter.GetBytes(version));
		ms.Write(BitConverter.GetBytes(dataCount));
		ms.Write(payload);
		ms.Seek(0, SeekOrigin.Begin);
		return ms;
	}

	/// <summary>认得出头、但块表读不出来(nullable-migration-34):<c>CreateReader</c> 此前以
	/// <c>catch { return null; }</c> 收尾,而 null 在调用方只意味着「不是二进制档」——于是它回退文本读取,
	/// 把损坏答成静默失败(LOADGLOBAL/LOADCHARA 更是什么都不说)。</summary>
	[Fact]
	public void CreateReader_WithTruncatedDataBlockTable_ThrowsFileEE()
	{
		// 声明 1 个数据块,块表一个字节都没有
		using MemoryStream save = BinarySave(EraBDConst.Header, 1808, 1);

		AssertTruncatedSave(() => EraBinaryDataReader.CreateReader(save));
	}

	/// <summary>版本不是本版本:头是本格式的,「读不出来」这件事必须报出来,版本号留在文案里
	/// (旧实现返回 null,最终以「不是二进制档」的形式被静默掉)。</summary>
	[Fact]
	public void CreateReader_WithUnknownVersion_ThrowsFileEE()
	{
		using MemoryStream save = BinarySave(EraBDConst.Header, 1807, 0);

		AssertCorruptSave(() => EraBinaryDataReader.CreateReader(save));
	}

	/// <summary>压缩档的正文损坏(GZipStream 解压失败)不是「读越界」而是同一份存档的 framing 坏了,
	/// 故与读取器的值级损坏同文案、同分级。</summary>
	[Fact]
	public void CreateReader_WithCorruptGzipBody_ThrowsFileEE()
	{
		// ZipHeader,其后不是 gzip 流
		using MemoryStream save = BinarySave(EraBDConst.ZipHeader, 1808, 0, 0x00, 0x01, 0x02);

		AssertCorruptSave(() => EraBinaryDataReader.CreateReader(save));
	}

	/// <summary>正控制:不是本格式的文件仍返回 null,调用方的文本回退路径不受影响。</summary>
	[Fact]
	public void CreateReader_OnForeignContent_ReturnsNull()
	{
		using MemoryStream save = new(Encoding.ASCII.GetBytes(EraDataReader.EMU_1808_START + "\n1\n"));

		Assert.Null(EraBinaryDataReader.CreateReader(save));
	}

	/// <summary>内容级损坏的出口之一(nullable-migration-34 第 2 组):重复键。它坏的是内容而不是流,
	/// 故不在 <c>SaveDataReader.Guarded</c> 的漏斗里,旧写法把 BCL 的 <see cref="ArgumentException"/>
	/// 漏给兜底分支;现在与解码失败同档同文案。</summary>
	[Fact]
	public void ReadMap_WithDuplicateKey_ThrowsFileEE()
	{
		// 计数 2;两组 ("a","1") 与 ("a","2")
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(2), .. Text("a"), .. Text("1"), .. Text("a"), .. Text("2")]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertCorruptSave(() => reader.ReadMap());
	}

	/// <summary>XML 正文不是合法 XML:<c>XmlDocument.LoadXml</c> 抛 <see cref="System.Xml.XmlException"/>,同一出口。</summary>
	[Fact]
	public void ReadXml_WithMalformedDocument_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(Text("<a><b></a>"));
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertCorruptSave(() => reader.ReadXml());
	}

	/// <summary>DataTable 的 schema 段不是合法 XML:<c>ReadXmlSchema</c> 抛 XML 侧异常,同一出口。</summary>
	[Fact]
	public void ReadDataTable_WithMalformedSchema_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(Text("<xs:schema"));
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertCorruptSave(() => reader.ReadDataTable());
	}

	/// <summary>声明长度为负(nullable-migration-34 第 3 组):读取器按它分配数组,旧写法由
	/// <see cref="OverflowException"/> 逃逸;现在按「这份存档的形状不可能存在」报存档错误。</summary>
	[Fact]
	public void ReadIntArray_WithNegativeDeclaredLength_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(BitConverter.GetBytes(-1));
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray(new long[4], false));
	}

	/// <summary>声明长度超出运行时能分配的数组上限:在分配之前拒绝,而不是让
	/// <see cref="OutOfMemoryException"/> 逃逸。票面裁定的边界是「只做确定性拒绝」——合法但巨大的长度
	/// (如 1e9)仍按资源不足处理,不在此收敛。</summary>
	[Fact]
	public void ReadIntArray_WithDeclaredLengthBeyondMaxArray_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream(BitConverter.GetBytes(Array.MaxLength + 1));
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray(new long[4], false));
	}

	/// <summary>元素越过数组末尾:声明 2 个元素却给了 3 个。旧写法在第三个元素处抛
	/// <see cref="IndexOutOfRangeException"/>。</summary>
	[Fact]
	public void ReadIntArray_WithMoreElementsThanDeclared_ThrowsFileEE()
	{
		// 声明长度 2;三个元素(0x01/0x02/0x03,都不大于 Ebdb.Byte,故按字面值编码)
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(2), 0x01, 0x02, 0x03]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray(new long[2], false));
	}

	/// <summary>零串越过数组末尾:声明 2 个元素却给了 5 个零。零串在旧写法里由
	/// <c>needInit</c> 门控写入,故取 <c>needInit: true</c> 走真正会越界的那条路径。</summary>
	[Fact]
	public void ReadIntArray_WithZeroRunPastTheEnd_ThrowsFileEE()
	{
		// 声明长度 2;Zero 标记(0xF0)+ 计数 5
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(2), 0xF0, 0x05]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray(new long[2], true));
	}

	/// <summary>二维声明形状的元素总数超出可分配上限(两个维度各自都合法,乘积不合法):在分配之前拒绝,
	/// 而不是让 <see cref="OverflowException"/> 逃逸。</summary>
	[Fact]
	public void ReadIntArray2D_WithShapeTooLargeToAllocate_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(100000), .. BitConverter.GetBytes(100000)]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray2D(new long[2, 2], false));
	}

	/// <summary>重分配路径的形状也要验证:两个形状各自都合法(1x100000 与 100000x1),但分配形状取逐维较大值
	/// 后是 100000x100000,元素总数超上限。这一条才是 <c>ValidateShape(alloc0, alloc1)</c> 的判据——
	/// 只验声明形状的实现会在 <c>new long[100000, 100000]</c> 处抛 <see cref="OverflowException"/>。</summary>
	[Fact]
	public void ReadIntArray2D_WithReallocatedShapeTooLargeToAllocate_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(100000), .. BitConverter.GetBytes(1)]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray2D(new long[1, 100000], false));
	}

	/// <summary>二维的列游标越界:声明 2x2,却在一行里给了 3 个元素。</summary>
	[Fact]
	public void ReadIntArray2D_WithCursorPastTheRow_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(2), .. BitConverter.GetBytes(2), 0x01, 0x02, 0x03]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray2D(new long[2, 2], false));
	}

	/// <summary>三维的深度游标越界:声明 2x2x2,却在一行里给了 3 个元素。</summary>
	[Fact]
	public void ReadIntArray3D_WithCursorPastTheDepth_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream([
			.. BitConverter.GetBytes(2), .. BitConverter.GetBytes(2), .. BitConverter.GetBytes(2),
			0x01, 0x02, 0x03]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadIntArray3D(new long[2, 2, 2], false));
	}

	/// <summary>字符串数组同样有游标上界:声明 2 个元素却给了 3 个。</summary>
	[Fact]
	public void ReadStrArray_WithMoreElementsThanDeclared_ThrowsFileEE()
	{
		using MemoryStream save = SaveStream([
			.. BitConverter.GetBytes(2),
			0xD8, .. Text("a"),
			0xD8, .. Text("b"),
			0xD8, .. Text("c")]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;

		AssertAbnormalBinaryData(() => reader.ReadStrArray(new string[2], false));
	}

	/// <summary>正控制(防误拒):零串正好填满声明长度是合法编码——上界判据是「超过」而不是「达到」;
	/// 写入器也不会为数组末尾的连续零写计数(见 EraBinaryDataWriter.writeData),故这条路径必须原样通过。</summary>
	[Fact]
	public void ReadIntArray_WithZeroRunFillingTheArray_Reads()
	{
		// 声明长度 3;Zero 标记 + 计数 3(正好到末尾),EoD
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(3), 0xF0, 0x03, 0xFF]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;
		long[] target = [1, 1, 1];

		reader.ReadIntArray(target, true);

		Assert.Equal(new long[] { 0, 0, 0 }, target);
	}

	/// <summary>正控制(防误拒):写入器的二维编码原样读回——非零行以 EoA1 收尾(游标在最后一行也必须放行),
	/// 末尾的全零行不写任何标记,由 <c>needInit</c> 补齐。</summary>
	[Fact]
	public void ReadIntArray2D_WithTrailingAllZeroRow_Reads()
	{
		// 声明 2x2;第 0 行 [1,2] + EoA1,第 1 行全零(无标记),EoD
		using MemoryStream save = SaveStream([.. BitConverter.GetBytes(2), .. BitConverter.GetBytes(2), 0x01, 0x02, 0xE0, 0xFF]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;
		long[,] target = new long[2, 2];

		reader.ReadIntArray2D(target, true);

		Assert.Equal(1, target[0, 0]);
		Assert.Equal(2, target[0, 1]);
		Assert.Equal(0, target[1, 0]);
		Assert.Equal(0, target[1, 1]);
	}

	/// <summary>正控制(防误拒):存档里的数组比当前变量大(重分配路径)——只有重叠部分回写,超出的行被丢弃;
	/// 游标会越过 <c>length0</c> 但始终在分配范围内,故不得判成坏输入。这是上界判据取「分配长度」
	/// 而不是「重叠长度」的直接依据。</summary>
	[Fact]
	public void ReadIntArray2D_WithSaveLargerThanTheVariable_AppliesTheOverlap()
	{
		// 声明 3x2;三行 [1,2] [3,4] [5,6] 各以 EoA1 收尾,EoD
		using MemoryStream save = SaveStream([
			.. BitConverter.GetBytes(3), .. BitConverter.GetBytes(2),
			0x01, 0x02, 0xE0,
			0x03, 0x04, 0xE0,
			0x05, 0x06, 0xE0, 0xFF]);
		using EraBinaryDataReader reader = EraBinaryDataReader.CreateReader(save)!;
		long[,] target = new long[2, 2];

		reader.ReadIntArray2D(target, true);

		Assert.Equal(1, target[0, 0]);
		Assert.Equal(2, target[0, 1]);
		Assert.Equal(3, target[1, 0]);
		Assert.Equal(4, target[1, 1]);
	}
}