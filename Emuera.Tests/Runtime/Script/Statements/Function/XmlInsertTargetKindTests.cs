using System.Xml;
using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// nullable-migration-32 §1 的回归网:XML_ADDATTRIBUTE/XML_ADDNODE 的插入目标按在线文档限定——
/// XML_ADDATTRIBUTE 的 methodType 0 作用于选中的元素、1/2 作用于选中的属性;XML_ADDNODE 的 methodType 0
/// 作用于选中的元素、1/2 插入到非根元素的同层(evilmask.gitlab.io/emuera.em.doc/en/Reference/ 的
/// XML_ADDATTRIBUTE.html 与 XML_ADDNODE.html)。
/// 缝是 FunctionMethod 的取值入口 + 文档表的 BYNAME 书式——slot 0 是字符串字面量时
/// saveToArg0 为假,故不需要可写的字符串变量。
/// 旧行为:选中的节点种类不支持该操作时由 CLR 抛异常,落「予期せぬエラー」+ 堆栈——
/// ADDATTRIBUTE(methodType=0)选中属性/文本/文档节点时 XmlNode.Attributes 为 null(空引用);
/// ADDNODE 在属性节点上 AppendChild 抛「the specified node is the wrong type」、在文本节点上抛
/// 「The current node cannot contain other nodes」、在根元素上插入同层节点抛
/// 「already has a 'DocumentElement' node」(文档已写明 1/2 不作用于根节点)。
/// 现在一律按文档的「Returns 0 on failure」返 0,且不改变文档;返 0 只发生在单匹配,
/// 多匹配沿用既定的「返匹配数而不新增」。属性节点作参照节点的 1/2 与文本节点的 1/2 本就成立
/// (属性无 ParentNode、文本节点是元素的子节点),这里一并钉住,确保收紧只覆盖今天会抛异常的组合。
/// </summary>
public class XmlInsertTargetKindTests : IDisposable
{
	private const string Key = "nl32_xml_insert_target";

	/// <summary>文档表是全局的,用例独占该键并在退出时还原。</summary>
	private readonly XmlDocument? previous;

	/// <summary>装载后的原文,用于断言「不新增」的用例没有改动文档。</summary>
	private readonly string original;

	public XmlInsertTargetKindTests()
	{
		TestBootstrap.Initialize();
		GlobalStatic.VEvaluator.VariableData.DataXmlDocument.TryGetValue(Key, out previous);
		XmlDocument doc = new();
		doc.LoadXml("<root a=\"1\">text<child/></root>");
		GlobalStatic.VEvaluator.VariableData.DataXmlDocument[Key] = doc;
		original = doc.OuterXml;
	}

	public void Dispose()
	{
		if (previous == null)
		{
			GlobalStatic.VEvaluator.VariableData.DataXmlDocument.Remove(Key);
		}
		else
		{
			GlobalStatic.VEvaluator.VariableData.DataXmlDocument[Key] = previous;
		}
	}

	private static long Int(string name, params AExpression?[] args) => (long)Invoke(Method(name), [.. args]);

	private static XmlDocument Doc() => GlobalStatic.VEvaluator.VariableData.DataXmlDocument[Key];

	// ── XML_ADDATTRIBUTE:methodType=0 要求选中元素节点 ──────────────────────────

	/// <summary>选中属性本身(xpath "/root/@a"):旧实现解引用 null 的 Attributes 抛空引用。</summary>
	[Fact]
	public void AddAttribute_Method0_WithAttributeSelected_ReturnsZero()
	{
		long count = Int("XML_ADDATTRIBUTE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/@a"), new SingleStrTerm("b"));

