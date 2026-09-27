using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Utils;
using System.Diagnostics.CodeAnalysis;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements;

/// <summary>
/// The single-line entry point shared by EmueraConsole.DebugCommand and PluginManager.ExecuteLine: it
/// turns a parsed line into an instruction that can run on its own and refuses what such an entry point
/// cannot run. Both callers used to spell these rules out separately, or not at all - which let
/// ExecuteLine hand flow control, argument-less and unparsable lines straight to a null reference.
/// </summary>
internal static class MemoryLineEntry
{
	/// <summary>
	/// Builds the argument of <paramref name="line"/> and accepts it only when one line can run by itself.
	/// Refused with <paramref name="errMes"/>: a line that is not an instruction, flow control, an
	/// instruction that waits for input, is not METHOD_SAFE or is part of a multi-line construct, and an
	/// argument that fails to parse. Debug instructions are refused outside -Debug, where SetArgumentTo
	/// leaves their argument unbuilt.
	/// </summary>
	public static bool TryBuild(LogicalLine? line, [NotNullWhen(true)] out InstructionLine? func, [NotNullWhen(false)] out string? errMes)
	{
		func = null;
		errMes = "";
		if (line == null)
		{
			errMes = trerror.CanNotInterpretedLine.Text;
			return false;
		}

		if (line is InvalidLine)
		{
			errMes = line.ErrMes;
			return false;
		}

		if (line is not InstructionLine instruction)
		{
			errMes = trerror.InvalidDebugCommand.Text;
			return false;
		}

		if (instruction.Function.IsFlowContorol())
		{
			errMes = trerror.CanNotUseFlowInstruction.Text;
			return false;
		}

		if (instruction.Function.IsWaitInput() || !instruction.Function.IsMethodSafe() || instruction.Function.IsPartial())
		{
			errMes = string.Format(trerror.CanNotUseInstruction.Text, instruction.Function.Name);
			return false;
		}

		// Outside -Debug a script skips debug instructions too (runScriptProc continues past them), and
		// SetArgumentTo leaves their argument unbuilt there, so running one reads a null BuiltArgument.
		if (!Program.DebugMode && instruction.Function.IsDebug())
		{
			errMes = string.Format(trerror.CanNotUseInstruction.Text, instruction.Function.Name);
			return false;
		}

		switch (instruction.FunctionCode)
		{
			// These need game state that a single line has no way to reach
			case FunctionCode.PUTFORM:
			case FunctionCode.UPCHECK:
			case FunctionCode.CUPCHECK:
			case FunctionCode.SAVEDATA:
				errMes = string.Format(trerror.CanNotUseInstruction.Text, instruction.Function.Name);
				return false;
		}

		// Building the argument writes back into the line, so this yields the instruction to run as well
		if (!ArgumentParser.SetArgumentTo(instruction) || instruction.IsError)
		{
			errMes = instruction.IsError ? instruction.ErrMes : trerror.ArgParsingError.Text;
			return false;
		}

		func = instruction;
		return true;
	}

	/// <summary>
	/// <see cref="TryBuild"/> for callers that have nothing to do with the refusal reason: the same lines
	/// are refused, as the <see cref="CodeEE"/> those callers used to raise themselves.
	/// </summary>
	public static InstructionLine Build(LogicalLine? line)
	{
		if (!TryBuild(line, out InstructionLine? func, out string? errMes))
		{
			throw new CodeEE(errMes);
		}

		return func;
	}
}
