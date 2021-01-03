using System;
using Newtonsoft.Json;

namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// GL Physcially Based Material.
    /// Can be constructed from a "regular" Rhino Material, or Physically Based Material. The latter is best, since it maps 1:1.
    /// </summary>
    public class Material
    {
        public string name;
        public MaterialMetalicRoughness pbrMetallicRoughness;
        public TextureNormal normalTexture;
        public TextureOcclusion occlusionTexture;
        public TextureEmissive emissiveTexture;

        [JsonIgnore]
        public Guid Id;

        [JsonIgnore]
        public int RhinoMaterialIndex;

        public Material(Rhino.DocObjects.Material material, Guid renderMatId)
        {
            Id = renderMatId;
            name = material.Name;

            var color = ColorUtils.ConvertSRGBToLinear(material.DiffuseColor);

            var diffuseColor = new float[4] {
                color.R,
                color.G,
                color.B,
                material.DiffuseColor.A
            };

            pbrMetallicRoughness = new MaterialMetalicRoughness()
            {
                baseColorFactor = diffuseColor,
                metallicFactor = (float)material.Reflectivity,
                roughnessFactor = 0.5f
            };
        }

        public Material(Rhino.DocObjects.PhysicallyBasedMaterial physMat, Guid renderMatId)
        {
            Id = renderMatId;
            RhinoMaterialIndex = physMat.Material.MaterialIndex;
            name = physMat.Material.Name;

            var color = ColorUtils.ConvertSRGBToLinear(physMat.BaseColor);

            var diffuseColor = new float[4] {
                color.R,
                color.G,
                color.B,
                physMat.BaseColor.A
            };

            pbrMetallicRoughness = new MaterialMetalicRoughness()
            {
                baseColorFactor = diffuseColor,
                metallicFactor = (float)physMat.Metallic,
                roughnessFactor = (float)physMat.Roughness
            };
        }
    }
}
