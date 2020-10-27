using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.UI;
using System.IO;
using System.Linq;

namespace Stykka.Common
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
            GetObject go = Utils.Selection.GetRhinoBlocksAndBreps();

            var dialog = new SaveFileDialog() { DefaultExt = ".glb", Title = "Select glTF Binary file to export to.", Filter = "glTF Binary (*.glb) | *.glb" };
            var fileSelected = dialog.ShowSaveDialog();

            if (!fileSelected) {
                return Result.Cancel;
	        }

            using (FileStream fileStream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.ReadWrite))
	        using (MemoryStream memoryStream = new MemoryStream(1024))
            {
                var rhinoObjects = go
                    .Objects()
                    .Select(o => o.Object())
                    .ToArray();

			    GlTFUtils.ExportBinary(memoryStream, rhinoObjects);
                memoryStream.Flush();
                memoryStream.Seek(0, SeekOrigin.Begin);
                memoryStream.CopyTo(fileStream);
                fileStream.Flush();
                fileStream.Close();
            }

            RhinoApp.WriteLine("Successfully exported selected geometry to file.");
            return Result.Success;
        }
    }
}
