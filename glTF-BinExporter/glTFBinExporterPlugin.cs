using Rhino;
using Rhino.FileIO;
using Rhino.PlugIns;
using System;
using System.Collections.Generic;
using System.IO;

namespace glTF_BinExporter
{
    public class glTFBinExporterPlugin : Rhino.PlugIns.FileExportPlugIn
    {
        public static glTFBinExporterPlugin Instance { get; private set; }

        public glTFBinExporterPlugin()
        {
            Instance = this;
        }

        protected override FileTypeList AddFileTypes(FileWriteOptions options)
        {
            FileTypeList typeList = new FileTypeList();

            typeList.AddFileType("glTF Binary File", ".glb", true);
            typeList.AddFileType("glTF Text File", ".gltf", true);

            return typeList;
        }

        protected override WriteFileResult WriteFile(string filename, int index, RhinoDoc doc, FileWriteOptions options)
        {
            bool binary = GlTFUtils.IsFileGltfBinary(filename);

            ExportOptionsDialog optionsDlg = new ExportOptionsDialog();

            if(optionsDlg.ShowModal() != Eto.Forms.DialogResult.Ok)
            {
                return WriteFileResult.Cancel;
            }

            glTFExportOptions exportOptions = glTFBinExporterPlugin.GetSavedOptions();

            IEnumerable<Rhino.DocObjects.RhinoObject> objects = GetObjectsToExport(doc, options);

            if(!GlTFExporterCommand.DoExport(filename, exportOptions, binary, objects, doc.RenderSettings.LinearWorkflow))
            {
                return WriteFileResult.Failure;
            }

            return WriteFileResult.Success;
        }

        private IEnumerable<Rhino.DocObjects.RhinoObject> GetObjectsToExport(RhinoDoc doc, FileWriteOptions options)
        {
            if(options.WriteSelectedObjectsOnly)
            {
                return doc.Objects.GetSelectedObjects(false, false);
            }
            else
            {
                return doc.Objects;
            }
        }

        protected override void DisplayOptionsDialog(IntPtr parent, string description, string extension)
        {
            ExportOptionsDialog exportOptionsDialog = new ExportOptionsDialog();

            exportOptionsDialog.ShowModal();
        }

        #region Settings

        private const string useDracoCompressionKey = "UseDracoCompression";
        public const bool UseDracoCompressionDefault = false;

        public static bool UseDracoCompression
        {
            get => Instance.Settings.GetBool(useDracoCompressionKey, UseDracoCompressionDefault);
            set => Instance.Settings.SetBool(useDracoCompressionKey, value);
        }

        private const string mapRhinoZToGltfYKey = "MapZYpToYUp";
        public const bool MapRhinoZToGltfYDefault = true;

        public static bool MapRhinoZToGltfY
        {
            get => Instance.Settings.GetBool(mapRhinoZToGltfYKey, MapRhinoZToGltfYDefault);
            set => Instance.Settings.SetBool(mapRhinoZToGltfYKey, value);
        }

        private const string exportMaterialsKey = "ExportMaterials";
        public const bool ExportMaterialsDefault = true;

        public static bool ExportMaterials
        {
            get => Instance.Settings.GetBool(exportMaterialsKey, ExportMaterialsDefault);
            set => Instance.Settings.SetBool(exportMaterialsKey, value);
        }

        private const string dracoCompressionLevelKey = "DracoCompressionLevel";
        public const int DracoCompressionLevelDefault = 10;
        
        public static int DracoCompressionLevel
        {
            get => Instance.Settings.GetInteger(dracoCompressionLevelKey, DracoCompressionLevelDefault);
            set => Instance.Settings.SetInteger(dracoCompressionLevelKey, value);
        }

        private const string dracoQuantizationBitsPositionKey = "DracoQuantizationBitsPosition";
        public const int DracoQuantizationBitsPositionDefault = 11;

        public static int DracoQuantizationBitsPosition
        {
            get => Instance.Settings.GetInteger(dracoQuantizationBitsPositionKey, DracoQuantizationBitsPositionDefault);
            set => Instance.Settings.SetInteger(dracoQuantizationBitsPositionKey, value);
        }

        private const string dracoQuantizationBitsNormalKey = "DracoQuantizationBitsNormal";
        public const int DracoQuantizationBitsNormalDefault = 8;

        public static int DracoQuantizationBitsNormal
        {
            get => Instance.Settings.GetInteger(dracoQuantizationBitsNormalKey, DracoQuantizationBitsNormalDefault);
            set => Instance.Settings.SetInteger(dracoQuantizationBitsNormalKey, value);
        }

        private const string dracoQuantizationBitsTextureKey = "DracoQuantizationBitsTextureKey";
        public const int DracoQuantizationBitsTextureDefault = 10;

        public static int DracoQuantizationBitsTexture
        {
            get => Instance.Settings.GetInteger(dracoQuantizationBitsTextureKey, DracoQuantizationBitsTextureDefault);
            set => Instance.Settings.SetInteger(dracoQuantizationBitsTextureKey, value);
        }

        public static glTFExportOptions GetSavedOptions()
        {
            return new glTFExportOptions()
            {
                MapRhinoZToGltfY = MapRhinoZToGltfY,
                ExportMaterials = ExportMaterials,
                UseDracoCompression = UseDracoCompression,
                DracoCompressionLevel = DracoCompressionLevel,
                DracoQuantizationBitsPosition = DracoQuantizationBitsPosition,
                DracoQuantizationBitsNormal = DracoQuantizationBitsNormal,
                DracoQuantizationBitsTexture = DracoQuantizationBitsTexture,
            };
        }

        #endregion

    }
}
