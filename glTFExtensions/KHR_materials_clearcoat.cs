using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    public class KHR_materials_clearcoat
    {
        [Newtonsoft.Json.JsonPropertyAttribute("clearcoatFactor")]
        public float ClearcoatFactor = 0.0f;

        [Newtonsoft.Json.JsonPropertyAttribute("clearcoatTexture")]
        public glTFLoader.Schema.TextureInfo ClearcoatTexture = null;

        [Newtonsoft.Json.JsonPropertyAttribute("clearcoatRoughnessFactor")]
        public float ClearcoatRoughnessFactor = 0.0f;

        [Newtonsoft.Json.JsonPropertyAttribute("clearcoatRoughnessTexture")]
        public glTFLoader.Schema.TextureInfo ClearcoatRoughnessTexture = null;

        [Newtonsoft.Json.JsonPropertyAttribute("clearcoatNormalTexture")]
        public glTFLoader.Schema.MaterialNormalTextureInfo ClearcoatNormalTexture = null;

        public bool ShouldSerializeClearcoatFactor()
        {
            return ClearcoatFactor != 0.0f;
        }

        public bool ShouldSerializeClearcoatTexture()
        {
            return ClearcoatTexture != null;
        }

        public bool ShouldSerializeClearcoatRoughnessFactor()
        {
            return ClearcoatRoughnessFactor != 0.0f;
        }

        public bool ShouldSerializeClearcoatRoughnessTexture()
        {
            return ClearcoatRoughnessTexture != null;
        }

        public bool ShouldSerializeClearcoatNormalTexture()
        {
            return ClearcoatNormalTexture != null;
        }

    }
}
