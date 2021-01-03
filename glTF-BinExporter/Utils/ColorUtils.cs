using System.Drawing;
using Rhino.Display;

namespace glTF_BinExporter.glTF
{
    public static class ColorUtils
    {
        /// <summary>
        /// Convert from sRGB to Linear.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Color4f ConvertSRGBToLinear(Color color)
        {
            return ConvertSRGBToLinear(new Color4f(color));
        }

        public static Color4f ConvertSRGBToLinear(Color4f color)
        {
            return Color4f.ApplyGamma(color, 2.4f);
        }
    }
}
