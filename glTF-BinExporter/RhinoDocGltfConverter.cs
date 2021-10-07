using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.DocObjects;
using Rhino;
using Rhino.Render;
using Rhino.Display;

namespace glTF_BinExporter
{
    public class ObjectExportData
    {
        public Rhino.Geometry.Mesh[] Meshes = null;
        public Rhino.Geometry.Transform Transform = Rhino.Geometry.Transform.Identity;
        public RenderMaterial RenderMaterial = null;
        public RhinoObject Object = null;
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

        private Dictionary<int, glTFLoader.Schema.Node> layers = new Dictionary<int, glTFLoader.Schema.Node>();

        private RenderMaterial defaultMaterial = null;
        private RenderMaterial DefaultMaterial
        {
            get
            {
                if(defaultMaterial == null)
                {
                    defaultMaterial = Rhino.DocObjects.Material.DefaultMaterial.RenderMaterial;
                }

                return defaultMaterial;
            }
        }
        public glTFLoader.Schema.Gltf ConvertToGltf()
        {
            dummy.Scene = 0;
            dummy.Scenes.Add(new gltfSchemaSceneDummy());

            dummy.Asset = new glTFLoader.Schema.Asset()
            {
                Version = "2.0",
            };

            dummy.Samplers.Add(new glTFLoader.Schema.Sampler()
            {
                MinFilter = glTFLoader.Schema.Sampler.MinFilterEnum.LINEAR,
                MagFilter = glTFLoader.Schema.Sampler.MagFilterEnum.LINEAR,
                WrapS = glTFLoader.Schema.Sampler.WrapSEnum.REPEAT,
                WrapT = glTFLoader.Schema.Sampler.WrapTEnum.REPEAT,
            });

            if(options.UseDracoCompression)
            {
                dummy.ExtensionsUsed.Add(glTFExtensions.KHR_draco_mesh_compression.Tag);
                dummy.ExtensionsRequired.Add(glTFExtensions.KHR_draco_mesh_compression.Tag);
            }

            dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_transmission.Tag);
            dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_clearcoat.Tag);
            dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_ior.Tag);
            dummy.ExtensionsUsed.Add(glTFExtensions.KHR_materials_specular.Tag);

            IEnumerable<Rhino.DocObjects.RhinoObject> pointClouds = objects.Where(x => x.ObjectType == ObjectType.PointSet);

            foreach(Rhino.DocObjects.RhinoObject rhinoObject in pointClouds)
            {
                RhinoPointCloudGltfConverter converter = new RhinoPointCloudGltfConverter(rhinoObject, options, binary, dummy, binaryBuffer);
                int meshIndex = converter.AddPointCloud();

                if(meshIndex != -1)
                {
                    glTFLoader.Schema.Node node = new glTFLoader.Schema.Node()
                    {
                        Mesh = meshIndex,
                        Name = GetObjectName(rhinoObject),
                    };

                    int nodeIndex = dummy.Nodes.AddAndReturnIndex(node);

                    AddNode(nodeIndex, rhinoObject);
                }
            }

            List<ObjectExportData> sanitized = SanitizeRhinoObjects(objects);

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

                AddNode(nodeIndex, exportData.Object);
            }

            if (binary && binaryBuffer.Count > 0)
            {
                //have to add the empty buffer for the binary file header
                dummy.Buffers.Add(new glTFLoader.Schema.Buffer()
                {
                    ByteLength = binaryBuffer.Count,
                    Uri = null,
                });
            }

            return dummy.ToSchemaGltf();
        }

        private void AddNode(int nodeIndex, Rhino.DocObjects.RhinoObject rhinoObject)
        {
            if (options.ExportLayers)
            {
                AddToLayer(doc.Layers[rhinoObject.Attributes.LayerIndex], nodeIndex);
            }
            else
            {
                dummy.Scenes[dummy.Scene].Nodes.Add(nodeIndex);
            }
        }

        private void AddToLayer(Layer layer, int child)
        {
            if(layers.TryGetValue(layer.Index, out glTFLoader.Schema.Node node))
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
                node = new glTFLoader.Schema.Node()
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
            return string.IsNullOrEmpty(rhinoObject.Name) ? rhinoObject.Id.ToString() : rhinoObject.Name;
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
                material = DefaultMaterial;
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
                PbrMetallicRoughness = new glTFLoader.Schema.MaterialPbrMetallicRoughness()
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

        public List<ObjectExportData> SanitizeRhinoObjects(IEnumerable<RhinoObject> rhinoObjects)
        {
            List<ObjectExportData> explodedObjects = new List<ObjectExportData>();

            foreach (var rhinoObject in rhinoObjects)
            {
                if (rhinoObject.ObjectType == ObjectType.InstanceReference && rhinoObject is InstanceObject instanceObject)
                {
                    List<RhinoObject> pieces = new List<RhinoObject>();
                    List<Rhino.Geometry.Transform> transforms = new List<Rhino.Geometry.Transform>();

                    ExplodeRecursive(instanceObject, Rhino.Geometry.Transform.Identity, pieces, transforms);

                    foreach (var item in pieces.Zip(transforms, (rObj, trans) => (rhinoObject: rObj, trans)))
                    {
                        explodedObjects.Add(new ObjectExportData()
                        {
                            Object = item.rhinoObject,
                            Transform = item.trans,
                            RenderMaterial = GetObjectMaterial(item.rhinoObject),
                        });
                    }
                }
                else
                {
                    explodedObjects.Add(new ObjectExportData()
                    {
                        Object = rhinoObject,
                        RenderMaterial = GetObjectMaterial(rhinoObject),
                    });
                }
            }

            //Remove Unmeshable
            explodedObjects.RemoveAll(x => !x.Object.IsMeshable(Rhino.Geometry.MeshType.Any));

            foreach (var item in explodedObjects)
            {
                //Mesh

                if (item.Object.ObjectType == ObjectType.SubD && item.Object.Geometry is Rhino.Geometry.SubD subd)
                {
                    if (options.SubDExportMode == SubDMode.ControlNet)
                    {
                        Rhino.Geometry.Mesh mesh = Rhino.Geometry.Mesh.CreateFromSubDControlNet(subd);

                        mesh.Transform(item.Transform);

                        item.Meshes = new Rhino.Geometry.Mesh[] { mesh };
                    }
                    else
                    {
                        int level = options.SubDLevel;

                        Rhino.Geometry.Mesh mesh = Rhino.Geometry.Mesh.CreateFromSubD(subd, level);

                        mesh.Transform(item.Transform);

                        item.Meshes = new Rhino.Geometry.Mesh[] { mesh };
                    }
                }
                else
                {
                    Rhino.Geometry.MeshingParameters parameters = item.Object.GetRenderMeshParameters();

                    if (item.Object.MeshCount(Rhino.Geometry.MeshType.Render, parameters) == 0)
                    {
                        item.Object.CreateMeshes(Rhino.Geometry.MeshType.Render, parameters, false);
                    }

                    List<Rhino.Geometry.Mesh> meshes = new List<Rhino.Geometry.Mesh>(item.Object.GetMeshes(Rhino.Geometry.MeshType.Render));

                    foreach (Rhino.Geometry.Mesh mesh in meshes)
                    {
                        mesh.EnsurePrivateCopy();
                        mesh.Transform(item.Transform);
                    }

                    //Remove bad meshes
                    meshes.RemoveAll(x => x == null || !MeshIsValidForExport(x));

                    item.Meshes = meshes.ToArray();
                }
            }

            //Remove meshless objects
            explodedObjects.RemoveAll(x => x.Meshes.Length == 0);

            return explodedObjects;
        }

        private RenderMaterial GetObjectMaterial(Rhino.DocObjects.RhinoObject rhinoObject)
        {
            ObjectMaterialSource source = rhinoObject.Attributes.MaterialSource;

            Rhino.Render.RenderMaterial renderMaterial = null;

            if(source == ObjectMaterialSource.MaterialFromObject)
            {
                renderMaterial = rhinoObject.RenderMaterial;
            }
            else if (source == ObjectMaterialSource.MaterialFromLayer)
            {
                int layerIndex = rhinoObject.Attributes.LayerIndex;

                renderMaterial = GetLayerMaterial(layerIndex);
            }

            return renderMaterial;
        }

        private Rhino.Render.RenderMaterial GetLayerMaterial(int layerIndex)
        {
            if(layerIndex < 0 || layerIndex >= doc.Layers.Count)
            {
                return null;
            }

            return doc.Layers[layerIndex].RenderMaterial;
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
