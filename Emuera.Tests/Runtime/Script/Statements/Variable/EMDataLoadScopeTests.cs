using MinorShift.Emuera;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using System.Data;
using System.Xml;
using Xunit;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace Emuera.Tests.Runtime.Script.Statements.Variable;

/// <summary>
/// LOADDATA/LOADGLOBAL 的 MAP/XML/DT 保全(nullable-migration-36):读取先写暂存,成功才与当前状态合成;
/// 失败(二进制档截断、正文中途失败、文本档损坏)不碰当前数据。旧实现是读取前就 Remove,
/// 故各条读档失败用例在旧代码下变红。
/// </summary>
public class EMDataLoadScopeTests : IDisposable
{
	private readonly List<string> saveKeys = [];
	private readonly List<string> globalKeys = [];
	private readonly List<string> staticKeys = [];
	private readonly List<string> tempFiles = [];

	public EMDataLoadScopeTests()
	{
		TestBootstrap.Initialize();
	}

	public void Dispose()
	{
		foreach (string key in saveKeys)
		{
			GlobalStatic.ConstantData.SaveMaps.Remove(key);
			GlobalStatic.ConstantData.SaveXmls.Remove(key);
			GlobalStatic.ConstantData.SaveDTs.Remove(key);
		}

		foreach (string key in globalKeys)
		{
			GlobalStatic.ConstantData.GlobalSaveMaps.Remove(key);
			GlobalStatic.ConstantData.GlobalSaveXmls.Remove(key);
			GlobalStatic.ConstantData.GlobalSaveDTs.Remove(key);
		}

		foreach (string key in staticKeys)
		{
			GlobalStatic.ConstantData.StaticMaps.Remove(key);
			GlobalStatic.ConstantData.StaticXmls.Remove(key);
			GlobalStatic.ConstantData.StaticDTs.Remove(key);
		}

		foreach (string path in tempFiles)
		{
			try
			{
				File.Delete(path);
			}
			catch (IOException)
			{
				// 写入器已释放;删不掉只留下临时目录里的一个文件,不影响断言。
			}
		}
	}

