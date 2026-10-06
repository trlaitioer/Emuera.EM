using System.Drawing;
using MinorShift.Emuera.UI;
using Xunit;

// 本目录的用例统一用 Emuera.Tests.Runtime:命名空间若叫 ...Runtime.Config 会把产品类型 Config 遮蔽掉
namespace Emuera.Tests.Runtime;

/// <summary>
/// Config.DefaultFont 的非空承诺(nullable-migration-28):绘制路径直接读该字体、不判空,而配置的字体家族
/// 可能构造不出来,故 FontFactory 必须有回退。覆盖的是回退本身(可构造性 + 逐字号缓存)。
/// <b>未覆盖</b>：<c>GetFont("") ?? GetFallbackFont(...)</c> 的接线——生产路径上该分支不可达(家族名非法不
/// 是失败原因:空串/空白/未知名/null 实测都被 GDI+ 静默替换成替代字体;只有字号 ≤ 0 才让 <c>CreateFont</c>
/// 返回 null,而配置加载与缩放都把字号钳到 ≥8)。夹具的「Config 未初始化」状态(FontSize 与
/// renderFontSizePx 均为 0)本可跑到该分支,但另有多个用例调用 <c>Config.ApplyWindowGeometry</c> 把
/// renderFontSizePx 永久留在正值,而 xUnit 只保证串行、不保证发现顺序,据此写出的用例是顺序相关的,
/// 故只钉住回退入口的契约。
/// </summary>
public class DefaultFontTests
{
	public DefaultFontTests()
	{
		TestBootstrap.Initialize();
	}

	/// <summary>回退字体在任意字号下都可构造:非法字号(≤ 0)被钳成正数,故绘制路径不会拿到 null。</summary>
	[Fact]
	public void FallbackFont_IsBuildableAtEverySize()
	{
		foreach (int size in new[] { 0, -3, 1, 18 })
		{
			Font font = FontFactory.GetFallbackFont(size);

			Assert.NotNull(font);
			Assert.True(font.Size > 0);
		}
	}

	/// <summary>同一字号复用缓存实例:该字体每帧被读取,每次新建会漏 GDI 句柄(ClearFont 也管不到)。</summary>
	[Fact]
	public void FallbackFont_IsCachedPerSize()
	{
		Assert.Same(FontFactory.GetFallbackFont(7), FontFactory.GetFallbackFont(7));
	}
}
