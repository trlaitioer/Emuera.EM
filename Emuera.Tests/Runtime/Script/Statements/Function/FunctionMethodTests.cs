using MinorShift.Emuera.GameData.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using Xunit;
using static Emuera.Tests.Runtime.Script.Statements.Function.BuiltinFunctionTestKit;

namespace Emuera.Tests.Runtime.Script.Statements.Function;

/// <summary>
/// 内建函数(FunctionMethod)纯函数测试(常用内建函数)。
/// 经 FunctionMethodCreator.GetMethodList() 取注册表实例;取值走 <see cref="BuiltinFunctionTestKit.InvokeWith"/>
/// (槽位入口 + 检查期角色),函数体迁移到 <c>ArgumentSlots</c> 后本文件无需跟着改。
/// SingleStrTerm / SingleLongTerm 的 GetStrValue / GetIntValue 不使用 exm,故这里显式传 null 钉住这一点。
/// </summary>
public class FunctionMethodTests
{
	#region 基础

	[Fact]
	public void Toupper_UpperCase()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOUPPER"];
		var result = InvokeWith(m, [new SingleStrTerm("abc")], null);
		Assert.Equal("ABC", result);
	}

	[Fact]
	public void Tolower_LowerCase()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOLOWER"];
		var result = InvokeWith(m, [new SingleStrTerm("ABC")], null);
		Assert.Equal("abc", result);
	}

	[Fact]
	public void Abs_Positive()
	{
		var m = FunctionMethodCreator.GetMethodList()["ABS"];
		var result = InvokeWith(m, [new SingleLongTerm(-5)], null);
		Assert.Equal(5L, result);
	}

	[Fact]
	public void Max_ReturnsGreater()
	{
		var m = FunctionMethodCreator.GetMethodList()["MAX"];
		var result = InvokeWith(m, [new SingleLongTerm(3), new SingleLongTerm(7)], null);
		Assert.Equal(7L, result);
	}

	[Fact]
	public void Min_ReturnsSmaller()
	{
		var m = FunctionMethodCreator.GetMethodList()["MIN"];
		var result = InvokeWith(m, [new SingleLongTerm(3), new SingleLongTerm(7)], null);
		Assert.Equal(3L, result);
	}

	[Fact]
	public void Sqrt_IntegerRoot()
	{
		var m = FunctionMethodCreator.GetMethodList()["SQRT"];
		var result = InvokeWith(m, [new SingleLongTerm(9)], null);
		Assert.Equal(3L, result);
	}

	#endregion

	#region 其他常用函数

	[Fact]
	public void Getbit_ReturnsBit()
	{
		var m = FunctionMethodCreator.GetMethodList()["GETBIT"];
		Assert.Equal(1L, InvokeWith(m, [new SingleLongTerm(5), new SingleLongTerm(0)], null));
		Assert.Equal(0L, InvokeWith(m, [new SingleLongTerm(5), new SingleLongTerm(1)], null));
	}

	[Fact]
	public void Inrange_Inclusive()
	{
		var m = FunctionMethodCreator.GetMethodList()["INRANGE"];
		Assert.Equal(1L, InvokeWith(m, [new SingleLongTerm(3), new SingleLongTerm(1), new SingleLongTerm(5)], null));
		Assert.Equal(1L, InvokeWith(m, [new SingleLongTerm(1), new SingleLongTerm(1), new SingleLongTerm(5)], null));
		Assert.Equal(0L, InvokeWith(m, [new SingleLongTerm(6), new SingleLongTerm(1), new SingleLongTerm(5)], null));
	}

	[Fact]
	public void Tostr_ConvertsInt()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOSTR"];
		Assert.Equal("123", InvokeWith(m, [new SingleLongTerm(123)], null));
	}

	[Fact]
	public void Tofull_ConvertsToFullWidth()
	{
		// 全角转换
		var m = FunctionMethodCreator.GetMethodList()["TOFULL"];
		Assert.Equal("ａｂｃ", InvokeWith(m, [new SingleStrTerm("abc")], null));
	}

	[Fact]
	public void Tohalf_ConvertsToHalfWidth()
	{
		var m = FunctionMethodCreator.GetMethodList()["TOHALF"];
		Assert.Equal("abc", InvokeWith(m, [new SingleStrTerm("ａｂｃ")], null));
	}

	#endregion
}
