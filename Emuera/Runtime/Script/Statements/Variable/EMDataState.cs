using System.Collections.Generic;
using System.Data;
using System.Xml;

namespace MinorShift.Emuera.GameData.Variable;

/// <summary>
/// The MAP/XML/DT half of the variable state: three dictionaries that always travel together. A
/// LOADDATA/LOADGLOBAL read works on a separate instance (see <see cref="EMDataLoadScope"/>), so the current
/// one is only touched once the read has succeeded (nullable-migration-36).
/// </summary>
internal sealed class EMDataState
{
	public Dictionary<string, Dictionary<string, string>> Maps { get; set; } = [];
	public Dictionary<string, XmlDocument> Xmls { get; set; } = [];
	public Dictionary<string, DataTable> Tables { get; set; } = [];

	/// <summary>
	/// A shallow copy: the outer dictionaries are new, the values are the same instances. A load builds its
	/// result on the copy, so a failure while building leaves the current state as it is.
	/// </summary>
	public EMDataState Copy()
	{
		return new EMDataState
		{
			Maps = new(Maps),
			Xmls = new(Xmls),
			Tables = new(Tables),
		};
	}

	/// <summary>Overwrites the entries a read staged, family by family.</summary>
	public void MergeFrom(EMDataState loaded)
	{
		foreach (KeyValuePair<string, Dictionary<string, string>> pair in loaded.Maps)
		{
			Maps[pair.Key] = pair.Value;
		}

		foreach (KeyValuePair<string, XmlDocument> pair in loaded.Xmls)
		{
			Xmls[pair.Key] = pair.Value;
		}

		foreach (KeyValuePair<string, DataTable> pair in loaded.Tables)
		{
			Tables[pair.Key] = pair.Value;
		}
	}

	/// <summary>
	/// The one reset that both RESETDATA/RESETGLOBAL and a successful LOADDATA/LOADGLOBAL apply: maps and
	/// DataTables are emptied in place (whoever holds the instance — a plugin that kept the table from
	/// <see cref="MinorShift.Emuera.Runtime.Utils.PluginSystem.PluginManager.GetDataTable"/>, say — sees it
	/// emptied), XML entries are removed. Keys <paramref name="provided"/> carries came from the file and are
	/// left alone; without it every target key is reset.
	/// </summary>
	public void ClearEMData(EMDataKeySets targets, EMDataState? provided = null)
	{
		foreach (string key in targets.Maps)
		{
			if (provided?.Maps.ContainsKey(key) != true && Maps.TryGetValue(key, out Dictionary<string, string>? map))
			{
				map.Clear();
			}
		}

		foreach (string key in targets.Xmls)
		{
			if (provided?.Xmls.ContainsKey(key) != true)
			{
				Xmls.Remove(key);
			}
		}

		foreach (string key in targets.DataTables)
		{
			if (provided?.Tables.ContainsKey(key) != true && Tables.TryGetValue(key, out DataTable? table))
			{
				table.Clear();
			}
		}
	}
}
