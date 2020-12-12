using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Render;

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

        [JsonIgnore]
        public Guid Id;

        public Material(Rhino.DocObjects.Material material)
        {
            Id = material.Id;
            name = material.Name;

            var diffuseColor = new float[4] {
                Convert.ToSingle(Convert.ToInt32(material.DiffuseColor.R)) / 255.0f,
                Convert.ToSingle(Convert.ToInt32(material.DiffuseColor.G)) / 255.0f,
                Convert.ToSingle(Convert.ToInt32(material.DiffuseColor.B)) / 255.0f,
                1.0f
            };

            pbrMetallicRoughness = new MaterialMetalicRoughness() {
                baseColorFactor = diffuseColor,
                metallicFactor = (float)material.Reflectivity,
                roughnessFactor = 0.5f
            };
        }

        public Material(Rhino.DocObjects.PhysicallyBasedMaterial physMat)
        {
            Id = physMat.Material.Id;
            name = physMat.Material.Name;


            var diffuseColor = new float[4] {
                physMat.BaseColor.R,
                physMat.BaseColor.G,
                physMat.BaseColor.B,
                physMat.BaseColor.A
            };

            pbrMetallicRoughness = new MaterialMetalicRoughness()
            {
                baseColorFactor = diffuseColor,
                metallicFactor = (float)physMat.Metallic,
                roughnessFactor = (float)physMat.Roughness
            };
        }
    }

    public class BaseColorTexture
    {
        public int index;
        public int texCoord;
    }

    public class Texture
    {
        public int source;
        public int sampler;
    }

    public class Image
    {
        public int bufferView;
        public string mimeType;
    }

    public class Sampler
    {
        public int magFilter = 9792;
        public int minFilter = 9987;
        public int wrapS = 10497;
        public int wrapT = 10497;
    }

    public class MaterialMetalicRoughness
    {
        public float[] baseColorFactor;
        public float metallicFactor;
        public float roughnessFactor;
        public BaseColorTexture baseColorTexture;
        public BaseColorTexture metallicRoughnessTexture;

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

        public List<Texture> textures;

        public List<Image> images;

        public List<Material> materials;

        public List<Sampler> samplers;

        public Asset asset;

        public string[] extensionsRequired = { "KHR_draco_mesh_compression" };
        public bool ShouldSerializeextensionsRequired()
        {
            return ExportOptions.UseDracoCompression;
        }
        public string[] extensionsUsed = { "KHR_draco_mesh_compression" };
        public bool ShouldSerializeextensionsUsed()
        {
            return ExportOptions.UseDracoCompression;
        }

        [JsonIgnore]
        public ExportOptions ExportOptions;

        public RootModel(ExportOptions exportOptions)
        {
            this.ExportOptions = exportOptions;
            asset = new Asset();
            scenes = new List<Scene>() { new Scene() };
            nodes = new List<Node>();
            meshes = new List<Mesh>();
            buffers = new List<Buffer>();
            bufferViews = new List<BufferView>();
            accessors = new List<Accessor>();
            materials = new List<Material>();
            images = new List<Image>();
            textures = new List<Texture>();
            // TODO: Using a default sampler here.
            // Put a default sampler in pos0
            samplers = new List<Sampler>() { new Sampler() };
        }

        public string SerializeToJSON()
        {
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
            using (MemoryStream jsonChunk = new MemoryStream(4096))
            {
                // ChunkHeader = { <chunklength (inc. header=8 bytes)>, <chunky type = binary> }
                uint[] binChunkHeader = new uint[] {
                    // Temporarily setting the length to zero, because we don't know it yet.
                    Convert.ToUInt32(0),
                    GlConstants.CHUNK_TYPE_BINARY
                };
                byte[] binChunkHeaderBytes = binChunkHeader.SelectMany(BitConverter.GetBytes).ToArray();
                binaryChunk.Write(binChunkHeaderBytes, 0, binChunkHeaderBytes.Length);

                // Dump the buffers
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

        public void AddRhinoObjectDraco(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material)
        {
            int currentMaterialIdx = materials.FindIndex(m => m.Id == material.Id);
            if (currentMaterialIdx == -1)
            {
                AddMaterial(material);
                currentMaterialIdx = materials.Count - 1;
            }

            var primitives = new List<Primitive>();

            var transformYUp = Transform.Identity;
            // Leave X as is
            // Change Y to Z
            transformYUp.M11 = 0;
            transformYUp.M12 = 1;

            // Change Z to -Y
            transformYUp.M21 = -1;
            transformYUp.M22 = 0;

            // For each rhino mesh, create gl-buffers, gl-meshes, etc.
            foreach (var rhinoMesh in rhinoMeshes)
            {
                rhinoMesh.Transform(transformYUp);
                rhinoMesh.TextureCoordinates.ReverseTextureCoordinates(1);

                var dracoComp = DracoCompression.Compress(
                    rhinoMesh,
                    new DracoCompressionOptions() {
                        CompressionLevel = ExportOptions.DracoCompressionLevel,
                        IncludeNormals = true,
                        IncludeTextureCoordinates = true,
                        IncludeVertexColors = false,
                        PositionQuantizationBits = ExportOptions.DracoQuantizationBits,
                        NormalQuantizationBits = ExportOptions.DracoQuantizationBits,
                        TextureCoordintateQuantizationBits = ExportOptions.DracoQuantizationBits
                    }
                );

                // Create buffers for data (position, normals, indices etc.)
                var compMeshBuffer = new Buffer(ExportOptions.UseBinary);
                buffers.Add(compMeshBuffer);
                int compMeshBufferIdx = buffers.Count - 1;

                var dracoGeoInfo = compMeshBuffer.Add(dracoComp);

                // Create bufferviews
                var byteCount = compMeshBuffer.RawBytes.SelectMany(i => i).Count();
                var compMeshBufferView = new BufferView() { bufferRef = compMeshBuffer, buffer = compMeshBufferIdx, byteOffset = 0, byteLength = byteCount };
                bufferViews.Add(compMeshBufferView);
                int compMeshBufferViewIdx = bufferViews.Count - 1;

                // Create accessors
                // Accessor POSITION
                var vtxAccessor = new AccessorVec3
                {
                    count = dracoGeoInfo.verticesNum,
                    min = dracoGeoInfo.verticesMin,
                    max = dracoGeoInfo.verticesMax
                };
                accessors.Add(vtxAccessor);
                int vtxAccessorIdx = accessors.Count - 1;

                // // Accessor Triangles Vertex IDs
                var idsAccessor = new AccessorScalar
                {
                    count = dracoGeoInfo.trianglesNum,
                    min = dracoGeoInfo.trianglesMin,
                    max = dracoGeoInfo.trianglesMax
                };
                accessors.Add(idsAccessor);
                int idsAccessorIdx = accessors.Count - 1;

                // Accessor Normals
                var normalsAccessor = new AccessorVec3
                {
                    count = dracoGeoInfo.normalsNum,
                    min = dracoGeoInfo.normalsMin,
                    max = dracoGeoInfo.normalsMax
                };
                accessors.Add(normalsAccessor);
                int normalsAccessorIdx = accessors.Count - 1;

                // Accessor TexCoords
                var texCoordsAccessor = new AccessorVec2
                {
                    count = dracoGeoInfo.texCoordsNum,
                    min = dracoGeoInfo.texCoordsMin,
                    max = dracoGeoInfo.texCoordsMax
                };
                accessors.Add(texCoordsAccessor);
                int texCoordsAccessorIdx = accessors.Count - 1;

                // Create primitives
                var attribute = new Attribute() { POSITION = vtxAccessorIdx, NORMAL = normalsAccessorIdx, TEXCOORD_0 = texCoordsAccessorIdx };
                var primitive = new Primitive() {
                    attributes = attribute,
                    indices = idsAccessorIdx,
                    material = currentMaterialIdx,
                    extensions = new {
                        KHR_draco_mesh_compression = new {
                            bufferView = compMeshBufferViewIdx,
                            attributes = new {
                                POSITION = 0,
                                NORMAL = 1,
                                TEXCOORD_0 = 2
                            }
                        }
                    }
                };

                // Create mesh
                primitives.Add(primitive);
            }
            var mesh = new Mesh() { primitives = primitives };
            meshes.Add(mesh);

            var node = new Node() { mesh = meshes.Count - 1 };
            nodes.Add(node);

            scenes[scene].nodes.Add(nodes.Count - 1);
        }

        private BaseColorTexture AddPBRBaseTexture(string texturePath) {
            var textureBuffer = new Buffer(ExportOptions.UseBinary);
            textureBuffer.ReadFileFromPath(texturePath);
            buffers.Add(textureBuffer);
            int textureBufferIdx = buffers.Count - 1;

            // Create bufferviews
            var byteCount = textureBuffer.RawBytes.SelectMany(i => i).Count();
            var textureBufferView = new BufferView() { bufferRef = textureBuffer, buffer = textureBufferIdx, byteOffset = 0, byteLength = byteCount };
            bufferViews.Add(textureBufferView);
            int textureBufferViewIdx = bufferViews.Count - 1;

            var image = new Image() { bufferView = textureBufferViewIdx, mimeType = "image/png" };
            images.Add(image);
            int imageIdx = images.Count - 1;

            var texture = new Texture() { source = imageIdx, sampler = 0 };
            textures.Add(texture);
            int textureIdx = textures.Count - 1;

            return new BaseColorTexture() { index = textureIdx, texCoord = 0 };
        }

        public BaseColorTexture AddMetallicRoughnessTexture(Rhino.DocObjects.Material rhinoMaterial) {
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
            if (isMetalTexture) {
                // Get the texture
                renderTextureMetal = rhinoMaterial.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrMetallic);
                // Figure out the size of the textures
                renderTextureMetal.PixelSize(out mWidth, out mHeight, out int _w0);
            }

            if (isRoughnessTexture) {
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
            if (isMetalTexture) {
                var evalMetal = renderTextureMetal.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
                imgMetal = evalMetal.WriteToByteArray(width, height).ToArray();
            }

            // Roughness
            if (isRoughnessTexture) {
                var evalRoughness = renderTextureRoughness.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
                imgRoughness = evalRoughness.WriteToByteArray(width, height).ToArray();
            }

            // Copy Metal to the blue channel, roughness to the green
            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            for (var y = 0; y < height - 1; y += 1) {
                for (var x = 0; x < width - 1; x += 1)
                {
                    int g = 0;
                    int b = 0;
                    if (isMetalTexture) {
                        b = imgMetal[(y * width + x) * 4 + 1]; // Note: Not sure if this offset is the red, or the green channel
                    }

                    if (isRoughnessTexture) {
                        g = imgRoughness[(y * width + x) * 4 + 2]; // Note: Not sure if this offset is the green or the blue channel
                    }

                    bitmap.SetPixel(x, height - y - 1, Color.FromArgb(0, g, b));
                }
            }

            // DEBUG
            //bitmap.Save("/Users/aske/Projects/glTF-BinExporter/metallicRoughness.png", System.Drawing.Imaging.ImageFormat.Png);

            var textureBuffer = new Buffer(ExportOptions.UseBinary);

            using (MemoryStream imageStream = new MemoryStream(4096))
            {
                bitmap.Save(imageStream, System.Drawing.Imaging.ImageFormat.Png);
                imageStream.Flush();

                textureBuffer.ReadPNGFromStream(imageStream);
            }

            buffers.Add(textureBuffer);
            int textureBufferIdx = buffers.Count - 1;

            // Create bufferviews
            var byteCount = textureBuffer.RawBytes.SelectMany(i => i).Count();
            var textureBufferView = new BufferView() { bufferRef = textureBuffer, buffer = textureBufferIdx, byteOffset = 0, byteLength = byteCount };
            bufferViews.Add(textureBufferView);
            int textureBufferViewIdx = bufferViews.Count - 1;

            var image = new Image() { bufferView = textureBufferViewIdx, mimeType = "image/png" };
            images.Add(image);
            int imageIdx = images.Count - 1;

            var texture = new Texture() { source = imageIdx, sampler = 0 };
            textures.Add(texture);
            int textureIdx = textures.Count - 1;

            return new BaseColorTexture() { index = textureIdx, texCoord = 0 };
        }

        public void AddMaterial(Rhino.DocObjects.Material rhinoMaterial) {
            // Prep
            var material = rhinoMaterial.IsPhysicallyBased ? new Material(rhinoMaterial.PhysicallyBased) : new Material(rhinoMaterial);

            // Textures
            Rhino.DocObjects.Texture baseColorTexture;
            Rhino.DocObjects.Texture metallicTexture;
            Rhino.DocObjects.Texture roughnessTexture;

            if (rhinoMaterial.IsPhysicallyBased) { 
                baseColorTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_BaseColor);
                metallicTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Metallic);
                roughnessTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Roughness);
            } else {
                baseColorTexture = rhinoMaterial.GetTexture(TextureType.PBR_BaseColor);
                metallicTexture = rhinoMaterial.GetTexture(TextureType.PBR_Metallic);
                roughnessTexture = rhinoMaterial.GetTexture(TextureType.PBR_Roughness);
            }

            if (baseColorTexture != null) {
                material.pbrMetallicRoughness.baseColorTexture = AddPBRBaseTexture(baseColorTexture.FileReference.FullPath);
            }

            if (metallicTexture != null || roughnessTexture != null)
            {
                material.pbrMetallicRoughness.metallicRoughnessTexture = AddMetallicRoughnessTexture(rhinoMaterial);
            }

            materials.Add(material);
        }

        public void AddRhinoObject(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material)
        {
            //int currentMaterialIdx = materials.FindIndex(m => m.Id == material.Id);
            //if (currentMaterialIdx == -1)
            //{
            //    AddMaterial(material);
            //    currentMaterialIdx = materials.Count - 1;
            //}

            //var primitives = new List<Primitive>();
            //// For each rhino mesh, create gl-buffers, gl-meshes, etc.
            //foreach (var rhinoMesh in rhinoMeshes)
            //{
            //    // Create buffers for data (position, normals, indices etc.)
            //    var vtxBuffer = new Buffer(IsGLBinaryMode);

            //    var min = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };
            //    var max = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };
            //    foreach (var p in rhinoMesh.Vertices)
            //    {
            //        vtxBuffer.Add(p);

            //        min.X = Math.Min(min.X, p.X);
            //        // Switch Y<=>Z for GL coords
            //        min.Y = Math.Min(min.Y, p.Z);
            //        min.Z = Math.Min(min.Z, -p.Y);

            //        max.X = Math.Max(max.X, p.X);
            //        // Switch Y<=>Z for GL coords
            //        max.Y = Math.Max(max.Y, p.Z);
            //        max.Z = Math.Max(max.Z, -p.Y);
            //    }
            //    buffers.Add(vtxBuffer);
            //    int vtxBufferIdx = buffers.Count - 1;

            //    var idsBuffer = new Buffer(IsGLBinaryMode);
            //    foreach (var f in rhinoMesh.Faces)
            //    {
            //        idsBuffer.Add(f);
            //    }
            //    buffers.Add(idsBuffer);
            //    int idsBufferIdx = buffers.Count - 1;

            //    // Create bufferviews
            //    var vtxBufferView = new BufferView() { bufferRef = vtxBuffer, buffer = vtxBufferIdx, byteOffset = 0, byteLength = vtxBuffer.byteLength, target = GlConstants.ARRAY_BUFFER };
            //    bufferViews.Add(vtxBufferView);
            //    int vtxBufferViewIdx = bufferViews.Count - 1;

            //    var idsBufferView = new BufferView() { bufferRef = idsBuffer, buffer = idsBufferIdx, byteOffset = 0, byteLength = idsBuffer.byteLength, target = GlConstants.ELEMENT_ARRAY_BUFFER };
            //    bufferViews.Add(idsBufferView);
            //    int idsBufferViewIdx = bufferViews.Count - 1;

            //    // Create accessors
            //    var vtxAccessor = new AccessorVec3
            //    {
            //        bufferView = vtxBufferViewIdx,
            //        count = vtxBuffer.PrimitiveCount,
            //        min = new float[] { (float)min.X, (float)min.Y, (float)min.Z },
            //        max = new float[] { (float)max.X, (float)max.Y, (float)max.Z }
            //    };
            //    accessors.Add(vtxAccessor);
            //    int vtxAccessorIdx = accessors.Count - 1;

            //    var idsAccessor = new AccessorScalar
            //    {
            //        bufferView = idsBufferViewIdx,
            //        count = idsBuffer.PrimitiveCount
            //    };
            //    accessors.Add(idsAccessor);
            //    int idsAccessorIdx = accessors.Count - 1;

            //    // Create primitives
            //    var attribute = new Attribute() { POSITION = vtxAccessorIdx };
            //    var primitive = new Primitive() { attributes = attribute, indices = idsAccessorIdx, material = currentMaterialIdx };

            //    // Create mesh
            //    primitives.Add(primitive);
            //}
            //var mesh = new Mesh() { primitives = primitives };
            //meshes.Add(mesh);

            //var node = new Node() { mesh = meshes.Count - 1 };
            //nodes.Add(node);

            //scenes[scene].nodes.Add(nodes.Count - 1);
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
        public object extensions;
    }

    public class Attribute
    {
        public int POSITION;
        public int? NORMAL;
        public int? TEXCOORD_0;
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
        public List<IEnumerable<byte>> RawBytes;

        public bool ShouldSerializeRawBytes() {
            return !IsGLBinaryMode;
        }

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

        public void Add(float[] floats)
        {
            // Switch GL coords for Y<=>Z
            IEnumerable<byte> byteList = floats.SelectMany(value => BitConverter.GetBytes(value));
            if (!IsGLBinaryMode)
            {
                uri += Convert.ToBase64String(byteList.ToArray());
                // 4 bytes / float * 3 (x,y,z)
                //byteLength += 4 * 3;
            }
            else
            {
                RawBytes.Add(byteList);
            }
            byteLength += byteList.Count();
        }

        public void Add(Point3d point)
        {
            // Switch GL coords for Y<=>Z
            float[] coords = new float[] { (float)point.X, (float)point.Z, -(float)point.Y };
            Add(coords);
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

        public class DracoGeoInfo
        {
            public bool success;

            public int verticesNum;
            public float[] verticesMin;
            public float[] verticesMax;

            public int trianglesNum;
            public int trianglesMin;
            public int trianglesMax;

            public int normalsNum;
            public float[] normalsMin;
            public float[] normalsMax;

            public int texCoordsNum;
            public float[] texCoordsMin;
            public float[] texCoordsMax;
        }

        public DracoGeoInfo Add(DracoCompression dracoCompression)
        {
            // Switch GL coords for Y<=>Z
            //float[] coords = new float[] { (float)point.X, (float)point.Z, -(float)point.Y };
            //IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));

            var dracoGeoInfo = new DracoGeoInfo();
            string filePath = Path.GetTempFileName();
            try
            {
                dracoCompression.Write(filePath);

                using (FileStream stream = File.Open(filePath, FileMode.Open))
                {
                    var bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, (int)stream.Length);
                    RawBytes.Add(bytes);
                }

                // DEBUG
                //File.Copy(filePath, @"/Users/aske/Desktop/rawfile.drc");

                // Draco compression might change the number of vertices, tris, normals.
                // Decompress the file again to get the correct geometry stats.
                var geo = DracoCompression.DecompressFile(filePath);
                if (geo.ObjectType == ObjectType.Mesh) {
                    var mesh = (Rhino.Geometry.Mesh)geo;
                    Point2f point2f;
                    Point3f point3f;
                    Vector3f vector3f;
                    // Vertices Stats
                    dracoGeoInfo.verticesNum = mesh.Vertices.Count;
                    point3f = mesh.Vertices.Min();
                    dracoGeoInfo.verticesMin = new float[] { point3f.X, point3f.Y, point3f.Z };
                    point3f = mesh.Vertices.Max();
                    dracoGeoInfo.verticesMax = new float[] { point3f.X, point3f.Y, point3f.Z };

                    // Triangle Stats
                    dracoGeoInfo.trianglesNum = mesh.Faces.TriangleCount;
                    dracoGeoInfo.trianglesMin = 0;
                    dracoGeoInfo.trianglesMax = dracoGeoInfo.verticesNum - 1;

                    // Normals Stats
                    dracoGeoInfo.normalsNum = mesh.Normals.Count;
                    vector3f = mesh.Normals.Min();
                    dracoGeoInfo.normalsMin = new float[] { vector3f.X, vector3f.Y, vector3f.Z };
                    vector3f = mesh.Normals.Max();
                    dracoGeoInfo.normalsMax = new float[] { vector3f.X, vector3f.Y, vector3f.Z };

                    // TexCoord Stats
                    dracoGeoInfo.texCoordsNum = mesh.TextureCoordinates.Count;
                    point2f = mesh.TextureCoordinates.Min();
                    dracoGeoInfo.texCoordsMin = new float[] { point2f.X, point2f.Y };
                    point2f = mesh.TextureCoordinates.Max();
                    dracoGeoInfo.texCoordsMax = new float[] { point2f.X, point2f.Y };

                    dracoGeoInfo.success = true;
                }
                geo.Dispose();
                dracoCompression.Dispose();
            }
            finally
            {
                File.Delete(filePath);
            }

            byteLength += RawBytes.Count;
            PrimitiveCount += 1;

            return dracoGeoInfo;
        }

        public void ReadFileFromPath(string filePath)
        {
            using (FileStream stream = File.Open(filePath, FileMode.Open))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                RawBytes.Add(bytes);
            }
    
            byteLength += RawBytes.Count;
            PrimitiveCount += 1;
        }

        public void ReadPNGFromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            RawBytes.Add(bytes);

            byteLength += RawBytes.Count;
            PrimitiveCount += 1;
        }
    }

    public class BufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int? target;

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
    }

    public class AccessorScalar : Accessor
    {
        public int max;
        public int min;

        public AccessorScalar()
        {
            byteOffset = 0;
            componentType = GlConstants.UNSIGNED_INT;
            type = GlConstants.SCALAR;
        }
    }

    public class AccessorVec2 : Accessor
    {
        public float[] max;
        public float[] min;

        public AccessorVec2()
        {
            byteOffset = 0;
            componentType = GlConstants.FLOAT;
            type = GlConstants.VEC2;
            max = new float[] { 1.0f, 1.0f };
            min = new float[] { 0.0f, 0.0f };
        }
    }

    public class AccessorVec3 : Accessor
    {
        public float[] max;
        public float[] min;

        public AccessorVec3()
        {
            byteOffset = 0;
            componentType = GlConstants.FLOAT;
            type = GlConstants.VEC3;
            max = new float[] { 1.0f, 1.0f, 1.0f };
            min = new float[] { 0.0f, 0.0f, 0.0f };
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
