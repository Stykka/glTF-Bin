using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    public class KHR_materials_transmission
    {
        public const string Tag = "KHR_materials_transmission";

        [Newtonsoft.Json.JsonPropertyAttribute("transmissionFactor")]
        public float TransmissionFactor = 0.0f;

        [Newtonsoft.Json.JsonPropertyAttribute("transmissionTexture")]
        public glTFLoader.Schema.TextureInfo TransmissionTexture = null;

        public bool ShouldSerializeTransmissionFactor()
        {
            return TransmissionFactor != 0.0f;
        }

        public bool ShouldSerializeTransmissionTexture()
        {
            return TransmissionTexture != null;
        }
    }
}
