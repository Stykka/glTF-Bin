using Rhino.Display;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    class RhinoMeshGltfConverter
    {
        public RhinoMeshGltfConverter(ObjectExportData exportData, int? materialIndex, glTFExportOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer)
        {
            this.exportData = exportData;
            this.materialIndex = materialIndex;
            this.options = options;
            this.binary = binary;
            this.dummy = dummy;
            this.binaryBuffer = binaryBuffer;
        }

        private ObjectExportData exportData;
        private int? materialIndex;
        private glTFExportOptions options = null;
        private bool binary = false;
        private gltfSchemaDummy dummy = null;
        private List<byte> binaryBuffer = null;

        private DracoGeometryInfo currentGeometryInfo = null;

        private readonly Transform ZtoYUp = new Transform()
        {
            M00 = 1,
            M01 = 0,
            M02 = 0,
            M03 = 0,

            M10 = 0,
            M11 = 0,
            M12 = 1,
            M13 = 0,

            M20 = 0,
            M21 = -1,
            M22 = 0,
            M23 = 0,

            M30 = 0,
            M31 = 0,
            M32 = 0,
            M33 = 1,
        };

        public int AddMesh()
        {
            List<glTFLoader.Schema.MeshPrimitive> primitives = GetPrimitives();

            glTFLoader.Schema.Mesh mesh = new glTFLoader.Schema.Mesh()
            {
                Primitives = primitives.ToArray(),
            };

            return dummy.Meshes.AddAndReturnIndex(mesh);
        }

        private void PreprocessMesh(Mesh rhinoMesh)
        {
            if (options.MapRhinoZToGltfY)
            {
                rhinoMesh.Transform(ZtoYUp);
            }

            rhinoMesh.TextureCoordinates.ReverseTextureCoordinates(1);
        }

        private List<glTFLoader.Schema.MeshPrimitive> GetPrimitives()
        {
            List<glTFLoader.Schema.MeshPrimitive> primitives = new List<glTFLoader.Schema.MeshPrimitive>();

            foreach(Mesh rhinoMesh in exportData.Meshes)
            {
                PreprocessMesh(rhinoMesh);

                if(options.UseDracoCompression)
                {
                    if(!SetDracoGeometryInfo(rhinoMesh))
                    {
                        continue;
                    }
                }

                bool exportNormals = ExportNormals(rhinoMesh);
                bool exportTextureCoordinates = ExportTextureCoordinates(rhinoMesh);
                bool exportVertexColors = ExportVertexColors(rhinoMesh);

                glTFLoader.Schema.MeshPrimitive primitive = new glTFLoader.Schema.MeshPrimitive()
                {
                    Attributes = new Dictionary<string, int>(),
                };

                int vertexAccessorIdx = GetVertexAccessor(rhinoMesh.Vertices);

                primitive.Attributes.Add(Constants.PositionAttributeTag, vertexAccessorIdx);

                int indicesAccessorIdx = GetIndicesAccessor(rhinoMesh.Faces, rhinoMesh.Vertices.Count);

                primitive.Indices = indicesAccessorIdx;

                if (exportNormals)
                {
                    int normalsAccessorIdx = GetNormalsAccessor(rhinoMesh.Normals);

                    primitive.Attributes.Add(Constants.NormalAttributeTag, normalsAccessorIdx);
                }

                if (exportTextureCoordinates)
                {
                    int textureCoordinatesAccessorIdx = GetTextureCoordinatesAccessor(rhinoMesh.TextureCoordinates);

                    primitive.Attributes.Add(Constants.TexCoord0AttributeTag, textureCoordinatesAccessorIdx);
                }

                if (exportVertexColors)
                {
                    int vertexColorsAccessorIdx = GetVertexColorAccessor(rhinoMesh.VertexColors);

                    primitive.Attributes.Add(Constants.VertexColorAttributeTag, vertexColorsAccessorIdx);
                }

                if(options.UseDracoCompression)
                {
                    glTFDracoMeshCompressionObject dracoCompressionObject = new glTFDracoMeshCompressionObject();

                    dracoCompressionObject.bufferView = currentGeometryInfo.BufferViewIndex;

                    int attributeIdCounter = 0;

                    dracoCompressionObject.attributes.Add(Constants.PositionAttributeTag, attributeIdCounter++);

                    if(exportNormals)
                    {
                        dracoCompressionObject.attributes.Add(Constants.NormalAttributeTag, attributeIdCounter++);
                    }
                    
                    if(exportTextureCoordinates)
                    {
                        dracoCompressionObject.attributes.Add(Constants.TexCoord0AttributeTag, attributeIdCounter++);
                    }
                    
                    if(exportVertexColors)
                    {
                        dracoCompressionObject.attributes.Add(Constants.VertexColorAttributeTag, attributeIdCounter++);
                    }

                    primitive.Extensions = new Dictionary<string, object>();

                    primitive.Extensions.Add(Constants.DracoMeshCompressionExtensionTag, dracoCompressionObject);
                }

                primitive.Material = materialIndex;

                primitives.Add(primitive);
            }

            return primitives;
        }

        private bool ExportNormals(Mesh rhinoMesh)
        {
            return rhinoMesh.Normals.Count > 0 && options.ExportVertexNormals;
        }

        private bool ExportTextureCoordinates(Mesh rhinoMesh)
        {
            return rhinoMesh.TextureCoordinates.Count > 0 && options.ExportTextureCoordinates;
        }

        private bool ExportVertexColors(Mesh rhinoMesh)
        {
            return rhinoMesh.VertexColors.Count > 0 && options.ExportVertexColors;
        }

        private bool SetDracoGeometryInfo(Mesh rhinoMesh)
        {
            var dracoComp = DracoCompression.Compress(
                rhinoMesh,
                new DracoCompressionOptions()
                {
                    CompressionLevel = options.DracoCompressionLevel,
                    IncludeNormals = ExportNormals(rhinoMesh),
                    IncludeTextureCoordinates = ExportTextureCoordinates(rhinoMesh),
                    IncludeVertexColors = ExportVertexColors(rhinoMesh),
                    PositionQuantizationBits = options.DracoQuantizationBitsPosition,
                    NormalQuantizationBits = options.DracoQuantizationBitsNormal,
                    TextureCoordintateQuantizationBits = options.DracoQuantizationBitsTexture
                }
            );

            currentGeometryInfo = AddDracoGeometry(dracoComp);

            return currentGeometryInfo.Success;
        }


        private int GetVertexAccessor(MeshVertexList vertices)
        {
            int? vertexBufferViewIdx = GetVertexBufferView(vertices, out Point3d min, out Point3d max, out int countVertices);

            glTFLoader.Schema.Accessor vertexAccessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = vertexBufferViewIdx,
                ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
                Count = countVertices,
                Min = min.ToFloatArray(),
                Max = max.ToFloatArray(),
                Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
                ByteOffset = 0,
            };

            return dummy.Accessors.AddAndReturnIndex(vertexAccessor);
        }

        private int? GetVertexBufferView(MeshVertexList vertices, out Point3d min, out Point3d max, out int countVertices)
        {
            if(options.UseDracoCompression)
            {
                min = currentGeometryInfo.VerticesMin;
                max = currentGeometryInfo.VerticesMax;
                countVertices = currentGeometryInfo.VerticesCount;
                return null;
            }

            int buffer = 0;
            int byteLength = 0;
            int byteOffset = 0;

            if (binary)
            {
                byte[] bytes = GetVertexBytes(vertices, out min, out max);
                buffer = 0;
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                buffer = GetVertexBuffer(vertices, out min, out max, out byteLength);
            }

            glTFLoader.Schema.BufferView vertexBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = buffer,
                ByteOffset = byteOffset,
                ByteLength = byteLength,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
            };

            countVertices = vertices.Count;

            return dummy.BufferViews.AddAndReturnIndex(vertexBufferView);
        }

        private int GetVertexBuffer(MeshVertexList vertices, out Point3d min, out Point3d max, out int length)
        {
            byte[] bytes = GetVertexBytes(vertices, out min, out max);

            length = bytes.Length;

            glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = length,
            };

            return dummy.Buffers.AddAndReturnIndex(buffer);
        }

        private byte[] GetVertexBytes(MeshVertexList vertices, out Point3d min, out Point3d max)
        {
            min = new Point3d(Double.PositiveInfinity,Double.PositiveInfinity,Double.PositiveInfinity);
            max = new Point3d(Double.NegativeInfinity,Double.NegativeInfinity,Double.NegativeInfinity);

            List<float> floats = new List<float>(vertices.Count * 3);

            foreach (Point3d vertex in vertices)
            {
                floats.AddRange(new float[] { (float)vertex.X, (float)vertex.Y, (float)vertex.Z });

                min.X = Math.Min(min.X, vertex.X);
                max.X = Math.Max(max.X, vertex.X);

                min.Y = Math.Min(min.Y, vertex.Y);
                max.Y = Math.Max(max.Y, vertex.Y);

                min.Z = Math.Min(min.Z, vertex.Z);
                max.Z = Math.Max(max.Z, vertex.Z);
            }

            IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

        private int GetIndicesAccessor(MeshFaceList faces, int verticesCount)
        {
            int? indicesBufferViewIdx = GetIndicesBufferView(faces, verticesCount, out float min, out float max, out int indicesCount);

            glTFLoader.Schema.Accessor indicesAccessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = indicesBufferViewIdx,
                Count = indicesCount,
                Min = new float[] { min },
                Max = new float[] { max },
                Type = glTFLoader.Schema.Accessor.TypeEnum.SCALAR,
                ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_INT,
                ByteOffset = 0,
            };

            return dummy.Accessors.AddAndReturnIndex(indicesAccessor);
        }

        private int? GetIndicesBufferView(MeshFaceList faces, int verticesCount, out float min, out float max , out int indicesCount)
        {
            if(options.UseDracoCompression)
            {
                min = currentGeometryInfo.IndicesMin;
                max = currentGeometryInfo.IndicesMax;
                indicesCount = currentGeometryInfo.IndicesCount;
                return null;
            }

            int bufferIndex = 0;
            int byteOffset = 0;
            int byteLength = 0;

            if (binary)
            {
                byte[] bytes = GetIndicesBytes(faces, out indicesCount);
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                bufferIndex = GetIndicesBuffer(faces, out indicesCount, out byteLength);
            }

            glTFLoader.Schema.BufferView indicesBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = bufferIndex,
                ByteOffset = byteOffset,
                ByteLength = byteLength,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ELEMENT_ARRAY_BUFFER,
            };

            min = 0;
            max = verticesCount - 1;

            return dummy.BufferViews.AddAndReturnIndex(indicesBufferView);
        }

        private int GetIndicesBuffer(MeshFaceList faces, out int indicesCount, out int byteLength)
        {
            byte[] bytes = GetIndicesBytes(faces, out indicesCount);

            byteLength = bytes.Length;

            glTFLoader.Schema.Buffer indicesBuffer =  new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };

            return dummy.Buffers.AddAndReturnIndex(indicesBuffer);
        }

        private byte[] GetIndicesBytes(MeshFaceList faces, out int indicesCount)
        {
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

        private int GetNormalsAccessor(MeshVertexNormalList normals)
        {
            int? normalsBufferIdx = GetNormalsBufferView(normals, out Vector3f min, out Vector3f max, out int normalsCount);

            glTFLoader.Schema.Accessor normalAccessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = normalsBufferIdx,
                ByteOffset = 0,
                ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
                Count = normalsCount,
                Min = min.ToFloatArray(),
                Max = max.ToFloatArray(),
                Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
            };

            return dummy.Accessors.AddAndReturnIndex(normalAccessor);
        }

        int? GetNormalsBufferView(MeshVertexNormalList normals, out Vector3f min, out Vector3f max, out int normalsCount)
        {
            if(options.UseDracoCompression)
            {
                min = currentGeometryInfo.NormalsMin;
                max = currentGeometryInfo.NormalsMax;
                normalsCount = currentGeometryInfo.NormalsCount;
                return null;
            }

            int buffer = 0;
            int byteOffset = 0;
            int byteLength = 0;

            if (binary)
            {
                byte[] bytes = GetNormalsBytes(normals, out min, out max);
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                buffer = GetNormalsBuffer(normals, out min, out max, out byteLength);
            }

            glTFLoader.Schema.BufferView normalsBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = buffer,
                ByteLength = byteLength,
                ByteOffset = byteOffset,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
            };

            normalsCount = normals.Count;

            return dummy.BufferViews.AddAndReturnIndex(normalsBufferView);
        }

        int GetNormalsBuffer(MeshVertexNormalList normals, out Vector3f min, out Vector3f max, out int byteLength)
        {
            byte[] bytes = GetNormalsBytes(normals, out min, out max);

            byteLength = bytes.Length;

            glTFLoader.Schema.Buffer normalBuffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };

            return dummy.Buffers.AddAndReturnIndex(normalBuffer);
        }

        byte[] GetNormalsBytes(MeshVertexNormalList normals, out Vector3f min, out Vector3f max)
        {
            min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            //Preallocate
            List<float> floats = new List<float>(normals.Count * 3);

            foreach (Vector3f normal in normals)
            {
                floats.AddRange(new float[] { normal.X, normal.Y, normal.Z });

                min.X = Math.Min(min.X, normal.X);
                max.X = Math.Max(max.X, normal.X);

                min.Y = Math.Min(min.Y, normal.Y);
                max.Y = Math.Max(max.Y, normal.Y);

                max.Z = Math.Max(max.Z, normal.Z);
                min.Z = Math.Min(min.Z, normal.Z);
            }

            IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

        int GetTextureCoordinatesAccessor(MeshTextureCoordinateList textureCoordinates)
        {
            int? textureCoordinatesBufferViewIdx = GetTextureCoordinatesBufferView(textureCoordinates, out Point2f min, out Point2f max, out int countCoordinates);

            glTFLoader.Schema.Accessor textureCoordinatesAccessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = textureCoordinatesBufferViewIdx,
                ByteOffset = 0,
                ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
                Count = countCoordinates,
                Min = min.ToFloatArray(),
                Max = max.ToFloatArray(),
                Type = glTFLoader.Schema.Accessor.TypeEnum.VEC2,
            };

            return dummy.Accessors.AddAndReturnIndex(textureCoordinatesAccessor);
        }

        int? GetTextureCoordinatesBufferView(MeshTextureCoordinateList textureCoordinates, out Point2f min, out Point2f max, out int countCoordinates)
        {
            if(options.UseDracoCompression)
            {
                min = currentGeometryInfo.TexCoordsMin;
                max = currentGeometryInfo.TexCoordsMax;
                countCoordinates = currentGeometryInfo.TexCoordsCount;
                return null;
            }

            int buffer = 0;
            int byteLength = 0;
            int byteOffset = 0;

            if (binary)
            {
                byte[] bytes = GetTextureCoordinatesBytes(textureCoordinates, out min, out max);
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                buffer = GetTextureCoordinatesBuffer(textureCoordinates, out min, out max, out byteLength);
            }

            glTFLoader.Schema.BufferView textureCoordinatesBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = buffer,
                ByteLength = byteLength,
                ByteOffset = byteOffset,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
            };

            countCoordinates = textureCoordinates.Count;

            return dummy.BufferViews.AddAndReturnIndex(textureCoordinatesBufferView);
        }

        int GetTextureCoordinatesBuffer(MeshTextureCoordinateList textureCoordinates, out Point2f min, out Point2f max, out int byteLength)
        {
            byte[] bytes = GetTextureCoordinatesBytes(textureCoordinates, out min, out max);

            glTFLoader.Schema.Buffer textureCoordinatesBuffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };

            byteLength = bytes.Length;

            return dummy.Buffers.AddAndReturnIndex(textureCoordinatesBuffer);
        }

        private byte[] GetTextureCoordinatesBytes(MeshTextureCoordinateList textureCoordinates, out Point2f min, out Point2f max)
        {
            min = new Point2f(float.PositiveInfinity, float.PositiveInfinity);
            max = new Point2f(float.NegativeInfinity, float.NegativeInfinity);

            List<float> coordinates = new List<float>(textureCoordinates.Count * 2);

            foreach (Point2f coordinate in textureCoordinates)
            {
                coordinates.AddRange(new float[] { coordinate.X, coordinate.Y });

                min.X = Math.Min(min.X, coordinate.X);
                max.X = Math.Max(max.X, coordinate.X);

                min.Y = Math.Min(min.Y, coordinate.Y);
                max.Y = Math.Max(max.Y, coordinate.Y);
            }

            IEnumerable<byte> bytesEnumerable = coordinates.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

        private int GetVertexColorAccessor(MeshVertexColorList vertexColors)
        {
            int? vertexColorsBufferViewIdx = GetVertexColorBufferView(vertexColors, out Color4f min, out Color4f max, out int countVertexColors);

            var type = options.UseDracoCompression ? glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE : glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT;

            glTFLoader.Schema.Accessor vertexColorAccessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = vertexColorsBufferViewIdx,
                ByteOffset = 0,
                Count = countVertexColors,
                ComponentType = type,
                Min = min.ToFloatArray(),
                Max = max.ToFloatArray(),
                Type = glTFLoader.Schema.Accessor.TypeEnum.VEC4,
                Normalized = options.UseDracoCompression,
            };

            return dummy.Accessors.AddAndReturnIndex(vertexColorAccessor);
        }

        int? GetVertexColorBufferView(MeshVertexColorList vertexColors, out Color4f min, out Color4f max, out int countVertexColors)
        {
            if(options.UseDracoCompression)
            {
                min = currentGeometryInfo.VertexColorMin;
                max = currentGeometryInfo.VertexColorMax;
                countVertexColors = currentGeometryInfo.VertexColorCount;
                return null;
            }

            int buffer = 0;
            int byteLength = 0;
            int byteOffset = 0;

            if (binary)
            {
                byte[] bytes = GetVertexColorBytes(vertexColors, out min, out max);
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                buffer = GetVertexColorBuffer(vertexColors, out min, out max, out byteLength);
            }

            glTFLoader.Schema.BufferView vertexColorsBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = buffer,
                ByteLength = byteLength,
                ByteOffset = byteOffset,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
            };

            countVertexColors = vertexColors.Count;

            return dummy.BufferViews.AddAndReturnIndex(vertexColorsBufferView);
        }

        int GetVertexColorBuffer(MeshVertexColorList vertexColors, out Color4f min, out Color4f max, out int byteLength)
        {
            byte[] bytes = GetVertexColorBytes(vertexColors, out min, out max);

            glTFLoader.Schema.Buffer vertexColorsBuffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };

            byteLength = bytes.Length;

            return dummy.Buffers.AddAndReturnIndex(vertexColorsBuffer);
        }

        byte[] GetVertexColorBytes(MeshVertexColorList vertexColors, out Color4f min, out Color4f max)
        {
            float [] minArr = new float[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
            float [] maxArr = new float[] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

            List<float> colors = new List<float>(vertexColors.Count * 4);

            for(int i = 0; i < vertexColors.Count; i++)
            {
                Color4f color = new Color4f(vertexColors[i]);
                
                colors.AddRange(color.ToFloatArray());

                minArr[0] = Math.Min(minArr[0], color.R);
                minArr[1] = Math.Min(minArr[1], color.G);
                minArr[2] = Math.Min(minArr[2], color.B);
                minArr[3] = Math.Min(minArr[3], color.A);

                maxArr[0] = Math.Max(maxArr[0], color.R);
                maxArr[1] = Math.Max(maxArr[1], color.G);
                maxArr[2] = Math.Max(maxArr[2], color.B);
                maxArr[3] = Math.Max(maxArr[3], color.A);
            }

            min = new Color4f(minArr[0], minArr[1], minArr[2], minArr[3]);
            max = new Color4f(maxArr[0], maxArr[1], maxArr[2], maxArr[3]);

            IEnumerable<byte> bytesEnumerable = colors.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

        public DracoGeometryInfo AddDracoGeometry(DracoCompression dracoCompression)
        {
            var dracoGeoInfo = new DracoGeometryInfo();

            string fileName = Path.GetTempFileName();

            try
            {
                dracoCompression.Write(fileName);

                byte[] dracoBytes = GetDracoBytes(fileName);

                WriteDracoBytes(dracoBytes, out dracoGeoInfo.BufferIndex, out dracoGeoInfo.ByteOffset, out dracoGeoInfo.ByteLength);

                glTFLoader.Schema.BufferView compMeshBufferView = new glTFLoader.Schema.BufferView()
                {
                    Buffer = dracoGeoInfo.BufferIndex,
                    ByteOffset = dracoGeoInfo.ByteOffset,
                    ByteLength = dracoGeoInfo.ByteLength,
                };

                dracoGeoInfo.BufferViewIndex = dummy.BufferViews.AddAndReturnIndex(compMeshBufferView);

                dracoGeoInfo.ByteLength = dracoBytes.Length;

                var geo = DracoCompression.DecompressFile(fileName);
                if (geo.ObjectType == Rhino.DocObjects.ObjectType.Mesh)
                {
                    var mesh = (Rhino.Geometry.Mesh)geo;

                    // Vertices Stats
                    dracoGeoInfo.VerticesCount = mesh.Vertices.Count;
                    dracoGeoInfo.VerticesMin = new Point3d(mesh.Vertices.Min());
                    dracoGeoInfo.VerticesMax = new Point3d(mesh.Vertices.Max());

                    dracoGeoInfo.IndicesCount = mesh.Faces.TriangleCount;
                    dracoGeoInfo.IndicesMin = 0;
                    dracoGeoInfo.IndicesMax = dracoGeoInfo.VerticesCount - 1;

                    dracoGeoInfo.NormalsCount = mesh.Normals.Count;
                    dracoGeoInfo.NormalsMin = mesh.Normals.Min();
                    dracoGeoInfo.NormalsMax = mesh.Normals.Max();

                    // TexCoord Stats
                    dracoGeoInfo.TexCoordsCount = mesh.TextureCoordinates.Count;
                    if (dracoGeoInfo.TexCoordsCount > 0)
                    {
                        dracoGeoInfo.TexCoordsMin = mesh.TextureCoordinates.Min();
                        dracoGeoInfo.TexCoordsMax = mesh.TextureCoordinates.Max();
                    }

                    dracoGeoInfo.VertexColorCount = mesh.VertexColors.Count;
                    dracoGeoInfo.VertexColorMin = Color4f.Black;
                    dracoGeoInfo.VertexColorMax = Color4f.White;

                    dracoGeoInfo.Success = true;
                }
                geo.Dispose();
                dracoCompression.Dispose();
            }
            finally
            {
                File.Delete(fileName);
            }

            return dracoGeoInfo;
        }

        private byte[] GetDracoBytes(string fileName)
        {
            using (FileStream stream = File.Open(fileName, FileMode.Open))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);

                return bytes;
            }
        }

        public void WriteDracoBytes(byte[] bytes, out int bufferIndex, out int byteOffset, out int byteLength)
        {
            byteLength = bytes.Length;

            if (binary)
            {
                byteOffset = (int)binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
                bufferIndex = 0;
            }
            else
            {
                glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
                {
                    Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                    ByteLength = bytes.Length,
                };
                bufferIndex = dummy.Buffers.AddAndReturnIndex(buffer);
                byteOffset = 0;
            }
        }

    }
}
