namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// Defines a sampler for use in WebGL. The constants 
    /// </summary>
    public class Sampler
    {
        public int magFilter = GLConstants.TEXTURE_MAG_FILTER;
        public int minFilter = GLConstants.TEXTURE_MIN_FILTER;
        public int wrapS = GLConstants.TEXTURE_REPEAT;
        public int wrapT = GLConstants.TEXTURE_REPEAT;
    }
}
