using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    public class KHR_materials_ior
    {
        public const string Tag = "KHR_materials_ior";

        [Newtonsoft.Json.JsonPropertyAttribute("ior")]
        public float Ior = 1.5f;
    }
}
