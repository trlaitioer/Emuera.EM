using System;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Runtime.Utils;

internal abstract class EmueraException : ApplicationException
{
	protected EmueraException(string errormes, ScriptPosition? position)
		: base(errormes)
	{
		Position = position;
	}

	protected EmueraException(string errormes)
		: base(errormes)
	{
		Position = null;
	}

	public ScriptPosition? Position;
}

/// <summary>
/// emuera本体に起因すると思われるエラー
/// </summary>
internal sealed class ExeEE : EmueraException
{
	public ExeEE(string errormes)
		: base(errormes)
	{ }

	public ExeEE(string errormes, ScriptPosition? position)
		: base(errormes, position)
	{ }
}

/// <summary>
/// スクリプト側に起因すると思われるエラー
/// </summary>
internal class CodeEE : EmueraException
{
	public CodeEE(string errormes, ScriptPosition? position)
		: base(errormes, position)
	{ }

	public CodeEE(string errormes)
		: base(errormes)
	{ }
}

/// <summary>
/// スクリプト側に起因すると思われるエラーのうち、未定義の識別子に関連するもの
/// </summary>
internal sealed class IdentifierNotFoundCodeEE : CodeEE
{
	public IdentifierNotFoundCodeEE(string errormes, ScriptPosition? position)
		: base(errormes, position)
	{ }

	public IdentifierNotFoundCodeEE(string errormes)
		: base(errormes)
	{ }
}

/// <summary>
/// 未実装エラー
/// </summary>
internal sealed class NotImplCodeEE : CodeEE
{
	public NotImplCodeEE(ScriptPosition? position)
		: base(trerror.CanNotUseFuncCurrentVer.Text, position)
	{ }

	public NotImplCodeEE()
		: base(trerror.CanNotUseFuncCurrentVer.Text)
	{ }
}

/// <summary>
/// Save, Load中のエラー
/// </summary>
internal sealed class FileEE : EmueraException
{
	public FileEE(string errormes)
		: base(errormes)
	{ }
}

/// <summary>
/// エラー箇所を表示するための位置データ。整形前のデータなのでエラー表示以外の理由で参照するべきではない。
/// </summary>
/// <remarks>
/// <see cref="Filename"/> is a position name rather than a path: a normal position is relative to csv\ or erb\,
/// while an analyzed file carries the path it was given on the command line. <c>Program.TryResolveScriptFile</c>
/// turns the former into the file it names; the latter has to be kept as it is.
/// </remarks>
internal readonly record struct ScriptPosition
{
	public ScriptPosition()
	{
		LineNo = -1;
		Filename = "";
	}

	public ScriptPosition(string srcFile, int srcLineNo)
	{
		LineNo = srcLineNo + 1;
		Filename = srcFile ?? "";
	}

	public readonly int LineNo;
	public readonly string Filename;
}
