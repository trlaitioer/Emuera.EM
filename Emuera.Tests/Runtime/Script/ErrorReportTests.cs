using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script;

/// <summary>
/// nullable-migration-30 的回归网:FileEE 是 EmueraException 的直接子类,既不是 CodeEE 也不是 ExeEE,
/// 旧的两处报错分支(Process.handleException / handleExceptionInSystemProc)只认后两者,于是存档损坏的
/// 可读文案被套上「予期せぬエラー」并连 CLR 堆栈一起打印。分级与「抬头文案 / 是否打堆栈」两件事都是纯函数,
/// 故用例直接钉住:漏判会让 FileEE 掉进 Unexpected,而 Unexpected 才是「连堆栈」的那一档。
/// 被截断存档真正抛出的 FileEE 与该分级的连接见 EraDataStreamTests;
/// 两处 handler 只剩打印调用本身没有用例(需要 EmueraConsole)。
/// </summary>
public class ErrorReportTests
{
	[Fact]
	public void Classify_FileEE_IsSaveData()
		=> Assert.Equal(ErrorReportKind.SaveData, ErrorReport.Classify(new FileEE("x")));

	[Fact]
	public void Classify_CodeEE_IsCode()
	{
		Assert.Equal(ErrorReportKind.Code, ErrorReport.Classify(new CodeEE("x")));
		// 两个脚本错误特化与基类同档
		Assert.Equal(ErrorReportKind.Code, ErrorReport.Classify(new IdentifierNotFoundCodeEE("x")));
		Assert.Equal(ErrorReportKind.Code, ErrorReport.Classify(new NotImplCodeEE()));
	}

	[Fact]
	public void Classify_ExeEE_IsEmuera()
		=> Assert.Equal(ErrorReportKind.Emuera, ErrorReport.Classify(new ExeEE("x")));

	/// <summary>编程错误仍是最后一档(唯一连 CLR 堆栈报出的档),不得被前三档收编。</summary>
	[Theory]
	[InlineData(typeof(NullReferenceException))]
	[InlineData(typeof(IndexOutOfRangeException))]
	[InlineData(typeof(InvalidOperationException))]
	[InlineData(typeof(IOException))]
	[InlineData(typeof(ArgumentException))]
	public void Classify_ProgrammingErrors_AreUnexpected(Type type)
		=> Assert.Equal(ErrorReportKind.Unexpected, ErrorReport.Classify((Exception)Activator.CreateInstance(type)!));

	/// <summary>四档各有自己的抬头文案:存档档不得借用「予期せぬエラー」(那正是旧实现的缺陷)。</summary>
	[Fact]
	public void Header_MapsEachKindToItsOwnText()
	{
		Assert.Equal(trerror.HasError.Text, ErrorReport.Header(ErrorReportKind.Code).Text);
		Assert.Equal(trerror.HasEmueraError.Text, ErrorReport.Header(ErrorReportKind.Emuera).Text);
		Assert.Equal(trerror.HasSaveDataError.Text, ErrorReport.Header(ErrorReportKind.SaveData).Text);
		Assert.Equal(trerror.HasUnexpectedError.Text, ErrorReport.Header(ErrorReportKind.Unexpected).Text);
	}

	/// <summary>函数终止路径(系统流程)用的是另一组文案,四档同样各自独立。</summary>
	[Fact]
	public void SystemProcHeader_MapsEachKindToItsOwnText()
	{
		Assert.Equal(trerror.FuncEndError.Text, ErrorReport.SystemProcHeader(ErrorReportKind.Code).Text);
		Assert.Equal(trerror.FuncEndEmueraError.Text, ErrorReport.SystemProcHeader(ErrorReportKind.Emuera).Text);
		Assert.Equal(trerror.FuncEndSaveDataError.Text, ErrorReport.SystemProcHeader(ErrorReportKind.SaveData).Text);
		Assert.Equal(trerror.FuncEndUnexpectedError.Text, ErrorReport.SystemProcHeader(ErrorReportKind.Unexpected).Text);
	}

	/// <summary>只有予期せぬエラー带 CLR 堆栈;脚本/Emuera/存档三档只打可读消息。
	/// 形参取 int:xUnit 的公开用例不能用 internal 类型签名(CS0051)。</summary>
	[Theory]
	[InlineData((int)ErrorReportKind.Code, false)]
	[InlineData((int)ErrorReportKind.Emuera, false)]
	[InlineData((int)ErrorReportKind.SaveData, false)]
	[InlineData((int)ErrorReportKind.Unexpected, true)]
	public void PrintsStackTrace_OnlyForUnexpected(int kind, bool expected)
		=> Assert.Equal(expected, ErrorReport.PrintsStackTrace((ErrorReportKind)kind));

	/// <summary>抬头下那一行:只有予期せぬエラー要带上 CLR 类型名(堆栈之外没有别的线索)。</summary>
	[Fact]
	public void Message_NamesTheClrTypeOnlyForUnexpected()
	{
		Exception unexpected = new NullReferenceException("boom");
		Assert.Equal("System.NullReferenceException:boom", ErrorReport.Message(ErrorReportKind.Unexpected, unexpected));

		FileEE fileError = new("セーブデータが壊れています");
		Assert.Equal(fileError.Message, ErrorReport.Message(ErrorReportKind.SaveData, fileError));
	}
}
