using System;
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

	/// <summary>Immediately resets the declared entries for RESETDATA or RESETGLOBAL.</summary>
	public void ClearEMData(EMDataKeySets targets)
	{
		ClearMaps(targets);
		RemoveXmls(targets);
		foreach (string key in targets.DataTables)
		{
			if (Tables.TryGetValue(key, out DataTable? table))
			{
				table.Clear();
			}
		}
	}

	public void ClearMaps(EMDataKeySets targets)
	{
		foreach (string key in targets.Maps)
		{
			if (Maps.TryGetValue(key, out Dictionary<string, string>? map))
			{
				map.Clear();
			}
		}
	}

	public void RemoveXmls(EMDataKeySets targets)
	{
		foreach (string key in targets.Xmls)
		{
			Xmls.Remove(key);
		}
	}
}

/// <summary>A save table whose rollback writes do not run plugin row or column handlers.</summary>
internal sealed class EMDataTable : DataTable
{
	private bool restoring;

	public EMDataTable()
	{
	}

	public EMDataTable(string name) : base(name)
	{
	}

	public void RestoreRows(Action restore)
	{
		bool wasRestoring = restoring;
		restoring = true;
		try
		{
			restore();
		}
		finally
		{
			restoring = wasRestoring;
		}
	}

	protected override void OnRowChanging(DataRowChangeEventArgs e)
	{
		if (!restoring)
		{
			base.OnRowChanging(e);
		}
	}

	protected override void OnRowChanged(DataRowChangeEventArgs e)
	{
		if (!restoring)
		{
			base.OnRowChanged(e);
		}
	}

	protected override void OnRowDeleting(DataRowChangeEventArgs e)
	{
		if (!restoring)
		{
			base.OnRowDeleting(e);
		}
	}

	protected override void OnRowDeleted(DataRowChangeEventArgs e)
	{
		if (!restoring)
		{
			base.OnRowDeleted(e);
		}
	}

	protected override void OnColumnChanging(DataColumnChangeEventArgs e)
	{
		if (!restoring)
		{
			base.OnColumnChanging(e);
		}
	}

	protected override void OnColumnChanged(DataColumnChangeEventArgs e)
	{
		if (!restoring)
		{
			base.OnColumnChanged(e);
		}
	}
}
