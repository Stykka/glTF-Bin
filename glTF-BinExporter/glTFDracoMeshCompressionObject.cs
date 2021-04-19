using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    class glTFDracoMeshCompressionObject
    {
        public int? bufferView = null;

        public Dictionary<string, int> attributes = new Dictionary<string, int>();
    }
}
