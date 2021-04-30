using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    public class glTFExportOptions
    {
        public bool MapRhinoZToGltfY = glTFBinExporterPlugin.MapRhinoZToGltfYDefault;
        public bool ExportMaterials = glTFBinExporterPlugin.ExportMaterialsDefault;
        public bool UseDisplayColorForUnsetMaterials = glTFBinExporterPlugin.UseDisplayColorForUnsetMaterialsDefault;
        public bool UseDracoCompression = glTFBinExporterPlugin.UseDracoCompressionDefault;
        public int DracoCompressionLevel = glTFBinExporterPlugin.DracoCompressionLevelDefault;
        public int DracoQuantizationBitsPosition = glTFBinExporterPlugin.DracoQuantizationBitsPositionDefault;
        public int DracoQuantizationBitsNormal = glTFBinExporterPlugin.DracoQuantizationBitsNormalDefault;
        public int DracoQuantizationBitsTexture = glTFBinExporterPlugin.DracoQuantizationBitsTextureDefault;
    }
}
