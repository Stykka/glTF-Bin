using glTFLoader.Schema;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    class RhinoMaterialGltfConverter
    {
        public RhinoMaterialGltfConverter(glTFExportOptions options, gltfSchemaDummy dummy, MemoryStream binaryBufferStream, Rhino.DocObjects.Material rhinoMaterial)
        {
            this.options = options;
            this.dummy = dummy;
            this.binaryBufferStream = binaryBufferStream;
            this.rhinoMaterial = rhinoMaterial;
        }

        private glTFExportOptions options = null;
        private gltfSchemaDummy dummy = null;
        private MemoryStream binaryBufferStream = null;

        private Rhino.DocObjects.Material rhinoMaterial = null;

        public int AddMaterial()
        {
            // Prep
            glTFLoader.Schema.Material material = new glTFLoader.Schema.Material()
            {
                Name = rhinoMaterial.Name,
            };

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
            if (options.UseBinary)
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
            int imageBytesOffset = (int)binaryBufferStream.Position;
            binaryBufferStream.Write(imageBytes, 0, imageBytes.Length);

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
            if (options.UseBinary)
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
            int imageBytesOffset = (int)binaryBufferStream.Position;
            binaryBufferStream.Write(imageBytes, 0, imageBytes.Length);

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
