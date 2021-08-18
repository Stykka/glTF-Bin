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

        public SubDMode SubDExportMode = glTFBinExporterPlugin.SubDModeDefault;
        public int SubDLevel = glTFBinExporterPlugin.SubDLevelDefault;

        public bool ExportTextureCoordinates = glTFBinExporterPlugin.ExportTextureCoordinatesDefault;
        public bool ExportVertexNormals = glTFBinExporterPlugin.ExportVertexNormalsDefault;
        public bool ExportOpenMeshes = glTFBinExporterPlugin.ExportOpenMeshesDefault;
        public bool ExportVertexColors = glTFBinExporterPlugin.ExportVertexColorsDefault;

        public bool UseDracoCompression = glTFBinExporterPlugin.UseDracoCompressionDefault;
        public int DracoCompressionLevel = glTFBinExporterPlugin.DracoCompressionLevelDefault;
        public int DracoQuantizationBitsPosition = glTFBinExporterPlugin.DracoQuantizationBitsPositionDefault;
        public int DracoQuantizationBitsNormal = glTFBinExporterPlugin.DracoQuantizationBitsNormalDefault;
        public int DracoQuantizationBitsTexture = glTFBinExporterPlugin.DracoQuantizationBitsTextureDefault;

        public bool ExportLayers = glTFBinExporterPlugin.ExportLayers;
    }
}
