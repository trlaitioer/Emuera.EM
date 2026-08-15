using MinorShift.Emuera.Runtime.Utils;
using System.Drawing;
using System.IO;

namespace MinorShift.Emuera.UI.Game.Image;

static class ImgUtils
{
	public static Bitmap LoadImage(string filepath)
	{
		if (!File.Exists(filepath))
		{
			return null;
		}
		Bitmap bmp;

		if (Path.GetExtension(filepath).Equals(".WEBP", System.StringComparison.InvariantCultureIgnoreCase))
		{
			bmp = WebP.Load(filepath);

			if (bmp == null)
			{
				return null;
			}
		}
		else
		{
			bmp = new Bitmap(filepath);
			if (bmp == null)
			{
				return null;
			}
		}

		return bmp;
	}
}
