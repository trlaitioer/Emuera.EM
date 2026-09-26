using MinorShift.Emuera.Runtime.Utils;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements;

/// <summary>
/// The position text an error report or a debug trace shows for one line: "file:line@label", the
/// "current function" line, one call stack entry, and the jump trace of a call.
/// A memory line (built by EmueraConsole.DebugCommand or PluginManager.ExecuteLine) has no parent label
/// and carries ScriptPosition's default value, so every form is assembled from the parts that are usable
/// and returns null when none is left; callers skip that output instead of asserting non-null.
/// </summary>
internal static class LineLocationText
{
	/// <summary>"current function": the label name and where that function is defined.</summary>
	public static string? CurrentFunction(LogicalLine? current)
	{
		Location location = Location.Of(current);
		if (location.LabelName is not { } labelName || location.LabelPosition is not { } labelPosition)
		{
			return null;
		}

		return string.Format(trerror.ErrorInFunc.Text, labelName, labelPosition.Filename, labelPosition.LineNo.ToString());
	}

	/// <summary>One call stack entry: the position of the line and the function it belongs to.</summary>
	public static string? CallStackEntry(LogicalLine? line)
	{
		Location location = Location.Of(line);
		if (location.LabelName is not { } labelName || location.Position is not { } position)
		{
			return null;
		}

		return string.Format(trerror.ErrorFuncStack.Text, position.Filename, position.LineNo.ToString(), labelName);
	}

	/// <summary>"file:line@label", dropping the parts that are not usable; null when none is.</summary>
	public static string? StackFrame(LogicalLine? line)
		=> Location.Of(line).ToString() is { Length: > 0 } text ? text : null;

	/// <summary>The debug trace's " at @label:labelFile:line"; null when the line has no function.</summary>
	public static string? JumpTrace(LogicalLine? line)
	{
		Location location = Location.Of(line);
		if (location.LabelName is not { } labelName)
		{
			return null;
		}

		List<string> parts = ["@" + labelName];
		if (location.LabelPosition is { Filename.Length: > 0 } labelPosition)
		{
			parts.Add(labelPosition.Filename);
			if (location.Position is { } position)
			{
				parts.Add(position.LineNo.ToString());
			}
		}

		return " at " + string.Join(":", parts);
	}

	/// <summary>
	/// True when a position can be shown: it exists and is not ScriptPosition's default value
	/// (LineNo = -1, Filename = ""), which is what a memory line carries.
	/// </summary>
	public static bool IsUsablePosition([NotNullWhen(true)] ScriptPosition? position)
		=> position is { } value && (value.Filename.Length > 0 || value.LineNo >= 0);

	/// <summary>The usable position and parent label of one line.</summary>
	private readonly record struct Location(ScriptPosition? Position, FunctionLabelLine? ParentLabel)
	{
		public static Location Of(LogicalLine? line)
			=> new(UsablePosition(line), line?.ParentLabelLine);

		public string? LabelName => ParentLabel?.LabelName;

		public ScriptPosition? LabelPosition => UsablePosition(ParentLabel);

		public override string ToString()
		{
			string text = Position is { } position ? $"{position.Filename}:{position.LineNo}" : "";
			if (LabelName is { } labelName)
			{
				text += "@" + labelName;
			}

			return text;
		}

		private static ScriptPosition? UsablePosition(LogicalLine? line)
		{
			ScriptPosition? position = line?.Position;
			return LineLocationText.IsUsablePosition(position) ? position : null;
		}
	}
}
