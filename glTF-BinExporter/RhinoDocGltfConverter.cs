using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.DocObjects;
using Rhino;
using Rhino.FileIO;
using glTFLoader.Schema;
using Rhino.Geometry;
using Rhino.Render;
using Rhino.Display;
using System.Drawing;

namespace glTF_BinExporter
{
    class RhinoDocGltfConverter
    {
        public RhinoDocGltfConverter(glTFExportOptions options, IEnumerable<RhinoObject> objects)
        {
            this.options = options;
            this.objects = objects;
        }

        public RhinoDocGltfConverter(glTFExportOptions options, RhinoDoc doc)
        {
            this.options = options;
            this.objects = doc.Objects;
        }

        private IEnumerable<RhinoObject> objects = null;
        private glTFExportOptions options = null;

        private Dictionary<Guid, int> materialsMap = new Dictionary<Guid, int>();

        private gltfSchemaDummy dummy = new gltfSchemaDummy();

        private MemoryStream stream = new MemoryStream();

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

            var sanitized = GlTFUtils.SanitizeRhinoObjects(objects);

            foreach(Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject> tuple in sanitized)
            {
                if(options.UseBinary)
                {
                    AddRhinoObjectBinary(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
                }
                else
                {
                    AddRhinoObjectText(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
                }
            }

            if(options.UseBinary)
            {
                //have to add the empty buffer for the binary file header
                dummy.Buffers.Add(new glTFLoader.Schema.Buffer()
                {
                    ByteLength = (int)stream.Length,
                    Uri = null,
                });
            }

            return dummy.ToSchemaGltf();
        }

        public byte[] GetBinaryBuffer()
        {
            return stream.ToArray();
        }

        private void AddRhinoObjectBinary(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material, Guid materialId, RhinoObject rhinoObject)
        {
            int materialIndex = GetMaterial(material, materialId);

            var primitives = new List<MeshPrimitive>();

            foreach (var rhinoMesh in rhinoMeshes)
            {
                rhinoMesh.Faces.ConvertQuadsToTriangles();

                Point3d vtxMin = Point3d.Origin;
                Point3d vtxMax = Point3d.Origin;

                byte[] verticesBytes = GetVerticesBytes(rhinoMesh.Vertices, out vtxMin, out vtxMax);
                int verticesByteLength = verticesBytes.Length;
                int verticesOffset = (int)stream.Position;
                stream.Write(verticesBytes, 0, verticesByteLength);

                int indicesCount = 0;

                byte[] indicesBytes = GetIndicesBytes(rhinoMesh.Faces, out indicesCount);
                int indicesBytesLength = indicesBytes.Length;
                int indicesOffset = (int)stream.Position;
                stream.Write(indicesBytes, 0, indicesBytesLength);

                Vector3f normalsMin = Vector3f.Zero;
                Vector3f normalsMax = Vector3f.Zero;

                byte[] normalsBytes = GetNormalsBytes(rhinoMesh.Normals, out normalsMin, out normalsMax);
                int normalsBytesLength = normalsBytes.Length;
                int normalsOffset = (int)stream.Position;
                stream.Write(normalsBytes, 0, normalsBytesLength);

                Point2f texCoordsMin = new Point2f(0.0f, 0.0f);
                Point2f texCoordsMax = new Point2f(0.0f, 0.0f);

                byte[] texCoordsBytes = GetTextureCoordinatesBytes(rhinoMesh.TextureCoordinates, out texCoordsMin, out texCoordsMax);
                int texCoordsBytesLength = texCoordsBytes.Length;
                int texCoordsOffset = (int)stream.Position;
                stream.Write(texCoordsBytes, 0, texCoordsBytesLength);

                var vtxBufferView = new BufferView()
                {
                    Buffer = 0,
                    ByteOffset = verticesOffset,
                    ByteLength = verticesByteLength,
                    Target = BufferView.TargetEnum.ARRAY_BUFFER,
                };

                int vtxBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(vtxBufferView);

                var idsBufferView = new BufferView()
                {
                    Buffer = 0,
                    ByteOffset = indicesOffset,
                    ByteLength = indicesBytesLength,
                    Target = BufferView.TargetEnum.ELEMENT_ARRAY_BUFFER,
                };

                int idsBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(idsBufferView);

                BufferView normalsBufferView = new BufferView()
                {
                    Buffer = 0,
                    ByteOffset = normalsOffset,
                    ByteLength = normalsBytesLength,
                    Target = BufferView.TargetEnum.ARRAY_BUFFER,
                };

                int normalsBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(normalsBufferView);

                BufferView texCoordsBufferView = new BufferView()
                {
                    Buffer = 0,
                    ByteOffset = texCoordsOffset,
                    ByteLength = texCoordsBytesLength,
                    Target = BufferView.TargetEnum.ARRAY_BUFFER,
                };

                int texCoordsBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(texCoordsBufferView);

                // Create accessors	
                Accessor vtxAccessor = new Accessor()
                {
                    BufferView = vtxBufferViewIdx,
                    Count = rhinoMesh.Vertices.Count,
                    Min = new float[] { (float)vtxMin.X, (float)vtxMin.Y, (float)vtxMin.Z },
                    Max = new float[] { (float)vtxMax.X, (float)vtxMax.Y, (float)vtxMax.Z },
                    Type = Accessor.TypeEnum.VEC3,
                    ComponentType = Accessor.ComponentTypeEnum.FLOAT,
                    ByteOffset = 0,
                };

                int vtxAccessorIdx = dummy.Accessors.AddAndReturnIndex(vtxAccessor);

                Accessor idsAccessor = new Accessor()
                {
                    BufferView = idsBufferViewIdx,
                    Count = indicesCount,
                    Min = new float[] { 0 },
                    Max = new float[] { rhinoMesh.Vertices.Count - 1 },
                    Type = Accessor.TypeEnum.SCALAR,
                    ComponentType = Accessor.ComponentTypeEnum.UNSIGNED_INT,
                    ByteOffset = 0,
                };

                int idsAccessorIdx = dummy.Accessors.AddAndReturnIndex(idsAccessor);

                Accessor normalsAccessor = new Accessor()
                {
                    BufferView = normalsBufferViewIdx,
                    Count = rhinoMesh.Normals.Count,
                    Min = new float[] { normalsMin.X, normalsMin.Y, normalsMin.Z },
                    Max = new float[] { normalsMax.X, normalsMax.Y, normalsMax.Z },
                    Type = Accessor.TypeEnum.VEC3,
                    ComponentType = Accessor.ComponentTypeEnum.FLOAT,
                    ByteOffset = 0,
                };

                int normalsAccessorIdx = dummy.Accessors.AddAndReturnIndex(normalsAccessor);

                Accessor texCoordsAccessor = new Accessor()
                {
                    BufferView = texCoordsBufferViewIdx,
                    Count = rhinoMesh.TextureCoordinates.Count,
                    Min = new float[] { texCoordsMin.X, texCoordsMin.Y },
                    Max = new float[] { texCoordsMax.X, texCoordsMax.Y },
                    Type = Accessor.TypeEnum.VEC2,
                    ComponentType = Accessor.ComponentTypeEnum.FLOAT,
                    ByteOffset = 0,
                };

                int texCoordsAccessorIdx = dummy.Accessors.AddAndReturnIndex(texCoordsAccessor);

                var primitive = new MeshPrimitive()
                {
                    Attributes = new Dictionary<string, int>()
                    {
                        { Constants.PositionAttributeTag, vtxAccessorIdx },
                        { Constants.NormalAttributeTag, normalsAccessorIdx },
                        { Constants.TexCoord0AttributeTag, texCoordsAccessorIdx },
                    },
                    Indices = idsAccessorIdx,
                    Material = materialIndex,
                };

                // Create mesh	
                primitives.Add(primitive);
            }

            var mesh = new glTFLoader.Schema.Mesh()
            {
                Primitives = primitives.ToArray()
            };
            int idxMesh = dummy.Meshes.AddAndReturnIndex(mesh);

            var node = new Node()
            {
                Mesh = idxMesh,
                Name = string.IsNullOrEmpty(rhinoObject.Name) ? null : rhinoObject.Name,
            };

            int idxNode = dummy.Nodes.AddAndReturnIndex(node);

            dummy.Scenes[dummy.Scene].Nodes.Add(idxNode);
        }

        private void AddRhinoObjectText(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material, Guid materialId, RhinoObject rhinoObject)
        {
            int materialIndex = GetMaterial(material, materialId);

            var primitives = new List<MeshPrimitive>();	

            foreach (var rhinoMesh in rhinoMeshes)	
            {
                rhinoMesh.Faces.ConvertQuadsToTriangles();

                Point3d vtxMin = Point3d.Origin;
                Point3d vtxMax = Point3d.Origin;

                var vtxBuffer = CreateVerticesBuffer(rhinoMesh.Vertices, out vtxMin, out vtxMax);
                int vtxBufferIdx = dummy.Buffers.AddAndReturnIndex(vtxBuffer);

                int indicesCount = 0;

                var idsBuffer = CreateIndicesBuffer(rhinoMesh.Faces, out indicesCount);
                int idsBufferIdx = dummy.Buffers.AddAndReturnIndex(idsBuffer);

                Vector3f normalsMin = Vector3f.Zero;
                Vector3f normalsMax = Vector3f.Zero;

                var normalsBuffer = CreateNormalsBuffer(rhinoMesh.Normals, out normalsMin, out normalsMax);	
                int normalsBufferIdx = dummy.Buffers.AddAndReturnIndex(normalsBuffer);

                Point2f texCoordsMin = new Point2f(0.0f, 0.0f);
                Point2f texCoordsMax = new Point2f(0.0f, 0.0f);

                var texCoordsBuffer = CreateTextureCoordinatesBuffer(rhinoMesh.TextureCoordinates, out texCoordsMin, out texCoordsMax);
                int texCoordsBufferIdx = dummy.Buffers.AddAndReturnIndex(texCoordsBuffer);	
	
                var vtxBufferView = new BufferView()
                {
                    Buffer = vtxBufferIdx,
                    ByteOffset = 0,
                    ByteLength = vtxBuffer.ByteLength,
                    Target = BufferView.TargetEnum.ARRAY_BUFFER,
                };

                int vtxBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(vtxBufferView);

                var idsBufferView = new BufferView()
                {
                    Buffer = idsBufferIdx,
                    ByteOffset = 0,
                    ByteLength = idsBuffer.ByteLength,
                    Target = BufferView.TargetEnum.ELEMENT_ARRAY_BUFFER,
                };
                
                int idsBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(idsBufferView);

                BufferView normalsBufferView = new BufferView()	
                {
                    Buffer = normalsBufferIdx,	
                    ByteOffset = 0,	
                    ByteLength = normalsBuffer.ByteLength,	
                    Target = BufferView.TargetEnum.ARRAY_BUFFER,
                };

                int normalsBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(normalsBufferView);	

                BufferView texCoordsBufferView = new BufferView()
                {
                    Buffer = texCoordsBufferIdx,	
                    ByteOffset = 0,	
                    ByteLength = texCoordsBuffer.ByteLength,	
                    Target = BufferView.TargetEnum.ARRAY_BUFFER,
                };

                int texCoordsBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(texCoordsBufferView);

                // Create accessors	
                Accessor vtxAccessor = new Accessor()	
                {	
                    BufferView = vtxBufferViewIdx,	
                    Count = rhinoMesh.Vertices.Count,	
                    Min = new float[] { (float)vtxMin.X, (float)vtxMin.Y, (float)vtxMin.Z },	
                    Max = new float[] { (float)vtxMax.X, (float)vtxMax.Y, (float)vtxMax.Z },
                    Type = Accessor.TypeEnum.VEC3,
                    ComponentType = Accessor.ComponentTypeEnum.FLOAT,
                    ByteOffset = 0,
                };

                int vtxAccessorIdx = dummy.Accessors.AddAndReturnIndex(vtxAccessor);

                Accessor idsAccessor = new Accessor()	
                {
                    BufferView = idsBufferViewIdx,
                    Count = indicesCount,
                    Min = new float[] { 0 },	
                    Max = new float[] { rhinoMesh.Vertices.Count - 1 },	
                    Type = Accessor.TypeEnum.SCALAR,
                    ComponentType = Accessor.ComponentTypeEnum.UNSIGNED_INT,
                    ByteOffset = 0,
                };	

                int idsAccessorIdx = dummy.Accessors.AddAndReturnIndex(idsAccessor);

                Accessor normalsAccessor = new Accessor()
                {
                    BufferView = normalsBufferViewIdx,
                    Count = rhinoMesh.Normals.Count,
                    Min = new float[] { normalsMin.X, normalsMin.Y, normalsMin.Z },
                    Max = new float[] { normalsMax.X, normalsMax.Y, normalsMax.Z },
                    Type = Accessor.TypeEnum.VEC3,
                    ComponentType = Accessor.ComponentTypeEnum.FLOAT,
                    ByteOffset = 0,
                };

                int normalsAccessorIdx = dummy.Accessors.AddAndReturnIndex(normalsAccessor);

                Accessor texCoordsAccessor = new Accessor()	
                {	
                    BufferView = texCoordsBufferViewIdx,	
                    Count = rhinoMesh.TextureCoordinates.Count,
                    Min = new float[] { texCoordsMin.X, texCoordsMin.Y },	
                    Max = new float[] { texCoordsMax.X, texCoordsMax.Y },
                    Type = Accessor.TypeEnum.VEC2,
                    ComponentType = Accessor.ComponentTypeEnum.FLOAT,
                    ByteOffset = 0,
                };

                int texCoordsAccessorIdx = dummy.Accessors.AddAndReturnIndex(texCoordsAccessor);	

                var primitive = new MeshPrimitive()
                {
                    Attributes = new Dictionary<string, int>()
                    {
                        { Constants.PositionAttributeTag, vtxAccessorIdx },
                        { Constants.NormalAttributeTag, normalsAccessorIdx },
                        { Constants.TexCoord0AttributeTag, texCoordsAccessorIdx },
                    },
                    Indices = idsAccessorIdx,
                    Material = materialIndex,
                };

                // Create mesh	
                primitives.Add(primitive);	
            }	

            var mesh = new glTFLoader.Schema.Mesh()
            {
                Primitives = primitives.ToArray(),
            };	
            int idxMesh = dummy.Meshes.AddAndReturnIndex(mesh);	

            var node = new Node()
            {
                Mesh = idxMesh,
                Name = string.IsNullOrEmpty(rhinoObject.Name) ? null : rhinoObject.Name,
            };

            int idxNode = dummy.Nodes.AddAndReturnIndex(node);

            dummy.Scenes[dummy.Scene].Nodes.Add(idxNode);
        }

        glTFLoader.Schema.Buffer CreateVerticesBuffer(Rhino.Geometry.Collections.MeshVertexList vertices, out Point3d min, out Point3d max)
        {
            byte[] bytes = GetVerticesBytes(vertices, out min, out max);

            return new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };
        }

