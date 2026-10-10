using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Utils;

#region reader/writer共通データ

public enum EraSaveFileType : byte
{
	Normal = 0x00,
	Global = 0x01,
	Var = 0x02,
	CharVar = 0x03
}

public enum EraSaveDataType : byte
{
	Int = 0x00,
	IntArray = 0x01,
	IntArray2D = 0x02,
	IntArray3D = 0x03,
	Str = 0x10,
	StrArray = 0x11,
	StrArray2D = 0x12,
	StrArray3D = 0x13,

	#region EM_私家版_セーブ拡張

	Map = 0x20,
	Xml = 0x21,
	DT = 0x22,

	#endregion

	//SOC = 0xFD,//キャラデータ始まり
	Separator = 0xFD, //データ区切り
	EOC = 0xFE, //キャラデータ終わり
	EOF = 0xFF //ファイル終端
}

internal static class Ebdb //EraBinaryData中のマジックナンバーなバイト
{
	public const byte Byte = 0xCF;
	public const byte Int16 = 0xD0; //直後の2バイトがInt16
	public const byte Int32 = 0xD1; //直後の4バイトがInt32
	public const byte Int64 = 0xD2; //直後の8バイトがInt64
	public const byte String = 0xD8; //直後がString

	public const byte EoA1 = 0xE0; //データ区切り（一次元
	public const byte EoA2 = 0xE1; //データ区切り（二次元
	public const byte Zero = 0xF0; //直後にゼロが連続する数
	public const byte ZeroA1 = 0xF1; //直後に空配列が連続する数（一次元
	public const byte ZeroA2 = 0xF2; //直後に空配列が連続する数（二次元
	public const byte EoD = 0xFF; //変数データ終わり
}

internal static class EraBDConst
{
	//Headerはpngのパクリ
	public const ulong Header = 0x0A1A0A0D41524589UL;

	#region EM_私家版_セーブ圧縮

	public const ulong ZipHeader = 0x0A50495A41524589UL;

	#endregion

	public const uint Version1808 = 1808;
	public const uint DataCount = 0;
}

#endregion

/// <summary>
/// 1808追加 新しいデータ保存形式
/// 将来形式を変更したときのためにabstractにしておく
/// </summary>
internal abstract class EraBinaryDataReader : IDisposable
{
	private EraBinaryDataReader()
	{ }

	// The only subclass is nested below, so the shared reader stays private to this file.
	private EraBinaryDataReader(SaveDataReader stream, int ver, uint[] buf)
	{
		reader = stream;
		version = ver;
		data = buf;
	}

	// Assigned by the constructor; nulled only by Dispose. Read directly by the nested subclass below.
	private SaveDataReader reader = null!;
	protected readonly int version;
	protected readonly uint[] data = null!;

	/// <summary>
	/// UTF-16 that throws on malformed text: net9's <see cref="BinaryReader.ReadString"/> stopped throwing and
	/// returned U+FFFD instead, which silently mangled a corrupt save's text segments.
	/// </summary>
	private static readonly Encoding SaveTextEncoding = EncodingHandler.GetEncoding(Encoding.Unicode.CodePage);

	/// <summary>
	/// Every read from a save stream goes through this type, so no such read can fail past it as a raw BCL
	/// exception: a read that runs past the end is a truncated save, and anything else a read can reject -
	/// malformed text, an unusable length prefix, a failure from the medium - is a corrupt one. Either is bad
	/// input and must become <see cref="FileEE"/> (the save/load error type); a raw BCL exception instead reaches
	/// the handlers' unexpected-error branch, which prints a CLR message and stack in place of the save-data
	/// grading (<see cref="MinorShift.Emuera.Runtime.Script.ErrorReport"/>). It wraps <see cref="BinaryReader"/>
	/// rather than deriving from it because the integral reads are not virtual and one override cannot catch them.
	/// </summary>
	private sealed class SaveDataReader
	{
		public SaveDataReader(Stream stream, bool leaveOpen)
		{
			reader = new BinaryReader(stream, SaveTextEncoding, leaveOpen);
		}

		private readonly BinaryReader reader;

		public Stream BaseStream => reader.BaseStream;

