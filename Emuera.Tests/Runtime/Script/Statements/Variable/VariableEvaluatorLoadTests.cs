using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System.Text;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>
/// LOADCHARA / LOADVAR 的 RESULT:名字被拒绝与文件不存在一样,都是失败,都要让脚本看到 0。
/// </summary>
public class VariableEvaluatorLoadTests : IDisposable
{
	private readonly long previousResult;

	public VariableEvaluatorLoadTests()
	{
		TestBootstrap.Initialize();
		previousResult = GlobalStatic.VEvaluator.RESULT;
	}

	/// <summary>用例把 RESULT 当"上一次残留值"写;TestBootstrap 是单例不重置变量,退出前必须还原。</summary>
	public void Dispose()
		=> GlobalStatic.VEvaluator.RESULT = previousResult;

	[Fact]
	public void LoadChara_RejectedName_ClearsResult()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadchara-rejected-name");

		evaluator.LoadChara(@"..\evil");

		Assert.Equal(0L, evaluator.RESULT);
	}

	[Fact]
	public void LoadVariable_RejectedName_ClearsResult()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadvar-rejected-name");

		evaluator.LoadVariable(@"..\evil");

		Assert.Equal(0L, evaluator.RESULT);
	}

	[Fact]
	public void LoadChara_MissingFile_ClearsResult()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadchara-missing-file");

		evaluator.LoadChara("no-such-chara");

		Assert.Equal(0L, evaluator.RESULT);
	}

	/// <summary>文件在、但读不出:必须给出原因(nullable-migration-34)。旧实现只让 RESULT 归零,
	/// 玩家与脚本都分不清「没有这个存档」和「这个存档坏了」。</summary>
	[Fact]
	public void LoadChara_ForeignContent_ReportsCorruptedSave()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadchara-foreign");
		WriteCharaSave("foreign", Encoding.ASCII.GetBytes(EraDataReader.EMU_1808_START + "\n1\n"));

		string? reason = evaluator.LoadChara("foreign");

		Assert.Equal(0L, evaluator.RESULT);
		Assert.Equal(trerror.CorruptedSaveData.Text, reason);
	}

	/// <summary>二进制档在文件类型之后被截断:读取器抛的 FileEE 文案就是玩家看到的那句,
	/// 不再从这里静默返回。</summary>
	[Fact]
	public void LoadChara_TruncatedBody_ReportsTheReadersDiagnostic()
	{
		VariableEvaluator evaluator = StartWithStaleResult("loadchara-truncated");
		// 0x03 = CharVar,其后不足 8 字节的 unique code
		WriteCharaSave("truncated", BinarySave(0x03));

		string? reason = evaluator.LoadChara("truncated");

		Assert.Equal(0L, evaluator.RESULT);
		Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, reason);
	}

	/// <summary>路径解析失败也走返回值:原实现把 <c>getSaveDataPathG()</c> 放在任何 try 之外,而夹具里
	/// <c>Config.SavDir</c> 未设(只有初始化异常的进程才会为空),旧代码在这一行以 NRE 逃逸——它落
	/// <c>Unexpected</c> 档并连 CLR 堆栈打印,正是本批要消掉的观感。</summary>
	[Fact]
	public void LoadGlobal_WhenThePathCannotBeResolved_ReturnsFalse()
	{
		GameRootScope.UseTempRoot("loadglobal-unresolvable-path");

		bool loaded = GlobalStatic.VEvaluator.LoadGlobal(out _);

		Assert.False(loaded);
	}

	/// <summary>global.sav 的同一条路:类型不是 Global 报「存档数据已损坏」,不返回 false 了事。</summary>
	[Fact]
	public void LoadGlobalFile_WithAnotherSaveType_ReportsCorruptedSave()
	{
		GameRootScope.UseTempRoot("loadglobal-wrong-type");
		string path = Path.Combine(Path.GetTempPath(), $"emuera-global-{Guid.NewGuid():N}.sav");
		File.WriteAllBytes(path, BinarySave(0x00)); // 0x00 = Normal

		try
		{
			bool loaded = GlobalStatic.VEvaluator.LoadGlobalFile(path, out string? reason);

			Assert.False(loaded);
			Assert.Equal(trerror.CorruptedSaveData.Text, reason);
		}
		finally
		{
			File.Delete(path);
		}
	}

	/// <summary>global.sav 的正文截断:与 chara 侧同一出口,原因取自读取器。</summary>
	[Fact]
	public void LoadGlobalFile_TruncatedBody_ReportsTheReadersDiagnostic()
	{
		GameRootScope.UseTempRoot("loadglobal-truncated");
		string path = Path.Combine(Path.GetTempPath(), $"emuera-global-{Guid.NewGuid():N}.sav");
		File.WriteAllBytes(path, BinarySave(0x01)); // 0x01 = Global

		try
		{
			bool loaded = GlobalStatic.VEvaluator.LoadGlobalFile(path, out string? reason);

			Assert.False(loaded);
			Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, reason);
		}
		finally
		{
			File.Delete(path);
		}
	}

	/// <summary>写一份 chara 存档到 dat 目录:LoadChara 按 Program.DatRoot 解析路径(与 Config.SavDir 无关,
	/// 故测试能直接指到临时游戏根)。</summary>
	private static void WriteCharaSave(string name, byte[] content)
	{
		string datRoot = Program.DatRoot.FullName;
		Directory.CreateDirectory(datRoot);
		File.WriteAllBytes(Path.Combine(datRoot, $"chara_{name}.dat"), content);
	}

	/// <summary>最小二进制存档:Header + 版本 1808 + 数据块数 0 + 文件类型字节,其后即文件末尾。
	/// 用于钉住「认得出二进制头、但读不出来」这条路(旧实现把它返回成 null,调用方只当它不是二进制档)。</summary>
	private static byte[] BinarySave(byte fileType)
	{
		using MemoryStream ms = new();
		ms.Write(BitConverter.GetBytes(EraBDConst.Header));
		ms.Write(BitConverter.GetBytes(1808u));
		ms.Write(BitConverter.GetBytes(0u));
		ms.WriteByte(fileType);
		return ms.ToArray();
	}

	/// <summary>一次成功读取会留下 RESULT=1,下一个用例从这里出发才能看见失败是否归零。</summary>
	private static VariableEvaluator StartWithStaleResult(string rootName)
	{
		GameRootScope.UseTempRoot(rootName);
		TestBootstrap.Initialize();

		VariableEvaluator evaluator = GlobalStatic.VEvaluator;
		evaluator.RESULT = 1;
		return evaluator;
	}
}
