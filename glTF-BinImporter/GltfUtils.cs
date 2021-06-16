using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinImporter
{
    static class GltfUtils
    {
        public static readonly Rhino.Geometry.Transform YupToZup = new Rhino.Geometry.Transform()
        {
            M00 = 1.0,
            M01 = 0.0,
            M02 = 0.0,
            M03 = 0.0,
            M10 = 0.0,
            M11 = 0.0,
            M12 = -1.0,
            M13 = 0.0,
            M20 = 0.0,
            M21 = 1.0,
            M22 = 0.0,
            M23 = 0.0,
            M30 = 0.0,
            M31 = 0.0,
            M32 = 0.0,
            M33 = 1.0,
        };

        public static Rhino.Display.Color4f ToColor4f(this float[] floats)
        {
            if(floats.Length == 3)
            {
                return new Rhino.Display.Color4f(floats[0], floats[1], floats[2], 1.0f);
            }
            else
            {
                return new Rhino.Display.Color4f(floats[0], floats[1], floats[2], floats[3]);
            }
        }

        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(Math.Min(max, value), min);
        }

    }
}
