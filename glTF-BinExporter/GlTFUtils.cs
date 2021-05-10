using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;

namespace glTF_BinExporter
{
    /// <summary>
    /// Functions for helping with adding RhinoObjects to the RootModel.
    /// </summary>
    public static class GlTFUtils
    {

        public static int AddAndReturnIndex<T>(this List<T> list, T item)
        {
            list.Add(item);
            return list.Count - 1;
        }

        public static bool IsFileGltfBinary(string filename)
        {
            string extension = Path.GetExtension(filename);

            return extension.ToLower() == ".glb";
        }

        public static float[] ToFloatArray(this Rhino.Display.Color4f color)
        {
            return new float[]
            {
                color.R,
                color.G,
                color.B,
                color.A,
            };
        }

        public static float[] ToFloatArray(this Point3d point)
        {
            return new float[]
            {
                (float)point.X,
                (float)point.Y,
                (float)point.Z,
            };
        }

        public static float[] ToFloatArray(this Vector3f vector)
        {
            return new float[]
            {
                vector.X,
                vector.Y,
                vector.Z,
            };
        }

        public static float[] ToFloatArray(this Point2f point)
        {
            return new float[]
            {
                point.X,
                point.Y,
            };
        }
    }
}
