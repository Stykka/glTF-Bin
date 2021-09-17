using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    /// <summary>
    /// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_volume/README.md
    /// </summary>
    public class KHR_materials_volume
    {
        public const string Tag = "KHR_materials_volume";

        [Newtonsoft.Json.JsonPropertyAttribute("thicknessFactor")]
        public float ThicknessFactor = 0.0f;

        [Newtonsoft.Json.JsonPropertyAttribute("thicknessTexture")]
        public glTFLoader.Schema.TextureInfo ThicknessTexture = null;

        [Newtonsoft.Json.JsonPropertyAttribute("attenuationDistance")]
        public float AttenuationDistance = float.PositiveInfinity;

        [Newtonsoft.Json.JsonPropertyAttribute("attenuationColor")]
        public float[] AttenuationColor = new float[] { 1.0f, 1.0f, 1.0f };

        public bool ShouldSerializeThicknessTexture()
        {
            return ThicknessTexture != null;
        }
    }
}
