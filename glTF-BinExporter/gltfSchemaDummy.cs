using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using glTFLoader.Schema;

namespace glTF_BinExporter
{
    /// <summary>
    /// Helper class to convert to the serializeable class.
    /// Primarily just makes things lists so appending is easier.
    /// </summary>
    class gltfSchemaDummy
    {
        public List<string> ExtensionsUsed = new List<string>();

        public List<string> ExtensionsRequired = new List<string>();

        public List<Accessor> Accessors = new List<Accessor>();

        public List<Animation> Animations = new List<Animation>();

        public Asset Asset = null;

        public List<Buffer> Buffers = new List<Buffer>();

        public List<BufferView> BufferViews = new List<BufferView>();

        public List<Camera> Cameras = new List<Camera>();

        public List<Image> Images = new List<Image>();

        public List<Material> Materials = new List<Material>();

        public List<Mesh> Meshes = new List<Mesh>();

        public List<Node> Nodes = new List<Node>();

        public List<Sampler> Samplers = new List<Sampler>();

        public int Scene = 0;

        public List<gltfSchemaSceneDummy> Scenes = new List<gltfSchemaSceneDummy>();

        public List<Skin> Skins = new List<Skin>();

        public List<Texture> Textures = new List<Texture>();

        public Dictionary<string, object> Extensions = new Dictionary<string, object>();

        public Extras Extras = null;

        public Gltf ToSchemaGltf()
        {
            Gltf gltf = new Gltf();

            gltf.ExtensionsUsed = this.ExtensionsUsed.Count == 0 ? null : this.ExtensionsUsed.ToArray();
            gltf.ExtensionsRequired = this.ExtensionsRequired.Count == 0 ? null : this.ExtensionsRequired.ToArray();
            gltf.Accessors = this.Accessors.Count == 0 ? null : this.Accessors.ToArray();
            gltf.Animations = this.Animations.Count == 0 ? null : this.Animations.ToArray();

            gltf.Asset = this.Asset;

            gltf.Buffers = this.Buffers.Count == 0 ? null : this.Buffers.ToArray();
            gltf.BufferViews = this.BufferViews.Count == 0 ? null : this.BufferViews.ToArray();
            gltf.Cameras = this.Cameras.Count == 0 ? null : this.Cameras.ToArray();
            gltf.Images = this.Images.Count == 0 ? null : this.Images.ToArray();
            gltf.Materials = this.Materials.Count == 0 ? null : this.Materials.ToArray();
            gltf.Meshes = this.Meshes.Count == 0 ? null : this.Meshes.ToArray();
            gltf.Nodes = this.Nodes.Count == 0 ? null : this.Nodes.ToArray();
            gltf.Samplers = this.Samplers.Count == 0 ? null : this.Samplers.ToArray();

            gltf.Scene = this.Scene;

            gltf.Scenes = this.Scenes.Count == 0 ? null : ConvertScenes();

            gltf.Skins = this.Skins.Count == 0 ? null : this.Skins.ToArray();
            gltf.Textures = this.Textures.Count == 0 ? null : this.Textures.ToArray();
            gltf.Extensions = this.Extensions;
            gltf.Extras = this.Extras;

            return gltf;
        }

        private Scene[] ConvertScenes()
        {
            List<Scene> scenes =  new List<Scene>();

            foreach(gltfSchemaSceneDummy dummy in Scenes)
            {
                scenes.Add(dummy.ToSchemaGltf());
            }

            return scenes.ToArray();
        }

    }
}
