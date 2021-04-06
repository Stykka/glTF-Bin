using Rhino;
using Rhino.FileIO;
using Rhino.PlugIns;
using System.Collections.Generic;
using System.IO;

namespace glTF_BinExporter
{
    ///<summary>
    /// <para>Every RhinoCommon .rhp assembly must have one and only one PlugIn-derived
    /// class. DO NOT create instances of this class yourself. It is the
    /// responsibility of Rhino to create an instance of this class.</para>
    /// <para>To complete plug-in information, please also see all PlugInDescription
    /// attributes in AssemblyInfo.cs (you might need to click "Project" ->
    /// "Show All Files" to see it in the "Solution Explorer" window).</para>
    ///</summary>
    public class glTF_BinExporterPlugin : Rhino.PlugIns.FileExportPlugIn
    {
        ///<summary>Gets the only instance of the glTF_BinExporterPlugin plug-in.</summary>
        public static glTF_BinExporterPlugin Instance { get; private set; }

        public glTF_BinExporterPlugin()
        {
            Instance = this;
        }

        protected override FileTypeList AddFileTypes(FileWriteOptions options)
        {
            FileTypeList typeList = new FileTypeList();

            typeList.AddFileType("glTF Binary File", ".glb");
            typeList.AddFileType("glTF Text File", ".gltf");

            return typeList;
        }

        protected override WriteFileResult WriteFile(string filename, int index, RhinoDoc doc, FileWriteOptions options)
        {
            bool binary = GlTFUtils.IsFileGltfBinary(filename);

            glTFExportOptions gltfOptions = new glTFExportOptions();
            gltfOptions.UseBinary = binary;

            ExportOptionsDialog optionsDlg = new ExportOptionsDialog(gltfOptions);

            if(optionsDlg.ShowModal() == null)
            {
                return WriteFileResult.Cancel;
            }

            IEnumerable<Rhino.DocObjects.RhinoObject> objects = GetObjectsToExport(doc, options);

            if(!GlTFExporterCommand.DoExport(filename, gltfOptions, objects, doc.RenderSettings.LinearWorkflow))
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
    }
}