		public ulong ReadUInt64() => Guarded(() => reader.ReadUInt64());
		public uint ReadUInt32() => Guarded(() => reader.ReadUInt32());
		public byte ReadByte() => Guarded(() => reader.ReadByte());
		public short ReadInt16() => Guarded(() => reader.ReadInt16());
		public int ReadInt32() => Guarded(() => reader.ReadInt32());
		public long ReadInt64() => Guarded(() => reader.ReadInt64());
		public string ReadString() => Guarded(() => reader.ReadString());
		public int PeekChar() => Guarded(() => reader.PeekChar());

		public void Close() => reader.Close();

		private static T Guarded<T>(Func<T> read)
		{
			try
			{
				return read();
			}
			catch (EndOfStreamException)
			{
				// The wording the text-format reader throws for the same situation
				// (EraDataStream.cs, EraDataReader.ReadInt64Array and the same ReadLine null check elsewhere).
				throw new FileEE(trerror.UnexpectedSaveDataEnd.Text);
			}
			catch (Exception e) when (e is DecoderFallbackException or FormatException or IOException)
			{
				// Undecodable text, an unusable length prefix (over-long, or a negative result that ReadString
				// reports as IOException), or a read that failed on the medium. The truncation case is
				// EndOfStreamException above; the type name stays in the message because the stack is not
				// printed, and an I/O fault would otherwise be indistinguishable from corrupt framing.
				throw CorruptedSaveDataError(e);
			}
		}
	}

	public abstract int ReaderVersion { get; }

	/// <summary>
	/// A save file whose content cannot be read: an unusable length prefix, undecodable text, a gzip body, a
	/// failure from the medium. The type name and the detail stay in the message because this exception is
	/// reported without a stack, so they are the only clue to which of those it was.
	/// </summary>
	private static FileEE CorruptedSaveDataError(Exception exc) =>
		new($"{trerror.CorruptedSaveData.Text} ({exc.GetType().Name}: {exc.Message})");

	/// <summary>
	/// The funnel for content-level failures: a duplicate key, malformed XML, an unusable DataTable schema.
	/// They are the same damaged save as the framing failures <see cref="SaveDataReader.Guarded"/> wraps, but
	/// they are raised outside that funnel because the content, not the stream, is what the reader rejects
	/// (nullable-migration-34). The type name stays in the message so the layer that rejected it is visible.
	/// A resource failure is left alone: <see cref="OutOfMemoryException"/> stays a resource failure rather
	/// than becoming "this save is corrupt", the same line the array-shape checks draw.
	/// </summary>
	private static T GuardedContent<T>(Func<T> read)
	{
		try
		{
			return read();
		}
		catch (Exception e) when (e is not FileEE and not OutOfMemoryException)
		{
			throw CorruptedSaveDataError(e);
		}
	}

	/// <summary>
	/// Rejects an array shape a save file cannot legitimately declare: a negative length, or a total element
	/// count the runtime cannot allocate. The reader allocates from these numbers, so an unchecked one turns
	/// damaged content into an <see cref="OverflowException"/>/<see cref="OutOfMemoryException"/> that escapes
	/// the save-error grading (nullable-migration-34).
	/// </summary>
	private static void ValidateShape(params int[] lengths)
	{
		long total = 1;
		foreach (int length in lengths)
		{
			if (length < 0)
			{
				throw new FileEE(trerror.AbnormalBinaryData.Text);
			}

			// Each factor is at most int.MaxValue and the running product stops at Array.MaxLength, so the
			// multiplication cannot overflow before the comparison rejects it.
			total *= length;
			if (total > Array.MaxLength)
			{
				throw new FileEE(trerror.AbnormalBinaryData.Text);
			}
		}
	}

	/// <summary>
	/// Rejects content that runs past the array it is being read into. The allocation is at least as long as
	/// the length the save declares, so a cursor beyond it is damaged content - not the "the save is bigger
	/// than the current variable" case, which the reallocation handles (the text-format reader skips that
	/// excess instead, see EraDataStream.cs ReadInt64Array). Callers pass a run as `cnt &gt; bound - cursor`
	/// rather than `cursor + cnt &gt; bound`: both operands can be near int.MaxValue, and the sum would wrap
	/// negative and let the run through.
	/// </summary>
	private static void ThrowIfOutOfRange(bool outOfRange)
	{
		if (outOfRange)
		{
			throw new FileEE(trerror.AbnormalBinaryData.Text);
		}
	}

