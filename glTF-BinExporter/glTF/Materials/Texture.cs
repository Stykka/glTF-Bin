namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// This is an entry in the textures array.
    /// `source` points to an entry in the `images` array.
    /// `sampler` points to an entry in the `samplers` array.
    /// </summary>
    public class Texture
    {
        public int source;
        public int sampler;
    }
}
