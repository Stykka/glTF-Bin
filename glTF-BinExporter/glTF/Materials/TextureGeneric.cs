namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// This is the baseclass for all PBRMetalRoughness textures.
    /// `index` points to an entry in the `textures` array.
    /// `texCoord` points to the primitives attributes map. I.e. texCoord = 1 => use TEXCOORD_1
    /// </summary>
    public class TextureGeneric
    {
        public int index;
        public int texCoord;
    }
}
