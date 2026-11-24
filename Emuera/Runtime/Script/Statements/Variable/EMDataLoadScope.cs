using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

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
	/// Publishes the staged values and empties the old target maps and tables in place, including instances
	/// the file replaces. A constraint or a plugin callback can reject a table clear during commit.
	/// </summary>
	public void Commit()
	{
		EMDataState result = previous.Copy();
		result.RemoveXmls(keySets);
		result.MergeFrom(staged);
		ClearPreviousTables();
		previous.ClearMaps(keySets);
		owner.EMData = result;
		committed = true;
	}

	private void ClearPreviousTables()
	{
		List<TableBackup> backups = [];
		HashSet<DataTable> seen = new(ReferenceEqualityComparer.Instance);
		// Clear omitted tables first, preserving the order within each group of keys.
		foreach (string key in keySets.DataTables.OrderBy(staged.Tables.ContainsKey))
		{
			if (previous.Tables.TryGetValue(key, out DataTable? table) && seen.Add(table))
			{
				backups.Add(new TableBackup(table));
			}
		}

		try
		{
			foreach (TableBackup backup in backups)
			{
				backup.Table.Clear();
			}
		}
		catch (Exception clearError)
		{
			RestorePreviousTables(backups, clearError);
			throw;
		}
	}

	private static void RestorePreviousTables(List<TableBackup> backups, Exception clearError)
	{
		List<Exception> errors = [clearError];
		List<DataSet> suspended = [];
		try
		{
			// A callback can change later tables through a foreign-key cascade. Restore the whole
			// group before checking constraints, without ending any plugin-owned bulk load.
			foreach (TableBackup backup in backups)
			{
				if (backup.Table.DataSet is { EnforceConstraints: true } set)
				{
					suspended.Add(set);
					set.EnforceConstraints = false;
				}
			}
			for (int index = backups.Count - 1; index >= 0; index--)
			{
				try
				{
					backups[index].RestoreIfChanged();
				}
				catch (Exception restoreError)
				{
					errors.Add(restoreError);
				}
			}
		}
		finally
		{
			foreach (DataSet set in suspended)
			{
				try
				{
					set.EnforceConstraints = true;
				}
				catch (Exception constraintError)
				{
					errors.Add(constraintError);
				}
			}
		}
		if (errors.Count > 1)
		{
			throw new AggregateException(errors);
		}
	}

	private sealed class TableBackup
	{
		public DataTable Table { get; }
		private readonly DataTable rows = new();
		private readonly Dictionary<int, object?[]> pendingEdits = [];

		public TableBackup(DataTable table)
		{
			Table = table;
			// Copy values without cloning expressions or constraints that depend on other tables in a plugin's DataSet.
			foreach (DataColumn column in table.Columns)
			{
				DataColumn copy = rows.Columns.Add(column.ColumnName, column.DataType);
				if (column.DataType == typeof(DateTime))
				{
					copy.DateTimeMode = column.DateTimeMode;
				}
			}
			foreach (DataRow row in table.Rows)
			{
				if (row.HasVersion(DataRowVersion.Proposed))
				{
					pendingEdits.Add(rows.Rows.Count, row.ItemArray);
				}
				rows.ImportRow(row);
			}
		}

		private bool MatchesBackup()
		{
			if (Table.Rows.Count != rows.Rows.Count)
			{
				return false;
			}
			for (int index = 0; index < rows.Rows.Count; index++)
			{
				DataRow live = Table.Rows[index];
				DataRow saved = rows.Rows[index];
				bool wasEditing = pendingEdits.TryGetValue(index, out object?[]? proposed);
				if (live.RowState != saved.RowState || live.RowError != saved.RowError || live.HasVersion(DataRowVersion.Proposed) != wasEditing)
				{
					return false;
				}
				foreach (DataColumn column in rows.Columns)
				{
					int i = column.Ordinal;
					if (live.GetColumnError(i) != saved.GetColumnError(i)
						|| (saved.HasVersion(DataRowVersion.Original) && !ValuesEqual(live[i, DataRowVersion.Original], saved[i, DataRowVersion.Original]))
						|| (saved.HasVersion(DataRowVersion.Current) && !ValuesEqual(live[i, DataRowVersion.Current], saved[i, DataRowVersion.Current]))
						|| (proposed != null && !ValuesEqual(live[i, DataRowVersion.Proposed], proposed[i])))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool ValuesEqual(object? left, object? right)
		{
			return System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals(left, right);
		}

		public void RestoreIfChanged()
		{
			if (MatchesBackup())
			{
				return;
			}
			if (Table is EMDataTable table)
			{
				table.RestoreRows(RestoreRows);
			}
			else
			{
				RestoreRows();
			}
		}

		private void RestoreRows()
		{
			DataSet? set = Table.DataSet;
			List<(ForeignKeyConstraint Key, Rule Delete, AcceptRejectRule Accept)> cascades = [];
			try
			{
				if (set != null && Table.Rows.Count > 0)
				{
					// Removing surviving parent rows during replay must not delete or accept changes in child tables.
					foreach (DataTable child in set.Tables)
					{
						foreach (Constraint constraint in child.Constraints)
						{
							if (constraint is ForeignKeyConstraint key && key.RelatedTable == Table)
							{
								cascades.Add((key, key.DeleteRule, key.AcceptRejectRule));
								key.DeleteRule = Rule.None;
								key.AcceptRejectRule = AcceptRejectRule.None;
							}
						}
					}
				}
				// Calling Clear again would re-enter the handler that rejected it.
				while (Table.Rows.Count > 0)
				{
					Table.Rows.RemoveAt(Table.Rows.Count - 1);
				}
				foreach (DataRow row in rows.Rows)
				{
					Table.ImportRow(row);
				}
			}
			finally
			{
				foreach ((ForeignKeyConstraint key, Rule delete, AcceptRejectRule accept) in cascades)
				{
					key.DeleteRule = delete;
					key.AcceptRejectRule = accept;
				}
			}
			foreach ((int index, object?[] values) in pendingEdits)
			{
				DataRow row = Table.Rows[index];
				row.BeginEdit();
				foreach (DataColumn column in Table.Columns)
				{
					object? value = values[column.Ordinal];
					bool readOnly = column.ReadOnly;
					if (readOnly && (column.Expression.Length != 0 || ValuesEqual(row[column], value)))
					{
						continue;
					}
					if (readOnly)
					{
						column.ReadOnly = false;
					}
					try
					{
						row[column] = value!;
					}
					finally
					{
						if (readOnly)
						{
							column.ReadOnly = true;
						}
					}
				}
			}
		}
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
