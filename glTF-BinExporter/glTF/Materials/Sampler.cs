namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// Defines a sampler using constants from the glTF spec
    /// </summary>
    public class Sampler
    {
        public int magFilter = GLConstants.LINEAR;
        public int minFilter = GLConstants.LINEAR;
        public int wrapS = GLConstants.TEXTURE_REPEAT;
        public int wrapT = GLConstants.TEXTURE_REPEAT;
    }
}
