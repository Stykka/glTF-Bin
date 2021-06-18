using Rhino;
using Rhino.PlugIns;
using System;

namespace glTF_BinImporter
{
    public class glTFBinImporterPlugIn : FileImportPlugIn

    {
        public glTFBinImporterPlugIn()
        {
            Instance = this;
        }

        public static glTFBinImporterPlugIn Instance
        {
            get; private set;
        }

        protected override FileTypeList AddFileTypes(Rhino.FileIO.FileReadOptions options)
        {
            FileTypeList result = new FileTypeList();
            result.AddFileType("glTF text file (*.gltf)", "gltf");
            result.AddFileType("glTF binary file (*.glb)", "glb");
            return result;
        }

        protected override bool ReadFile(string filename, int index, RhinoDoc doc, Rhino.FileIO.FileReadOptions options)
        {
            glTFLoader.Schema.Gltf gltf = glTFLoader.Interface.LoadModel(filename);

            GltfRhinoConverter converter = new GltfRhinoConverter(gltf, doc, filename);

            try
            {
                return converter.Convert();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
                return false;
            }
        }

    }
}