		Assert.Equal(0L, count);
		Assert.Equal(original, Doc().OuterXml);
	}

	/// <summary>选中文本节点:Attributes 同样为 null。</summary>
	[Fact]
	public void AddAttribute_Method0_WithTextSelected_ReturnsZero()
	{
		long count = Int("XML_ADDATTRIBUTE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/text()"), new SingleStrTerm("b"));

		Assert.Equal(0L, count);
		Assert.Equal(original, Doc().OuterXml);
	}

	/// <summary>正控制:选中元素节点仍新增属性并返回新增次数(attrValue 省略即空串)。</summary>
	[Fact]
	public void AddAttribute_Method0_WithElementSelected_AddsAttribute()
	{
		long count = Int("XML_ADDATTRIBUTE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root"), new SingleStrTerm("b"));

		Assert.Equal(1L, count);
		Assert.NotNull(Doc().SelectSingleNode("/root/@b"));
	}

	/// <summary>正控制:methodType=1 要求选中属性,这条既有语义不得被这次收紧误伤。</summary>
	[Fact]
	public void AddAttribute_Method1_WithAttributeSelected_AddsAttribute()
	{
		long count = Int("XML_ADDATTRIBUTE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/@a"), new SingleStrTerm("b"), new SingleStrTerm("2"),
			new SingleLongTerm(1));

		Assert.Equal(1L, count);
		Assert.Equal("2", Doc().SelectSingleNode("/root/@b")!.Value);
	}

	// ── XML_ADDNODE:methodType=0 要求选中元素节点 ───────────────────────────────

	/// <summary>选中文本节点:AppendChild 抛「The current node cannot contain other nodes」。</summary>
	[Fact]
	public void AddNode_Method0_WithTextSelected_ReturnsZero()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/text()"), new SingleStrTerm("<c/>"));

		Assert.Equal(0L, count);
		Assert.Equal(original, Doc().OuterXml);
	}

	/// <summary>选中属性节点:AppendChild 在属性上抛「the specified node is the wrong type」。</summary>
	[Fact]
	public void AddNode_Method0_WithAttributeSelected_ReturnsZero()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/@a"), new SingleStrTerm("<c/>"));

		Assert.Equal(0L, count);
		Assert.Equal(original, Doc().OuterXml);
	}

	/// <summary>正控制:选中元素节点仍新增子节点并返回匹配次数(在线文档的示例形态)。</summary>
	[Fact]
	public void AddNode_Method0_WithElementSelected_AddsChild()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root"), new SingleStrTerm("<c/>"));

		Assert.Equal(1L, count);
		Assert.NotNull(Doc().SelectSingleNode("/root/c"));
	}

	// ── XML_ADDNODE:methodType=1/2 要求选中的是非根元素节点 ─────────────────────

	/// <summary>根元素:其父是文档,插入同层节点会抛「already has a 'DocumentElement' node」;文档写明 1/2 不作用于根节点。</summary>
	[Fact]
	public void AddNode_Method1_WithRootSelected_ReturnsZero()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root"), new SingleStrTerm("<c/>"), new SingleLongTerm(1));

		Assert.Equal(0L, count);
		Assert.Equal(original, Doc().OuterXml);
	}

	/// <summary>
	/// 属性节点:其 ParentNode 为 null(属性不经 ChildNodes 到达),故这条在旧实现下也返 0——钉子而非修复,
	/// 防止后续把「参照节点须有元素父节点」这条判据改成只看节点种类。
	/// </summary>
	[Fact]
	public void AddNode_Method1_WithAttributeSelected_ReturnsZero()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/@a"), new SingleStrTerm("<c/>"), new SingleLongTerm(1));

		Assert.Equal(0L, count);
		Assert.Equal(original, Doc().OuterXml);
	}

	/// <summary>正控制:选中的子元素上插入同层节点(在线文档的示例形态)。</summary>
	[Fact]
	public void AddNode_Method1_WithChildSelected_InsertsSibling()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/child"), new SingleStrTerm("<brother/>"), new SingleLongTerm(1));

		Assert.Equal(1L, count);
		Assert.Equal(["brother", "child"], Doc().SelectNodes("/root/*")!.Cast<XmlElement>().Select(e => e.Name));
	}

	/// <summary>
	/// 选中文本节点时 1/2 本来就成立(文本节点是元素的子节点,可作参照节点),本次收紧不改它——
	/// 改的只是「今天会抛异常」的那几种组合。
	/// </summary>
	[Fact]
	public void AddNode_Method1_WithTextSelected_InsertsSibling()
	{
		long count = Int("XML_ADDNODE_BYNAME",
			new SingleStrTerm(Key), new SingleStrTerm("/root/text()"), new SingleStrTerm("<c/>"), new SingleLongTerm(1));

		Assert.Equal(1L, count);
		Assert.Equal(["c", "child"], Doc().SelectNodes("/root/*")!.Cast<XmlElement>().Select(e => e.Name));
	}
}
