using Rhino.DocObjects;

namespace glTF_BinExporter
{
    public static class Constants
    {
        public static ObjectType[] ValidObjectTypes = new ObjectType[] {
            ObjectType.Brep,
            ObjectType.InstanceReference,
            ObjectType.Mesh,
            ObjectType.Extrusion,
            ObjectType.Surface,
            ObjectType.SubD
        };

        public static byte[][] Paddings = new byte[][]
        {
            new byte[] { },
            new byte[] { 0, 0, 0 },
            new byte[] { 0, 0 },
            new byte[] { 0 },
        };

        public const string TextBufferHeader = "data:application/octet-stream;base64,";
        public const string PositionAttributeTag = "POSITION";
        public const string NormalAttributeTag = "NORMAL";
        public const string TexCoord0AttributeTag = "TEXCOORD_0";
    }
}
