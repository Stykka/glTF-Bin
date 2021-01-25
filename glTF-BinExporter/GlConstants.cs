using System;
using Rhino.DocObjects;

namespace glTF_BinExporter.glTF
{
    public static class GLConstants
    {
        // componentType
        // Byte size = 1
        public const int BYTE = 5120;
        public const int UNSIGNED_BYTE = 5121;
        // Byte size = 2
        public const int SHORT = 5122;
        public const int UNSIGNED_SHORT = 5123;
        // Byte size = 4
        public const int UNSIGNED_INT = 5125;
        public const int FLOAT = 5126;

        // type
        // #components=1
        public const string SCALAR = "SCALAR";
        // #components=2
        public const string VEC2 = "VEC2";
        // #components=3
        public const string VEC3 = "VEC3";
        // #components=4
        public const string VEC4 = "VEC4";
        // #components=4
        public const string MAT2 = "MAT2";
        // #components=9
        public const string MAT3 = "MAT3";
        // #components=16
        public const string MAT4 = "MAT4";

        // bufferview.target
        // array of data such as vertices
        public const int ARRAY_BUFFER = 34962;
        // array of indices
        public const int ELEMENT_ARRAY_BUFFER = 34963;

        public const uint GLB_MAGIC_BYTE = 0x46546C67;
        public const uint CHUNK_TYPE_JSON = 0x4E4F534A;
        public const uint CHUNK_TYPE_BINARY = 0x004E4942;

        //https://github.com/KhronosGroup/glTF/blob/master/specification/2.0/README.md#samplermagfilter
        //https://github.com/KhronosGroup/glTF/blob/master/specification/2.0/README.md#samplerminfilter
        public const int NEAREST = 9728;
        public const int LINEAR = 9729;
        public const int NEAREST_MIPMAP_NEAREST = 9984;
        public const int LINEAR_MIPMAP_NEAREST = 9985;
        public const int NEAREST_MIPMAP_LINEAR = 9986;
        public const int LINEAR_MIPMAP_LINEAR = 9987;

        public const int TEXTURE_REPEAT = 0x2901;

        public const string TextBufferHeader = "data:application/octet-stream;base64,";
        public const string PositionAttributeTag = "POSITION";
        public const string NormalAttributeTag = "NORMAL";
        public const string TexCoord0AttributeTag = "TEXCOORD_0";
    }
}
