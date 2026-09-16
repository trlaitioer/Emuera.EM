#nullable enable
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameProc.Function;

//1756 LogicalLineParserから分離。処理をArgumentBuilderに分割
internal static partial class ArgumentParser
{
	public static bool SetArgumentTo(InstructionLine line)
	{
		if (line == null)
			return false;
		if (line.Argument != null)
			return true;
		if (line.IsError)
			return false;
		if (!Program.DebugMode && line.Function.IsDebug())
		{//非DebugモードでのDebug系命令。何もしないので引数解析も不要
			line.Argument = null;
			return true;
		}

		Argument? arg;
		string errmes;
		try
		{
			if (line.Function.ArgBuilder != null)
				arg = line.Function.ArgBuilder.CreateArgument(line, GlobalStatic.EMediator);
			else
				// A null ArgBuilder means the identifier was built from an AInstruction, which carries the Instruction.
				arg = line.Function.Instruction!.CreateArgument(line, GlobalStatic.EMediator);
		}
		catch (EmueraException e)
		{
			errmes = e.Message;
			return error(line, errmes);
		}
		if (arg == null)
		{
			if (!line.IsError)
			{
				errmes = trerror.ArgParsingError.Text;
				return error(line, errmes);
			}
			return false;
		}
		line.Argument = arg;
		return true;

		static bool error(InstructionLine line, string errmes)
		{
			//ParserMediator.Warn sets line.IsError/ErrMes under its own lock, so they must not be
			//written here as well: one discipline for those two fields. The sound is played by
			//NotifyParseError immediately for runtime callers and deferred during the parallel parse.
			ParserMediator.Warn(errmes, line, 2, true, false);
			ParserMediator.NotifyParseError();
			return false;
		}
	}
}