	/// <summary>
	/// Creates a reader over a save file. Returns null when the file does not declare the binary format
	/// (the callers then read it as the text format); once the header says it is one, a failure to read it
	/// is a damaged save and is thrown as <see cref="FileEE"/> rather than answered with null, which every
	/// caller can only read as "a different format" (nullable-migration-34).
	/// </summary>
	/// <param name="fs"></param>
	/// <returns></returns>

	#region EM_私家版_セーブ圧縮

	//public static EraBinaryDataReader CreateReader(FileStream fs)
	public static EraBinaryDataReader? CreateReader(Stream fs)

		#endregion

	{
		if (fs == null || fs.Length < sizeof(ulong))
		{
			return null;
		}

		SaveDataReader reader = new(fs, true);

		#region EM_私家版_セーブ圧縮

		ulong header;
		try
		{
			//if (reader.ReadUInt64() != EraBDConst.Header)
			//	return null;
			header = reader.ReadUInt64();
			if (header != EraBDConst.Header && header != EraBDConst.ZipHeader)
			{
				return null;
			}
		}
		catch (Exception)
		{
			// The magic itself could not be read (a failure from the medium). The file never said it was a
			// binary save, so the text fallback stays the answer.
			return null;
		}

		#endregion

		try
		{
			int version = (int)reader.ReadUInt32();
			int datacount = (int)reader.ReadUInt32();
			uint[] data = new uint[datacount];
			for (int i = 0; i < datacount; i++)
			{
				data[i] = reader.ReadUInt32();
			}

			#region EM_私家版_セーブ圧縮

			if (header == EraBDConst.ZipHeader)
			{
				MemoryStream ms = new();
				GZipStream gzips = new(reader.BaseStream, CompressionMode.Decompress);
				gzips.CopyTo(ms);
				gzips.Close();
				ms.Seek(0, SeekOrigin.Begin);
				reader = new SaveDataReader(ms, false);
			}
			else
			{
				reader = new SaveDataReader(reader.BaseStream, false);
			}

			#endregion

			if (version == EraBDConst.Version1808)
			{
				return new EraBinaryDataReader1808(reader, version, data);
			}

			// A version this build does not read. The version stays in the message because it is the only
			// thing that tells an older save apart from a damaged one.
			throw new FileEE($"{trerror.CorruptedSaveData.Text} (version: {version})");
		}
		catch (Exception e) when (e is not FileEE)
		{
			throw CorruptedSaveDataError(e);
		}
	}

	#region EM_私家版_セーブ拡張

	public bool EOF()
	{
		return reader.PeekChar() < 0;
	}

	#endregion

	public abstract EraSaveFileType ReadFileType();

	/// <summary>
	/// システム用の特殊処理・圧縮なし
	/// </summary>
	/// <returns></returns>
	public abstract long ReadInt64();

	#region EM_私家版_セーブ拡張

	public abstract Dictionary<string, string> ReadMap();
	public abstract DataTable ReadDataTable();
	public abstract XmlDocument ReadXml();

	#endregion

	public abstract string ReadString();
	public abstract long ReadInt();
	public abstract void ReadIntArray(long[]? refArray, bool needInit);
	public abstract void ReadIntArray2D(long[,]? refArray, bool needInit);
	public abstract void ReadIntArray3D(long[,,]? refArray, bool needInit);
	public abstract void ReadStrArray(string?[]? refArray, bool needInit);
	public abstract void ReadStrArray2D(string?[,]? refArray, bool needInit);
	public abstract void ReadStrArray3D(string?[,,]? refArray, bool needInit);
	public abstract KeyValuePair<string?, EraSaveDataType> ReadVariableCode();

	#region IDisposable メンバ

	public void Dispose()
	{
		reader?.Close();
		reader = null!;
	}

	#endregion

	public void Close()
	{
		Dispose();
	}

	private sealed class EraBinaryDataReader1808 : EraBinaryDataReader
	{
		public EraBinaryDataReader1808(SaveDataReader stream, int ver, uint[] buf)
			: base(stream, ver, buf)
		{ }

