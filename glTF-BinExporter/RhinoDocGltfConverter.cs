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
    public struct ObjectExportData
    {
        public Rhino.Geometry.Mesh[] Meshes;
        public RenderMaterial RenderMaterial;
        public RhinoObject Object;
    }

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
            dummy.Scenes.Add(new gltfSchemaSceneDummy() { Name = RhinoDoc.ActiveDoc.Name });

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

            dummy.ExtensionsUsed.Add(Constants.MaterialsTransmissionExtensionTag);
            dummy.ExtensionsUsed.Add(Constants.MaterialsClearcoatExtensionTag);

            var sanitized = SanitizeRhinoObjects(objects);

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

                //dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
                AddToLayer(RhinoDoc.ActiveDoc.Layers[exportData.Object.Attributes.LayerIndex], nodeIndex);

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

        Dictionary<int, Node> Layers = new Dictionary<int, Node>();

        private void AddToLayer(Layer layer, int child)
        {
            if(Layers.TryGetValue(layer.Index, out Node node))
            {
                if (node.Children == null) node.Children = new int[1] { child };
                else node.Children = node.Children.Append(child).ToArray();
            }
            else
            {
                node = new Node();
                node.Name = layer.Name;
                node.Children = new int[1] { child };
                Layers.Add(layer.Index, node);
                int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);
                Layer parentLayer = RhinoDoc.ActiveDoc.Layers.FindId(layer.ParentLayerId);
                if (parentLayer == null) dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
                else AddToLayer(parentLayer, nodeIndex);
            }
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

        public Rhino.Geometry.Mesh[] GetMeshes(RhinoObject rhinoObject)
        {

            if (rhinoObject.ObjectType == ObjectType.Mesh)
            {
                MeshObject meshObj = rhinoObject as MeshObject;

                return new Rhino.Geometry.Mesh[] { meshObj.MeshGeometry };
            }
            else if(rhinoObject.ObjectType == ObjectType.SubD)
            {
                SubDObject subdObject = rhinoObject as SubDObject;

                Rhino.Geometry.SubD subd = subdObject.Geometry as Rhino.Geometry.SubD;

                Rhino.Geometry.Mesh mesh = null;

                if (options.SubDExportMode == SubDMode.ControlNet)
                {
                    mesh = Rhino.Geometry.Mesh.CreateFromSubDControlNet(subd);
                }
                else
                {
                    int level = options.SubDLevel;

                    mesh = Rhino.Geometry.Mesh.CreateFromSubD(subd, level);
                }

                return new Rhino.Geometry.Mesh[] { mesh };
            }

            // Need to get a Mesh from the None-mesh object. Using the FastRenderMesh here. Could be made configurable.
            // First make sure the internal rhino mesh has been created
            rhinoObject.CreateMeshes(Rhino.Geometry.MeshType.Preview, Rhino.Geometry.MeshingParameters.FastRenderMesh, true);

            // Then get the internal rhino meshes
            Rhino.Geometry.Mesh[] meshes = rhinoObject.GetMeshes(Rhino.Geometry.MeshType.Preview);

            List<Rhino.Geometry.Mesh> validMeshes = new List<Rhino.Geometry.Mesh>();

            foreach (Rhino.Geometry.Mesh mesh in meshes)
            {
                if (MeshIsValidForExport(mesh))
                {
                    mesh.EnsurePrivateCopy();
                    validMeshes.Add(mesh);
                }
            }

            return validMeshes.ToArray();
        }

        public bool MeshIsValidForExport(Rhino.Geometry.Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            if (mesh.Vertices.Count == 0)
            {
                return false;
            }

            if (mesh.Faces.Count == 0)
            {
                return false;
            }

            if(!options.ExportOpenMeshes && !mesh.IsClosed)
            {
                return false;
            }

            return true;
        }

        private string GetDebugName(RhinoObject rhinoObject)
        {
            if (string.IsNullOrEmpty(rhinoObject.Name))
            {
                return "(Unnamed)";
            }

            return rhinoObject.Name;
        }

        public List<ObjectExportData> SanitizeRhinoObjects(IEnumerable<RhinoObject> rhinoObjects)
        {
            var rhinoObjectsRes = new List<ObjectExportData>();

            foreach (var rhinoObject in rhinoObjects)
            {
                if (!rhinoObject.IsMeshable(Rhino.Geometry.MeshType.Any))
                {
                    RhinoApp.WriteLine("Skipping " + GetDebugName(rhinoObject) + ", object is not meshable. Object is a " + rhinoObject.ObjectType.ToString());
                    continue;
                }

                var mat = rhinoObject.RenderMaterial;

                var isValidGeometry = Constants.ValidObjectTypes.Contains(rhinoObject.ObjectType);

                if (isValidGeometry && rhinoObject.ObjectType != ObjectType.InstanceReference)
                {
                    var meshes = GetMeshes(rhinoObject);

                    if (meshes.Length > 0) //Objects need a mesh to export
                    {
                        rhinoObjectsRes.Add(new ObjectExportData()
                        {
                            Meshes = meshes,
                            RenderMaterial = mat,
                            Object = rhinoObject,
                        });
                    }
                }
                else if (rhinoObject.ObjectType == ObjectType.InstanceReference)
                {
                    InstanceObject instanceObject = rhinoObject as InstanceObject;

                    List<RhinoObject> objects = new List<RhinoObject>();
                    List<Rhino.Geometry.Transform> transforms = new List<Rhino.Geometry.Transform>();

                    ExplodeRecursive(instanceObject, instanceObject.InstanceXform, objects, transforms);

                    // Transform the exploded geo into its correct place
                    foreach (var item in objects.Zip(transforms, (rObj, trans) => (rhinoObject: rObj, trans)))
                    {
                        var meshes = GetMeshes(item.rhinoObject);

                        foreach (var mesh in meshes)
                        {
                            mesh.Transform(item.trans);
                        }

                        if (meshes.Length > 0) //Objects need a mesh to export
                        {
                            rhinoObjectsRes.Add(new ObjectExportData()
                            {
                                Meshes = meshes,
                                RenderMaterial = mat,
                                Object = item.rhinoObject,
                            });
                        }
                    }
                }
                else
                {
                    RhinoApp.WriteLine("Unknown geometry type encountered.");
                }
            }

            return rhinoObjectsRes;
        }

        private void ExplodeRecursive(InstanceObject instanceObject, Rhino.Geometry.Transform instanceTransform, List<RhinoObject> pieces, List<Rhino.Geometry.Transform> transforms)
        {
            for (int i = 0; i < instanceObject.InstanceDefinition.ObjectCount; i++)
            {
                RhinoObject rhinoObject = instanceObject.InstanceDefinition.Object(i);

                if (rhinoObject is InstanceObject nestedObject)
                {
                    Rhino.Geometry.Transform nestedTransform = instanceTransform * nestedObject.InstanceXform;

                    ExplodeRecursive(nestedObject, nestedTransform, pieces, transforms);
                }
                else
                {
                    pieces.Add(rhinoObject);

                    transforms.Add(instanceTransform);
                }
            }
        }
    }
}
