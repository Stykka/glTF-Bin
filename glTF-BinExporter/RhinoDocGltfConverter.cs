using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.DocObjects;
using Rhino;
using glTFLoader.Schema;
using Rhino.Render;
using Rhino.Display;

namespace glTF_BinExporter
{
    class RhinoDocGltfConverter
    {
        public RhinoDocGltfConverter(glTFExportOptions options, bool binary, IEnumerable<RhinoObject> objects, LinearWorkflow workflow)
        {
            this.options = options;
            this.binary = binary;
            this.objects = objects;
            this.workflow = workflow;
        }

        public RhinoDocGltfConverter(glTFExportOptions options, bool binary, RhinoDoc doc, LinearWorkflow workflow)
        {
            this.options = options;
            this.binary = binary;
            this.objects = doc.Objects;
            this.workflow = null;
        }

        private IEnumerable<RhinoObject> objects = null;

        private bool binary = false;
        private glTFExportOptions options = null;
        private LinearWorkflow workflow = null;

        private Dictionary<Guid, int> materialsMap = new Dictionary<Guid, int>();

        private gltfSchemaDummy dummy = new gltfSchemaDummy();

        private List<byte> binaryBuffer = new List<byte>();

        public Gltf ConvertToGltf()
        {
            dummy.Scene = 0;
            dummy.Scenes.Add(new gltfSchemaSceneDummy());

            dummy.Asset = new Asset()
            {
                Version = "2.0",
            };

            dummy.Samplers.Add(new Sampler()
            {
                MinFilter = Sampler.MinFilterEnum.LINEAR,
                MagFilter = Sampler.MagFilterEnum.LINEAR,
                WrapS = Sampler.WrapSEnum.REPEAT,
                WrapT = Sampler.WrapTEnum.REPEAT,
            });

            if(options.UseDracoCompression)
            {
                dummy.ExtensionsUsed.Add(Constants.DracoMeshCompressionExtensionTag);
                dummy.ExtensionsRequired.Add(Constants.DracoMeshCompressionExtensionTag);
            }

            var sanitized = GlTFUtils.SanitizeRhinoObjects(objects);

            foreach(ObjectExportData exportData in sanitized)
            {
                int? materialIndex = GetMaterial(exportData.RenderMaterial, exportData.Object);

                RhinoMeshGltfConverter meshConverter = new RhinoMeshGltfConverter(exportData, materialIndex, options, binary, dummy, binaryBuffer);
                int meshIndex = meshConverter.AddMesh();

                glTFLoader.Schema.Node node = new glTFLoader.Schema.Node()
                {
                    Mesh = meshIndex,
                    Name = GetObjectName(exportData.Object),
                };

                int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

                dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
            }

            if(binary && binaryBuffer.Count > 0)
            {
                //have to add the empty buffer for the binary file header
                dummy.Buffers.Add(new glTFLoader.Schema.Buffer()
                {
                    ByteLength = (int)binaryBuffer.Count,
                    Uri = null,
                });
            }

            return dummy.ToSchemaGltf();
        }

        public string GetObjectName(RhinoObject rhinoObject)
        {
            return string.IsNullOrEmpty(rhinoObject.Name) ? null : rhinoObject.Name;
        }

        public byte[] GetBinaryBuffer()
        {
            return binaryBuffer.ToArray();
        }

        int? GetMaterial(RenderMaterial material, RhinoObject rhinoObject)
        {
            if(!options.ExportMaterials)
            {
                return null;
            }
            
            if(material == null && options.UseDisplayColorForUnsetMaterials)
            {
                Color4f objectColor = GetObjectColor(rhinoObject);
                return CreateSolidColorMaterial(objectColor);
            }
            else if(material == null)
            {
                material = Rhino.DocObjects.Material.DefaultMaterial.RenderMaterial;
            }

            Guid materialId = material.Id;

            if(!materialsMap.TryGetValue(materialId, out int materialIndex))
            {
                RhinoMaterialGltfConverter materialConverter = new RhinoMaterialGltfConverter(options, binary, dummy, binaryBuffer, material, workflow);
                materialIndex = materialConverter.AddMaterial();
                materialsMap.Add(materialId, materialIndex);
            }

            return materialIndex;
        }

        int CreateSolidColorMaterial(Color4f color)
        {
            glTFLoader.Schema.Material material = new glTFLoader.Schema.Material()
            {
                PbrMetallicRoughness = new MaterialPbrMetallicRoughness()
                {
                    BaseColorFactor = color.ToFloatArray(),
                }
            };

            return dummy.Materials.AddAndReturnIndex(material);
        }

        Color4f GetObjectColor(RhinoObject rhinoObject)
        {
            if(rhinoObject.Attributes.ColorSource == ObjectColorSource.ColorFromLayer)
            {
                int layerIndex = rhinoObject.Attributes.LayerIndex;

                return new Color4f(rhinoObject.Document.Layers[layerIndex].Color);
            }
            else
            {
                return new Color4f(rhinoObject.Attributes.ObjectColor);
            }
        }

    }
}
