using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using Xunit;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>
/// SORTCHARA 以字符串角色二维变量为键时的回归网(nullable-migration-14 认定的 CharacterData.temp_SortKey 站点)。
/// 缝是 SORTCHARA 实际调用的入口 VariableEvaluator.SortChara,观察点是角色列表次序。
/// 字符串二维数组按 new string[n, m] 分配、未赋值的元素是 null;旧实现把它经 ! 写进非空的
/// temp_SortKey,而比较器是以该角色为接收者调用 CompareTo,故排序期抛 NullReferenceException
/// (List.Sort 会把它包成 InvalidOperationException)。
/// </summary>
public class SortCharaTests : IDisposable
{
	/// <summary>#DIMS CHARADATA SORTKEY2D, 2, 2。注册是进程级且无撤销 API,故整个类只注册一次;
	/// 变量名取测试专用,避免与别的用例解析的脚本撞名。</summary>
	static VariableToken? sortKey;

	/// <summary>排序改写的系统变量(TARGET/MASTER/ASSI)按 README 约定快照还原。</summary>
	readonly (VariableToken Token, long Value)[] globalsBefore;

	/// <summary>SortChara 排序整份角色列表,夹具先清空、退出时还原。</summary>
	readonly CharacterData[] charactersBefore;

	/// <summary>键未赋值的角色(列表首位)。</summary>
	readonly CharacterData unset;

	/// <summary>键为 "b" 的角色。</summary>
	readonly CharacterData keyB;

	/// <summary>键为 "a" 的角色。</summary>
	readonly CharacterData keyA;

	public SortCharaTests()
	{
		TestBootstrap.Initialize();
		sortKey ??= RegisterSortKey();

		VariableData varData = GlobalStatic.VariableData;
		charactersBefore = [.. varData.CharacterList];
		varData.CharacterList.Clear();

		globalsBefore =
		[
			.. new[] { "TARGET", "MASTER", "ASSI" }
				.Select(name => varData.GetSystemVariableToken(name))
				.Select(token => (token, ((long[])token.GetArray())[0]))
		];

		GlobalStatic.VEvaluator.AddPseudoCharacter();
		GlobalStatic.VEvaluator.AddPseudoCharacter();
		GlobalStatic.VEvaluator.AddPseudoCharacter();
		unset = varData.CharacterList[0];
		keyB = varData.CharacterList[1];
		keyA = varData.CharacterList[2];
	}

	public void Dispose()
	{
		foreach (var (token, value) in globalsBefore)
			token.SetValue(value, [0]);

		VariableData varData = GlobalStatic.VariableData;
		varData.CharacterList.Clear();
		varData.CharacterList.AddRange(charactersBefore);
	}

	/// <summary>
	/// 声明 #DIMS CHARADATA SORTKEY2D, 2, 2:与 ErhLoader.analyzeSharpDimLines 同一条注册链
	/// (UserDefinedVariableData.Create → VariableData.CreateUserDefCharaVariable → IdentifierDictionary)。
	/// </summary>
	static VariableToken RegisterSortKey()
	{
		var wc = LexicalAnalyzer.Analyse(new CharStream("CHARADATA SORTKEY2D, 2, 2"), LexEndWith.EoL,
			LexAnalyzeFlag.AllowAssignment);
		var dimline = new DimLineWC(wc, true, false, new ScriptPosition("test.erh", 1));
		UserDefinedVariableData data = UserDefinedVariableData.Create(dimline);
		VariableToken token = GlobalStatic.VariableData.CreateUserDefCharaVariable(data, dimline);
		GlobalStatic.IdentifierDictionary.AddUseDefinedVariable(token);
		return token;
	}

	/// <summary>
	/// 未赋值的键按空串参与比较,期望次序由「空串 &lt; "a" &lt; "b"」独立给出,不是复述实现。
	/// 旧行为:首位角色的键为 null,排序的首次比较即以它为接收者,抛 NullReferenceException。
	/// </summary>
	[Fact]
	public void SortChara_WithUnsetString2DKey_SortsItAsEmptyString()
	{
		sortKey!.SetValue("b", [1, 0, 0]);
		sortKey.SetValue("a", [2, 0, 0]);

		GlobalStatic.VEvaluator.SortChara(sortKey, 0, SortOrder.ASCENDING, false);

		List<CharacterData> sorted = GlobalStatic.VariableData.CharacterList;
		Assert.Same(unset, sorted[0]);
		Assert.Same(keyA, sorted[1]);
		Assert.Same(keyB, sorted[2]);
	}
}
