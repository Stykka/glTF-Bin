using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTFExtensions
{
    /// <summary>
    /// https://github.com/KhronosGroup/glTF/blob/master/extensions/2.0/Khronos/KHR_draco_mesh_compression/README.md
    /// </summary>
    public class KHR_draco_mesh_compression
    {
        public const string Tag = "KHR_draco_mesh_compression";

        [Newtonsoft.Json.JsonPropertyAttribute("bufferView")]
        public int BufferView;

        [Newtonsoft.Json.JsonPropertyAttribute("attributes")]
        public Dictionary<string, int> Attributes = new Dictionary<string, int>();
    }
}
