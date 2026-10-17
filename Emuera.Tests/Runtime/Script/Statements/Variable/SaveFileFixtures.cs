using MinorShift.Emuera.Runtime.Utils;
using System;
using System.IO;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>读档用例共用的最小二进制档夹具。</summary>
internal static class SaveFileFixtures
{
	/// <summary>Header + 版本 1808 + 数据块数 0 + 文件类型字节,其后即文件末尾:头认得出、正文读不出。
	/// 用于钉住「认得出二进制头、但读不出来」这条路。</summary>
	public static byte[] MinimalBinarySave(EraSaveFileType fileType)
	{
		using MemoryStream ms = new();
		ms.Write(BitConverter.GetBytes(EraBDConst.Header));
		ms.Write(BitConverter.GetBytes(1808u));
		ms.Write(BitConverter.GetBytes(0u));
		ms.WriteByte((byte)fileType);
		return ms.ToArray();
	}
}
