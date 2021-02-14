using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    public class glTFExportOptions
    {
        public bool UseDracoCompression = false;
        public bool UseBinary = true;
        public int DracoCompressionLevel = 10;
        public bool MapRhinoZToGltfY = true;
        public int DracoQuantizationBitsPosition = 11;
        public int DracoQuantizationBitsNormal = 8;
        public int DracoQuantizationBitsTexture = 10;
    }
}
