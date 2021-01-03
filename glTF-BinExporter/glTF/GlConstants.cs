using System;
using Rhino.DocObjects;

namespace glTF_BinExporter.glTF
{
    public static class GLConstants
    {
        // componentType
        // Byte size = 1
        public static int BYTE = 5120;
        public static int UNSIGNED_BYTE = 5121;
        // Byte size = 2
        public static int SHORT = 5122;
        public static int UNSIGNED_SHORT = 5123;
        // Byte size = 4
        public static int UNSIGNED_INT = 5125;
        public static int FLOAT = 5126;

        // type
        // #components=1
        public static string SCALAR = "SCALAR";
        // #components=2
        public static string VEC2 = "VEC2";
        // #components=3
        public static string VEC3 = "VEC3";
        // #components=4
        public static string VEC4 = "VEC4";
        // #components=4
        public static string MAT2 = "MAT2";
        // #components=9
        public static string MAT3 = "MAT3";
        // #components=16
        public static string MAT4 = "MAT4";

        // bufferview.target
        // array of data such as vertices
        public static int ARRAY_BUFFER = 34962;
        // array of indices
        public static int ELEMENT_ARRAY_BUFFER = 34963;

        public static uint GLB_MAGIC_BYTE = 0x46546C67;
        public static uint CHUNK_TYPE_JSON = 0x4E4F534A;
        public static uint CHUNK_TYPE_BINARY = 0x004E4942;

        // https://developer.mozilla.org/en-US/docs/Web/API/WebGL_API/Constants
        public static int TEXTURE_NEAREST = 0x2600;
        public static int TEXTURE_LINEAR = 0x2601;
        public static int TEXTURE_MAG_FILTER = 0x2800;
        public static int TEXTURE_MIN_FILTER = 0x2801;

        public static int TEXTURE_REPEAT = 0x2901;
    }
}
