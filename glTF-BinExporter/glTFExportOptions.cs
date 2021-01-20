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
        public int DracoQuantizationBits = 16;
    }
}
