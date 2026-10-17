using System;
using System.Collections.Generic;
using System.Data;
using System.Xml;

namespace MinorShift.Emuera.GameData.Variable;

/// <summary>The MAP/XML/DT key sets of one save group (normal, global or static); they always travel together.</summary>
internal readonly struct EMDataKeySets
{
	public EMDataKeySets(HashSet<string> maps, HashSet<string> xmls, HashSet<string> dataTables)
	{
		Maps = maps;
		Xmls = xmls;
		DataTables = dataTables;
	}

	public HashSet<string> Maps { get; }
	public HashSet<string> Xmls { get; }
	public HashSet<string> DataTables { get; }

	/// <summary>The keys a normal save slot carries (RESETDATA and LOADDATA).</summary>
	public static EMDataKeySets Save()
	{
		return new EMDataKeySets(
			GlobalStatic.ConstantData.SaveMaps,
			GlobalStatic.ConstantData.SaveXmls,
			GlobalStatic.ConstantData.SaveDTs);
	}

	/// <summary>The keys global.sav carries (RESETGLOBAL and LOADGLOBAL).</summary>
	public static EMDataKeySets GlobalSave()
	{
		return new EMDataKeySets(
			GlobalStatic.ConstantData.GlobalSaveMaps,
			GlobalStatic.ConstantData.GlobalSaveXmls,
			GlobalStatic.ConstantData.GlobalSaveDTs);
	}

	/// <summary>The keys that belong to the game rather than to a slot; RESETGLOBAL clears them too.</summary>
	public static EMDataKeySets Static()
	{
		return new EMDataKeySets(
			GlobalStatic.ConstantData.StaticMaps,
			GlobalStatic.ConstantData.StaticXmls,
			GlobalStatic.ConstantData.StaticDTs);
	}
}

/// <summary>
/// The MAP/XML/DT half of a LOADDATA/LOADGLOBAL read. Constructing it swaps a staging <see cref="EMDataState"/>
/// into the variable data, so the reader writes whole new values into a state of its own while the current one
/// stays untouched. <see cref="Commit"/> builds the result off to the side, empties the previous target
/// instances and publishes it; disposing without a Commit drops the staged data and hands the current state
/// back. Loads do not nest, so a second Begin would overwrite the swap (nullable-migration-36).
/// </summary>
internal sealed class EMDataLoadScope : IDisposable
{
	private readonly VariableData owner;
	private readonly EMDataKeySets keySets;
	private readonly EMDataState previous;
	private readonly EMDataState staged = new();
	private bool committed;

	internal EMDataLoadScope(VariableData owner, EMDataKeySets keySets)
	{
		this.owner = owner;
		this.keySets = keySets;
		// The current state is detached, never modified: the reader writes whole new values into the staging
		// state, so handing the previous one back is all a rollback needs.
		previous = owner.EMData;
		owner.EMData = staged;
	}

	/// <summary>
	/// Publishes the read: the staged values are merged into the result and the previous targets are emptied —
	/// in place, so a holder of such a map or DataTable sees it emptied, just as the old clear-before-read
	/// left it. That holds for the keys the file replaced as well: they are published as the file's values, but
	/// the old instance is emptied rather than left with stale rows. The result is built on a copy first;
	/// copying and merging are the only steps that can throw, and they run before anything current is touched.
	/// </summary>
	public void Commit()
	{
		EMDataState result = previous.Copy();
		result.MergeFrom(staged);
		result.ClearEMData(keySets, staged);
		// The old instances are emptied for every target key, not only the ones the file left alone: the
		// clear-before-read emptied all of them, and a plugin may still hold the instance the file replaced.
		previous.ClearEMData(keySets);
		owner.EMData = result;
		committed = true;
	}

	public void Dispose()
	{
		if (committed)
		{
			return;
		}

		owner.EMData = previous;
	}
}