	/// <summary>Commit 前当前字典被换成暂存:旧键不可见;Commit 后未提供键按旧 Remove 语义清空、
	/// 文件键取新值、非目标键未提供时保留/文件提供时照旧覆盖。</summary>
	[Fact]
	public void Commit_AppliesTheDeferredResetAndTheLoadedData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "OLDMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveXmls, "OLDXML");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "OLDDT");
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "FILEMAP");
		// OTHERMAP / OTHERFILEMAP 不在任何目标键集合里:旧 Remove 不碰它们,新 Commit 也应保持
		data.DataStringMaps["OLDMAP"] = new Dictionary<string, string> { ["k"] = "old" };
		data.DataStringMaps["FILEMAP"] = new Dictionary<string, string> { ["k"] = "stale" };
		data.DataStringMaps["OTHERMAP"] = new Dictionary<string, string> { ["k"] = "other" };
		data.DataStringMaps["OTHERFILEMAP"] = new Dictionary<string, string> { ["k"] = "stale" };
		data.DataXmlDocument["OLDXML"] = new XmlDocument();
		data.DataDataTables["OLDDT"] = new DataTable();

		using (EMDataLoadScope scope = data.BeginEMSaveDataLoad())
		{
			Assert.False(data.DataStringMaps.ContainsKey("OLDMAP"));
			Assert.False(data.DataXmlDocument.ContainsKey("OLDXML"));
			Assert.False(data.DataDataTables.ContainsKey("OLDDT"));

			// 读取写入暂存区:文件提供 FILEMAP 的新值,非目标键由文件提供时也照旧覆盖
			data.DataStringMaps["FILEMAP"] = new Dictionary<string, string> { ["k"] = "from-file" };
			data.DataStringMaps["OTHERFILEMAP"] = new Dictionary<string, string> { ["k"] = "from-file" };
			scope.Commit();
		}

		Assert.True(data.DataStringMaps.TryGetValue("OLDMAP", out Dictionary<string, string>? oldMap));
		Assert.NotNull(oldMap);
		Assert.Empty(oldMap);
		Assert.Equal("from-file", data.DataStringMaps["FILEMAP"]["k"]);
		Assert.Equal("other", data.DataStringMaps["OTHERMAP"]["k"]);
		Assert.Equal("from-file", data.DataStringMaps["OTHERFILEMAP"]["k"]);
		Assert.False(data.DataXmlDocument.ContainsKey("OLDXML"));
		Assert.True(data.DataDataTables.TryGetValue("OLDDT", out DataTable? oldTable));
		Assert.NotNull(oldTable);
		Assert.Empty(oldTable.Rows);
	}

	/// <summary>Commit 在原实例上清空文件未提供的键:外部持有者(插件经 GetDataTable 拿到的表)看到的是同一实例
	/// 且为空,而不是换一张新表、让它继续留着旧行;schema 与主键随实例保留。</summary>
	[Fact]
	public void Commit_EmptiesTheInstancesTheFileDidNotProvide()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "OLDMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "OLDDT");
		Dictionary<string, string> oldMap = new() { ["k"] = "old" };
		DataTable oldTable = new();
		oldTable.Columns.Add("C", typeof(long));
		oldTable.PrimaryKey = [oldTable.Columns["C"]!];
		oldTable.Rows.Add(1L);
		data.DataStringMaps["OLDMAP"] = oldMap;
		data.DataDataTables["OLDDT"] = oldTable;

		using (EMDataLoadScope scope = data.BeginEMSaveDataLoad())
		{
			scope.Commit();
		}

		Assert.Same(oldMap, data.DataStringMaps["OLDMAP"]);
		Assert.Empty(oldMap);
		Assert.Same(oldTable, data.DataDataTables["OLDDT"]);
		Assert.Empty(oldTable.Rows);
		Assert.Single(oldTable.Columns);
		Assert.Single(oldTable.PrimaryKey);
	}

	/// <summary>不 Commit 直接 Dispose:读取写入的键与替换都丢弃,字典引用和内容回到读取前。</summary>
	[Fact]
	public void Dispose_WithoutCommit_LeavesTheCurrentDataUntouched()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = SaveGuard(data, "KEEPMAP", "KEEPXML", "KEEPDT");

		using (data.BeginEMSaveDataLoad())
		{
			data.DataStringMaps["KEEPMAP"] = new Dictionary<string, string> { ["k"] = "from-file" };
			data.DataStringMaps["NEWMAP"] = new Dictionary<string, string>();
		}

		guard.AssertPreserved();
		Assert.False(data.DataStringMaps.ContainsKey("NEWMAP"));
	}

	/// <summary>全局用暂存只清理 GlobalSave* 组:普通读档的 Save* 键不受影响。</summary>
	[Fact]
	public void BeginEMGlobalDataLoad_CommitsOnlyTheGlobalKeys()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "SAVEMAP");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "GLOBALMAP");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "GLOBALFROMFILE");
		data.DataStringMaps["SAVEMAP"] = new Dictionary<string, string> { ["k"] = "current" };
		data.DataStringMaps["GLOBALMAP"] = new Dictionary<string, string> { ["k"] = "old" };

		using (EMDataLoadScope scope = data.BeginEMGlobalDataLoad())
		{
			data.DataStringMaps["GLOBALFROMFILE"] = new Dictionary<string, string> { ["k"] = "from-file" };
			scope.Commit();
		}

		Assert.Equal("current", data.DataStringMaps["SAVEMAP"]["k"]);
		Assert.True(data.DataStringMaps.TryGetValue("GLOBALMAP", out Dictionary<string, string>? globalMap));
		Assert.NotNull(globalMap);
		Assert.Empty(globalMap);
		Assert.Equal("from-file", data.DataStringMaps["GLOBALFROMFILE"]["k"]);
	}

	/// <summary>头认得出、正文读不出:旧实现已在读取前清掉当前 Save* 数据,新实现连实例带内容都原样保留。</summary>
	[Fact]
	public void LoadFromFile_WhenTheBinarySaveIsTruncated_KeepsTheCurrentSaveData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = SaveGuard(data, "KEEPMAP", "KEEPXML", "KEEPDT");

		string path = NewSavePath();
		File.WriteAllBytes(path, SaveFileFixtures.MinimalBinarySave(EraSaveFileType.Normal));

		Assert.Throws<FileEE>(() => isolated.Evaluator.LoadFromFile(path));

		guard.AssertPreserved();
	}

	/// <summary>LOADDATA 侧的文件类型不符:在复位与读正文之前就抛,当前数据不动(旧实现读取前已清)。</summary>
	[Fact]
	public void LoadFromFile_WhenTheFileTypeIsNotNormal_KeepsTheCurrentSaveData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = SaveGuard(data, "KEEPMAP", "KEEPXML", "KEEPDT");

		string path = NewSavePath();
		File.WriteAllBytes(path, SaveFileFixtures.MinimalBinarySave(EraSaveFileType.Global));

		FileEE error = Assert.Throws<FileEE>(() => isolated.Evaluator.LoadFromFile(path));

		Assert.Equal(trerror.CorruptedSaveData.Text, error.Message);
		guard.AssertPreserved();
	}

	/// <summary>LOADGLOBAL 的删除原本排在打开文件之前,坏档的破坏面比 LOADDATA 更大;这条钉住它。</summary>
	[Fact]
	public void LoadGlobalFile_WhenTheBinarySaveIsTruncated_KeepsTheCurrentGlobalData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = GlobalGuard(data, "KEEPGLOBALMAP", "KEEPGLOBALXML", "KEEPGLOBALDT");

		string path = NewSavePath();
		File.WriteAllBytes(path, SaveFileFixtures.MinimalBinarySave(EraSaveFileType.Global));

		bool loaded = isolated.Evaluator.LoadGlobalFile(path, out string? reason);

		Assert.False(loaded);
		Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, reason);
		guard.AssertPreserved();
	}

	/// <summary>文件类型不符走 LoadGlobalFile 的提前 return false:暂存从未写入,Dispose 后当前全局数据原样。
	/// 旧实现无论读不读得成都先在打开文件前清掉全局 MAP/XML/DT,这条在旧代码下变红。</summary>
	[Fact]
	public void LoadGlobalFile_WhenTheFileTypeIsNotGlobal_KeepsTheCurrentGlobalData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = GlobalGuard(data, "KEEPGLOBALMAP", "KEEPGLOBALXML", "KEEPGLOBALDT");

		string path = NewSavePath();
		File.WriteAllBytes(path, SaveFileFixtures.MinimalBinarySave(EraSaveFileType.Normal));

		bool loaded = isolated.Evaluator.LoadGlobalFile(path, out string? reason);

		Assert.False(loaded);
		Assert.Equal(trerror.CorruptedSaveData.Text, reason);
		guard.AssertPreserved();
	}

	/// <summary>失败发生在正文中途:文件已写进暂存的 MAP 必须随暂存一起丢弃(旧实现会把它留在当前状态里)。</summary>
	[Fact]
	public void LoadGlobalFile_WhenTheSaveFailsAfterReadingEmData_DiscardsWhatItRead()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "KEEPGLOBALMAP");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveXmls, "KEEPGLOBALXML");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveDTs, "KEEPGLOBALDT");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "FROMFILE");
		EMDataGuard guard = new(data, "KEEPGLOBALMAP", "KEEPGLOBALXML", "KEEPGLOBALDT");

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Global);
			writer.WriteInt64(0); // unique code:0 表示不校验
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString(""); // saveMes
			writer.WriteWithKey("FROMFILE", new Dictionary<string, string> { ["k"] = "from-file" });
			// 不写 EOF:读完该块后即遇文件末尾
		}

		bool loaded = isolated.Evaluator.LoadGlobalFile(path, out string? reason);

		Assert.False(loaded);
		Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, reason);
		guard.AssertPreserved();
		Assert.False(data.DataStringMaps.ContainsKey("FROMFILE"));
	}

	/// <summary>成功读档的终态不变:文件未提供的键仍被清空(旧 Remove 语义),文件提供的取文件值。
	/// 这条在旧实现下也是绿的,用来防「忘记 Commit」把清理整个丢掉。</summary>
	[Fact]
	public void LoadFromFile_OnSuccess_ClearsTheSaveDataTheFileDidNotProvide()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "OLDMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveXmls, "OLDXML");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "OLDDT");
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "FILEMAP");
		data.DataStringMaps["OLDMAP"] = new Dictionary<string, string> { ["k"] = "old" };
		data.DataStringMaps["FILEMAP"] = new Dictionary<string, string> { ["k"] = "stale" };
		data.DataXmlDocument["OLDXML"] = new XmlDocument();
		data.DataDataTables["OLDDT"] = new DataTable();

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Normal);
			writer.WriteInt64(0); // unique code:0 表示不校验
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString(""); // saveMes
			writer.WriteInt64(0); // 角色数
			writer.WriteEOF(); // 普通变量段结束
			writer.WriteWithKey("FILEMAP", new Dictionary<string, string> { ["k"] = "from-file" });
			writer.WriteEOF();
		}

		Assert.True(isolated.Evaluator.LoadFromFile(path));

		Assert.True(data.DataStringMaps.TryGetValue("OLDMAP", out Dictionary<string, string>? oldMap));
		Assert.NotNull(oldMap);
		Assert.Empty(oldMap);
		Assert.Equal("from-file", data.DataStringMaps["FILEMAP"]["k"]);
		Assert.False(data.DataXmlDocument.ContainsKey("OLDXML"));
		Assert.True(data.DataDataTables.TryGetValue("OLDDT", out DataTable? oldTable));
		Assert.NotNull(oldTable);
		Assert.Empty(oldTable.Rows);
	}

	/// <summary>文件提供了某个目标键时,该键的旧实例同样被清空:旧「先删后读」会先清,插件持有的旧表不该留着旧行。</summary>
	[Fact]
	public void LoadFromFile_OnSuccess_EmptiesTheInstanceTheFileReplaced()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "FILEMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "FILEDT");
		Dictionary<string, string> oldMap = new() { ["k"] = "stale" };
		DataTable oldTable = new();
		oldTable.Columns.Add("C", typeof(long));
		oldTable.Rows.Add(1L);
		data.DataStringMaps["FILEMAP"] = oldMap;
		data.DataDataTables["FILEDT"] = oldTable;

		DataTable fileTable = new("FILEDT");
		fileTable.Columns.Add("C", typeof(long));
		DataRow fileRow = fileTable.NewRow();
		fileRow["C"] = 42L;
		fileTable.Rows.Add(fileRow);

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Normal);
			writer.WriteInt64(0); // unique code:0 表示不校验
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString(""); // saveMes
			writer.WriteInt64(0); // 角色数
			writer.WriteEOF(); // 普通变量段结束
			writer.WriteWithKey("FILEMAP", new Dictionary<string, string> { ["k"] = "from-file" });
			writer.WriteWithKey("FILEDT", fileTable);
			writer.WriteEOF();
		}

		Assert.True(isolated.Evaluator.LoadFromFile(path));

		Assert.Empty(oldMap);
		Assert.Empty(oldTable.Rows);
		Assert.Single(oldTable.Columns);
		Assert.Equal("from-file", data.DataStringMaps["FILEMAP"]["k"]);
		Assert.Equal(42L, (long)data.DataDataTables["FILEDT"].Rows[0]["C"]);
	}

	/// <summary>LOADGLOBAL 成功读档的终态:GlobalSave* 目标键按旧语义清空/移除,文件提供的取文件值,
	/// 非目标的 Save* 键保持原值。</summary>
	[Fact]
	public void LoadGlobalFile_OnSuccess_ClearsTheGlobalDataTheFileDidNotProvide()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "GLOBALOLDMAP");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveXmls, "GLOBALOLDXML");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveDTs, "GLOBALOLDDT");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "GLOBALFILEMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "SAVEMAP");
		data.DataStringMaps["GLOBALOLDMAP"] = new Dictionary<string, string> { ["k"] = "old" };
		data.DataStringMaps["GLOBALFILEMAP"] = new Dictionary<string, string> { ["k"] = "stale" };
		data.DataStringMaps["SAVEMAP"] = new Dictionary<string, string> { ["k"] = "current" };
		data.DataXmlDocument["GLOBALOLDXML"] = new XmlDocument();
		data.DataDataTables["GLOBALOLDDT"] = new DataTable();

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Global);
			writer.WriteInt64(0); // unique code:0 表示不校验
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString(""); // saveMes
			writer.WriteEOF(); // 全局变量段结束
			writer.WriteWithKey("GLOBALFILEMAP", new Dictionary<string, string> { ["k"] = "from-file" });
			writer.WriteEOF();
		}

		Assert.True(isolated.Evaluator.LoadGlobalFile(path, out string? reason));
		Assert.Null(reason);

		Assert.True(data.DataStringMaps.TryGetValue("GLOBALOLDMAP", out Dictionary<string, string>? oldMap));
		Assert.NotNull(oldMap);
		Assert.Empty(oldMap);
		Assert.Equal("from-file", data.DataStringMaps["GLOBALFILEMAP"]["k"]);
		Assert.Equal("current", data.DataStringMaps["SAVEMAP"]["k"]);
		Assert.False(data.DataXmlDocument.ContainsKey("GLOBALOLDXML"));
		Assert.True(data.DataDataTables.TryGetValue("GLOBALOLDDT", out DataTable? oldTable));
		Assert.NotNull(oldTable);
		Assert.Empty(oldTable.Rows);
	}

	/// <summary>文本档读损坏同样不碰当前数据(原议题第 3 条):旧实现在读取前就 Remove,这条在旧代码下变红。
	/// 夹具只有 unique code 与版本两行,读取 PUTFORM 时即遇 EOF。</summary>
	[Fact]
	public void LoadFromFile_WhenTheTextSaveIsTruncated_KeepsTheCurrentSaveData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = SaveGuard(data, "KEEPMAP", "KEEPXML", "KEEPDT");

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraDataWriter writer = new(fs))
		{
			writer.Write(0L); // unique code:0 表示不校验
			writer.Write(1000L); // 游戏版本
			// 不写 PUTFORM:读取存档文本时即遇 EOF
		}

		Assert.Throws<FileEE>(() => isolated.Evaluator.LoadFromFile(path));

		guard.AssertPreserved();
	}

	/// <summary>LOADGLOBAL 的文本档读损坏同样不碰当前数据:旧实现在打开文件前就 Remove,这条在旧代码下变红。
	/// 夹具只有 unique code 与版本两行,读 GLOBAL 数组时即遇 EOF。</summary>
	[Fact]
	public void LoadGlobalFile_WhenTheTextSaveIsTruncated_KeepsTheCurrentGlobalData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = GlobalGuard(data, "KEEPGLOBALMAP", "KEEPGLOBALXML", "KEEPGLOBALDT");

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraDataWriter writer = new(fs))
		{
			writer.Write(0L); // unique code:0 表示不校验
			writer.Write(1000L); // 游戏版本
			// 不写 GLOBAL 数组:ReadInt64Array 即遇 EOF
		}

		bool loaded = isolated.Evaluator.LoadGlobalFile(path, out string? reason);

		Assert.False(loaded);
		Assert.Equal(trerror.UnexpectedSaveDataEnd.Text, reason);
		guard.AssertPreserved();
	}

	/// <summary>文本档成功读档:文本格式不保存 MAP/XML/DT,目标键仍按旧语义全部清空,非目标键保留。
	/// 用 SaveToFile 生成真实的文本档(测试未加载配置,SystemSaveInBinary 保持默认 false)。</summary>
	[Fact]
	public void LoadFromFile_WhenTheTextSaveLoads_ClearsTheSaveData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "OLDMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveXmls, "OLDXML");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "OLDDT");
		data.DataStringMaps["OLDMAP"] = new Dictionary<string, string> { ["k"] = "old" };
		data.DataStringMaps["OTHERMAP"] = new Dictionary<string, string> { ["k"] = "other" };
		data.DataXmlDocument["OLDXML"] = new XmlDocument();
		data.DataDataTables["OLDDT"] = new DataTable();

		string path = NewSavePath();
		Assert.True(isolated.Evaluator.SaveToFile(path, "TEXT", out string? saveReason));
		Assert.Null(saveReason);
		Assert.True(isolated.Evaluator.LoadFromFile(path));

		Assert.True(data.DataStringMaps.TryGetValue("OLDMAP", out Dictionary<string, string>? oldMap));
		Assert.NotNull(oldMap);
		Assert.Empty(oldMap);
		Assert.Equal("other", data.DataStringMaps["OTHERMAP"]["k"]);
		Assert.False(data.DataXmlDocument.ContainsKey("OLDXML"));
		Assert.True(data.DataDataTables.TryGetValue("OLDDT", out DataTable? oldTable));
		Assert.NotNull(oldTable);
		Assert.Empty(oldTable.Rows);
	}

	/// <summary>唯一码不符在复位与读正文之前就报出:当前数据不动(旧实现在这之前已经清过)。</summary>
	[Fact]
	public void LoadFromFile_WhenTheUniqueCodeDiffers_KeepsTheCurrentSaveData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = SaveGuard(data, "KEEPMAP", "KEEPXML", "KEEPDT");

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Normal);
			writer.WriteInt64(1234); // 任意非 0 唯一码:与当前游戏不符
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString("");
		}

		FileEE error = Assert.Throws<FileEE>(() => isolated.Evaluator.LoadFromFile(path));

		Assert.Equal(trerror.DifferentGame.Text, error.Message);
		guard.AssertPreserved();
	}

	/// <summary>LOADGLOBAL 的唯一码分支与 LOADDATA 同一判据:错码时暂存未写入、当前全局数据原样。</summary>
	[Fact]
	public void LoadGlobalFile_WhenTheUniqueCodeDiffers_KeepsTheCurrentGlobalData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = GlobalGuard(data, "KEEPGLOBALMAP", "KEEPGLOBALXML", "KEEPGLOBALDT");

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Global);
			writer.WriteInt64(1234); // 任意非 0 唯一码:与当前游戏不符
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString("");
		}

		bool loaded = isolated.Evaluator.LoadGlobalFile(path, out string? reason);

		Assert.False(loaded);
		Assert.Equal(trerror.DifferentGame.Text, reason);
		guard.AssertPreserved();
	}

	/// <summary>版本不符走 LoadGlobalFile 的提前 return false:暂存尚未写入,当前全局数据原样。</summary>
	[Fact]
	public void LoadGlobalFile_WhenTheVersionDiffers_KeepsTheCurrentGlobalData()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = GlobalGuard(data, "KEEPGLOBALMAP", "KEEPGLOBALXML", "KEEPGLOBALDT");
		GameBase gamebase = GlobalStatic.GameBaseData;
		bool defined = gamebase.ScriptVersionDefined;
		long version = gamebase.ScriptVersion;
		long minVersion = gamebase.ScriptCompatibleMinVersion;
		// 夹具里的 GameBase 没有版本定义,CheckVersion 对任何版本都放行;临时给出一组真版本再看 1000 是否被拒
		gamebase.ScriptVersionDefined = true;
		gamebase.ScriptVersion = 1808;
		gamebase.ScriptCompatibleMinVersion = 1808;
		try
		{
			string path = NewSavePath();
			using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
			using (EraBinaryDataWriter writer = new(fs))
			{
				writer.WriteHeader();
				writer.WriteFileType(EraSaveFileType.Global);
				writer.WriteInt64(0); // unique code:0 表示不校验
				writer.WriteInt64(1000); // 游戏版本:低于当前定义版本
				writer.WriteString("");
			}

			bool loaded = isolated.Evaluator.LoadGlobalFile(path, out string? reason);

			Assert.False(loaded);
			Assert.Equal(trerror.DifferentVersion.Text, reason);
			guard.AssertPreserved();
		}
		finally
		{
			gamebase.ScriptVersionDefined = defined;
			gamebase.ScriptVersion = version;
			gamebase.ScriptCompatibleMinVersion = minVersion;
		}
	}

	/// <summary>LOADDATA 侧:失败发生在正文中途时,文件已写进暂存的 MAP 必须随暂存一起丢弃(旧实现会把它留在
	/// 当前状态里,而那时当前数据已被先删过)。</summary>
	[Fact]
	public void LoadFromFile_WhenTheSaveFailsAfterReadingEmData_DiscardsWhatItRead()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		EMDataGuard guard = SaveGuard(data, "KEEPMAP", "KEEPXML", "KEEPDT");
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "FROMFILE");

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Normal);
			writer.WriteInt64(0); // unique code:0 表示不校验
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString(""); // saveMes
			writer.WriteInt64(0); // 角色数
			writer.WriteEOF(); // 普通变量段结束
			writer.WriteWithKey("FROMFILE", new Dictionary<string, string> { ["k"] = "from-file" });
			// 不写 EOF:读完该块后即遇文件末尾
		}

		Assert.Throws<FileEE>(() => isolated.Evaluator.LoadFromFile(path));

		guard.AssertPreserved();
		Assert.False(data.DataStringMaps.ContainsKey("FROMFILE"));
	}

	/// <summary>成功路径合并文件提供的 XML 与 DataTable;未提供的声明 DT 保留 schema 与主键(原地清空,不换表)。</summary>
	[Fact]
	public void LoadFromFile_OnSuccess_MergesTheXmlAndDataTableTheFileProvides()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveXmls, "FILEXML");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "FILEDT");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "OLDDT");
		DataTable oldTable = new();
		oldTable.Columns.Add("C", typeof(long));
		oldTable.PrimaryKey = [oldTable.Columns["C"]!];
		oldTable.Rows.Add(1L);
		data.DataDataTables["OLDDT"] = oldTable;

		XmlDocument doc = new();
		doc.LoadXml("<root><v>from-file</v></root>");
		DataTable fileTable = new("FILEDT");
		fileTable.Columns.Add("C", typeof(long));
		DataRow row = fileTable.NewRow();
		row["C"] = 42L;
		fileTable.Rows.Add(row);

		string path = NewSavePath();
		using (FileStream fs = new(path, FileMode.Create, FileAccess.Write))
		using (EraBinaryDataWriter writer = new(fs))
		{
			writer.WriteHeader();
			writer.WriteFileType(EraSaveFileType.Normal);
			writer.WriteInt64(0); // unique code:0 表示不校验
			writer.WriteInt64(1000); // 游戏版本
			writer.WriteString(""); // saveMes
			writer.WriteInt64(0); // 角色数
			writer.WriteEOF(); // 普通变量段结束
			writer.WriteWithKey("FILEXML", doc);
			writer.WriteWithKey("FILEDT", fileTable);
			writer.WriteEOF();
		}

		Assert.True(isolated.Evaluator.LoadFromFile(path));

		Assert.True(data.DataXmlDocument.TryGetValue("FILEXML", out XmlDocument? loadedXml));
		Assert.NotNull(loadedXml);
		Assert.Equal("from-file", loadedXml.DocumentElement?["v"]?.InnerText);
		Assert.True(data.DataDataTables.TryGetValue("FILEDT", out DataTable? loadedTable));
		Assert.NotNull(loadedTable);
		Assert.Single(loadedTable.Rows);
		Assert.Equal(42L, loadedTable.Rows[0]["C"]);
		Assert.Same(oldTable, data.DataDataTables["OLDDT"]);
		Assert.Empty(oldTable.Rows);
		Assert.Single(oldTable.PrimaryKey);
	}

	/// <summary>路径不存在不再以裸 FileNotFoundException 逃出:与 LoadFrom 同一句「ファイルがありません」。</summary>
	[Fact]
	public void LoadFromFile_WhenTheFileIsMissing_ThrowsTheNotExistPath()
	{
		using IsolatedEvaluator isolated = new();
		string path = Path.Combine(GameRootScope.NewTempDirectory("em-load-scope-missing"), "save00.sav");

		ExeEE error = Assert.Throws<ExeEE>(() => isolated.Evaluator.LoadFromFile(path));

		Assert.Equal(trerror.NotExistPath.Text, error.Message);
	}

	/// <summary>RESETDATA 的即时清理只动普通档组:全局档的条目保持原值。</summary>
	[Fact]
	public void RemoveEMSaveData_ResetsOnlyTheSlotGroup()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "SLOTMAP");
		AddSaveKey(GlobalStatic.ConstantData.SaveXmls, "SLOTXML");
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, "SLOTDT");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "GLOBALMAP");
		data.DataStringMaps["SLOTMAP"] = new Dictionary<string, string> { ["k"] = "slot" };
		data.DataXmlDocument["SLOTXML"] = new XmlDocument();
		data.DataDataTables["SLOTDT"] = new DataTable();
		data.DataStringMaps["GLOBALMAP"] = new Dictionary<string, string> { ["k"] = "global" };

		data.RemoveEMSaveData();

		Assert.Empty(data.DataStringMaps["SLOTMAP"]);
		Assert.False(data.DataXmlDocument.ContainsKey("SLOTXML"));
		Assert.Empty(data.DataDataTables["SLOTDT"].Rows);
		Assert.Equal("global", data.DataStringMaps["GLOBALMAP"]["k"]);
	}

	/// <summary>RESETGLOBAL 连静态组一起清:普通档组不受影响。这两次调用就是 ResetGlobalData 的两行。</summary>
	[Fact]
	public void RemoveEMGlobalDataAndStaticData_ResetBothGlobalGroups()
	{
		using IsolatedEvaluator isolated = new();
		VariableData data = isolated.Data;
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, "GLOBALMAP");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveXmls, "GLOBALXML");
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveDTs, "GLOBALDT");
		AddStaticKey(GlobalStatic.ConstantData.StaticMaps, "STATICMAP");
		AddStaticKey(GlobalStatic.ConstantData.StaticXmls, "STATICXML");
		AddStaticKey(GlobalStatic.ConstantData.StaticDTs, "STATICDT");
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, "SLOTMAP");
		data.DataStringMaps["GLOBALMAP"] = new Dictionary<string, string> { ["k"] = "global" };
		data.DataXmlDocument["GLOBALXML"] = new XmlDocument();
		data.DataDataTables["GLOBALDT"] = new DataTable();
		data.DataStringMaps["STATICMAP"] = new Dictionary<string, string> { ["k"] = "static" };
		data.DataXmlDocument["STATICXML"] = new XmlDocument();
		data.DataDataTables["STATICDT"] = new DataTable();
		data.DataStringMaps["SLOTMAP"] = new Dictionary<string, string> { ["k"] = "slot" };

		data.RemoveEMGlobalData();
		data.RemoveEMStaticData();

		Assert.Empty(data.DataStringMaps["GLOBALMAP"]);
		Assert.False(data.DataXmlDocument.ContainsKey("GLOBALXML"));
		Assert.Empty(data.DataDataTables["GLOBALDT"].Rows);
		Assert.Empty(data.DataStringMaps["STATICMAP"]);
		Assert.False(data.DataXmlDocument.ContainsKey("STATICXML"));
		Assert.Empty(data.DataDataTables["STATICDT"].Rows);
		Assert.Equal("slot", data.DataStringMaps["SLOTMAP"]["k"]);
	}

	private void AddSaveKey(HashSet<string> keySet, string key)
	{
		keySet.Add(key);
		saveKeys.Add(key);
	}

	private void AddGlobalKey(HashSet<string> keySet, string key)
	{
		keySet.Add(key);
		globalKeys.Add(key);
	}

	private void AddStaticKey(HashSet<string> keySet, string key)
	{
		keySet.Add(key);
		staticKeys.Add(key);
	}

	/// <summary>失败保全用例的共享夹具:登记三把普通档键、放入哨兵值并返回守卫。</summary>
	private EMDataGuard SaveGuard(VariableData data, string mapKey, string xmlKey, string dtKey)
	{
		AddSaveKey(GlobalStatic.ConstantData.SaveMaps, mapKey);
		AddSaveKey(GlobalStatic.ConstantData.SaveXmls, xmlKey);
		AddSaveKey(GlobalStatic.ConstantData.SaveDTs, dtKey);
		return new EMDataGuard(data, mapKey, xmlKey, dtKey);
	}

	/// <summary>同上,针对 global.sav 组。</summary>
	private EMDataGuard GlobalGuard(VariableData data, string mapKey, string xmlKey, string dtKey)
	{
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveMaps, mapKey);
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveXmls, xmlKey);
		AddGlobalKey(GlobalStatic.ConstantData.GlobalSaveDTs, dtKey);
		return new EMDataGuard(data, mapKey, xmlKey, dtKey);
	}

	private string NewSavePath()
	{
		string directory = GameRootScope.NewTempDirectory("em-load-scope");
		string path = Path.Combine(directory, "save00.sav");
		tempFiles.Add(path);
		return path;
	}

	/// <summary>失败保全用例的共用夹具:记下三只字典与三类实体的引用并放入哨兵值,失败后一次核对
	/// 「实例与内容都不变」。</summary>
	private sealed class EMDataGuard
	{
		private readonly VariableData data;
		private readonly Dictionary<string, Dictionary<string, string>> maps;
		private readonly Dictionary<string, XmlDocument> xmls;
		private readonly Dictionary<string, DataTable> tables;
		private readonly string mapKey;
		private readonly string xmlKey;
		private readonly string dtKey;
		private readonly Dictionary<string, string> map;
		private readonly XmlDocument xml;
		private readonly DataTable table;

		public EMDataGuard(VariableData data, string mapKey, string xmlKey, string dtKey)
		{
			this.data = data;
			this.mapKey = mapKey;
			this.xmlKey = xmlKey;
			this.dtKey = dtKey;
			maps = data.DataStringMaps;
			xmls = data.DataXmlDocument;
			tables = data.DataDataTables;
			map = new Dictionary<string, string> { ["k"] = "current" };
			xml = new XmlDocument();
			table = new DataTable();
			table.Columns.Add("C", typeof(long));
			table.Rows.Add(1L);
			data.DataStringMaps[mapKey] = map;
			data.DataXmlDocument[xmlKey] = xml;
			data.DataDataTables[dtKey] = table;
		}

		public void AssertPreserved()
		{
			Assert.Same(maps, data.DataStringMaps);
			Assert.Same(xmls, data.DataXmlDocument);
			Assert.Same(tables, data.DataDataTables);
			Assert.Same(map, data.DataStringMaps[mapKey]);
			Assert.Equal("current", data.DataStringMaps[mapKey]["k"]);
			Assert.Same(xml, data.DataXmlDocument[xmlKey]);
			Assert.Same(table, data.DataDataTables[dtKey]);
			// 表里留一行:旧实现失败时会在读取前 Clear,只比实例认不出这种破坏
			Assert.Single(table.Columns);
			Assert.Single(table.Rows);
		}
	}

	/// <summary>共享单例在整个程序集存活:用例在独立实例上装载,退出时恢复 GlobalStatic 并释放副本。
	/// 夹具存档只含 MAP/EOF 块、不含变量块,故不会触及绑定在单例 VariableData 上的标识符令牌。</summary>
	private sealed class IsolatedEvaluator : IDisposable
	{
		private readonly VariableEvaluator previousEvaluator;
		private readonly VariableData previousData;

		public VariableEvaluator Evaluator { get; }
		public VariableData Data => Evaluator.VariableData;

		public IsolatedEvaluator()
		{
			previousEvaluator = GlobalStatic.VEvaluator;
			previousData = GlobalStatic.VariableData;
			Evaluator = new VariableEvaluator(GlobalStatic.GameBaseData, GlobalStatic.ConstantData);
			GlobalStatic.VEvaluator = Evaluator;
		}

		public void Dispose()
		{
			GlobalStatic.VEvaluator = previousEvaluator;
			GlobalStatic.VariableData = previousData;
			Evaluator.Dispose();
		}
	}
}
