using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>
/// 普通存档写入的失败面(nullable-migration-35):<c>SaveTo</c> 此前以 <c>catch (Exception) { return false; }</c>
/// 收尾,原因被吞掉,三个调用点只能打一句「予期しないエラー」。这里钉三件事:失败带回原因,写失败不动已有存档,
/// 以及 <c>SaveChara</c> 的失败归入存档档(而非裸 BCL 异常落 <c>Unexpected</c> 档连堆栈打印)。
/// </summary>
public class SaveDataWriteTests
{
	public SaveDataWriteTests() => TestBootstrap.Initialize();

	/// <summary>目标所在目录不存在:写入连开始都做不到,失败必须带回原因(旧实现只返回 false)。
	/// <c>SaveToFile</c> 是新的可测入口——原 <c>SaveTo</c> 的目标路径取自 <c>Config.SavDir</c>,测试指不到别处。</summary>
	[Fact]
	public void SaveToFile_WithUnwritableTarget_ReportsTheReason()
	{
		string root = GameRootScope.UseTempRoot("save-unwritable");
		string target = Path.Combine(root, "no-such-directory", "save00.sav");

		bool saved = GlobalStatic.VEvaluator.SaveToFile(target, "SAVE TEXT", out string? reason);

		Assert.False(saved);
		Assert.NotNull(reason);
		Assert.NotEmpty(reason);
		Assert.False(File.Exists(target));
	}

	/// <summary>写入无法开始时必须保留已有存档:旧写法用 <c>FileMode.Create</c> 直接开目标档,
	/// 它在写入前就截断文件,任何写入期失败都会毁掉旧存档并留下半截文件。</summary>
	[Fact]
	public void SaveToFile_WhenTheWriteCannotStart_KeepsTheExistingSave()
	{
		string root = GameRootScope.UseTempRoot("save-keeps-previous");
		string target = Path.Combine(root, "save00.sav");
		File.WriteAllText(target, "PREVIOUS SAVE");
		// 临时兄弟位被目录占住 → 写入无法开始;旧写法不使用临时文件,会直接覆盖目标档
		Directory.CreateDirectory(target + ".tmp");

		bool saved = GlobalStatic.VEvaluator.SaveToFile(target, "NEW SAVE", out string? reason);

		Assert.False(saved);
		Assert.NotNull(reason);
		Assert.Equal("PREVIOUS SAVE", File.ReadAllText(target));
	}

	/// <summary>成功路径的回归:写入后目标档是新的那份内容,且不留下临时文件。</summary>
	[Fact]
	public void SaveToFile_OnSuccess_ReplacesTheSaveAndDropsTheTemporaryFile()
	{
		string root = GameRootScope.UseTempRoot("save-success");
		string target = Path.Combine(root, "save00.sav");
		File.WriteAllText(target, "PREVIOUS SAVE");

		bool saved = GlobalStatic.VEvaluator.SaveToFile(target, "NEW SAVE", out string? reason);

		Assert.True(saved);
		Assert.Null(reason);
		Assert.Contains("NEW SAVE", File.ReadAllText(target));
		Assert.False(File.Exists(target + ".tmp"));
	}

	/// <summary>SAVECHARA 的写入失败归入存档档(nullable-migration-35 §影响的对外可见面):此前是裸 BCL 异常,
	/// 落 <c>Unexpected</c> 档连 CLR 堆栈一起打印;现在是 <c>FileEE</c>,只打可读消息。
	/// 临时兄弟位被目录占住 → 写入无法开始,与普通存档那条用例同一手法。</summary>
	[Fact]
	public void SaveChara_WhenTheWriteCannotStart_ThrowsASaveDataError()
	{
		GameRootScope.UseTempRoot("savechara-failure");
		string datRoot = Program.DatRoot.FullName;
		Directory.CreateDirectory(datRoot);
		Directory.CreateDirectory(Path.Combine(datRoot, "chara_blocked.dat.tmp"));

		FileEE error = Assert.Throws<FileEE>(() => GlobalStatic.VEvaluator.SaveChara("blocked", "SAVE TEXT", []));

		Assert.Equal(ErrorReportKind.SaveData, ErrorReport.Classify(error));
		Assert.NotEmpty(error.Message);
	}

	/// <summary>SAVEGLOBAL 的写入失败归存档档(nullable-migration-35 §a2):此前抛 <c>CodeEE</c>,而
	/// <c>IOException</c> 属于 <c>SystemException</c>,于是磁盘故障被报成「脚本错误」+ 脚本侧调用栈。
	/// 夹具里 <c>Config.SavDir</c> 未设,失败取自路径解析(与 <c>LoadGlobal</c> 同一处守卫),不影响档位与抬头文案的断言。</summary>
	[Fact]
	public void SaveGlobal_WhenTheWriteFails_ThrowsASaveDataError()
	{
		GameRootScope.UseTempRoot("saveglobal-failure");

		FileEE error = Assert.Throws<FileEE>(() => GlobalStatic.VEvaluator.SaveGlobal());

		Assert.Equal(ErrorReportKind.SaveData, ErrorReport.Classify(error));
		Assert.Contains(trerror.ErrorSavingGlobalData.Text, error.Message);
	}
}
