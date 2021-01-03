namespace glTF_BinExporter.glTF
{
    public class MaterialMetalicRoughness
    {
        public float[] baseColorFactor;
        public float metallicFactor;
        public float roughnessFactor;
        public TextureBaseColor baseColorTexture;
        public TextureBaseColor metallicRoughnessTexture;

        public MaterialMetalicRoughness()
        {
            baseColorFactor = new float[4] { 0.3f, 0.3f, 0.3f, 1.0f };
            metallicFactor = 0.3f;
            roughnessFactor = 0.3f;
        }
    }
}
