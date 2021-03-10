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

            var opts = new glTFExportOptions() { UseDracoCompression = false, DracoCompressionLevel = 10, DracoQuantizationBitsPosition = 11, DracoQuantizationBitsNormal = 8, DracoQuantizationBitsTexture = 10, UseBinary = binary };

            if (mode == RunMode.Scripted)
            {

                Rhino.Input.RhinoGet.GetBool("Compression", true, "None", "Draco", ref opts.UseDracoCompression);

                if (opts.UseDracoCompression)
                {
                    Rhino.Input.RhinoGet.GetInteger("Draco Compression Level (max=10)", true, ref opts.DracoCompressionLevel, 1, 10);
                    Rhino.Input.RhinoGet.GetInteger("Quantization Position", true, ref opts.DracoQuantizationBitsPosition, 8, 32);
                    Rhino.Input.RhinoGet.GetInteger("Quantization Normal", true, ref opts.DracoQuantizationBitsNormal, 8, 32);
                    Rhino.Input.RhinoGet.GetInteger("Quantization Texture", true, ref opts.DracoQuantizationBitsTexture, 8, 32);
                }

                Rhino.Input.RhinoGet.GetBool("Map Rhino Z to glTF Y", true, "No", "Yes", ref opts.MapRhinoZToGltfY);
            }
            else
            {
                ExportOptionsDialog optionsDlg = new ExportOptionsDialog(opts);

                if (optionsDlg.ShowModal() == null)
                {
                    return Result.Cancel;
                }
            }

            var rhinoObjects = go
                                .Objects()
                                .Select(o => o.Object())
                                .ToArray();

            if(!DoExport(dialog.FileName, opts, rhinoObjects, doc.RenderSettings.LinearWorkflow))
            {
                return Result.Failure;
            }

            return Result.Success;
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

        public static bool DoExport(string fileName, glTFExportOptions opts, IEnumerable<Rhino.DocObjects.RhinoObject> rhinoObjects, Rhino.Render.LinearWorkflow workflow)
        {
            try
            {
                RhinoDocGltfConverter converter = new RhinoDocGltfConverter(opts, rhinoObjects, workflow);
                glTFLoader.Schema.Gltf gltf = converter.ConvertToGltf();

                if(opts.UseBinary)
                {
                    byte[] bytes = converter.GetBinaryBuffer();
                    glTFLoader.Interface.SaveBinaryModel(gltf, bytes, fileName);
                }
                else
                {
                    glTFLoader.Interface.SaveModel(gltf, fileName);
                }

                RhinoApp.WriteLine("Successfully exported selected geometry to glTF(Binary).");
                return true;
            }
            catch (Exception e)
            {
                RhinoApp.WriteLine("ERROR: Failed exporting selected geometry to file.");
                System.Diagnostics.Debug.WriteLine(e.Message);
                return false;
            }
        }

    }
}
