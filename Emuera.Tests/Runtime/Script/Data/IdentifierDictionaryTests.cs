using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Data;

/// <summary>
/// IdentifierDictionary(标识符字典)测试,依赖 TestBootstrap 的运行时最小初始化。
/// </summary>
public class IdentifierDictionaryTests
{
	public IdentifierDictionaryTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>
	/// 解析文件里首个 @函数 的参数时,scaningLine 指向该标签,而它的 ParentLabelLine 仍为 null
	/// (ErbLoader 只在遇到过标签之后才把 parentLine 交给 ParseLine)。
	/// 此时带显式 @子键 的局部变量引用必须给出脚本可见的错误,而不是空引用。
	/// </summary>
	[Fact]
	public void ExplicitSubKey_WithoutParentLabel_ThrowsCodeEE()
	{
		var label = new FunctionLabelLine(new ScriptPosition("test.erb", 1), "FIRST", new WordCollection());
		var saved = GlobalStatic.Process.scaningLine;
		try
		{
			GlobalStatic.Process.scaningLine = label;
			var e = Assert.Throws<CodeEE>(() => GlobalStatic.IdentifierDictionary.GetVariableToken("LOCAL", "x", true));
			Assert.Contains("LOCAL", e.Message);
		}
		finally
		{
			GlobalStatic.Process.scaningLine = saved;
		}
	}
}
