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

        public const string DracoMeshCompressionExtensionTag = "KHR_draco_mesh_compression";
    }

    public class DracoGeometryInfo
    {
        public bool success;

        public int bufferIndex;
        public int byteOffset;
        public int byteLength;

        public int verticesNum;
        public float[] verticesMin;
        public float[] verticesMax;

        public int trianglesNum;
        public float trianglesMin;
        public float trianglesMax;

        public int normalsNum;
        public float[] normalsMin;
        public float[] normalsMax;

        public int texCoordsNum;
        public float[] texCoordsMin;
        public float[] texCoordsMax;
    }
}
