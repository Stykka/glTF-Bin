using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinImporter
{
    class GltfRhinoConverter
    {
        public GltfRhinoConverter(glTFLoader.Schema.Gltf gltf, Rhino.RhinoDoc doc, string path)
        {
            this.gltf = gltf;
            this.doc = doc;

            this.path = path;
            directory = Path.GetDirectoryName(path);
            filename = Path.GetFileName(path);
            extension = Path.GetExtension(path);

            binaryFile = extension.ToLower() == ".glb";
        }

        glTFLoader.Schema.Gltf gltf = null;
        Rhino.RhinoDoc doc = null;

        string path = "";
        string directory = "";
        string filename = "";
        string extension = "";

        bool binaryFile = false;

        List<byte[]> buffers = new List<byte[]>();

        List<System.Drawing.Bitmap> images = new List<System.Drawing.Bitmap>();

        Dictionary<int, RhinoGltfMetallicRoughnessConverter> metalRoughnessTextures = new Dictionary<int, RhinoGltfMetallicRoughnessConverter>(); 

        List<Rhino.Render.RenderMaterial> materials = new List<Rhino.Render.RenderMaterial>();

        List<GltfMeshHolder> meshHolders = new List<GltfMeshHolder>();

        HashSet<string> Names = new HashSet<string>();

        int nameCounter = 0;

        public string GetUniqueName(string name)
        {
            if(string.IsNullOrEmpty(name))
            {
                name = "Unnamed";
            }

            while (Names.Contains(name))
            {
                name = name + "-" + nameCounter.ToString();
                nameCounter++;
            }

            Names.Add(name);

            return name;
        }

        public bool Convert()
        {
            for(int i = 0; i < gltf.Buffers.Length; i++)
            {
                buffers.Add(glTFLoader.Interface.LoadBinaryBuffer(gltf, i, path));
            }

            if(gltf.Images != null)
            {
                for (int i = 0; i < gltf.Images.Length; i++)
                {
                    Stream stream = glTFLoader.Interface.OpenImageFile(gltf, i, path);

                    System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(stream);

                    images.Add(bmp);
                }
            }

            if(gltf.Materials != null)
            {
                for (int i = 0; i < gltf.Materials.Length; i++)
                {
                    GltfRhinoMaterialConverter converter = new GltfRhinoMaterialConverter(gltf, gltf.Materials[i], doc, this);
                    materials.Add(converter.Convert());
                }
            }

            for(int i = 0; i < gltf.Meshes.Length; i++)
            {
                GltfRhinoMeshConverter converter = new GltfRhinoMeshConverter(gltf, gltf.Meshes[i], this, doc);
                meshHolders.Add(converter.Convert());
            }

            ProcessHierarchy();

            return true;
        }

        private void ProcessHierarchy()
        {
            HashSet<int> children = new HashSet<int>();

            for (int i = 0; i < gltf.Nodes.Length; i++)
            {
                glTFLoader.Schema.Node node = gltf.Nodes[i];

                if(node.Children != null)
                {
                    foreach (int child in node.Children)
                    {
                        children.Add(child);
                    }
                }
            }

            List<int> parents = new List<int>();

            for(int i = 0; i < gltf.Nodes.Length; i++)
            {
                if(!children.Contains(i))
                {
                    parents.Add(i);
                }
            }

            foreach(int parentIndex in parents)
            {
                glTFLoader.Schema.Node parent = gltf.Nodes[parentIndex];

                AddNodeRecursive(parent, Rhino.Geometry.Transform.Identity);
            }

        }

        private void AddNodeRecursive(glTFLoader.Schema.Node node, Rhino.Geometry.Transform transform)
        {
            Rhino.Geometry.Transform finalTransform = transform * GetNodeTransform(node);

            if(node.Mesh.HasValue)
            {
                meshHolders[node.Mesh.Value].AddInstance(finalTransform);
            }

            if(node.Children != null)
            {
                foreach (int childIndex in node.Children)
                {
                    glTFLoader.Schema.Node child = gltf.Nodes[childIndex];

                    AddNodeRecursive(child, finalTransform);
                }
            }
        }

        Rhino.Geometry.Transform GetNodeTransform(glTFLoader.Schema.Node node)
        {
            Rhino.Geometry.Transform matrixTransform = GetMatrixTransform(node);

            if(!matrixTransform.IsIdentity)
            {
                return matrixTransform;
            }
            else
            {
                return GetTrsTransform(node);
            }
        }

        public Rhino.Geometry.Transform GetMatrixTransform(glTFLoader.Schema.Node node)
        {
            Rhino.Geometry.Transform xform = Rhino.Geometry.Transform.Identity;

            if (node.Matrix != null)
            {
                xform.M00 = node.Matrix[0];
                xform.M01 = node.Matrix[1];
                xform.M02 = node.Matrix[2];
                xform.M03 = node.Matrix[3];
                xform.M10 = node.Matrix[4];
                xform.M11 = node.Matrix[5];
                xform.M12 = node.Matrix[6];
                xform.M13 = node.Matrix[7];
                xform.M20 = node.Matrix[8];
                xform.M21 = node.Matrix[9];
                xform.M22 = node.Matrix[10];
                xform.M23 = node.Matrix[11];
                xform.M30 = node.Matrix[12];
                xform.M31 = node.Matrix[13];
                xform.M32 = node.Matrix[14];
                xform.M33 = node.Matrix[15];

                xform = xform.Transpose();
            }

            return xform;
        }

        public Rhino.Geometry.Transform GetTrsTransform(glTFLoader.Schema.Node node)
        {
            Rhino.Geometry.Vector3d translation = Rhino.Geometry.Vector3d.Zero;

            if (node.Translation != null && node.Translation.Length == 3)
            {
                translation.X = node.Translation[0];
                translation.Y = node.Translation[1];
                translation.Z = node.Translation[2];
            }

            Rhino.Geometry.Quaternion rotation = Rhino.Geometry.Quaternion.Identity;

            if (node.Rotation != null && node.Rotation.Length == 4)
            {
                rotation.A = node.Rotation[3];
                rotation.B = node.Rotation[0];
                rotation.C = node.Rotation[1];
                rotation.D = node.Rotation[2];
            }

            Rhino.Geometry.Vector3d scaling = Rhino.Geometry.Vector3d.Zero;

            if (node.Scale != null && node.Scale.Length == 3)
            {
                scaling.X = node.Scale[0];
                scaling.Y = node.Scale[1];
                scaling.Z = node.Scale[2];
            }

            Rhino.Geometry.Transform translationTransform = Rhino.Geometry.Transform.Translation(translation);

            rotation.GetRotation(out double angle, out Rhino.Geometry.Vector3d axis);

            Rhino.Geometry.Transform rotationTransform = Rhino.Geometry.Transform.Rotation(angle, axis, Rhino.Geometry.Point3d.Origin);

            Rhino.Geometry.Transform scalingTransform = Rhino.Geometry.Transform.Scale(Rhino.Geometry.Plane.WorldXY, scaling.X, scaling.Y, scaling.Z);

            return translationTransform * rotationTransform * scalingTransform;
        }

        public Rhino.Render.RenderTexture GetRenderTexture(int textureIndex)
        {
            Rhino.Render.RenderTexture renderTexture = null;

            if (gltf.Textures != null && textureIndex < gltf.Textures.Length && textureIndex >= 0)
            {
                glTFLoader.Schema.Texture texture = gltf.Textures[textureIndex];

                if (texture.Source != null)
                {
                    int imageIndex = texture.Source.Value;

                    System.Drawing.Bitmap bmp = images[imageIndex];

                    renderTexture = Rhino.Render.RenderTexture.NewBitmapTexture(bmp, doc);

                    renderTexture.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Program);

                    renderTexture.Name = GetUniqueName(texture.Name);

                    renderTexture.EndChange();
                }
            }

            return renderTexture;
        }

        public RhinoGltfMetallicRoughnessConverter GetMetallicRoughnessTexture(int imageIndex)
        {
            if(!metalRoughnessTextures.TryGetValue(imageIndex, out RhinoGltfMetallicRoughnessConverter converter))
            {
                System.Drawing.Bitmap bmp = images[imageIndex];

                converter = new RhinoGltfMetallicRoughnessConverter(bmp, doc);

                metalRoughnessTextures.Add(imageIndex, converter);
            }

            return converter;
        }

        public byte[] GetBuffer(int index)
        {
            return buffers[index];
        }

        public Rhino.Render.RenderMaterial GetMaterial(int index)
        {
            return materials[index];
        }

    }
}
