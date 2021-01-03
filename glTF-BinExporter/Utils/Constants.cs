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
    }
}
