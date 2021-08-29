using glTFLoader.Schema;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace glTF_BinExporter
{
    public struct ObjectExportData
    {
        public Rhino.Geometry.Mesh[] Meshes;
        public RenderMaterial[] RenderMaterials;
        public RhinoObject Object;
    }

    class RhinoDocGltfConverter
    {
        public RhinoDocGltfConverter(glTFExportOptions options, bool binary, RhinoDoc doc, IEnumerable<RhinoObject> objects, LinearWorkflow workflow)
        {
            this.doc = doc;
            this.options = options;
            this.binary = binary;
            this.objects = objects;
            this.workflow = workflow;
        }

        public RhinoDocGltfConverter(glTFExportOptions options, bool binary, RhinoDoc doc, LinearWorkflow workflow)
        {
            this.doc = doc;
            this.options = options;
            this.binary = binary;
            this.objects = doc.Objects;
            this.workflow = null;
        }

        private RhinoDoc doc = null;

        private IEnumerable<RhinoObject> objects = null;

        private bool binary = false;
        private glTFExportOptions options = null;
        private LinearWorkflow workflow = null;

        private Dictionary<Guid, int> materialsMap = new Dictionary<Guid, int>();

        private gltfSchemaDummy dummy = new gltfSchemaDummy();

        private List<byte> binaryBuffer = new List<byte>();

        private Dictionary<int, Node> layers = new Dictionary<int, Node>();

        private Dictionary<int, int> layerMaterialIndices = new Dictionary<int, int>();

        private Dictionary<int, int> colorMaterials = new Dictionary<int, int>();

        private int defaultMaterialIndex = -1;

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

            if (options.UseDracoCompression)
            {
                dummy.ExtensionsUsed.Add(Constants.DracoMeshCompressionExtensionTag);
                dummy.ExtensionsRequired.Add(Constants.DracoMeshCompressionExtensionTag);
            }

            dummy.ExtensionsUsed.Add(Constants.MaterialsTransmissionExtensionTag);
            dummy.ExtensionsUsed.Add(Constants.MaterialsClearcoatExtensionTag);

            var sanitized = SanitizeRhinoObjects(objects);

            foreach (ObjectExportData exportData in sanitized)
            {
                int[] materialIndices = null;
                if (options.ExportMaterials) materialIndices = GetMaterials(exportData.RenderMaterials, exportData.Object);

                RhinoMeshGltfConverter meshConverter = new RhinoMeshGltfConverter(exportData, materialIndices, options, binary, dummy, binaryBuffer);
                int meshIndex = meshConverter.AddMesh();

                glTFLoader.Schema.Node node = new glTFLoader.Schema.Node()
                {
                    Mesh = meshIndex,
                    Name = GetObjectName(exportData.Object),
                };

                int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

                if (options.ExportLayers)
                {
                    AddToLayer(doc.Layers[exportData.Object.Attributes.LayerIndex], nodeIndex);
                }
                else
                {
                    dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
                }
            }

            if (binary && binaryBuffer.Count > 0)
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

        private void AddToLayer(Layer layer, int child)
        {
            if (layers.TryGetValue(layer.Index, out Node node))
            {
                if (node.Children == null)
                {
                    node.Children = new int[1] { child };
                }
                else
                {
                    node.Children = node.Children.Append(child).ToArray();
                }
            }
            else
            {
                node = new Node()
                {
                    Name = layer.Name,
                    Children = new int[1] { child },
                };

                layers.Add(layer.Index, node);
                int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);
                Layer parentLayer = doc.Layers.FindId(layer.ParentLayerId);

                if (parentLayer == null)
                {
                    dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
                }
                else
                {
                    AddToLayer(parentLayer, nodeIndex);
                }
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

        int[] GetMaterials(RenderMaterial[] materials, RhinoObject rhinoObject)
        {
            RhinoObject[] subObjects = rhinoObject.GetSubObjects();
            if (subObjects.Length == 0) subObjects = new RhinoObject[1] { rhinoObject };
            int[] materialIndices = new int[materials.Length];

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];

                if (material == null)
                {
                    if (options.UseDisplayColorForUnsetMaterials)
                    {
                        if(subObjects[i].Attributes.ColorSource == ObjectColorSource.ColorFromLayer)
                        {
                            materialIndices[i] = GetLayerMaterial(rhinoObject);
                        }
                        else
                        {
                            materialIndices[i] = GetColorMaterial(subObjects[i]);
                        }
                    }
                    else
                    {
                        materialIndices[i] = GetDefaultMaterial();
                    }
                }
                else
                {
                    Guid materialId = material.Id;
                    if (!materialsMap.TryGetValue(materialId, out int materialIndex))
                    {
                        RhinoMaterialGltfConverter materialConverter = new RhinoMaterialGltfConverter(options, binary, dummy, binaryBuffer, material, workflow);
                        materialIndex = materialConverter.AddMaterial();
                        materialsMap.Add(materialId, materialIndex);
                    }
                    materialIndices[i] = materialIndex;
                }
            }

            return materialIndices;
        }

        private int GetColorMaterial(RhinoObject rhinoObject)
        {
            Color objectColor = GetObjectColor(rhinoObject);
            int colorIndex = objectColor.ToArgb();
            if (!colorMaterials.TryGetValue(colorIndex, out int colorMaterialIndex))
            {
                colorMaterialIndex = CreateSolidColorMaterial(new Color4f(objectColor), GetColorName(objectColor));
                colorMaterials.Add(colorIndex, colorMaterialIndex);
            }
            return colorMaterialIndex;
        }

        private string GetColorName(Color color)
        {
            if (color.IsNamedColor)
            {
                return color.Name;
            }
            else
            {
                return color.ToString();
            }
        }

        private int GetDefaultMaterial()
        {
            if (defaultMaterialIndex == -1)
            {
                RenderMaterial material = Rhino.DocObjects.Material.DefaultMaterial.RenderMaterial;
                material.Name = "DefaultMaterial";
                RhinoMaterialGltfConverter materialConverter = new RhinoMaterialGltfConverter(options, binary, dummy, binaryBuffer, material, workflow);
                defaultMaterialIndex = materialConverter.AddMaterial();
            }
            return defaultMaterialIndex;
        }

        private int GetLayerMaterial(RhinoObject rhinoObject)
        {
            if (!layerMaterialIndices.TryGetValue(rhinoObject.Attributes.LayerIndex, out int layerMaterialIndex))
            {
                Color4f objectColor = new Color4f(GetLayerColor(rhinoObject));
                layerMaterialIndex = CreateSolidColorMaterial(objectColor, doc.Layers[rhinoObject.Attributes.LayerIndex].Name);
                layerMaterialIndices.Add(rhinoObject.Attributes.LayerIndex, layerMaterialIndex);
            }
            return layerMaterialIndex;
        }

        int CreateSolidColorMaterial(Color4f color, string name)
        {
            glTFLoader.Schema.Material material = new glTFLoader.Schema.Material()
            {
                PbrMetallicRoughness = new MaterialPbrMetallicRoughness()
                {
                    BaseColorFactor = color.ToFloatArray(),
                },
                Name = name
            };

            return dummy.Materials.AddAndReturnIndex(material);
        }

        Color GetObjectColor(RhinoObject rhinoObject)
        {
            if (rhinoObject.Attributes.ColorSource == ObjectColorSource.ColorFromLayer)
            {
                return GetLayerColor(rhinoObject);
            }
            else
            {
                return rhinoObject.Attributes.ObjectColor;
            }
        }

        Color GetLayerColor(RhinoObject rhinoObject)
        {
            int layerIndex = rhinoObject.Attributes.LayerIndex;
            return doc.Layers[layerIndex].Color;
        }

        public Rhino.Geometry.Mesh[] GetMeshes(RhinoObject rhinoObject)
        {

            if (rhinoObject.ObjectType == ObjectType.Mesh)
            {
                MeshObject meshObj = rhinoObject as MeshObject;

                return new Rhino.Geometry.Mesh[] { meshObj.MeshGeometry };
            }
            else if (rhinoObject.ObjectType == ObjectType.SubD)
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
            //Rhino.Geometry.Mesh[] meshes = rhinoObject.GetMeshes(Rhino.Geometry.MeshType.Preview);

            var subObjects = rhinoObject.GetSubObjects();
            if (subObjects.Length == 0) subObjects = new RhinoObject[1] { rhinoObject };
            Rhino.Geometry.Mesh[] meshes = new Rhino.Geometry.Mesh[subObjects.Length];

            for (int i = 0; i < subObjects.Length; i++)
            {
                var subMeshes = subObjects[i].GetMeshes(Rhino.Geometry.MeshType.Preview);
                if(subMeshes.Length == 0)
                {
                    subObjects[i].CreateMeshes(Rhino.Geometry.MeshType.Preview, Rhino.Geometry.MeshingParameters.FastRenderMesh, true);
                    subMeshes = subObjects[i].GetMeshes(Rhino.Geometry.MeshType.Preview);

                }
                meshes[i] = subMeshes[0];
            }

            List<Rhino.Geometry.Mesh> validMeshes = new List<Rhino.Geometry.Mesh>();

            foreach (Rhino.Geometry.Mesh mesh in meshes)
            {
                if (MeshIsValidForExport(mesh))
                {
                    mesh.EnsurePrivateCopy();
                    validMeshes.Add(mesh);
                }
                else
                {
                    validMeshes.Add(null);
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

            if (!options.ExportOpenMeshes && !mesh.IsClosed)
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

                // Need to get a Mesh from the None-mesh object. Using the FastRenderMesh here. Could be made configurable.
                // First make sure the internal rhino mesh has been created
                //rhinoObject.CreateMeshes(Rhino.Geometry.MeshType.Preview, Rhino.Geometry.MeshingParameters.FastRenderMesh, true);

                var isValidGeometry = Constants.ValidObjectTypes.Contains(rhinoObject.ObjectType);

                if (isValidGeometry && rhinoObject.ObjectType != ObjectType.InstanceReference)
                {
                    var mats = GetRenderMaterials(rhinoObject);
                    var meshes = GetMeshes(rhinoObject);

                    if (meshes.Length > 0) //Objects need a mesh to export
                    {
                        rhinoObjectsRes.Add(new ObjectExportData()
                        {
                            Meshes = meshes,
                            RenderMaterials = mats,
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
                        var mats = GetRenderMaterials(item.rhinoObject);
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
                                RenderMaterials = mats,
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

        private RenderMaterial[] GetRenderMaterials(RhinoObject rhinoObject)
        {
            var subObjects = rhinoObject.GetSubObjects();
            if (subObjects.Length == 0) subObjects = new RhinoObject[1] { rhinoObject };
            var mats = new RenderMaterial[subObjects.Length];

            var components = rhinoObject.SubobjectMaterialComponents;
            for (int i = 0; i < subObjects.Length; i++)
            {
                mats[i] = rhinoObject.RenderMaterial;
                foreach (var component in components)
                {
                    if (component.Index == i) mats[i] = rhinoObject.GetRenderMaterial(component);
                }
                if (mats[i] == null)
                {
                    mats[i] = doc.Layers[rhinoObject.Attributes.LayerIndex].RenderMaterial;
                }
            }

            return mats;
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
