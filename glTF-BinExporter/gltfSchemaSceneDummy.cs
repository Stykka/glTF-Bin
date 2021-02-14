using glTFLoader.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    class gltfSchemaSceneDummy
    {
        public List<int> Nodes = new List<int>();

        public string Name = null;

        public Dictionary<string, object> Extensions = new Dictionary<string, object>();

        private Extras Extras = null;

        public Scene ToSchemaGltf()
        {
            Scene scene = new Scene();

            scene.Nodes = this.Nodes.Count == 0 ? null : Nodes.ToArray();
            scene.Name = this.Name;
            scene.Extensions = this.Extensions;
            scene.Extras = this.Extras;

            return scene;
        }

    }
}
