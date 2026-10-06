// Graphics argument readers shared by the builtins in Graphics.cs, which call them unqualified through a
// `using static`. Not registered itself: this class carries no [BuiltinMethod] factory.

using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.UI.Game.Image;
using System.Drawing;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.GameData.Function;

internal static class BuiltinArgReaders
{
	/// <summary>
	/// argNo番目の引数をGraphicsImageのIDを示す整数値として読み取り、 GraphicsImage又はnullを返す。
	/// </summary>
	internal static GraphicsImage ReadGraphics(string Name, ExpressionMediator exm, FunctionMethod.ArgumentSlots slots, int argNo)
	{
		long target = slots.Int(exm, argNo);
		if (target < 0) //funcname + "関数:GraphicsIDに負の値(" + target.ToString() + ")が指定されました"
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGraphicsID0, Name, target));
		{
			throw new CodeEE(string.Format(trerror.GIdIsNegative.Text, Name, target));
		}
		else if (target > int.MaxValue) //funcname + "関数:GraphicsIDの値(" + target.ToString() + ")が大きすぎます"
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGraphicsID1, Name, target));
		{
			throw new CodeEE(string.Format(trerror.GIdIsTooLarge.Text, Name, target));
		}

		return AppContents.GetGraphics((int)target);
	}

	/// <summary>
	/// 引数で指定したIDのGraphicsImageを読み取り、 GraphicsImage又はnullを返す。
	/// </summary>
	internal static GraphicsImage ReadGraphics(int target)
	{
		if (target < 0) //funcname + "関数:GraphicsIDに負の値(" + target.ToString() + ")が指定されました"
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGraphicsID0, Name, target));
		{
			throw new CodeEE(string.Format(trerror.GIdIsNegative.Text, "HTML_PRINT", target));
		}
		else if (target > int.MaxValue) //funcname + "関数:GraphicsIDの値(" + target.ToString() + ")が大きすぎます"
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGraphicsID1, Name, target));
		{
			throw new CodeEE(string.Format(trerror.GIdIsTooLarge.Text, "HTML_PRINT", target));
		}

		return AppContents.GetGraphics(target);
	}

	/// <summary>
	/// argNo番目の引数を整数値として読み取り、 アルファ値を含むColor構造体にして返す。
	/// </summary>
	internal static Color ReadColor(string Name, ExpressionMediator exm, FunctionMethod.ArgumentSlots slots, int argNo)
	{
		long c64 = slots.Int(exm, argNo);
		if (c64 is < 0 or > 0xFFFFFFFF)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodColorARGB0, Name, c64));
		{
			throw new CodeEE(string.Format(trerror.InvalidColorARGB.Text, Name, c64));
		}

		return Color.FromArgb((int)(c64 >> 24) & 0xFF, (int)(c64 >> 16) & 0xFF, (int)(c64 >> 8) & 0xFF, (int)c64 & 0xFF);
	}

	/// <summary>
	/// argNo番目を含む2つの引数を整数値として読み取り、Point形式にして返す。
	/// </summary>
	internal static Point ReadPoint(string Name, ExpressionMediator exm, FunctionMethod.ArgumentSlots slots, int argNo)
	{
		long x64 = slots.Int(exm, argNo);
		if (x64 is < int.MinValue or > int.MaxValue)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name,x64, argNo+1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, argNo + 1, x64, int.MinValue, int.MaxValue));
		}

		long y64 = slots.Int(exm, argNo + 1);
		if (y64 is < int.MinValue or > int.MaxValue)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name,y64, argNo+1+1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, argNo + 2, y64, int.MinValue, int.MaxValue));
		}

		return new Point((int)x64, (int)y64);
	}

	/// <summary>
	/// argNo番目を含む4つの引数を整数値として読み取り、Rectangle形式にして返す。
	/// </summary>
	internal static Rectangle ReadRectangle(string Name, ExpressionMediator exm, FunctionMethod.ArgumentSlots slots, int argNo)
	{
		long x64 = slots.Int(exm, argNo);
		if (x64 is < int.MinValue or > int.MaxValue)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, x64, argNo + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, argNo + 1, x64, int.MinValue, int.MaxValue));
		}

		long y64 = slots.Int(exm, argNo + 1);
		if (y64 is < int.MinValue or > int.MaxValue)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, y64, argNo + 1 + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRange.Text, Name, argNo + 2, y64, int.MinValue, int.MaxValue));
		}

		long w64 = slots.Int(exm, argNo + 2);
		if (w64 is < int.MinValue or > int.MaxValue or 0)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, w64, argNo + 2 + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRangeExcept.Text, Name, argNo + 3, w64, int.MinValue, int.MaxValue, 0));
		}

		long h64 = slots.Int(exm, argNo + 3);
		if (h64 is < int.MinValue or > int.MaxValue or 0)
			// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodDefaultArgumentOutOfRange0, Name, h64, argNo + 3 + 1));
		{
			throw new CodeEE(string.Format(trerror.ArgIsOutOfRangeExcept.Text, Name, argNo + 4, h64, int.MinValue, int.MaxValue, 0));
		}

		return new Rectangle((int)x64, (int)y64, (int)w64, (int)h64);
	}

	/// <summary>
	/// argNo番目の引数を5x5のカラーマトリクス配列変数として読み取り、 5x5のfloat[][]形式にして返す。
	/// </summary>
	internal static float[][] ReadColormatrix(string Name, ExpressionMediator exm, FunctionMethod.ArgumentSlots slots, int argNo)
	{
		//数値型二次元以上配列変数のはず
		// The rule of the caller's slot declares it RefInt2D or RefInt3D, and the IsArray2D/IsArray3D guards
		// below select the matching array, so GetArray() is the array type each cast names.
		FixedVariableTerm p = slots.Var(argNo).GetFixedVariableTerm(exm);
		long e1, e2;
		float[][] cm = new float[5][];
		if (p.Identifier.IsArray2D)
		{
			long[,] array;
			if (p.Identifier.IsCharacterData)
			{
				array = (p.Identifier.GetArrayChara((int)p.Index1) as long[,])!;
				e1 = p.Index2;
				e2 = p.Index3;
			}
			else
			{
				array = (p.Identifier.GetArray() as long[,])!;
				e1 = p.Index1;
				e2 = p.Index2;
			}

			if (e1 < 0 || e2 < 0 || e1 + 5 > array.GetLength(0) || e2 + 5 > array.GetLength(1))
				// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGColorMatrix0, Name, e1, e2));
			{
				throw new CodeEE(string.Format(trerror.InvalidColorMatrix.Text, Name, e1, e2));
			}

			for (int x = 0; x < 5; x++)
			{
				cm[x] = new float[5];
				for (int y = 0; y < 5; y++)
				{
					cm[x][y] = array[e1 + x, e2 + y] / 256f;
				}
			}
		}

		if (p.Identifier.IsArray3D)
		{
			long[,,] array;
			long e3;
			if (p.Identifier.IsCharacterData)
			{
				throw new NotImplCodeEE();
			}
			else
			{
				array = (p.Identifier.GetArray() as long[,,])!;
				e1 = p.Index1;
				e2 = p.Index2;
				e3 = p.Index3;
			}

			if (e1 < 0 || e1 >= array.GetLength(0))
				// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGColorMatrix0, Name, e2, e3));
			{
				throw new CodeEE(string.Format(trerror.InvalidColorMatrix.Text, Name, e2, e3));
			}

			if (e2 < 0 || e3 < 0 || e2 + 5 > array.GetLength(1) || e3 + 5 > array.GetLength(2))
				// throw new CodeEE(string.Format(Properties.Resources.RuntimeErrMesMethodGColorMatrix0, Name, e2, e3));
			{
				throw new CodeEE(string.Format(trerror.InvalidColorMatrix.Text, Name, e2, e3));
			}

			for (int x = 0; x < 5; x++)
			{
				cm[x] = new float[5];
				for (int y = 0; y < 5; y++)
				{
					cm[x][y] = array[e1, e2 + x, e3 + y] / 256f;
				}
			}
		}

		return cm;
	}
}
