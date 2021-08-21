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
        public bool ExportLayers = glTFBinExporterPlugin.ExportLayers;

        public SubDMode SubDExportMode = glTFBinExporterPlugin.SubDModeDefault;
        public int SubDLevel = glTFBinExporterPlugin.SubDLevelDefault;

        public bool ExportTextureCoordinates = glTFBinExporterPlugin.ExportTextureCoordinatesDefault;
        public int UV0 = glTFBinExporterPlugin.UV0Default;
        public int UV1 = glTFBinExporterPlugin.UV1Default;
        public bool ExportAllTextureCoordinates = glTFBinExporterPlugin.ExportAllTextureCoordinatesDefault;

        public bool ExportVertexNormals = glTFBinExporterPlugin.ExportVertexNormalsDefault;
        public bool ExportOpenMeshes = glTFBinExporterPlugin.ExportOpenMeshesDefault;
        public bool ExportVertexColors = glTFBinExporterPlugin.ExportVertexColorsDefault;

        public bool UseDracoCompression = glTFBinExporterPlugin.UseDracoCompressionDefault;
        public int DracoCompressionLevel = glTFBinExporterPlugin.DracoCompressionLevelDefault;
        public int DracoQuantizationBitsPosition = glTFBinExporterPlugin.DracoQuantizationBitsPositionDefault;
        public int DracoQuantizationBitsNormal = glTFBinExporterPlugin.DracoQuantizationBitsNormalDefault;
        public int DracoQuantizationBitsTexture = glTFBinExporterPlugin.DracoQuantizationBitsTextureDefault;
    }
}
