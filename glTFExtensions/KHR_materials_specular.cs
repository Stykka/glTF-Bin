using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    public class KHR_materials_specular
    {
        public const string Tag = "KHR_materials_specular";

        [Newtonsoft.Json.JsonPropertyAttribute("specularFactor")]
        public float SpecularFactor = 1.0f;

        [Newtonsoft.Json.JsonPropertyAttribute("specularTexture")]
        public glTFLoader.Schema.TextureInfo SpecularTexture = null;

        [Newtonsoft.Json.JsonPropertyAttribute("specularColorFactor")]
        public float[] SpecularColorFactor = new float[3]
        {
            1.0f,
            1.0f,
            1.0f,
        };

        [Newtonsoft.Json.JsonPropertyAttribute("specularColorTexture")]
        public glTFLoader.Schema.TextureInfo SpecularColorTexture = null;

        public bool ShouldSerializeSpecularTexture()
        {
            return SpecularTexture != null;
        }

        public bool ShouldSerializeSpecularColorFactor()
        {
            return SpecularColorFactor != null;
        }

        public bool ShouldSerializeSpecularColorTexture()
        {
            return SpecularColorTexture != null;
        }

    }
}