		//public bool EOF
		//{
		//    get
		//    {
		//        return (reader.BaseStream.Length == reader.BaseStream.Position);
		//    }
		//}

		public override int ReaderVersion => 1808;

		public override EraSaveFileType ReadFileType()
		{
			byte type = reader.ReadByte();
			if (type is >= 0 and <= 3)
			{
				return (EraSaveFileType)type;
			}

			throw new FileEE(trerror.AbnormalFileData.Text);
		}

		private long m_ReadInt()
		{
			byte b = reader.ReadByte();
			if (b <= Ebdb.Byte)
			{
				return b;
			}

			if (b == Ebdb.Int16)
			{
				return reader.ReadInt16();
			}

			if (b == Ebdb.Int32)
			{
				return reader.ReadInt32();
			}

			if (b == Ebdb.Int64)
			{
				return reader.ReadInt64();
			}

			throw new FileEE(trerror.AbnormalBinaryData.Text);
		}

		public override long ReadInt64()
		{
			return reader.ReadInt64();
		}


		#region EM_私家版_セーブ拡張

		public override Dictionary<string, string> ReadMap()
		{
			return GuardedContent(() =>
			{
				int count = reader.ReadInt32();
				Dictionary<string, string> dict = new();
				for (int i = 0; i < count; i++)
				{
					dict.Add(reader.ReadString(), reader.ReadString());
				}

				return dict;
			});
		}

		public override XmlDocument ReadXml()
		{
			return GuardedContent(() =>
			{
				XmlDocument doc = new();
				doc.LoadXml(reader.ReadString());
				return doc;
			});
		}

		public override DataTable ReadDataTable()
		{
			return GuardedContent(() =>
			{
				DataTable dt = new();
				StringReader ss = new(reader.ReadString());
				dt.ReadXmlSchema(ss);
				ss = new StringReader(reader.ReadString());
				dt.ReadXml(ss);
				return dt;
			});
		}

		#endregion

		public override KeyValuePair<string?, EraSaveDataType> ReadVariableCode()
		{
			EraSaveDataType type = (EraSaveDataType)reader.ReadByte();

			#region EM_私家版_セーブ拡張

			//if (type == EraSaveDataType.EOC || type == EraSaveDataType.EOF || type == EraSaveDataType.Separator)
			if (type is EraSaveDataType.EOC or EraSaveDataType.EOF or EraSaveDataType.Separator or EraSaveDataType.Map or EraSaveDataType.Xml or EraSaveDataType.DT)
			{
				return new KeyValuePair<string?, EraSaveDataType>(null, type);
			}

			#endregion

			string key = reader.ReadString();
			return new KeyValuePair<string?, EraSaveDataType>(key, type);
		}

		//配列じゃないやつは特殊処理
		public override long ReadInt()
		{
			return m_ReadInt();
		}


		public override string ReadString()
		{
			return reader.ReadString();
		}

		public override void ReadIntArray(long[]? refArray, bool needInit)
		{
			long[]? oriArray = null;
			byte b;
			int x = 0;
			int saveLength0 = reader.ReadInt32();
			ValidateShape(saveLength0);
			if (refArray == null) //読み捨て。レアケースのはず
			{
				refArray = new long[saveLength0];
			}

			int length0 = refArray.Length;

			//保存されたデータの方が大きいとき。レアケースのはず
			if (length0 < saveLength0)
			{
				oriArray = refArray;
				//1818修正 サイズ違いの時にあふれないように/配列は最大まで確保、作業するのは重複部分だけ
				refArray = new long[Math.Max(length0, saveLength0)];

				length0 = Math.Min(length0, saveLength0);
			}

			int bound0 = refArray.Length;

			while (true)
			{
				b = reader.ReadByte();
				if (b == Ebdb.EoD)
				{
					break;
				}

				if (b == Ebdb.Zero)
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || cnt > bound0 - x);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						{
							refArray[x + i] = 0;
						}
					}

					x += cnt;
					continue;
				}

				ThrowIfOutOfRange(x >= bound0);
				if (b <= Ebdb.Byte)
				{
					refArray[x] = b;
				}
				else if (b == Ebdb.Int16)
				{
					refArray[x] = reader.ReadInt16();
				}
				else if (b == Ebdb.Int32)
				{
					refArray[x] = reader.ReadInt32();
				}
				else if (b == Ebdb.Int64)
				{
					refArray[x] = reader.ReadInt64();
				}
				else
				{
					throw new FileEE(trerror.AbnormalBinaryData.Text);
				}