        private byte[] GetVerticesBytes(Rhino.Geometry.Collections.MeshVertexList vertices, out Point3d min, out Point3d max)
        {
            var vtxMin = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };
            var vtxMax = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };

            //Preallocate to reduce time spent on allocations
            List<float> floats = new List<float>(vertices.Count * 3);

            foreach (Point3d vertex in vertices)
            {
                floats.AddRange(new float[] { (float)vertex.X, (float)vertex.Z, (float)-vertex.Y });

                vtxMin.X = Math.Min(vtxMin.X, vertex.X);
                // Switch Y<=>Z for GL coords	
                vtxMin.Y = Math.Min(vtxMin.Y, vertex.Z);
                vtxMin.Z = Math.Min(vtxMin.Z, -vertex.Y);

                vtxMax.X = Math.Max(vtxMax.X, vertex.X);
                // Switch Y<=>Z for GL coords	
                vtxMax.Y = Math.Max(vtxMax.Y, vertex.Z);
                vtxMax.Z = Math.Max(vtxMax.Z, -vertex.Y);
            }

            min = vtxMin;
            max = vtxMax;

            IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

        glTFLoader.Schema.Buffer CreateIndicesBuffer(Rhino.Geometry.Collections.MeshFaceList faces, out int indicesCount)
        {
            byte[] bytes = GetIndicesBytes(faces, out indicesCount);

            return new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };
        }

        byte[] GetIndicesBytes(Rhino.Geometry.Collections.MeshFaceList faces, out int indicesCount)
        {
            //Preallocate to reduce time spent on allocations
            List<uint> faceIndices = new List<uint>(faces.Count * 3);

            foreach (Rhino.Geometry.MeshFace face in faces)
            {
                if (face.IsTriangle)
                {
                    faceIndices.AddRange(new uint[] { (uint)face.A, (uint)face.B, (uint)face.C });
                }
                else
                {
                    //Triangulate
                    faceIndices.AddRange(new uint[] { (uint)face.A, (uint)face.B, (uint)face.C, (uint)face.A, (uint)face.C, (uint)face.D });
                }
            }

            IEnumerable<byte> bytesEnumerable = faceIndices.SelectMany(value => BitConverter.GetBytes(value));

            indicesCount = faceIndices.Count;

            return bytesEnumerable.ToArray();
        }

        glTFLoader.Schema.Buffer CreateNormalsBuffer(Rhino.Geometry.Collections.MeshVertexNormalList normals, out Vector3f min, out Vector3f max)
        {
            byte[] bytes = GetNormalsBytes(normals, out min, out max);

            return new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };
        }

        byte[] GetNormalsBytes(Rhino.Geometry.Collections.MeshVertexNormalList normals, out Vector3f min, out Vector3f max)
        {
            Vector3f vMin = new Vector3f() { X = float.PositiveInfinity, Y = float.PositiveInfinity, Z = float.PositiveInfinity };
            Vector3f vMax = new Vector3f() { X = float.NegativeInfinity, Y = float.NegativeInfinity, Z = float.NegativeInfinity };

            //Preallocate to reduce time spent on allocations
            List<float> floats = new List<float>(normals.Count * 3);

            foreach (Vector3f normal in normals)
            {
                floats.AddRange(new float[] { normal.X, normal.Z, -normal.Y });

                vMin.X = Math.Min(vMin.X, normal.X);
                // Switch Y<=>Z for GL coords	
                vMin.Y = Math.Min(vMin.Y, normal.Z);
                vMin.Z = Math.Min(vMin.Z, -normal.Y);

                vMax.X = Math.Max(vMax.X, normal.X);
                // Switch Y<=>Z for GL coords	
                vMax.Y = Math.Max(vMax.Y, normal.Z);
                vMax.Z = Math.Max(vMax.Z, -normal.Y);
            }

            IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

            min = vMin;
            max = vMax;

            return bytesEnumerable.ToArray();
        }

        glTFLoader.Schema.Buffer CreateTextureCoordinatesBuffer(Rhino.Geometry.Collections.MeshTextureCoordinateList texCoords, out Point2f min, out Point2f max)
        {
            byte[] bytes = GetTextureCoordinatesBytes(texCoords, out min, out max);

            return new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };
        }

        private byte[] GetTextureCoordinatesBytes(Rhino.Geometry.Collections.MeshTextureCoordinateList texCoords, out Point2f min, out Point2f max)
        {
            Point2f texCoordsMin = new Point2f() { X = float.PositiveInfinity, Y = float.PositiveInfinity };
            Point2f texCoordsMax = new Point2f() { X = float.NegativeInfinity, Y = float.NegativeInfinity };

            List<float> coordinates = new List<float>(texCoords.Count * 2);

            foreach (Point2f coordinate in texCoords)
            {
                coordinates.AddRange(new float[] { coordinate.X, -coordinate.Y });

                texCoordsMin.X = Math.Min(texCoordsMin.X, coordinate.X);
                // Switch Y<=>Z for GL coords	
                texCoordsMin.Y = Math.Min(texCoordsMin.Y, -coordinate.Y);

                texCoordsMax.X = Math.Max(texCoordsMax.X, coordinate.X);
                // Switch Y<=>Z for GL coords	
                texCoordsMax.Y = Math.Max(texCoordsMax.Y, -coordinate.Y);
            }

            IEnumerable<byte> bytesEnumerable = coordinates.SelectMany(value => BitConverter.GetBytes(value));

            min = texCoordsMin;
            max = texCoordsMax;

            return bytesEnumerable.ToArray();
        }

        int GetMaterial(Rhino.DocObjects.Material material, Guid materialId)
        {
            int materialIndex = -1;
            if(!materialsMap.TryGetValue(materialId, out materialIndex))
            {
                materialIndex = AddMaterial(material, materialId);
                materialsMap.Add(materialId, materialIndex);
            }

            return materialIndex;
        }

        public int AddMaterial(Rhino.DocObjects.Material rhinoMaterial, Guid renderMatId)
        {
            // Prep
            glTFLoader.Schema.Material material = new glTFLoader.Schema.Material();

            // Textures
            Rhino.DocObjects.Texture baseColorTexture = null;
            Rhino.DocObjects.Texture metallicTexture = null;
            Rhino.DocObjects.Texture roughnessTexture = null;
            Rhino.DocObjects.Texture normalTexture = null;
            Rhino.DocObjects.Texture occlusionTexture = null;
            Rhino.DocObjects.Texture emissiveTexture = null;

            if (rhinoMaterial.IsPhysicallyBased)
            {
                baseColorTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_BaseColor);
                metallicTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Metallic);
                roughnessTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Roughness);
                normalTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.Bump);
                occlusionTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_AmbientOcclusion);
                emissiveTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Emission);
            }
            else
            {
                baseColorTexture = rhinoMaterial.GetTexture(TextureType.PBR_BaseColor);
                metallicTexture = rhinoMaterial.GetTexture(TextureType.PBR_Metallic);
                roughnessTexture = rhinoMaterial.GetTexture(TextureType.PBR_Roughness);
                normalTexture = rhinoMaterial.GetTexture(TextureType.Bump);
                occlusionTexture = null; // Oldschool shaders don't have this.
                emissiveTexture = null; // TODO: Don't know where to pull this from. It should be there...
            }

            if (baseColorTexture != null)
            {
                material.PbrMetallicRoughness = new MaterialPbrMetallicRoughness();
                material.PbrMetallicRoughness.BaseColorTexture = AddTexture(baseColorTexture.FileReference.FullPath);
            }

            if (metallicTexture != null || roughnessTexture != null)
            {
                if (material.PbrMetallicRoughness == null)
                {
                    material.PbrMetallicRoughness = new MaterialPbrMetallicRoughness();
                }
                material.PbrMetallicRoughness.MetallicRoughnessTexture = AddMetallicRoughnessTexture(rhinoMaterial);
            }

            if (normalTexture != null)
            {
                material.NormalTexture = AddTextureNormal(normalTexture.FileReference.FullPath);
            }

            if (occlusionTexture != null)
            {
                material.OcclusionTexture = AddTextureOcclusion(occlusionTexture.FileReference.FullPath);
            }

            if (emissiveTexture != null)
            {
                material.EmissiveTexture = AddTexture(emissiveTexture.FileReference.FullPath);
            }

            return dummy.Materials.AddAndReturnIndex(material);
        }

        private int AddTextureToBuffers(string texturePath)
        {
            var image = GetImageFromFile(texturePath);
            
            int imageIdx = dummy.Images.AddAndReturnIndex(image);

            var texture = new glTFLoader.Schema.Texture()
            {
                Source = imageIdx,
                Sampler = 0
            };

            return dummy.Textures.AddAndReturnIndex(texture);
        }

        private glTFLoader.Schema.Image GetImageFromFileText(string fileName)
        {
            byte[] imageBytes = GetImageBytesFromFile(fileName);

            var textureBuffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(imageBytes),
                ByteLength = imageBytes.Length,
            };

            int textureBufferIdx = dummy.Buffers.AddAndReturnIndex(textureBuffer);

            var textureBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = textureBufferIdx,
                ByteOffset = 0,
                ByteLength = textureBuffer.ByteLength,
            };
            int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

            return new glTFLoader.Schema.Image()
            {
                BufferView = textureBufferViewIdx,
                MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
            };
        }

        private glTFLoader.Schema.Image GetImageFromFile(string fileName)
        {
            if(options.UseBinary)
            {
                return GetImageFromFileBinary(fileName);
            }
            else
            {
                return GetImageFromFileText(fileName);
            }
        }

        private glTFLoader.Schema.Image GetImageFromFileBinary(string fileName)
        {
            byte[] imageBytes = GetImageBytesFromFile(fileName);
            int imageBytesOffset = (int)stream.Position;
            stream.Write(imageBytes, 0, imageBytes.Length);

            var textureBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = 0,
                ByteOffset = imageBytesOffset,
                ByteLength = imageBytes.Length,
            };
            int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

            return new glTFLoader.Schema.Image()
            {
                BufferView = textureBufferViewIdx,
                MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
            };
        }

        private byte[] GetImageBytesFromFile(string fileName)
        {
            using (FileStream stream = File.Open(fileName, FileMode.Open))
            {
                //padding so 4 byte aligned
                long length = stream.Length;
                long mod = length % 4;
                if (mod != 0)
                {
                    length += 4 - mod;
                }
                var bytes = new byte[length];
                
                stream.Read(bytes, 0, (int)stream.Length);

                return bytes;
            }
        }

        private glTFLoader.Schema.TextureInfo AddTexture(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new glTFLoader.Schema.TextureInfo() { Index = textureIdx, TexCoord = 0 };
        }

        private glTFLoader.Schema.MaterialNormalTextureInfo AddTextureNormal(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new glTFLoader.Schema.MaterialNormalTextureInfo() { Index = textureIdx, TexCoord = 0, Scale = 0.9f };
        }

        private glTFLoader.Schema.MaterialOcclusionTextureInfo AddTextureOcclusion(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new glTFLoader.Schema.MaterialOcclusionTextureInfo() { Index = textureIdx, TexCoord = 0, Strength = 0.9f };
        }

        public glTFLoader.Schema.TextureInfo AddMetallicRoughnessTexture(Rhino.DocObjects.Material rhinoMaterial)
        {
            // glTF metallicRoughness expects a texture with metal in the green channel and roughness in the blue channel.
            // This method mashes the two textures into a PNG, then writes it to a texture buffer.

            var isMetalTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Metallic) != null;
            var isRoughnessTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Roughness) != null;

            RenderTexture renderTextureMetal = null;
            RenderTexture renderTextureRoughness = null;

            int mWidth = 0;
            int mHeight = 0;
            int rWidth = 0;
            int rHeight = 0;

            // Get the textures
            if (isMetalTexture)
            {
                // Get the texture
                renderTextureMetal = rhinoMaterial.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrMetallic);
                // Figure out the size of the textures
                renderTextureMetal.PixelSize(out mWidth, out mHeight, out int _w0);
            }

            if (isRoughnessTexture)
            {
                // Get the texture
                renderTextureRoughness = rhinoMaterial.RenderMaterial.GetTextureFromUsage(RenderMaterial.StandardChildSlots.PbrRoughness);
                // Figure out the size of the textures
                renderTextureRoughness.PixelSize(out rWidth, out rHeight, out int _w1);
            }

            // Figure out the size of the new combined texture
            // TODO: This is probably not great if mW > rW && mH < rH
            int width = Math.Max(mWidth, rWidth);
            int height = Math.Max(mHeight, rHeight);

            byte[] imgMetal = null;
            byte[] imgRoughness = null;

            // Metal
            if (isMetalTexture)
            {
                var evalMetal = renderTextureMetal.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
                imgMetal = evalMetal.WriteToByteArray(width, height).ToArray();
            }

            // Roughness
            if (isRoughnessTexture)
            {
                var evalRoughness = renderTextureRoughness.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
                imgRoughness = evalRoughness.WriteToByteArray(width, height).ToArray();
            }

            // Copy Metal to the blue channel, roughness to the green
            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (var y = 0; y < height - 1; y += 1)
            {
                for (var x = 0; x < width - 1; x += 1)
                {
                    int g = 0;
                    int b = 0;
                    if (isMetalTexture)
                    {
                        b = imgMetal[(y * width + x) * 4 + 1]; // Note: Not sure if this offset is the red, or the green channel
                    }

                    if (isRoughnessTexture)
                    {
                        g = imgRoughness[(y * width + x) * 4 + 2]; // Note: Not sure if this offset is the green or the blue channel
                    }

                    // GLTF expects metallic and roughness textures to be in linear color space.
                    //TODO: This doesn't seem right... var color = ColorUtils.ConvertSRGBToLinear(new Color4f(0, g / 255.0f, b / 255.0f, 1.0f));
                    // Using it without gammacorrecting for now.
                    var color = new Color4f(0, g / 255.0f, b / 255.0f, 1.0f);
                    bitmap.SetPixel(x, height - y - 1, color.AsSystemColor());
                }
            }

            var image = GetImageFromBitmap(bitmap);
            
            int imageIdx = dummy.Images.AddAndReturnIndex(image);

            var texture = new glTFLoader.Schema.Texture()
            {
                Source = imageIdx,
                Sampler = 0
            };
            
            int textureIdx = dummy.Textures.AddAndReturnIndex(texture);

            return new glTFLoader.Schema.TextureInfo()
            {
                Index = textureIdx,
                TexCoord = 0
            };
        }

        private glTFLoader.Schema.Image GetImageFromBitmap(Bitmap bitmap)
        {
            if(options.UseBinary)
            {
                return GetImageFromBitmapBinary(bitmap);
            }
            else
            {
                return GetImageFromBitmapText(bitmap);
            }
        }

        private glTFLoader.Schema.Image GetImageFromBitmapText(Bitmap bitmap)
        {
            byte[] imageBytes = GetImageBytes(bitmap);

            var textureBuffer = new glTFLoader.Schema.Buffer();

            textureBuffer.Uri = Constants.TextBufferHeader + Convert.ToBase64String(imageBytes);
            textureBuffer.ByteLength = imageBytes.Length;

            int textureBufferIdx = dummy.Buffers.AddAndReturnIndex(textureBuffer);

            // Create bufferviews
            var textureBufferView = new BufferView()
            {
                Buffer = textureBufferIdx,
                ByteOffset = 0,
                ByteLength = textureBuffer.ByteLength,
            };
            int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

            return new glTFLoader.Schema.Image()
            {
                BufferView = textureBufferViewIdx,
                MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
            };
        }

        private glTFLoader.Schema.Image GetImageFromBitmapBinary(Bitmap bitmap)
        {
            byte[] imageBytes = GetImageBytes(bitmap);
            int imageBytesOffset = (int)stream.Position;
            stream.Write(imageBytes, 0, imageBytes.Length);

            // Create bufferviews
            var textureBufferView = new BufferView()
            {
                Buffer = 0,
                ByteOffset = imageBytesOffset,
                ByteLength = imageBytes.Length,
            };
            int textureBufferViewIdx = dummy.BufferViews.AddAndReturnIndex(textureBufferView);

            return new glTFLoader.Schema.Image()
            {
                BufferView = textureBufferViewIdx,
                MimeType = glTFLoader.Schema.Image.MimeTypeEnum.image_png,
            };
        }

        private byte[] GetImageBytes(Bitmap bitmap)
        {
            using (MemoryStream imageStream = new MemoryStream(4096))
            {
                bitmap.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);

                //Zero pad so its 4 byte aligned
                long mod = imageStream.Position % 4;
                imageStream.Write(Constants.Paddings[mod], 0, Constants.Paddings[mod].Length);

                return imageStream.ToArray();
            }
        }

    }
}
