using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Data;

/// <summary>
/// ConstantData 角色模板查询:No/csvNo 键查找、SP 角色收录、重复定义先到先得。
/// 夹具为最小 chara*.csv,经 LoadData 走真实加载路径。
/// </summary>
public class ConstantDataTests
{
	public ConstantDataTests()
	{
		TestBootstrap.Initialize();
	}

	static async Task<ConstantData> LoadFromAsync(params (string file, string content)[] files)
	{
		var dir = Path.Combine(Path.GetTempPath(), "emuera-tests", "constant-data", Path.GetRandomFileName());
		Directory.CreateDirectory(dir);
		try
		{
			foreach (var (file, content) in files)
				File.WriteAllText(Path.Combine(dir, file), content);
			// 等价启动流程: Preload.Load(CsvDir) 先于 ConstantData.LoadData, OpenOnCache 依赖该缓存
			await Preload.Load(dir);
			var constant = new ConstantData();
			constant.LoadData(dir, null!, false);
			return constant;
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}

	[Fact]
	public async Task GetCharacterTemplate_LooksUpByNo_IncludingSp()
	{
		var constant = await LoadFromAsync(
			("CHARA0.CSV", "NO,0\r\nNAME,Zero\r\n"),
			("CHARA2.CSV", "NO,20\r\nNAME,Twenty\r\nCFLAG,0,1\r\n"),
			("CHARA7.CSV", "NO,7\r\nNAME,Seven\r\n"));

		Assert.Equal("Zero", constant.GetCharacterTemplate(0).Name);
		Assert.Equal("Seven", constant.GetCharacterTemplate(7).Name);
		Assert.Equal("Twenty", constant.GetCharacterTemplate(20).Name);
		Assert.True(constant.GetCharacterTemplate(20)!.IsSpchara);
		Assert.Null(constant.GetCharacterTemplate(999));
	}

	[Fact]
	public async Task GetCharacterTemplateFromCsvNo_LooksUpByCsvNo()
	{
		var constant = await LoadFromAsync(
			("CHARA0.CSV", "NO,0\r\nNAME,Zero\r\n"),
			("CHARA2.CSV", "NO,20\r\nNAME,Twenty\r\n"));

		Assert.Equal("Twenty", constant.GetCharacterTemplateFromCsvNo(2)!.Name);
		Assert.Equal("Zero", constant.GetCharacterTemplateFromCsvNo(0)!.Name);
		Assert.Null(constant.GetCharacterTemplateFromCsvNo(5));
	}

	[Fact]
	public async Task GetCharacterTemplate_UseSp_IgnoresSpParameter()
	{
		var constant = await LoadFromAsync(
			("CHARA2.CSV", "NO,20\r\nNAME,Twenty\r\nCFLAG,0,1\r\n"));

		var byNo = constant.GetCharacterTemplate(20);
		Assert.Same(byNo, constant.GetCharacterTemplate_UseSp(20, false));
		Assert.Same(byNo, constant.GetCharacterTemplate_UseSp(20, true));
		Assert.Null(constant.GetCharacterTemplate_UseSp(999, false));
	}

	[Fact]
	public async Task DuplicateNo_FirstDefinedWins()
	{
		// 文件枚举按 NTFS 名称序, CHARA1.CSV 先于 CHARA2.CSV 加载
		var constant = await LoadFromAsync(
			("CHARA1.CSV", "NO,10\r\nNAME,First\r\n"),
			("CHARA2.CSV", "NO,10\r\nNAME,Second\r\n"));

		Assert.Equal("First", constant.GetCharacterTemplate(10)!.Name);
	}
}
