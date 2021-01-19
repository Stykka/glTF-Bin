using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using Rhino.Render;

namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// RootModel represents the entire glTF encoding. It's a two step process:
    /// 1) add any objects that are to be included in the result, using the approriate Add*(...) method.
    /// 2) dump the contents to a file with SerializeToGLB(...) or SerializeToJSON(...)
    /// </summary>
    public class RootModel
    {
        public int scene = 0;

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
            // TODO: Using a default sampler here. Should maybe be configurable?
            // Put a default sampler in pos0
            samplers = new List<Sampler>() { new Sampler() };
        }

        public string SerializeToJSON()
        {
            var settings = new JsonSerializerSettings
            {
                // This is _super_ important. Otherwise null values are serialized to the gltf.
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

        /// <summary>
        /// After all RhinoObjects have been Add'ed to the RootModel, this method serializes the content of the model to a MemoryStream.
        /// </summary>
        /// <param name="memoryStream"></param>
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
                    GLConstants.CHUNK_TYPE_BINARY
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

                //Have to get rid of the other buffers so only the new buffer
                //all the data was dumped into remains
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
                    GLConstants.CHUNK_TYPE_JSON
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
                    GLConstants.GLB_MAGIC_BYTE,
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

        public void AddRhinoObjectDraco(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material, Guid renderMatId)
        {
            // Check if we already added the Material to the glb
            //material.
            int currentMaterialIdx = materials.FindIndex(m => m.Id == renderMatId);
            if (currentMaterialIdx == -1)
            {
                AddMaterial(material, renderMatId);
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
                    min = new int[] { dracoGeoInfo.trianglesMin },
                    max = new int[] { dracoGeoInfo.trianglesMax }
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

        private int AddTextureToBuffers(string texturePath) {
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
            return textureIdx;
        }

        private TextureBaseColor AddPBRBaseTexture(string texturePath) {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new TextureBaseColor() { index = textureIdx, texCoord = 0 };
        }

        private TextureNormal AddTextureNormal(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new TextureNormal() { index = textureIdx, texCoord = 0, scale = 0.9f };
        }

        private TextureOcclusion AddTextureOcclusion(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new TextureOcclusion() { index = textureIdx, texCoord = 0, strength = 0.9f };
        }

        private TextureEmissive AddTextureEmissive(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new TextureEmissive() { index = textureIdx, texCoord = 0 };
        }

        public TextureBaseColor AddMetallicRoughnessTexture(Rhino.DocObjects.Material rhinoMaterial) {
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

                    // GLTF expects metallic and roughness textures to be in linear color space.
                    //TODO: This doesn't seem right... var color = ColorUtils.ConvertSRGBToLinear(new Color4f(0, g / 255.0f, b / 255.0f, 1.0f));
                    // Using it without gammacorrecting for now.
		            var color = new Color4f(0, g / 255.0f, b / 255.0f, 1.0f);
                    bitmap.SetPixel(x, height - y - 1, color.AsSystemColor());
                }
            }

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

            return new TextureBaseColor() { index = textureIdx, texCoord = 0 };
        }

        public void AddMaterial(Rhino.DocObjects.Material rhinoMaterial, Guid renderMatId) {
            // Prep
            var material = rhinoMaterial.IsPhysicallyBased ? new Material(rhinoMaterial.PhysicallyBased, renderMatId) : new Material(rhinoMaterial, renderMatId);

            // Textures
            Rhino.DocObjects.Texture baseColorTexture;
            Rhino.DocObjects.Texture metallicTexture;
            Rhino.DocObjects.Texture roughnessTexture;
            Rhino.DocObjects.Texture normalTexture;
            Rhino.DocObjects.Texture occlusionTexture;
            Rhino.DocObjects.Texture emissiveTexture;

            if (rhinoMaterial.IsPhysicallyBased) { 
                baseColorTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_BaseColor);
                metallicTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Metallic);
                roughnessTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Roughness);
                normalTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.Bump);
                occlusionTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_AmbientOcclusion);
                emissiveTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Emission);
            } else {
                baseColorTexture = rhinoMaterial.GetTexture(TextureType.PBR_BaseColor);
                metallicTexture = rhinoMaterial.GetTexture(TextureType.PBR_Metallic);
                roughnessTexture = rhinoMaterial.GetTexture(TextureType.PBR_Roughness);
                normalTexture = rhinoMaterial.GetTexture(TextureType.Bump);
                occlusionTexture = null; // Oldschool shaders don't have this.
                emissiveTexture = null; // TODO: Don't know where to pull this from. It should be there...
            }

            if (baseColorTexture != null) {
                material.pbrMetallicRoughness.baseColorTexture = AddPBRBaseTexture(baseColorTexture.FileReference.FullPath);
            }

            if (metallicTexture != null || roughnessTexture != null)
            {
                material.pbrMetallicRoughness.metallicRoughnessTexture = AddMetallicRoughnessTexture(rhinoMaterial);
            }

            if (normalTexture != null) { 
                material.normalTexture = AddTextureNormal(normalTexture.FileReference.FullPath);
            }

            if (occlusionTexture != null) {
                material.occlusionTexture = AddTextureOcclusion(occlusionTexture.FileReference.FullPath);
            }

            if (emissiveTexture != null) {
                material.emissiveTexture = AddTextureEmissive(emissiveTexture.FileReference.FullPath);
            }

            materials.Add(material);
        }

        public void AddRhinoObject(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material, Guid renderMatId)
        {
            int currentMaterialIdx = materials.FindIndex(m => m.Id == material.Id);
            if (currentMaterialIdx == -1)
            {
                AddMaterial(material, renderMatId);
                currentMaterialIdx = materials.Count - 1;
            }

            var primitives = new List<Primitive>();

            foreach (var rhinoMesh in rhinoMeshes)
            {
                // Create buffers for data (position, normals, indices etc.)
                var vtxBuffer = new Buffer(ExportOptions.UseBinary);
                var vtxMin = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };
                var vtxMax = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };
                foreach (var p in rhinoMesh.Vertices)
                {
                    vtxBuffer.Add(p);

                    vtxMin.X = Math.Min(vtxMin.X, p.X);
                    // Switch Y<=>Z for GL coords
                    vtxMin.Y = Math.Min(vtxMin.Y, p.Z);
                    vtxMin.Z = Math.Min(vtxMin.Z, -p.Y);

                    vtxMax.X = Math.Max(vtxMax.X, p.X);
                    // Switch Y<=>Z for GL coords
                    vtxMax.Y = Math.Max(vtxMax.Y, p.Z);
                    vtxMax.Z = Math.Max(vtxMax.Z, -p.Y);
                }
                buffers.Add(vtxBuffer);
                int vtxBufferIdx = buffers.Count - 1;

                var idsBuffer = new Buffer(ExportOptions.UseBinary);
                foreach (var f in rhinoMesh.Faces)
                {
                    idsBuffer.Add(f);
                }
                buffers.Add(idsBuffer);
                int idsBufferIdx = buffers.Count - 1;

                Buffer normalsBuffer = new Buffer(ExportOptions.UseBinary);
                var normalsMin = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };
                var normalsMax = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };
                //normalsBuffer.Add(rhinoMesh.Normals.ToFloatArray());
                foreach (var n in rhinoMesh.Normals)
                {
                    normalsBuffer.Add(n);

                    normalsMin.X = Math.Min(normalsMin.X, n.X);
                    // Switch Y<=>Z for GL coords
                    normalsMin.Y = Math.Min(normalsMin.Y, n.Z);
                    normalsMin.Z = Math.Min(normalsMin.Z, -n.Y);

                    normalsMax.X = Math.Max(normalsMax.X, n.X);
                    // Switch Y<=>Z for GL coords
                    normalsMax.Y = Math.Max(normalsMax.Y, n.Z);
                    normalsMax.Z = Math.Max(normalsMax.Z, -n.Y);
                }
                int normalsIdx = buffers.AddAndReturnIndex(normalsBuffer);

                Buffer texCoordsBuffer = new Buffer(ExportOptions.UseBinary);
                var texCoordsMin = new Point2d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity };
                var texCoordsMax = new Point2d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity };
                foreach (var tx in rhinoMesh.TextureCoordinates)
                {
                    texCoordsBuffer.Add(tx);

                    texCoordsMin.X = Math.Min(texCoordsMin.X, tx.X);
                    // Switch Y<=>Z for GL coords
                    texCoordsMin.Y = Math.Min(texCoordsMin.Y, -tx.Y);

                    texCoordsMax.X = Math.Max(texCoordsMax.X, tx.X);
                    // Switch Y<=>Z for GL coords
                    texCoordsMax.Y = Math.Max(texCoordsMax.Y, -tx.Y);
                }

                int texCoordsIdx = buffers.AddAndReturnIndex(texCoordsBuffer);

                // Create bufferviews
                var vtxBufferView = new BufferView() { bufferRef = vtxBuffer, buffer = vtxBufferIdx, byteOffset = 0, byteLength = vtxBuffer.byteLength, target = GLConstants.ARRAY_BUFFER };
                bufferViews.Add(vtxBufferView);
                int vtxBufferViewIdx = bufferViews.Count - 1;

                var idsBufferView = new BufferView() { bufferRef = idsBuffer, buffer = idsBufferIdx, byteOffset = 0, byteLength = idsBuffer.byteLength, target = GLConstants.ELEMENT_ARRAY_BUFFER };
                bufferViews.Add(idsBufferView);
                int idsBufferViewIdx = bufferViews.Count - 1;

                BufferView normalsBufferView = new BufferView()
                {
                    bufferRef = normalsBuffer,
                    buffer = normalsIdx,
                    byteOffset = 0,
                    byteLength = normalsBuffer.byteLength,
                    target = GLConstants.ARRAY_BUFFER
                };
                int normalsBufferViewIdx = bufferViews.AddAndReturnIndex(normalsBufferView);

                BufferView texCoordsBufferView = new BufferView()
                {
                    bufferRef = texCoordsBuffer,
                    buffer = texCoordsIdx,
                    byteOffset = 0,
                    byteLength = texCoordsBuffer.byteLength,
                    target = GLConstants.ARRAY_BUFFER
                };
                int texCoordsBufferViewIdx = bufferViews.AddAndReturnIndex(texCoordsBufferView);

                // Create accessors
                var vtxAccessor = new AccessorVec3()
                {
                    bufferView = vtxBufferViewIdx,
                    count = vtxBuffer.PrimitiveCount,
                    min = new float[] { (float)vtxMin.X, (float)vtxMin.Y, (float)vtxMin.Z },
                    max = new float[] { (float)vtxMax.X, (float)vtxMax.Y, (float)vtxMax.Z }
                };

                accessors.Add(vtxAccessor);
                int vtxAccessorIdx = accessors.Count - 1;

                var idsAccessor = new AccessorScalar()
                {
                    min = new int[] { 0 },
                    max = new int[] { rhinoMesh.Vertices.Count - 1 },
                    bufferView = idsBufferViewIdx,
                    count = idsBuffer.PrimitiveCount
                };
                accessors.Add(idsAccessor);
                int idsAccessorIdx = accessors.Count - 1;

                AccessorVec3 normalsAccessor = new AccessorVec3()
                {
                    bufferView = normalsBufferViewIdx,
                    count = rhinoMesh.Normals.Count,
                    min = new float[] { (float)normalsMin.X, (float)normalsMin.Y, (float)normalsMin.Z },
                    max = new float[] { (float)normalsMax.X, (float)normalsMax.Y, (float)normalsMax.Z }
                };
                int normalsAccessorIdx = accessors.AddAndReturnIndex(normalsAccessor);

                AccessorVec2 texCoordsAccessor = new AccessorVec2()
                {
                    bufferView = texCoordsBufferViewIdx,
                    count = rhinoMesh.TextureCoordinates.Count,
                    min = new float[] { 0.0f, 0.0f },
                    max = new float[] { 1.0f, 1.0f },
                };
                int texCoordsAccessorIdx = accessors.AddAndReturnIndex(texCoordsAccessor);

                // Create primitives
                var attribute = new Attribute()
                {
                    POSITION = vtxAccessorIdx,
                    NORMAL = normalsAccessorIdx,
                    TEXCOORD_0 = texCoordsAccessorIdx
                };

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
}