				x++;
			}

			if (needInit)
			{
				for (; x < length0; x++)
				{
					refArray[x] = 0;
				}
			}

			if (oriArray != null)
			{
				for (x = 0; x < length0; x++)
				{
					oriArray[x] = refArray[x];
				}
			}
		}

		public override void ReadIntArray2D(long[,]? refArray, bool needInit)
		{
			long[,]? oriArray = null;
			byte b;
			int x = 0;
			int y = 0;
			int saveLength0 = reader.ReadInt32();
			int saveLength1 = reader.ReadInt32();
			ValidateShape(saveLength0, saveLength1);
			if (refArray == null)
			{
				refArray = new long[saveLength0, saveLength1];
			}

			int length0 = refArray.GetLength(0);
			int length1 = refArray.GetLength(1);

			if (length0 < saveLength0 || length1 < saveLength1)
			{
				oriArray = refArray;
				//1818修正 サイズ違いの時にあふれないように/配列は最大まで確保、作業するのは重複部分だけ
				int alloc0 = Math.Max(length0, saveLength0);
				int alloc1 = Math.Max(length1, saveLength1);
				// The reallocation can be wider than either shape (the other dimension may be bigger), so
				// validate it too.
				ValidateShape(alloc0, alloc1);
				refArray = new long[alloc0, alloc1];

				length0 = Math.Min(length0, saveLength0);
				length1 = Math.Min(length1, saveLength1);
			}

			int bound0 = refArray.GetLength(0);
			int bound1 = refArray.GetLength(1);

			while (true)
			{
				b = reader.ReadByte();
				if (b == Ebdb.EoD)
				{
					break;
				}

				if (b == Ebdb.ZeroA1)
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || cnt > bound0 - x);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						for (y = 0; y < length1; y++)
						{
							refArray[x + i, y] = 0;
						}
					}

					x += cnt;
					y = 0;
					continue;
				}

				if (b == Ebdb.EoA1)
				{
					ThrowIfOutOfRange(x >= bound0);
					if (needInit)
					{
						for (; y < length1; y++)
						{
							refArray[x, y] = 0;
						}
					}

					x++;
					y = 0;
					continue;
				}

				if (b == Ebdb.Zero)
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || x >= bound0 || cnt > bound1 - y);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						{
							refArray[x, y + i] = 0;
						}
					}

					y += cnt;
					continue;
				}

				ThrowIfOutOfRange(x >= bound0 || y >= bound1);
				if (b <= Ebdb.Byte)
				{
					refArray[x, y] = b;
				}
				else if (b == Ebdb.Int16)
				{
					refArray[x, y] = reader.ReadInt16();
				}
				else if (b == Ebdb.Int32)
				{
					refArray[x, y] = reader.ReadInt32();
				}
				else if (b == Ebdb.Int64)
				{
					refArray[x, y] = reader.ReadInt64();
				}
				else
				{
					throw new FileEE(trerror.AbnormalBinaryData.Text);
				}

				y++;
			}

			if (needInit)
			{
				for (; x < length0; x++)
				{
					for (; y < length1; y++)
					{
						refArray[x, y] = 0;
					}

					y = 0;
				}
			}

			if (oriArray != null)
			{
				for (x = 0; x < length0; x++)
				for (y = 0; y < length1; y++)
				{
					oriArray[x, y] = refArray[x, y];
				}
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="refArray">データを書き出す先。読み捨てるならnull</param>
		/// <param name="needInit">データがない部分を0で埋める必要があるか</param>
		public override void ReadIntArray3D(long[,,]? refArray, bool needInit)
		{
			long[,,]? oriArray = null;
			byte b;
			int x = 0;
			int y = 0;
			int z = 0;
			int saveLength0 = reader.ReadInt32();
			int saveLength1 = reader.ReadInt32();
			int saveLength2 = reader.ReadInt32();
			ValidateShape(saveLength0, saveLength1, saveLength2);
			if (refArray == null)
			{
				refArray = new long[saveLength0, saveLength1, saveLength2];
			}

			int length0 = refArray.GetLength(0);
			int length1 = refArray.GetLength(1);
			int length2 = refArray.GetLength(2);

			if (length0 < saveLength0 || length1 < saveLength1 || length2 < saveLength2)
			{
				oriArray = refArray;
				//1818修正 サイズ違いの時にあふれないように/配列は最大まで確保、作業するのは重複部分だけ
				int alloc0 = Math.Max(length0, saveLength0);
				int alloc1 = Math.Max(length1, saveLength1);
				int alloc2 = Math.Max(length2, saveLength2);
				// The reallocation can be wider than either shape (the other dimensions may be bigger), so
				// validate it too.
				ValidateShape(alloc0, alloc1, alloc2);
				refArray = new long[alloc0, alloc1, alloc2];

				length0 = Math.Min(length0, saveLength0);
				length1 = Math.Min(length1, saveLength1);
				length2 = Math.Min(length2, saveLength2);
			}

			int bound0 = refArray.GetLength(0);
			int bound1 = refArray.GetLength(1);
			int bound2 = refArray.GetLength(2);

			while (true)
			{
				b = reader.ReadByte();
				if (b == Ebdb.EoD)
				{
					break;
				}

				if (b == Ebdb.ZeroA2) //cnt分だけ空の行列が連続
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || cnt > bound0 - x);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						for (y = 0; y < length1; y++)
						for (z = 0; z < length2; z++)
						{
							refArray[x + i, y, z] = 0;
						}
					}

					x += cnt;
					y = 0;
					z = 0;
					continue;
				}

				if (b == Ebdb.EoA2) //行列終わりor残りが全て0
				{
					ThrowIfOutOfRange(x >= bound0);
					if (needInit)
					{
						for (; y < length1; y++)
						{
							for (; z < length2; z++)
							{
								refArray[x, y, z] = 0;
							}

							z = 0;
						}
					}

					x++;
					y = 0;
					z = 0;
					continue;
				}

				if (b == Ebdb.ZeroA1) //cnt分だけ空の列が連続
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || x >= bound0 || cnt > bound1 - y);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						for (z = 0; z < length2; z++)
						{
							refArray[x, y + i, z] = 0;
						}
					}

					y += cnt;
					z = 0;
					continue;
				}

				if (b == Ebdb.EoA1) //列終わりor残り全て0
				{
					ThrowIfOutOfRange(x >= bound0 || y >= bound1);
					if (needInit)
					{
						for (; z < length2; z++)
						{
							refArray[x, y, z] = 0;
						}
					}

					y++;
					z = 0;
					continue;
				}

				if (b == Ebdb.Zero) //cnt分だけ0が連続
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || x >= bound0 || y >= bound1 || cnt > bound2 - z);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						{
							refArray[x, y, z + i] = 0;
						}
					}

					z += cnt;
					continue;
				}

				ThrowIfOutOfRange(x >= bound0 || y >= bound1 || z >= bound2);
				if (b <= Ebdb.Byte)
				{
					refArray[x, y, z] = b;
				}
				else if (b == Ebdb.Int16)
				{
					refArray[x, y, z] = reader.ReadInt16();
				}
				else if (b == Ebdb.Int32)
				{
					refArray[x, y, z] = reader.ReadInt32();
				}
				else if (b == Ebdb.Int64)
				{
					refArray[x, y, z] = reader.ReadInt64();
				}
				else
				{
					throw new FileEE(trerror.AbnormalBinaryData.Text);
				}

				z++;
			}

			if (needInit)
			{
				for (; x < length0; x++)
				{
					for (; y < length1; y++)
					{
						for (; z < length2; z++)
						{
							refArray[x, y, z] = 0;
						}

						z = 0;
					}

					y = 0;
				}
			}

			if (oriArray != null)
			{
				for (x = 0; x < length0; x++)
				for (y = 0; y < length1; y++)
				for (z = 0; z < length2; z++)
				{
					oriArray[x, y, z] = refArray[x, y, z];
				}
			}
		}

		public override void ReadStrArray(string?[]? refArray, bool needInit)
		{
			string?[]? oriArray = null;
			byte b;
			int x = 0;
			int saveLength0 = reader.ReadInt32();
			ValidateShape(saveLength0);
			if (refArray == null) //読み捨て。レアケースのはず
			{
				refArray = new string[saveLength0];
			}

			int length0 = refArray.Length;

			//保存されたデータの方が大きいとき。レアケースのはず
			if (length0 < saveLength0)
			{
				oriArray = refArray;
				//1818修正 サイズ違いの時にあふれないように/配列は最大まで確保、作業するのは重複部分だけ
				refArray = new string[Math.Max(length0, saveLength0)];

				length0 = Math.Min(length0, saveLength0);
			}

			int bound0 = refArray.Length;

			while (true)
			{
				b = reader.ReadByte();
				if (b == Ebdb.EoD)
				{
					break;
				}

				if (b == Ebdb.Zero)
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || cnt > bound0 - x);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						{
							refArray[x + i] = null;
						}
					}

					x += cnt;
					continue;
				}

				ThrowIfOutOfRange(x >= bound0);
				if (b == Ebdb.String)
				{
					refArray[x] = ReadString();
				}
				else
				{
					throw new FileEE(trerror.AbnormalBinaryData.Text);
				}

				x++;
			}

			if (needInit)
			{
				for (; x < length0; x++)
				{
					refArray[x] = null;
				}
			}

			if (oriArray != null)
			{
				for (x = 0; x < length0; x++)
				{
					oriArray[x] = refArray[x];
				}
			}
		}

		public override void ReadStrArray2D(string?[,]? refArray, bool needInit)
		{
			string?[,]? oriArray = null;
			byte b;
			int x = 0;
			int y = 0;
			int saveLength0 = reader.ReadInt32();
			int saveLength1 = reader.ReadInt32();
			ValidateShape(saveLength0, saveLength1);
			if (refArray == null)
			{
				refArray = new string[saveLength0, saveLength1];
			}

			int length0 = refArray.GetLength(0);
			int length1 = refArray.GetLength(1);

			if (length0 < saveLength0 || length1 < saveLength1)
			{
				oriArray = refArray;
				//1818修正 サイズ違いの時にあふれないように/配列は最大まで確保、作業するのは重複部分だけ
				int alloc0 = Math.Max(length0, saveLength0);
				int alloc1 = Math.Max(length1, saveLength1);
				// The reallocation can be wider than either shape (the other dimension may be bigger), so
				// validate it too.
				ValidateShape(alloc0, alloc1);
				refArray = new string[alloc0, alloc1];

				length0 = Math.Min(length0, saveLength0);
				length1 = Math.Min(length1, saveLength1);
			}

			int bound0 = refArray.GetLength(0);
			int bound1 = refArray.GetLength(1);

			while (true)
			{
				b = reader.ReadByte();
				if (b == Ebdb.EoD)
				{
					break;
				}

				if (b == Ebdb.ZeroA1)
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || cnt > bound0 - x);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						for (y = 0; y < length1; y++)
						{
							refArray[x + i, y] = null;
						}
					}

					x += cnt;
					y = 0;
					continue;
				}

				if (b == Ebdb.EoA1)
				{
					ThrowIfOutOfRange(x >= bound0);
					if (needInit)
					{
						for (; y < length1; y++)
						{
							refArray[x, y] = null;
						}
					}

					x++;
					y = 0;
					continue;
				}

				if (b == Ebdb.Zero)
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || x >= bound0 || cnt > bound1 - y);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						{
							refArray[x, y + i] = null;
						}
					}

					y += cnt;
					continue;
				}

				ThrowIfOutOfRange(x >= bound0 || y >= bound1);
				if (b == Ebdb.String)
				{
					refArray[x, y] = ReadString();
				}
				else
				{
					throw new FileEE(trerror.AbnormalBinaryData.Text);
				}

				y++;
			}

			if (needInit)
			{
				for (; x < length0; x++)
				{
					for (; y < length1; y++)
					{
						refArray[x, y] = null;
					}

					y = 0;
				}
			}

			if (oriArray != null)
			{
				for (x = 0; x < length0; x++)
				for (y = 0; y < length1; y++)
				{
					oriArray[x, y] = refArray[x, y];
				}
			}
		}

		public override void ReadStrArray3D(string?[,,]? refArray, bool needInit)
		{
			string?[,,]? oriArray = null;
			byte b;
			int x = 0;
			int y = 0;
			int z = 0;
			int saveLength0 = reader.ReadInt32();
			int saveLength1 = reader.ReadInt32();
			int saveLength2 = reader.ReadInt32();
			ValidateShape(saveLength0, saveLength1, saveLength2);
			if (refArray == null)
			{
				refArray = new string[saveLength0, saveLength1, saveLength2];
			}

			int length0 = refArray.GetLength(0);
			int length1 = refArray.GetLength(1);
			int length2 = refArray.GetLength(2);

			if (length0 < saveLength0 || length1 < saveLength1 || length2 < saveLength2)
			{
				oriArray = refArray;
				//1818修正 サイズ違いの時にあふれないように/配列は最大まで確保、作業するのは重複部分だけ
				int alloc0 = Math.Max(length0, saveLength0);
				int alloc1 = Math.Max(length1, saveLength1);
				int alloc2 = Math.Max(length2, saveLength2);
				// The reallocation can be wider than either shape (the other dimensions may be bigger), so
				// validate it too.
				ValidateShape(alloc0, alloc1, alloc2);
				refArray = new string[alloc0, alloc1, alloc2];

				length0 = Math.Min(length0, saveLength0);
				length1 = Math.Min(length1, saveLength1);
				length2 = Math.Min(length2, saveLength2);
			}

			int bound0 = refArray.GetLength(0);
			int bound1 = refArray.GetLength(1);
			int bound2 = refArray.GetLength(2);

			while (true)
			{
				b = reader.ReadByte();
				if (b == Ebdb.EoD)
				{
					break;
				}

				if (b == Ebdb.ZeroA2) //cnt分だけ空の行列が連続
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || cnt > bound0 - x);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						for (y = 0; y < length1; y++)
						for (z = 0; z < length2; z++)
						{
							refArray[x + i, y, z] = null;
						}
					}

					x += cnt;
					y = 0;
					z = 0;
					continue;
				}

				if (b == Ebdb.EoA2) //行列終わりor残りが全て0
				{
					ThrowIfOutOfRange(x >= bound0);
					if (needInit)
					{
						for (; y < length1; y++)
						{
							for (; z < length2; z++)
							{
								refArray[x, y, z] = null;
							}

							z = 0;
						}
					}

					x++;
					y = 0;
					z = 0;
					continue;
				}

				if (b == Ebdb.ZeroA1) //cnt分だけ空の列が連続
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || x >= bound0 || cnt > bound1 - y);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						for (z = 0; z < length2; z++)
						{
							refArray[x, y + i, z] = null;
						}
					}

					y += cnt;
					z = 0;
					continue;
				}

				if (b == Ebdb.EoA1) //列終わりor残り全て0
				{
					ThrowIfOutOfRange(x >= bound0 || y >= bound1);
					if (needInit)
					{
						for (; z < length2; z++)
						{
							refArray[x, y, z] = null;
						}
					}

					y++;
					z = 0;
					continue;
				}

				if (b == Ebdb.Zero) //cnt分だけ0が連続
				{
					int cnt = (int)m_ReadInt();
					ThrowIfOutOfRange(cnt < 0 || x >= bound0 || y >= bound1 || cnt > bound2 - z);
					if (needInit)
					{
						for (int i = 0; i < cnt; i++)
						{
							refArray[x, y, z + i] = null;
						}
					}

					z += cnt;
					continue;
				}

				ThrowIfOutOfRange(x >= bound0 || y >= bound1 || z >= bound2);
				if (b == Ebdb.String)
				{
					refArray[x, y, z] = ReadString();
				}
				else
				{
					throw new FileEE(trerror.AbnormalBinaryData.Text);
				}

				z++;
			}

			if (needInit)
			{
				for (; x < length0; x++)
				{
					for (; y < length1; y++)
					{
						for (; z < length2; z++)
						{
							refArray[x, y, z] = null;
						}

						z = 0;
					}

					y = 0;
				}
			}

			if (oriArray != null)
			{
				for (x = 0; x < length0; x++)
				for (y = 0; y < length1; y++)
				for (z = 0; z < length2; z++)
				{
					oriArray[x, y, z] = refArray[x, y, z];
				}
			}
		}
	}
}
