using Rhino.DocObjects;

namespace glTF_BinExporter.glTF
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
    }
}
