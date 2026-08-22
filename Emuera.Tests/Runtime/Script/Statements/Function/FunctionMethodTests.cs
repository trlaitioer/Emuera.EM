using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// 内建函数(FunctionMethod)纯函数测试(常用内建函数)。
/// 经 FunctionMethodCreator.GetMethodList() 取注册表实例;
/// SingleStrTerm / SingleLongTerm 的 GetStrValue / GetIntValue 不使用 exm,传 null 即可。
/// </summary>
public class FunctionMethodTests
{
	#region 基础

	[Fact]
	public void Toupper_UpperCase()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOUPPER"];
		var result = m.GetStrValue(null!, [new SingleStrTerm("abc")]);
		Assert.Equal("ABC", result);
	}

	[Fact]
	public void Tolower_LowerCase()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOLOWER"];
		var result = m.GetStrValue(null!, [new SingleStrTerm("ABC")]);
		Assert.Equal("abc", result);
	}

	[Fact]
	public void Abs_Positive()
	{
		var m = FunctionMethodCreator.GetMethodList()["ABS"];
		var result = m.GetIntValue(null!, [new SingleLongTerm(-5)]);
		Assert.Equal(5L, result);
	}

	[Fact]
	public void Max_ReturnsGreater()
	{
		var m = FunctionMethodCreator.GetMethodList()["MAX"];
		var result = m.GetIntValue(null!, [new SingleLongTerm(3), new SingleLongTerm(7)]);
		Assert.Equal(7L, result);
	}

	[Fact]
	public void Min_ReturnsSmaller()
	{
		var m = FunctionMethodCreator.GetMethodList()["MIN"];
		var result = m.GetIntValue(null!, [new SingleLongTerm(3), new SingleLongTerm(7)]);
		Assert.Equal(3L, result);
	}

	[Fact]
	public void Sqrt_IntegerRoot()
	{
		var m = FunctionMethodCreator.GetMethodList()["SQRT"];
		var result = m.GetIntValue(null!, [new SingleLongTerm(9)]);
		Assert.Equal(3L, result);
	}

	#endregion

	#region 其他常用函数

	[Fact]
	public void Getbit_ReturnsBit()
	{
		var m = FunctionMethodCreator.GetMethodList()["GETBIT"];
		Assert.Equal(1L, m.GetIntValue(null!, [new SingleLongTerm(5), new SingleLongTerm(0)]));
		Assert.Equal(0L, m.GetIntValue(null!, [new SingleLongTerm(5), new SingleLongTerm(1)]));
	}

	[Fact]
	public void Inrange_Inclusive()
	{
		var m = FunctionMethodCreator.GetMethodList()["INRANGE"];
		Assert.Equal(1L, m.GetIntValue(null!, [new SingleLongTerm(3), new SingleLongTerm(1), new SingleLongTerm(5)]));
		Assert.Equal(1L, m.GetIntValue(null!, [new SingleLongTerm(1), new SingleLongTerm(1), new SingleLongTerm(5)]));
		Assert.Equal(0L, m.GetIntValue(null!, [new SingleLongTerm(6), new SingleLongTerm(1), new SingleLongTerm(5)]));
	}

	[Fact]
	public void Tostr_ConvertsInt()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOSTR"];
		Assert.Equal("123", m.GetStrValue(null!, [new SingleLongTerm(123)]));
	}

	[Fact]
	public void Tofull_ConvertsToFullWidth()
	{
		// 全角转换
		var m = FunctionMethodCreator.GetMethodList()["TOFULL"];
		Assert.Equal("ａｂｃ", m.GetStrValue(null!, [new SingleStrTerm("abc")]));
	}

	[Fact]
	public void Tohalf_ConvertsToHalfWidth()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOHALF"];
		Assert.Equal("abc", m.GetStrValue(null!, [new SingleStrTerm("ａｂｃ")]));
	}

	#endregion
}
