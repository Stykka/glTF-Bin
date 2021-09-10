using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.UI;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace glTF_BinExporter
{

    [System.Runtime.InteropServices.Guid("82936404-bd41-46f4-8fe7-e594c2a7e8af")]
    public class GlTFExporterCommand : Rhino.Commands.Command
    {
        public GlTFExporterCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static GlTFExporterCommand Instance { get; private set; }

        public override string EnglishName => "glTFBinExport";

        protected override Result RunCommand(Rhino.RhinoDoc doc, RunMode mode)
        {
            GetObject go = Selection.GetValidExportObjects("Select objects to export.");

            var dialog = GetSaveFileDialog();

            var fileSelected = dialog.ShowSaveDialog();

            if (!fileSelected)
            {
                return Result.Cancel;
            }

            bool binary = GlTFUtils.IsFileGltfBinary(dialog.FileName);

            if(!GetExportOptions(mode, out glTFExportOptions opts))
            {
                return Result.Cancel;
            }

            Rhino.DocObjects.RhinoObject[] rhinoObjects = go.Objects().Select(o => o.Object()).ToArray();

            if(!DoExport(dialog.FileName, opts, binary, doc, rhinoObjects, doc.RenderSettings.LinearWorkflow))
            {
                return Result.Failure;
            }

            return Result.Success;
        }

        private bool GetExportOptions(RunMode mode, out glTFExportOptions options)
        {
            if(mode == RunMode.Scripted)
            {
                options = new glTFExportOptions();

                if(Rhino.Input.RhinoGet.GetBool("Compression", true, "None", "Draco", ref options.UseDracoCompression) != Result.Success)
                {
                    return false;
                }

                if(Rhino.Input.RhinoGet.GetBool("Export Materials", true, "No", "Yes", ref options.ExportMaterials) != Result.Success)
                {
                    return false;
                }

                if(options.ExportMaterials)
                {
                    if(Rhino.Input.RhinoGet.GetBool("Use display color for objects with unset material", true, "No", "Yes", ref options.UseDisplayColorForUnsetMaterials) != Result.Success)
                    {
                        return false;
                    }
                }

                if (options.UseDracoCompression)
                {
                    if(Rhino.Input.RhinoGet.GetInteger("Draco Compression Level (max=10)", true, ref options.DracoCompressionLevel, 1, 10) != Result.Success)
                    {
                        return false;
                    }

                    if(Rhino.Input.RhinoGet.GetInteger("Quantization Position", true, ref options.DracoQuantizationBitsPosition, 8, 32) != Result.Success)
                    {
                        return false;
                    }

                    if(Rhino.Input.RhinoGet.GetInteger("Quantization Normal", true, ref options.DracoQuantizationBitsNormal, 8, 32) != Result.Success)
                    {
                        return false;
                    }

                    if(Rhino.Input.RhinoGet.GetInteger("Quantization Texture", true, ref options.DracoQuantizationBitsTexture, 8, 32) != Result.Success)
                    {
                        return false;
                    }
                }

                if(Rhino.Input.RhinoGet.GetBool("Map Rhino Z to glTF Y", true, "No", "Yes", ref options.MapRhinoZToGltfY) != Result.Success)
                {
                    return false;
                }

                return true;
            }
            else
            {
                ExportOptionsDialog optionsDlg = new ExportOptionsDialog();

                optionsDlg.RestorePosition();
                Eto.Forms.DialogResult result = optionsDlg.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

                options = glTFBinExporterPlugin.GetSavedOptions();

                return result == Eto.Forms.DialogResult.Ok;
            }
        }

        private SaveFileDialog GetSaveFileDialog()
        {
            return new SaveFileDialog()
            {
                DefaultExt = ".glb",
                Title = "Select glTF file to export to.",
                Filter = "glTF Binary (*.glb) | *.glb |glTF Text (*.gltf) | *.gltf",
            };
        }

        public static bool DoExport(string fileName, glTFExportOptions options, bool binary, RhinoDoc doc, IEnumerable<Rhino.DocObjects.RhinoObject> rhinoObjects, Rhino.Render.LinearWorkflow workflow)
        {
            RhinoDocGltfConverter converter = new RhinoDocGltfConverter(options, binary, doc, rhinoObjects, workflow);
            glTFLoader.Schema.Gltf gltf = converter.ConvertToGltf();

            if (binary)
            {
                byte[] bytes = converter.GetBinaryBuffer();
                glTFLoader.Interface.SaveBinaryModel(gltf, bytes.Length == 0 ? null : bytes, fileName);
            }
            else
            {
                glTFLoader.Interface.SaveModel(gltf, fileName);
            }

            RhinoApp.WriteLine("Successfully exported selected geometry to glTF(Binary).");
            return true;
        }

    }
}
