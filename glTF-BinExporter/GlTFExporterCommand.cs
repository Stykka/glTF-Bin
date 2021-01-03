using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.UI;
using System;
using System.IO;
using System.Linq;

namespace glTF_BinExporter.glTF
{
    public class ExportOptions
    {
        public bool UseDracoCompression;
        public bool UseBinary;
        public int DracoCompressionLevel;
        public int DracoQuantizationBits;
    }

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

            var opts = new ExportOptions() { UseDracoCompression = true, DracoCompressionLevel = 10, DracoQuantizationBits = 16, UseBinary = true };

            // NOTE: The following options can be useful in dev/debug:
            //bool useDracoCompression = true;
            //Rhino.Input.RhinoGet.GetBool("Compression", true, "None", "Draco", ref useDracoCompression);
            //bool useBinary = true;
            //Rhino.Input.RhinoGet.GetBool("Mode", true, "Text", "Binary", ref useBinary);
            //bool includeMaterials = true;
            //Rhino.Input.RhinoGet.GetBool("Materials", true, "Exclude", "Include", ref opts.IncludeMaterial);
            //int dracoCompressionLevel = 10;
            Rhino.Input.RhinoGet.GetInteger("Draco Compression Level (max=10)", true, ref opts.DracoCompressionLevel, 1, 10);
            //bool quantizaionBits = true;
            Rhino.Input.RhinoGet.GetInteger("Quantization", true, ref opts.DracoQuantizationBits, 8, 32);
            //Rhino.Input.RhinoGet.GetBool("Draco Quantization", true, "B", "Bits 16 Bits", ref quantizaionBits);
            //opts.DracoQuantizationBits = quantizaionBits ? 24 : 12;

            var dialog = new SaveFileDialog() { DefaultExt = ".glb", Title = "Select glTF Binary file to export to.", Filter = "glTF Binary (*.glb) | *.glb" };
            var fileSelected = dialog.ShowSaveDialog();

            if (!fileSelected) {
                return Result.Cancel;
            }

            try
            {
                // Writes the result to a memory stream, then dumps it to a file.
                using (FileStream fileStream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.ReadWrite))
                using (MemoryStream memoryStream = new MemoryStream(1024))
                {
                    var rhinoObjects = go
                        .Objects()
                        .Select(o => o.Object())
                        .ToArray();

                    if (opts.UseBinary)
                    {
                        GlTFUtils.ExportBinary(memoryStream, rhinoObjects, opts);
                    }
                    else
                    {
                        GlTFUtils.ExportText(memoryStream, rhinoObjects, opts);
                    }

                    memoryStream.Flush();
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    memoryStream.CopyTo(fileStream);
                    fileStream.Flush();
                    fileStream.Close();
                }

                RhinoApp.WriteLine("Successfully exported selected geometry to glTF(Binary).");
                return Result.Success;
            } catch (Exception e) {
                RhinoApp.WriteLine("ERROR: Failed exporting selected geometry to file.");
                return Result.Failure;
            }
        }
    }
}
