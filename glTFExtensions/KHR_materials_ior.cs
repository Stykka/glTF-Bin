using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    /// <summary>
    /// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_materials_ior/README.md
    /// </summary>
    public class KHR_materials_ior
    {
        public const string Tag = "KHR_materials_ior";

        [Newtonsoft.Json.JsonPropertyAttribute("ior")]
        public float Ior = 1.5f;
    }
}
