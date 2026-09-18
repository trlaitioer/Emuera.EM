using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Utils;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Script.Statements;

internal abstract class AInstruction
{
	protected int flag;
	public int Flag => flag;

	// Left null on purpose by the instructions that build their argument in the parser
	// (VARI/VARS) or take no argument at all (CUSTOMDRAWLINE).
	public ArgumentBuilder? ArgBuilder { get; protected init; }

	public virtual void SetJumpTo(ref bool useCallForm, InstructionLine func, int currentDepth, ref string FunctionoNotFoundName)
	{ }

	public virtual void DoInstruction(ExpressionMediator exm, InstructionLine func, ProcessState state)
	{
		throw new ExeEE(trerror.NotImplementedOrCallError.Text);
	}

	public virtual Argument? CreateArgument(InstructionLine line, ExpressionMediator exm)
	{
		throw new ExeEE(trerror.NotImplemented.Text);
	}
}
