using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinImporter
{
    struct GltfMeshMaterialPair
    {
        public Rhino.Geometry.Mesh RhinoMesh;
        public int? MaterialIndex;
        public string Name;
    }

    struct GltfPointCloudHolder
    {
        public string Name;
        public Rhino.Geometry.PointCloud PointCloud;
    }

    class GltfMeshHolder
    {
        public GltfMeshHolder(GltfRhinoConverter converter, Rhino.RhinoDoc doc)
        {
            this.converter = converter;
            this.doc = doc;
        }

        private GltfRhinoConverter converter = null;
        private Rhino.RhinoDoc doc = null;

        private List<GltfMeshMaterialPair> meshMaterialPairs = new List<GltfMeshMaterialPair>();
        private List<GltfPointCloudHolder> pointCloudHolders = new List<GltfPointCloudHolder>();

        public void AddPrimitive(Rhino.Geometry.Mesh rhinoMesh, int? materialIndex, string name)
        {
            meshMaterialPairs.Add(new GltfMeshMaterialPair()
            {
                RhinoMesh = rhinoMesh,
                MaterialIndex = materialIndex,
                Name = name,
            });
        }

        public void AddPointCloudPrimitive(Rhino.Geometry.PointCloud pointCloud, string name)
        {
            pointCloudHolders.Add(new GltfPointCloudHolder()
            {
                Name = name,
                PointCloud = pointCloud,
            });
        }

        public void AddInstance(Rhino.Geometry.Transform transform)
        {
            foreach(GltfMeshMaterialPair pair in  meshMaterialPairs)
            {
                Rhino.Geometry.Mesh rhinoMesh = pair.RhinoMesh.DuplicateMesh();

                rhinoMesh.Transform(GltfUtils.YupToZup * transform);

                rhinoMesh.TextureCoordinates.ReverseTextureCoordinates(1);

                Guid objectId = doc.Objects.AddMesh(rhinoMesh);

                Rhino.DocObjects.RhinoObject rhinoObject = doc.Objects.Find(objectId);

                Rhino.Render.RenderMaterial material = converter.GetMaterial(pair.MaterialIndex);

                if (rhinoObject != null && material != null)
                {
                    rhinoObject.RenderMaterial = material;
                    rhinoObject.Attributes.MaterialSource = Rhino.DocObjects.ObjectMaterialSource.MaterialFromObject;
                    rhinoObject.Attributes.Name = pair.Name;

                    rhinoObject.CommitChanges();
                }
            }

            foreach(GltfPointCloudHolder holder in pointCloudHolders)
            {
                Rhino.Geometry.PointCloud pointCloud = holder.PointCloud.Duplicate() as Rhino.Geometry.PointCloud;

                if(pointCloud == null)
                {
                    continue;
                }

                pointCloud.Transform(GltfUtils.YupToZup * transform);

                Guid objectId = doc.Objects.Add(pointCloud);

                Rhino.DocObjects.RhinoObject rhinoObject = doc.Objects.Find(objectId);

                rhinoObject.Attributes.Name = holder.Name;

                rhinoObject.CommitChanges();
            }
        }
    }

    class GltfRhinoMeshConverter
    {
        public const string PositionAttributeTag = "POSITION";
        public const string NormalAttributeTag = "NORMAL";
        public const string TexCoord0AttributeTag = "TEXCOORD_0";
        public const string VertexColorAttributeTag = "COLOR_0";

        public GltfRhinoMeshConverter(glTFLoader.Schema.Mesh mesh, GltfRhinoConverter converter, Rhino.RhinoDoc doc)
        {
            this.mesh = mesh;
            this.converter = converter;
            this.doc = doc;
        }

        glTFLoader.Schema.Mesh mesh = null;
        GltfRhinoConverter converter = null;
        Rhino.RhinoDoc doc = null;

        public GltfMeshHolder Convert()
        {
            GltfMeshHolder meshHolder = new GltfMeshHolder(converter, doc);

            foreach (var primitive in mesh.Primitives)
            {
                if(primitive.Mode == glTFLoader.Schema.MeshPrimitive.ModeEnum.POINTS)
                {
                    Rhino.Geometry.PointCloud pointCloud = GetPointCloud(primitive);

                    if(pointCloud == null)
                    {
                        continue;
                    }

                    meshHolder.AddPointCloudPrimitive(pointCloud, mesh.Name);
                }
                else
                {
                    Rhino.Geometry.Mesh rhinoMesh = GetMesh(primitive);

                    if (rhinoMesh == null)
                    {
                        continue;
                    }

                    rhinoMesh.Weld(0.01);

                    rhinoMesh.Compact();

                    if (!rhinoMesh.IsValidWithLog(out string log))
                    {
                        Rhino.RhinoApp.WriteLine(log);
                    }

                    meshHolder.AddPrimitive(rhinoMesh, primitive.Material, mesh.Name);
                }
            }

            return meshHolder;
        }

        Rhino.Geometry.PointCloud GetPointCloud(glTFLoader.Schema.MeshPrimitive primitive)
        {
            if(!AttemptGetVertexFloats(primitive, out List<Rhino.Geometry.Point3d> points))
            {
                return null;
            }

            Rhino.Geometry.PointCloud pointCloud = new Rhino.Geometry.PointCloud();

            for(int i = 0; i < points.Count; i++)
            {
                pointCloud.Add(points[i]);
            }

            if(AttemptGetVertexColors(primitive, out List<System.Drawing.Color> colors))
            {
                int min = Math.Min(colors.Count, pointCloud.Count);

                for(int i = 0; i < min; i++)
                {
                    pointCloud[i].Color = colors[i];
                }
            }

            if(AttemptGetNormals(primitive, out List<Rhino.Geometry.Vector3d> normals))
            {
                int min = Math.Min(normals.Count, pointCloud.Count);

                for(int i = 0; i < min; i++)
                {
                    pointCloud[i].Normal = normals[i];
                }
            }
            
            return pointCloud;
        }

        Rhino.Geometry.Mesh GetMesh(glTFLoader.Schema.MeshPrimitive primitive)
        {
            if (primitive.Extensions != null && primitive.Extensions.TryGetValue(glTFExtensions.KHR_draco_mesh_compression.Tag, out object value))
            {
                return ConvertDraco(value.ToString());
            }
            else
            {
                return ConvertPrimtive(primitive);
            }
        }

        Rhino.Geometry.Mesh ConvertDraco(string text)
        {
            var khr_draco = Newtonsoft.Json.JsonConvert.DeserializeObject<glTFExtensions.KHR_draco_mesh_compression>(text);

            if (khr_draco == null)
            {
                return null;
            }

            glTFLoader.Schema.BufferView view = converter.GetBufferView(khr_draco.BufferView);

            byte[] buffer = converter.GetBuffer(view.Buffer);

            if(buffer == null)
            {
                return null;
            }

            int offset = view.ByteOffset;
            int length = view.ByteLength;

            byte[] dracoBytes = new byte[length];
            Array.Copy(buffer, offset, dracoBytes, 0, length);

            return Rhino.FileIO.DracoCompression.DecompressByteArray(dracoBytes) as Rhino.Geometry.Mesh;
        }

        Rhino.Geometry.Mesh ConvertPrimtive(glTFLoader.Schema.MeshPrimitive primitive)
        {
            Rhino.Geometry.Mesh rhinoMesh = new Rhino.Geometry.Mesh();

            if(!AttemptConvertVerticesAndIndices(primitive, rhinoMesh)) //Only part that is required
            {
                return null;
            }

            if(!AttemptConvertNormals(primitive, rhinoMesh))
            {
                rhinoMesh.RebuildNormals();
            }

            AttemptConvertTextureCoordinates(primitive, rhinoMesh);

            AttemptConvertVertexColors(primitive, rhinoMesh);

            return rhinoMesh;
        }

        private bool AttemptConvertVerticesAndIndices(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            if(!AttemptConvertVertices(primitive, rhinoMesh))
            {
                return false;
            }

            return HandleIndices(primitive, rhinoMesh);
        }

        private bool HandleIndices(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            if(primitive.Indices.HasValue)
            {
                return AttemptConvertIndices(primitive, rhinoMesh);
            }
            else if (primitive.Mode == glTFLoader.Schema.MeshPrimitive.ModeEnum.TRIANGLES)
            {
                return HandleTrianglesMode(rhinoMesh);
            }
            else if(primitive.Mode == glTFLoader.Schema.MeshPrimitive.ModeEnum.TRIANGLE_STRIP)
            {
                return HandleTriangleStripMode(rhinoMesh);
            }
            else if(primitive.Mode == glTFLoader.Schema.MeshPrimitive.ModeEnum.TRIANGLE_FAN)
            {
                return HandleTriangleFanMode(rhinoMesh);
            }

            return false;
        }

        private bool HandleTriangleFanMode(Rhino.Geometry.Mesh rhinoMesh)
        {
            for (int i = 1; i < rhinoMesh.Vertices.Count - 1; i++)
            {
                Rhino.Geometry.MeshFace face = new Rhino.Geometry.MeshFace(0, i, i + 1);

                rhinoMesh.Faces.AddFace(face);
            }

            return true;
        }

        private bool HandleTriangleStripMode(Rhino.Geometry.Mesh rhinoMesh)
        {
            for(int i = 0; i < rhinoMesh.Vertices.Count - 2; i++)
            {
                Rhino.Geometry.MeshFace face = new Rhino.Geometry.MeshFace(i, i + 1, i + 2);

                rhinoMesh.Faces.AddFace(face);
            }

            return true;
        }

        private bool HandleTrianglesMode(Rhino.Geometry.Mesh rhinoMesh)
        {
            int count = rhinoMesh.Vertices.Count / 3;

            for(int i = 0; i < count; i++)
            {
                int index = i * 3;

                Rhino.Geometry.MeshFace face = new Rhino.Geometry.MeshFace(index, index + 1, index + 2);

                rhinoMesh.Faces.AddFace(face);
            }

            return true;
        }

        private bool AttemptConvertIndices(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            glTFLoader.Schema.Accessor indicesAccessor = converter.GetAccessor(primitive.Indices);

            if (indicesAccessor == null)
            {
                return false;
            }

            glTFLoader.Schema.BufferView indicesView = converter.GetBufferView(indicesAccessor.BufferView);

            if (indicesView == null)
            {
                return false;
            }

            byte[] indicesBuffer = converter.GetBuffer(indicesView.Buffer);

            if (indicesBuffer == null)
            {
                return false;
            }

            int indicesOffset = indicesAccessor.ByteOffset + indicesView.ByteOffset;
            int indicesStride = indicesView.ByteStride.HasValue ? indicesView.ByteStride.Value : TotalStride(indicesAccessor.ComponentType, indicesAccessor.Type);
            int indicesComponentsCount = ComponentsCount(indicesAccessor.Type);
            int indicesComponentSize = ComponentSize(indicesAccessor.ComponentType);

            List<uint> indices = new List<uint>();

            for (int i = 0; i < indicesAccessor.Count; i++)
            {
                int index = indicesOffset + indicesStride * i;

                for (int j = 0; j < indicesComponentsCount; j++)
                {
                    int location = index + j * indicesComponentSize;

                    if (indicesAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE)
                    {
                        byte b = indicesBuffer[location];
                        indices.Add(b);
                    }
                    else if (indicesAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                    {
                        ushort s = BitConverter.ToUInt16(indicesBuffer, location);
                        indices.Add(s);
                    }
                    else if (indicesAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_INT)
                    {
                        uint u = BitConverter.ToUInt32(indicesBuffer, location);
                        indices.Add(u);
                    }
                }

            }

            int faces = indices.Count / 3;
            for (int i = 0; i < faces; i++)
            {
                int index = i * 3;

                int indexOne = (int)indices[index + 0];
                int indexTwo = (int)indices[index + 1];
                int indexThree = (int)indices[index + 2];

                if (ValidFace(indexOne, indexTwo, indexThree, rhinoMesh.Vertices.Count))
                {
                    rhinoMesh.Faces.AddFace(indexOne, indexTwo, indexThree);
                }
            }

            return true;
        }

        private bool AttemptConvertVertices(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            if (!AttemptGetVertexFloats(primitive, out List<Rhino.Geometry.Point3d> vertices))
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                rhinoMesh.Vertices.Add(vertices[i]);
            }

            return true;
        }

        private bool AttemptGetVertexFloats(glTFLoader.Schema.MeshPrimitive primitive, out List<Rhino.Geometry.Point3d> vertices)
        {
            vertices = new List<Rhino.Geometry.Point3d>();

            glTFLoader.Schema.Accessor vertexAcessor = null;

            if (!primitive.Attributes.TryGetValue(PositionAttributeTag, out int vertexAcessorIndex))
            {
                return false;
            }

            vertexAcessor = converter.GetAccessor(vertexAcessorIndex);

            if (vertexAcessor == null)
            {
                return false;
            }

            glTFLoader.Schema.BufferView vertexView = converter.GetBufferView(vertexAcessor.BufferView);

            if (vertexView == null)
            {
                return false;
            }

            byte[] vertexBuffer = converter.GetBuffer(vertexView.Buffer);

            if (vertexBuffer == null)
            {
                return false;
            }

            int vertexOffset = vertexAcessor.ByteOffset + vertexView.ByteOffset;
            int vertexStride = vertexView.ByteStride.HasValue ? vertexView.ByteStride.Value : TotalStride(vertexAcessor.ComponentType, vertexAcessor.Type);
            int vertexComponentsCount = ComponentsCount(vertexAcessor.Type);
            int vertexComponentSize = ComponentSize(vertexAcessor.ComponentType);

            List<float> floats = new List<float>();

            for (int i = 0; i < vertexAcessor.Count; i++)
            {
                int index = vertexOffset + vertexStride * i;

                for (int j = 0; j < vertexComponentsCount; j++)
                {
                    int offset = index + j * vertexComponentSize;

                    float f = BitConverter.ToSingle(vertexBuffer, offset);

                    floats.Add(f);
                }
            }

            int count = floats.Count / 3;

            for (int i = 0; i < count; i++)
            {
                int index = i * 3;
                vertices.Add(new Rhino.Geometry.Point3d(floats[index], floats[index + 1], floats[index + 2]));
            }

            return true;
        }

        private bool AttemptConvertNormals(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            if(!AttemptGetNormals(primitive, out List<Rhino.Geometry.Vector3d> normals))
            {
                return false;
            }

            for(int i = 0; i < normals.Count; i++)
            {
                rhinoMesh.Normals.Add(normals[i]);
            }

            return true;
        }

        private bool AttemptGetNormals(glTFLoader.Schema.MeshPrimitive primitive, out List<Rhino.Geometry.Vector3d> normals)
        {
            normals = new List<Rhino.Geometry.Vector3d>();

            if (!primitive.Attributes.TryGetValue(NormalAttributeTag, out int normalAttributeAccessorIndex))
            {
                return false;
            }

            glTFLoader.Schema.Accessor normalsAccessor = converter.GetAccessor(normalAttributeAccessorIndex);

            if (normalsAccessor == null)
            {
                return false;
            }

            glTFLoader.Schema.BufferView normalsView = converter.GetBufferView(normalsAccessor.BufferView);

            if (normalsView == null)
            {
                return false;
            }

            byte[] normalsBuffer = converter.GetBuffer(normalsView.Buffer);

            if (normalsBuffer == null)
            {
                return false;
            }

            int normalsOffset = normalsView.ByteOffset + normalsAccessor.ByteOffset;

            int normalsStride = normalsView.ByteStride.HasValue ? normalsView.ByteStride.Value : TotalStride(normalsAccessor.ComponentType, normalsAccessor.Type);

            int normalsComponentsCount = ComponentsCount(normalsAccessor.Type);

            int normalsComponentSize = ComponentSize(normalsAccessor.ComponentType);

            List<float> normalsFloats = new List<float>();

            for (int i = 0; i < normalsAccessor.Count; i++)
            {
                int normalsIndex = normalsOffset + i * normalsStride;

                for (int j = 0; j < normalsComponentsCount; j++)
                {
                    int location = normalsIndex + j * normalsComponentSize;

                    float normalComponent = BitConverter.ToSingle(normalsBuffer, location);

                    normalsFloats.Add(normalComponent);
                }
            }

            int count = normalsFloats.Count / 3;

            for (int i = 0; i < count; i++)
            {
                int index = i * 3;
                normals.Add(new Rhino.Geometry.Vector3d(normalsFloats[index], normalsFloats[index + 1], normalsFloats[index + 2]));
            }

            return true;
        }

        private bool AttemptConvertTextureCoordinates(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            if (!primitive.Attributes.TryGetValue(TexCoord0AttributeTag, out int texCoordsAttributeAccessorIndex))
            {
                return false;
            }

            glTFLoader.Schema.Accessor texCoordsAccessor = converter.GetAccessor(texCoordsAttributeAccessorIndex);

            if(texCoordsAccessor == null)
            {
                return false;
            }

            glTFLoader.Schema.BufferView texCoordsBufferView = converter.GetBufferView(texCoordsAccessor.BufferView);

            if (texCoordsBufferView == null)
            {
                return false;
            }

            byte[] texCoordsBuffer = converter.GetBuffer(texCoordsBufferView.Buffer);

            if(texCoordsBuffer == null)
            {
                return false;
            }

            int texCoordsOffset = texCoordsAccessor.ByteOffset + texCoordsBufferView.ByteOffset;

            int texCoordsStride = texCoordsBufferView.ByteStride.HasValue ? texCoordsBufferView.ByteStride.Value : TotalStride(texCoordsAccessor.ComponentType, texCoordsAccessor.Type);

            int texCoordsComponentCount = ComponentsCount(texCoordsAccessor.Type);

            int texCoordsComponentSize = ComponentSize(texCoordsAccessor.ComponentType);

            List<float> texCoords = new List<float>();

            for (int i = 0; i < texCoordsAccessor.Count; i++)
            {
                int texCoordsIndex = texCoordsOffset + i * texCoordsStride;

                for (int j = 0; j < texCoordsComponentCount; j++)
                {
                    int location = texCoordsIndex + j * texCoordsComponentSize;

                    float coordinate = 0.0f;

                    if (texCoordsAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT)
                    {
                        coordinate = BitConverter.ToSingle(texCoordsBuffer, location);
                    }
                    else if (texCoordsAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE)
                    {
                        byte byteVal = texCoordsBuffer[location];
                        coordinate = (float)byteVal / (float)byte.MaxValue;
                    }
                    else if (texCoordsAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                    {
                        ushort shortValue = BitConverter.ToUInt16(texCoordsBuffer, location);
                        coordinate = (float)shortValue / (float)ushort.MaxValue;
                    }

                    texCoords.Add(coordinate);
                }
            }

            int coordinates = texCoords.Count / 2;

            for (int i = 0; i < coordinates; i++)
            {
                int index = i * 2;

                Rhino.Geometry.Point2f coordinate = new Rhino.Geometry.Point2f(texCoords[index + 0], texCoords[index + 1]);

                rhinoMesh.TextureCoordinates.Add(coordinate);
            }

            return true;
        }

        private bool AttemptConvertVertexColors(glTFLoader.Schema.MeshPrimitive primitive, Rhino.Geometry.Mesh rhinoMesh)
        {
            if(!AttemptGetVertexColors(primitive, out List<System.Drawing.Color> colors))
            {
                return false;
            }

            foreach(System.Drawing.Color color in colors)
            {
                rhinoMesh.VertexColors.Add(color);
            }

            return true;
        }

        private bool AttemptGetVertexColors(glTFLoader.Schema.MeshPrimitive primitive, out List<System.Drawing.Color> colors)
        {
            colors = new List<System.Drawing.Color>();

            if (!primitive.Attributes.TryGetValue(VertexColorAttributeTag, out int vertexColorAccessorIndex))
            {
                return false;
            }

            glTFLoader.Schema.Accessor vertexColorAccessor = converter.GetAccessor(vertexColorAccessorIndex);

            if (vertexColorAccessor == null)
            {
                return false;
            }

            glTFLoader.Schema.BufferView vertexColorBufferView = converter.GetBufferView(vertexColorAccessor.BufferView);

            if (vertexColorBufferView == null)
            {
                return false;
            }

            byte[] vertexColorBuffer = converter.GetBuffer(vertexColorBufferView.Buffer);

            if (vertexColorBuffer == null)
            {
                return false;
            }

            int vertexColorOffset = vertexColorAccessor.ByteOffset + vertexColorBufferView.ByteOffset;

            int vertexColorStride = vertexColorBufferView.ByteStride.HasValue ? vertexColorBufferView.ByteStride.Value : TotalStride(vertexColorAccessor.ComponentType, vertexColorAccessor.Type);

            int vertexColorComponentCount = ComponentsCount(vertexColorAccessor.Type);

            int vertexColorComponentSize = ComponentSize(vertexColorAccessor.ComponentType);

            List<float> vertexColors = new List<float>();

            for (int i = 0; i < vertexColorAccessor.Count; i++)
            {
                int vertexColorIndex = vertexColorOffset + i * vertexColorStride;

                for (int j = 0; j < vertexColorComponentCount; j++)
                {
                    int location = vertexColorIndex + j * vertexColorComponentSize;

                    float channelColor = 0.0f;

                    if (vertexColorAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT)
                    {
                        channelColor = BitConverter.ToSingle(vertexColorBuffer, location);
                    }
                    else if (vertexColorAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                    {
                        ushort value = BitConverter.ToUInt16(vertexColorBuffer, location);
                        channelColor = (float)value / (float)ushort.MaxValue;
                    }
                    else if (vertexColorAccessor.ComponentType == glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE)
                    {
                        byte value = vertexColorBuffer[location];
                        channelColor = (float)value / (float)byte.MaxValue;
                    }

                    vertexColors.Add(channelColor);
                }
            }

            int countVertexColors = vertexColors.Count / vertexColorComponentCount;

            for (int i = 0; i < countVertexColors; i++)
            {
                int index = i * vertexColorComponentCount;

                if (vertexColorAccessor.Type == glTFLoader.Schema.Accessor.TypeEnum.VEC3)
                {
                    float r = GltfUtils.Clamp(vertexColors[index + 0], 0.0f, 1.0f);
                    float g = GltfUtils.Clamp(vertexColors[index + 1], 0.0f, 1.0f);
                    float b = GltfUtils.Clamp(vertexColors[index + 2], 0.0f, 1.0f);

                    Rhino.Display.Color4f color = new Rhino.Display.Color4f(r, g, b, 1.0f);

                    colors.Add(color.AsSystemColor());
                }
                else if (vertexColorAccessor.Type == glTFLoader.Schema.Accessor.TypeEnum.VEC4)
                {
                    float r = GltfUtils.Clamp(vertexColors[index + 0], 0.0f, 1.0f);
                    float g = GltfUtils.Clamp(vertexColors[index + 1], 0.0f, 1.0f);
                    float b = GltfUtils.Clamp(vertexColors[index + 2], 0.0f, 1.0f);
                    float a = GltfUtils.Clamp(vertexColors[index + 3], 0.0f, 1.0f);

                    Rhino.Display.Color4f color = new Rhino.Display.Color4f(r, g, b, a);

                    colors.Add(color.AsSystemColor());
                }
            }

            return true;
        }

        int TotalStride(glTFLoader.Schema.Accessor.ComponentTypeEnum componentType, glTFLoader.Schema.Accessor.TypeEnum type)
        {
            return ComponentSize(componentType) * ComponentsCount(type);
        }

        int ComponentSize(glTFLoader.Schema.Accessor.ComponentTypeEnum componentType)
        {
            switch(componentType)
            {
                case glTFLoader.Schema.Accessor.ComponentTypeEnum.BYTE:
                    return sizeof(sbyte);
                case glTFLoader.Schema.Accessor.ComponentTypeEnum.SHORT:
                    return sizeof(short);
                case glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE:
                    return sizeof(byte);
                case glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_SHORT:
                    return sizeof(ushort);
                case glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_INT:
                    return sizeof(uint);
                case glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT:
                    return sizeof(float);
                default:
                    return sizeof(byte);
            }
        }

        int ComponentsCount(glTFLoader.Schema.Accessor.TypeEnum type)
        {
            switch(type)
            {
                case glTFLoader.Schema.Accessor.TypeEnum.SCALAR:
                    return 1;
                case glTFLoader.Schema.Accessor.TypeEnum.VEC2:
                    return 2;
                case glTFLoader.Schema.Accessor.TypeEnum.VEC3:
                    return 3;
                case glTFLoader.Schema.Accessor.TypeEnum.VEC4:
                    return 4;
                case glTFLoader.Schema.Accessor.TypeEnum.MAT2:
                    return 2 * 2;
                case glTFLoader.Schema.Accessor.TypeEnum.MAT3:
                    return 3 * 3;
                case glTFLoader.Schema.Accessor.TypeEnum.MAT4:
                    return 4 * 4;
                default:
                    return 1;
            }
        }

        bool ValidFace(int indexOne, int indexTwo, int indexThree, int vertexCount)
        {
            bool indexOneInRange = indexOne >= 0 && indexOne < vertexCount;
            bool indexTwoInRange = indexTwo >= 0 && indexTwo < vertexCount;
            bool indexThreeInRange = indexThree >= 0 && indexThree < vertexCount;
            bool oneNotTwo = indexOne != indexTwo;
            bool oneNotThree = indexOne != indexThree;
            bool twoNotThree = indexTwo != indexThree;

            if (indexOneInRange && indexTwoInRange && indexThreeInRange && oneNotTwo && oneNotThree && twoNotThree)
            {
                return true;
            }

            return false;
        }

    }
}
