using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace glTF_BinExporter
{
    public static class Constants
    {
        public static readonly ObjectType[] ValidObjectTypes = new ObjectType[] {
            ObjectType.Brep,
            ObjectType.InstanceReference,
            ObjectType.Mesh,
            ObjectType.Extrusion,
            ObjectType.Surface,
            ObjectType.SubD
        };

        public static readonly byte[][] Paddings = new byte[][]
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
        public const string TexCoord1AttributeTag = "TEXCOORD_1";
        public const string VertexColorAttributeTag = "COLOR_0";

        public const string DracoMeshCompressionExtensionTag = "KHR_draco_mesh_compression";
        public const string MaterialsTransmissionExtensionTag = "KHR_materials_transmission";
        public const string MaterialsClearcoatExtensionTag = "KHR_materials_clearcoat";
    }

    public class DracoGeometryInfo
    {
        public bool Success;

        public int BufferIndex;
        public int ByteOffset;
        public int ByteLength;

        public int BufferViewIndex;

        public int VerticesCount;
        public Point3d VerticesMin;
        public Point3d VerticesMax;

        public int IndicesCount;
        public float IndicesMin;
        public float IndicesMax;

        public int NormalsCount;
        public Vector3f NormalsMin;
        public Vector3f NormalsMax;

        public int TexCoordsCount;
        public Point2f TexCoordsMin;
        public Point2f TexCoordsMax;

        public int VertexColorCount;
        public Color4f VertexColorMin;
        public Color4f VertexColorMax;

        public int VertexAttributePosition;
        public int NormalAttributePosition;
        public int TextureCoordinatesAttributePosition;
        public int VertexColorAttributePosition;
    }
}
