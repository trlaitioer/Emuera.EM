#nullable enable
using MinorShift.Emuera.UI.Game;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace MinorShift.Emuera.Runtime.Utils.EvilMask;

internal sealed class ConsoleEscapedParts
{
	private const int ROW_LINE = 0, ROW_DEPTH = 1, ROW_TOP = 2, ROW_BOTTOM = 3, ROW_ID = 4, ROW_DIVTYPE = 5;
	private readonly static DataTable dt = new(); // divの情報を保存する場所
	private readonly static Dictionary<long, AConsoleDisplayNode> parts = []; //　実際にdivデータを保存する場所

	/// <summary>
	/// 最近GetPartsInRangeを行ったか
	/// </summary>
	private static bool getOnce;

	/// <summary>
	/// 前回GetPartsInRangeを行った時の状態
	/// </summary>
	private static int lastTop, lastBottom, lastGeneration;

	/// <summary>
	/// 前回のGetPartsInRangeから変動があるか
	/// </summary>
	public static bool Changed { get; private set; }

	/// <summary>
	/// 前回GetPartsInRangeを行った時と状況が変わったらfalseを返す。
	/// 重複計算防止用。
	/// </summary>
	public static bool TestedInRange(int top, int bottom, int gen)
	{
		return getOnce && top == lastTop && bottom == lastBottom && gen == lastGeneration;
	}

	static ConsoleEscapedParts()
	{
		// データベースを構築
		dt.Columns.Add("line", typeof(int)); // div所属のline
		dt.Columns.Add("depth", typeof(int)); // divの奥行き
		dt.Columns.Add("top", typeof(int)); // divの一行目がこの行より下回る
		dt.Columns.Add("bottom", typeof(int)); // divの一行目がこの行より上回る
		dt.Columns.Add("id", typeof(long)); // ユニークID（保存時点のTimeStamp）
		dt.Columns.Add("div", typeof(sbyte)); //　divの種類（display:absoluteかdisplay:relativeか）
	}

	public static void Clear()
	{
		getOnce = false;
		dt.Clear();
		parts.Clear();
		Changed = false;
	}

	/// <summary>
	/// Converting to line counts depends on the line height, so top/bottom are stored in pixels and converted on lookup.
	/// topOffset/bottomOffset are pixel offsets from the start of the owning line.
	/// </summary>
	public static void Add(AConsoleDisplayNode part, int line, int depth, int topOffset, int bottomOffset)
	{
		long id = Utils.TimePoint();
		DataRow row = dt.NewRow();
		row[ROW_LINE] = line;
		row[ROW_DEPTH] = depth;
		row[ROW_TOP] = topOffset;
		row[ROW_BOTTOM] = bottomOffset;
		row[ROW_ID] = id;
		if (part is ConsoleDivPart div)
		{
			row[ROW_DIVTYPE] = (!div.IsRelative ? 2 : 0) | 1; // bit1 : divであるなら1　bit2 : divがdisplay:absoluteなら1
		}
		else
		{
			row[ROW_DIVTYPE] = 0;
		}

		dt.Rows.Add(row);
		parts.Add(id, part);
		Changed = true;
	}

	/// <summary>
	/// Forces the stored ranges to be recomputed after the line height changed.
	/// </summary>
	public static void MarkChanged()
	{
		Changed = true;
	}

	/// <summary>
	/// 所在行のlineNoがlineの行とその以降の行のdivデータを全部削除
	/// </summary>
	public static void Remove(int line)
	{
		foreach (DataRow row in dt.AsEnumerable().Where(r => (int)r[ROW_LINE] >= line).ToArray())
		{
			parts.Remove((long)row[ROW_ID]);
			dt.Rows.Remove(row);
			Changed = true;
		}
	}

	/// <summary>
	/// 所在行のlineNoがlineの行のdivデータを削除
	/// </summary>
	public static void RemoveAt(int line)
	{
		foreach (DataRow row in dt.AsEnumerable().Where(r => (int)r[ROW_LINE] == line).ToArray())
		{
			parts.Remove((long)row[ROW_ID]);
			dt.Rows.Remove(row);
			Changed = true;
		}
	}

	/// <summary>
	/// top行からbottom行までの範囲内表示内容があったdivをrmapで保存。
	/// genは今のbuttonGeneration，重複計算防止用。
	/// </summary>
	public static void GetPartsInRange(int top, int bottom, int gen, Dictionary<int, List<AConsoleDisplayNode>> rmap)
	{
		if (GlobalStatic.Console?.GetLineNo > Config.Config.MaxLog)
		{
			int correction = GlobalStatic.Console.GetLineNo - Config.Config.MaxLog;
			top += correction;
			bottom += correction;
		}

		if (rmap == null)
		{
			return;
		}

		rmap.Clear();
		int lineHeight = Math.Max(1, Config.Config.LineHeight);
		// すべてのdisplay:absoluteのdiv
		// とtop行からbottom行までの範囲内表示内容があったdiv
		// と所在行がtop行からbottom行までの範囲内のdiv
		// に対し
		foreach (DataRow row in dt.Rows)
		{
			int line = (int)row[ROW_LINE];
			int rowTop = line + (int)Math.Ceiling((float)(int)row[ROW_TOP] / lineHeight);
			int rowBottom = line + (int)Math.Floor((float)Math.Max(0, (int)row[ROW_BOTTOM] - 1) / lineHeight);
			sbyte divType = (sbyte)row[ROW_DIVTYPE];
			if ((divType & 2) == 0
			    && !(rowTop <= bottom + 1 && rowBottom >= top
			                              && (divType != 0 || top > line || line > bottom + 1)))
			{
				continue;
			}

			int depth = (int)row[ROW_DEPTH];
			if (!rmap.TryGetValue(depth, out List<AConsoleDisplayNode>? list))
			{
				list = [];
				rmap.Add(depth, list);
			}

			list.Add(parts[(long)row[ROW_ID]]);
		}

		getOnce = true;
		lastTop = top;
		lastBottom = bottom;
		lastGeneration = gen;
		Changed = false;
	}
}