using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Statements;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements;

/// <summary>
/// 调试窗口监视列表的取值路径(nullable-migration-26)。缝是 DebugWatchExpression.GetValueString——
/// 该项由 DebugDialog.getValueString 抽出(表单本身需要 WinForms/STA,不进用例)。
/// </summary>
public class DebugWatchExpressionTests
{
	public DebugWatchExpressionTests()
	{
		TestBootstrap.Initialize();
	}

	private static string Value(string expression)
		=> DebugWatchExpression.GetValueString(expression, GlobalStatic.EMediator);

	/// <summary>只含空白的表达式没有可解析的项:必须给出解析错误文本,而不是空引用异常文本。</summary>
	[Fact]
	public void WhitespaceOnly_ReportsAParseError()
	{
		foreach (string expression in new[] { " ", "\t", "  \t " })
		{
			string text = Value(expression);

			Assert.Equal(trerror.CanNotInterpretedExpression.Text, text);
			Assert.DoesNotContain(nameof(NullReferenceException), text);
		}
	}

	/// <summary>空串是「该项尚未填写」,显示为空。</summary>
	[Fact]
	public void EmptyEntry_IsBlank()
	{
		Assert.Equal("", Value(""));
	}

	/// <summary>正控制:可求值的表达式照常给出取值文本。</summary>
	[Fact]
	public void EvaluableExpression_RendersTheValue()
	{
		Assert.Equal("2", Value("1 + 1"));
	}

	/// <summary>无法解析的表达式给出诊断文本(既有 catch 兜住),不抛出。</summary>
	[Fact]
	public void UnparsableExpression_ReportsADiagnostic()
	{
		string text = Value("(");

		Assert.NotEqual("", text);
		Assert.DoesNotContain(nameof(NullReferenceException), text);
	}
}
