using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Stykka.Common.glTF
{
    public static class GlConstants
    {
        // componentType
        // Byte size = 1
        public static int BYTE = 5120;
        public static int UNSIGNED_BYTE = 5121;
        // Byte size = 2
        public static int SHORT = 5122;
        public static int UNSIGNED_SHORT = 5123;
        // Byte size = 4
        public static int UNSIGNED_INT = 5125;
        public static int FLOAT = 5126;

        // type
        // #components=1
        public static string SCALAR = "SCALAR";
        // #components=2
        public static string VEC2 = "VEC2";
        // #components=3
        public static string VEC3 = "VEC3";
        // #components=4
        public static string VEC4 = "VEC4";
        // #components=4
        public static string MAT2 = "MAT2";
        // #components=9
        public static string MAT3 = "MAT3";
        // #components=16
        public static string MAT4 = "MAT4";

        // bufferview.target
        // array of data such as vertices
        public static int ARRAY_BUFFER = 34962;
        // array of indices
        public static int ELEMENT_ARRAY_BUFFER = 34963;

        public static uint GLB_MAGIC_BYTE = 0x46546C67;
        public static uint CHUNK_TYPE_JSON = 0x4E4F534A;
        public static uint CHUNK_TYPE_BINARY = 0x004E4942;
    }

    public class Material
    {
        public string name;
        public MaterialMetalicRoughness pbrMetallicRoughness;

        public Material(Rhino.DocObjects.Material material)
        {
            name = material.Name;

            var diffuseColor = new float[4] {
                Convert.ToSingle(Convert.ToInt32(material.DiffuseColor.R)) / 255.0f,
                Convert.ToSingle(Convert.ToInt32(material.DiffuseColor.G)) / 255.0f,
                Convert.ToSingle(Convert.ToInt32(material.DiffuseColor.B)) / 255.0f,
                1.0f
            };

            pbrMetallicRoughness = new MaterialMetalicRoughness() {
                baseColorFactor = diffuseColor,
                metallicFactor = 0.2f,
                roughnessFactor = (float)material.ReflectionGlossiness
            };
        }
    }

    public class MaterialMetalicRoughness
    {
        public float[] baseColorFactor;
        public float metallicFactor;
        public float roughnessFactor;

        public MaterialMetalicRoughness()
        {
            baseColorFactor = new float[4] { 0.3f, 0.3f, 0.3f, 1.0f };
            metallicFactor = 0.3f;
            roughnessFactor = 0.3f;
        }
    }

    public class RootModel
    {
        public int scene;
        public List<Scene> scenes;

        public List<Node> nodes;

        public List<Mesh> meshes;

        public List<Buffer> buffers;

        public List<BufferView> bufferViews;

        public List<Accessor> accessors;

        public Asset asset;

        [JsonIgnore]
        public List<Rhino.DocObjects.Material> tempMaterials;
        public List<Material> materials;

        [JsonIgnore]
        public bool IsGLBinaryMode;

        public RootModel(bool IsGLBinaryMode)
        {
            this.IsGLBinaryMode = IsGLBinaryMode;
            asset = new Asset();
            scenes = new List<Scene>() { new Scene() };
            nodes = new List<Node>();
            meshes = new List<Mesh>();
            buffers = new List<Buffer>();
            bufferViews = new List<BufferView>();
            accessors = new List<Accessor>();
            tempMaterials = new List<Rhino.DocObjects.Material>();
            materials = new List<Material>();
        }

        public string SerializeToJSON()
        {
            // Prep
            materials = new List<Material>(tempMaterials.Select(m => new Material(m)));

            var settings = new JsonSerializerSettings
            {
                // This is _super_ important. Otherwise null values are sprayed into the gltf.
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.SerializeObject(this, Formatting.Indented, settings);
        }

        public static void PadRightWithValue(in MemoryStream memoryStream, byte padValue) 
        {
            int padLength = 4 - ((int)memoryStream.Length % 4);
            if (padLength != 4)
            {
                byte[] padding = Enumerable.Repeat(padValue, padLength).ToArray();
                memoryStream.Write(padding, 0, padLength);
            }
        }

        public void SerializeToGLB(in MemoryStream memoryStream)
        {
            // "buffers": [
            //    {
            //        "byteLength": 35884
            //    },
            //{
            //        "byteLength": 504,
            //    "uri": "external.bin"
            //}

            // First dump all the data
            using (MemoryStream binaryChunk = new MemoryStream(4096))
            using(MemoryStream jsonChunk = new MemoryStream(4096))
            {
                // ChunkHeader = { <chunklength (inc. header=8 bytes)>, <chunky type = binary> }
                uint[] binChunkHeader = new uint[] {
                    // Temporarily setting the length to zero, because we don't know it yet.
                    Convert.ToUInt32(0),
                    GlConstants.CHUNK_TYPE_BINARY
                };
                byte[] binChunkHeaderBytes = binChunkHeader.SelectMany(BitConverter.GetBytes).ToArray();
                binaryChunk.Write(binChunkHeaderBytes, 0, binChunkHeaderBytes.Length);
                foreach (var b in buffers)
                {
                    byte[] flatBytes = b.RawBytes.SelectMany(i => i).ToArray();
                    b.binaryOffset = (int)binaryChunk.Position - binChunkHeaderBytes.Length;
                    binaryChunk.Write(flatBytes, 0, flatBytes.Length);
                }
                int binaryChunkUnpaddedLength = (int)binaryChunk.Length - 8;
                byte padBinaryValue = 0x00;
                PadRightWithValue(binaryChunk, padBinaryValue);
                // Go back and set the length, because we know it now.
                binaryChunk.Seek(0, SeekOrigin.Begin);
                binaryChunk.Write(BitConverter.GetBytes(Convert.ToUInt32(binaryChunk.Length - 8)), 0, 4);
                binaryChunk.Seek(0, SeekOrigin.End);

                // Change the bufferviews to point to the binary blob with an offset
                foreach (var bv in bufferViews)
                {
                    bv.buffer = 0;
                    bv.byteOffset = bv.bufferRef.binaryOffset;
                }

                // TODO: Throwing away all json-buffers here. There could still be valid buffers for extern resources.
                buffers.Clear();
                buffers.Add(new Buffer(false) { byteLength = binaryChunkUnpaddedLength, uri = null });

                // JSON Chunk
                string json = this.SerializeToJSON();
                //Console.WriteLine(json);
                //byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
                byte[] jsonBytes = Encoding.ASCII.GetBytes(json);
                // ChunkHeader = { <chunklength (inc. header=8 bytes)>, <chunky type = json> }
                uint[] jsonChunkHeader = new uint[] {
                    Convert.ToUInt32(0),
                    GlConstants.CHUNK_TYPE_JSON
                };
                byte[] jsonChunkHeaderBytes = jsonChunkHeader.SelectMany(BitConverter.GetBytes).ToArray();
                jsonChunk.Write(jsonChunkHeaderBytes, 0, jsonChunkHeaderBytes.Length);
                jsonChunk.Write(jsonBytes, 0, jsonBytes.Length);
                byte padJSONValue = 0x20;
                PadRightWithValue(jsonChunk, padJSONValue);
                // Go back and set the length, because we know it now.
                jsonChunk.Seek(0, SeekOrigin.Begin);
                jsonChunk.Write(BitConverter.GetBytes(Convert.ToUInt32(jsonChunk.Length - 8)), 0, 4);
                jsonChunk.Seek(0, SeekOrigin.End);

                // Then write the header { <magic byte>, <gltf version>, <total size of file> }
                // TODO: Don't forget to change the size param (the last)!!!
                uint[] header = new uint[3] {
                    GlConstants.GLB_MAGIC_BYTE,
                    Convert.ToUInt32(2),
                    Convert.ToUInt32(12 + jsonChunk.Length + binaryChunk.Length)
                };
                byte[] headerBytes = header.SelectMany(v => BitConverter.GetBytes(v)).ToArray();
                memoryStream.Write(headerBytes, 0, headerBytes.Length);

                // Copy the jsonChunk to the memory stream
                jsonChunk.WriteTo(memoryStream);

                // Copy the binChunk to the memory stream
                binaryChunk.WriteTo(memoryStream);
            }
        }

        public void AddRhinoObject(in RhinoObject rhinoObject)
        {
            // First make sure the internal rhino mesh has been created
            rhinoObject.CreateMeshes(MeshType.Preview, MeshingParameters.FastRenderMesh, false);
            // Then get the internal rhino meshes
            Rhino.Geometry.Mesh[] rhinoMeshes = rhinoObject.GetMeshes(MeshType.Preview);

            var mat = rhinoObject.GetMaterial(true);

            int currentMaterialIdx = tempMaterials.FindIndex(m => m.Id == mat.Id);
            if (currentMaterialIdx == -1)
            {
                tempMaterials.Add(mat);
                currentMaterialIdx = tempMaterials.Count - 1;
            }

            var primitives = new List<Primitive>();
            // For each rhino mesh, create gl-buffers, gl-meshes, etc.
            foreach (var rhinoMesh in rhinoMeshes)
            {
                // Create buffers for data (position, normals, indices etc.)
                var vtxBuffer = new Buffer(IsGLBinaryMode);

                var min = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };
                var max = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };
                foreach (var p in rhinoMesh.Vertices)
                {
                    vtxBuffer.Add(p);

                    min.X = Math.Min(min.X, p.X);
                    // Switch Y<=>Z for GL coords
                    min.Y = Math.Min(min.Y, p.Z);
                    min.Z = Math.Min(min.Z, -p.Y); 

                    max.X = Math.Max(max.X, p.X);
                    // Switch Y<=>Z for GL coords
                    max.Y = Math.Max(max.Y, p.Z);
                    max.Z = Math.Max(max.Z, -p.Y);
                }
                buffers.Add(vtxBuffer);
                int vtxBufferIdx = buffers.Count - 1;

                var idsBuffer = new Buffer(IsGLBinaryMode);
                foreach (var f in rhinoMesh.Faces)
                {
                    idsBuffer.Add(f);
                }
                buffers.Add(idsBuffer);
                int idsBufferIdx = buffers.Count - 1;

                // Create bufferviews
                var vtxBufferView = new BufferView() { bufferRef = vtxBuffer, buffer = vtxBufferIdx, byteOffset = 0, byteLength = vtxBuffer.byteLength, target = GlConstants.ARRAY_BUFFER };
                bufferViews.Add(vtxBufferView);
                int vtxBufferViewIdx = bufferViews.Count - 1;

                var idsBufferView = new BufferView() { bufferRef = idsBuffer, buffer = idsBufferIdx, byteOffset = 0, byteLength = idsBuffer.byteLength, target = GlConstants.ELEMENT_ARRAY_BUFFER };
                bufferViews.Add(idsBufferView);
                int idsBufferViewIdx = bufferViews.Count - 1;

                // Create accessors
                var vtxAccessor = Accessor.AccessorVec3();
                vtxAccessor.bufferView = vtxBufferViewIdx;
                vtxAccessor.count = vtxBuffer.PrimitiveCount;
                vtxAccessor.min = new float[] { (float)min.X, (float)min.Y, (float)min.Z };
                vtxAccessor.max = new float[] { (float)max.X, (float)max.Y, (float)max.Z };
                accessors.Add(vtxAccessor);
                int vtxAccessorIdx = accessors.Count - 1;

                var idsAccessor = Accessor.AccessorScalar();
                idsAccessor.bufferView = idsBufferViewIdx;
                idsAccessor.count = idsBuffer.PrimitiveCount;
                accessors.Add(idsAccessor);
                int idsAccessorIdx = accessors.Count - 1;

                // Create primitives
                var attribute = new Attribute() { POSITION = vtxAccessorIdx };
                var primitive = new Primitive() { attributes = attribute, indices = idsAccessorIdx, material = currentMaterialIdx };

                // Create mesh
                primitives.Add(primitive);
            }
            var mesh = new Mesh() { primitives = primitives };
            meshes.Add(mesh);

            var node = new Node() { mesh = meshes.Count - 1 };
            nodes.Add(node);

            scenes[scene].nodes.Add(nodes.Count - 1);
        }
    }

    public class Asset {
        public string version;

        public Asset()
        {
            version = "2.0";
        }
    }

    public class Mesh
    {
        public IEnumerable<Primitive> primitives;

        public Mesh() { }
    }

    public class Primitive
    {
        public Attribute attributes;
        public int indices;
        public int material;
    }

    public class Attribute
    {
        public int POSITION;
    }

    public class Buffer
    {
        public string uri;
        public int byteLength;

        [JsonIgnore]
        public int PrimitiveCount;

        [JsonIgnore]
        public bool IsGLBinaryMode;

        // dump to byte[] with ".flatten" + .ToArray();
        [JsonIgnore]
        public List<IEnumerable<byte>> RawBytes;

        [JsonIgnore]
        public int binaryOffset;

        public Buffer(bool IsGLBinaryMode)
        {
            this.IsGLBinaryMode = IsGLBinaryMode;
            RawBytes = new List<IEnumerable<byte>>();

            if (!IsGLBinaryMode)
            {
                uri = "data:application/octet-stream;base64,";
            }
        }

        public void Add(Point3d point)
        {
            // Switch GL coords for Y<=>Z
            float[] coords = new float[] { (float)point.X, (float)point.Z, -(float)point.Y };
            IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));
            if (!IsGLBinaryMode)
            {
                uri += Convert.ToBase64String(byteList.ToArray());
                // 4 bytes / float * 3 (x,y,z)
                //byteLength += 4 * 3;
            } else
            {
                RawBytes.Add(byteList);
            }
            byteLength += byteList.Count();
            PrimitiveCount += 1;
        }

        public void Add(MeshFace face)
        {
            if (face.IsTriangle)
            {
                // If the face is a triangle, we serialize the 3 indices to b64
                // NOTE: A, B, C produces f
                int[] coords = new int[] { face.A, face.B, face.C };
                IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));
                if (!IsGLBinaryMode)
                {
                    uri += Convert.ToBase64String(byteList.ToArray());
                    // 4 bytes / int * 3 (A, B, C)
                    //byteLength += 4 * 3;
                } else
                {
                    RawBytes.Add(byteList);
                }
                byteLength += byteList.Count();
                PrimitiveCount += 3;
            } else
            {
                // If the face is a quad, we serialize the 4 indices in two batches of 3 to b64
                int[] coords = new int[] { face.A, face.B, face.C, face.A, face.C, face.D };
                IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));
                if (!IsGLBinaryMode)
                {
                    uri += Convert.ToBase64String(byteList.ToArray());
                    // 4 bytes / int * 6 (A, B, C, A, C, D)
                    //byteLength += 4 * 6;
                } else
                {
                    RawBytes.Add(byteList);
                }
                byteLength += byteList.Count();
                PrimitiveCount += 6;
            }
        }
    }

    public class BufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int target;

        [JsonIgnore]
        public Buffer bufferRef;
    }

    public class Accessor
    {
        public int bufferView;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
        public float[] max;
        public float[] min;

        public static Accessor AccessorScalar()
        {
            return new Accessor()
            {
                byteOffset = 0,
                componentType = GlConstants.UNSIGNED_INT,
                type = GlConstants.SCALAR,
                max = null,
                min = null
            };
        }

        public static Accessor AccessorVec3()
        {
            return new Accessor()
            {
                byteOffset = 0,
                componentType = GlConstants.FLOAT,
                type = GlConstants.VEC3,
                max = new float[] { 1.0f, 1.0f, 1.0f },
                min = new float[] { 0.0f, 0.0f, 0.0f }
            };
        }
    }


    public class Scene
    {
        public List<int> nodes;

        public Scene()
        {
            nodes = new List<int>();
        }
    }

    public class Node
    {
        //// This is this nodes assigned index in the node array.
        //public int nodesArrayIndex;
        //public Matrix matrix;
        //public List<int> children;
        public int mesh;
    }

    public class Matrix
    {
        public List<double> values;
    }
}
