using glTFLoader.Schema;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
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
        public RhinoMaterialGltfConverter(glTFExportOptions options, gltfSchemaDummy dummy, List<byte> binaryBuffer, Rhino.DocObjects.Material rhinoMaterial, LinearWorkflow workflow)
        {
            this.options = options;
            this.dummy = dummy;
            this.binaryBuffer = binaryBuffer;
            this.rhinoMaterial = rhinoMaterial;
            this.workflow = workflow;
        }

        private glTFExportOptions options = null;
        private gltfSchemaDummy dummy = null;
        private List<byte> binaryBuffer = null;
        private LinearWorkflow workflow = null;

        private Rhino.DocObjects.Material rhinoMaterial = null;

        public int AddMaterial()
        {
            // Prep
            glTFLoader.Schema.Material material = new glTFLoader.Schema.Material()
            {
                Name = rhinoMaterial.Name,
                PbrMetallicRoughness = new MaterialPbrMetallicRoughness(),
            };
            
            if(!rhinoMaterial.IsPhysicallyBased)
            {
                rhinoMaterial.ToPhysicallyBased();
            }

            // Textures
            Rhino.DocObjects.Texture metallicTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Metallic);
            Rhino.DocObjects.Texture roughnessTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Roughness);
            Rhino.DocObjects.Texture normalTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.Bump);
            Rhino.DocObjects.Texture occlusionTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_AmbientOcclusion);
            Rhino.DocObjects.Texture emissiveTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Emission);

            HandleBaseColor(rhinoMaterial, material);

            if (metallicTexture != null || roughnessTexture != null)
            {
                material.PbrMetallicRoughness.MetallicRoughnessTexture = AddMetallicRoughnessTexture(rhinoMaterial);

                material.PbrMetallicRoughness.MetallicFactor = 1.0f;
                material.PbrMetallicRoughness.RoughnessFactor = 1.0f;
            }
            else
            {
                material.PbrMetallicRoughness.MetallicFactor = (float)rhinoMaterial.PhysicallyBased.Metallic;
                material.PbrMetallicRoughness.RoughnessFactor = (float)rhinoMaterial.PhysicallyBased.Roughness;
            }

            if (normalTexture != null && normalTexture.Enabled)
            {
                material.NormalTexture = AddTextureNormal(normalTexture);
            }

            if (occlusionTexture != null && occlusionTexture.Enabled)
            {
                material.OcclusionTexture = AddTextureOcclusion(occlusionTexture.FileReference.FullPath);
            }

            if (emissiveTexture != null && emissiveTexture.Enabled)
            {
                material.EmissiveTexture = AddTexture(emissiveTexture.FileReference.FullPath);

                float emissionMultiplier = 1.0f;

                var param = rhinoMaterial.RenderMaterial.GetParameter("emission-multiplier");

                if (param != null)
                {
                    emissionMultiplier = (float)Convert.ToDouble(param);
                }

                material.EmissiveFactor = new float[]
                {
                    emissionMultiplier,
                    emissionMultiplier,
                    emissionMultiplier,
                };
            }
            else
            {
                material.EmissiveFactor = new float[]
                {
                    rhinoMaterial.PhysicallyBased.Emission.R,
                    rhinoMaterial.PhysicallyBased.Emission.G,
                    rhinoMaterial.PhysicallyBased.Emission.B,
                };
            }

            return dummy.Materials.AddAndReturnIndex(material);
        }

        void HandleBaseColor(Rhino.DocObjects.Material rhinoMaterial, glTFLoader.Schema.Material gltfMaterial)
        {
            Rhino.DocObjects.Texture baseColorDoc = rhinoMaterial.GetTexture(TextureType.PBR_BaseColor);
            Rhino.DocObjects.Texture alphaTextureDoc = rhinoMaterial.GetTexture(TextureType.PBR_Alpha);

            RenderTexture baseColorTexture = rhinoMaterial.RenderMaterial.GetTextureFromUsage(RenderMaterial.StandardChildSlots.PbrBaseColor);
            RenderTexture alphaTexture = rhinoMaterial.RenderMaterial.GetTextureFromUsage(RenderMaterial.StandardChildSlots.PbrAlpha);
            
            bool hasBaseColorTexture = baseColorDoc == null ? false : baseColorDoc.Enabled;
            bool hasAlphaTexture = alphaTextureDoc == null ? false : alphaTextureDoc.Enabled;

            bool baseColorDiffuseAlphaForTransparency = rhinoMaterial.PhysicallyBased.UseBaseColorTextureAlphaForObjectAlphaTransparencyTexture;

            if(!baseColorDiffuseAlphaForTransparency && !hasAlphaTexture)
            {
                gltfMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.OPAQUE;
            }
            else
            {
                gltfMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.BLEND;
            }

            Color4f baseColor = rhinoMaterial.PhysicallyBased.BaseColor;

            if (workflow.PreProcessColors)
            {
                baseColor = Color4f.ApplyGamma(baseColor, workflow.PreProcessGamma);
            }

            if (!hasBaseColorTexture && !hasAlphaTexture)
            {
                float alpha = 1.0f;

                if (baseColorDiffuseAlphaForTransparency)
                {
                    alpha = rhinoMaterial.PhysicallyBased.BaseColor.A;
                }

                alpha *= (float)rhinoMaterial.PhysicallyBased.Alpha;

                gltfMaterial.PbrMetallicRoughness.BaseColorFactor = new float[]
                {
                    baseColor.R,
                    baseColor.G,
                    baseColor.B,
                    alpha,
                };
            }
            else
            {
                if(!hasAlphaTexture)
                {
                    float alpha = (float)rhinoMaterial.PhysicallyBased.Alpha;
                    alphaTexture = CreateSolidColorRhinoTexture(new Color4f(alpha, alpha, alpha, 1.0f), new Size(256, 256));
                }

                gltfMaterial.PbrMetallicRoughness.BaseColorTexture = CombineBaseColorAndAlphaTexture(baseColorTexture, alphaTexture, baseColorDiffuseAlphaForTransparency, baseColor);
            }
        }

        RenderTexture CreateSolidColorRhinoTexture(Color4f color, Size size)
        {
            Bitmap bmp = SolidColorImage(color, size);

            return RenderTexture.NewBitmapTexture(bmp, null);
        }

        Bitmap SolidColorImage(Color4f color, Size size)
        {
            Bitmap bmp = new Bitmap(size.Width, size.Height);

            using(Graphics gfx = Graphics.FromImage(bmp))
            {
                gfx.FillRectangle(new SolidBrush(color.AsSystemColor()), 0, 0, size.Width, size.Height);
            }

            return bmp;
        }

        TextureInfo CombineBaseColorAndAlphaTexture(Rhino.Render.RenderTexture baseColorTexture, Rhino.Render.RenderTexture alphaTexture, bool baseColorDiffuseAlphaForTransparency, Color4f baseColor)
        {
            bool hasBaseColorTexture = baseColorTexture != null;

            int baseColorWidth, baseColorHeight, baseColorDepth;
            baseColorWidth = baseColorHeight = baseColorDepth = 0;

            if (hasBaseColorTexture)
            {
                baseColorTexture.PixelSize(out baseColorWidth, out baseColorHeight, out baseColorDepth);
            }
            
            alphaTexture.PixelSize(out int alphaWidth, out int alphaHeight, out int alphaDepth);

            int width = Math.Max(baseColorWidth, alphaWidth);
            int height = Math.Max(baseColorHeight, alphaHeight);

            if(width <= 0)
            {
                width = 1024;
            }

            if(height <= 0)
            {
                height = 1024;
            }

            TextureEvaluator baseColorTextureEvaluator = baseColorTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
            TextureEvaluator alphaTextureEvaluator = alphaTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);

            Bitmap bitmap = new Bitmap(width, height);

            for(int i = 0; i < width; i++)
            {
                for(int j = 0; j < height; j++)
                {
                    double x = (double)i / ((double)(width - 1));
                    double y = (double)j / ((double)(height - 1));

                    y = 1.0 - y;

                    Point3d uvw = new Point3d(x, y, 0.0);

                    Color4f baseColorOut = baseColor;

                    if (hasBaseColorTexture)
                    {
                        baseColorOut = baseColorTextureEvaluator.GetColor(uvw, Vector3d.Zero, Vector3d.Zero);

                        if(workflow.PreProcessTextures)
                        {
                            baseColorOut = Color4f.ApplyGamma(baseColorOut, workflow.PreProcessGamma);
                        }
                    }

                    if(!baseColorDiffuseAlphaForTransparency)
                    {
                        baseColorOut = new Color4f(baseColorOut.R, baseColorOut.G, baseColorOut.B, 1.0f);
                    }

                    Color4f alphaColor = alphaTextureEvaluator.GetColor(uvw, Vector3d.Zero, Vector3d.Zero);

                    float alpha = baseColor.A * alphaColor.L;

                    Color4f colorFinal = new Color4f(baseColorOut.R, baseColorOut.G, baseColorOut.B, alpha);

                    bitmap.SetPixel(i, j, colorFinal.AsSystemColor());
                }
            }

            bitmap.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "out.png"));

            return GetTextureInfoFromBitmap(bitmap);
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
            int imageBytesOffset = (int)binaryBuffer.Count;
            binaryBuffer.AddRange(imageBytes);

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
            Bitmap bmp = new Bitmap(fileName);

            return GetImageBytes(bmp);
        }

        private glTFLoader.Schema.TextureInfo AddTexture(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new glTFLoader.Schema.TextureInfo() { Index = textureIdx, TexCoord = 0 };
        }

        private glTFLoader.Schema.MaterialNormalTextureInfo AddTextureNormal(Rhino.DocObjects.Texture normalTexture)
        {
            Rhino.Render.RenderTexture normalRenderTexture = rhinoMaterial.RenderMaterial.GetTextureFromUsage(RenderMaterial.StandardChildSlots.Bump);

            int textureIdx = AddNormalTexture(normalTexture, normalRenderTexture);

            double scale = rhinoMaterial.RenderMaterial.GetTextureAmountFromUsage(RenderMaterial.StandardChildSlots.Bump) / 100.0;

            return new glTFLoader.Schema.MaterialNormalTextureInfo()
            {
                Index = textureIdx,
                TexCoord = 0,
                Scale = (float)scale,
            };
        }

        private int AddNormalTexture(Rhino.DocObjects.Texture normalTexture, RenderTexture normalRenderTexture)
        {
            if (normalRenderTexture.IsNormalMap())
            {
                return AddTextureToBuffers(normalTexture.FileReference.FullPath);
            }
            else
            {
                Bitmap bmp = ConvertBumpToNormal(normalRenderTexture);
                return GetTextureFromBitmap(bmp);
            }
        }

        private Bitmap ConvertBumpToNormal(RenderTexture bumpMapTexture)
        {
            bumpMapTexture.PixelSize(out int width, out int height, out int depth);

            if(width <= 0)
            {
                width = 1024;
            }

            if(height <= 0)
            {
                height = 1024;
            }

            TextureEvaluator evaluator = bumpMapTexture.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);

            Bitmap bmp = new Bitmap(width, height);

            //Sobel filter for bump to normal conversion https://en.wikipedia.org/wiki/Sobel_operator

            double widthScaler = 1.0 / (width - 1);
            double heightScaler = 1.0 / (height - 1);

            for (int x = 0; x < width; x++)
            {
                for(int y = 0; y < height; y++)
                {
                    Point3d aLocation = new Point3d(Mod(x - 1, width) * widthScaler,  1.0 - Mod(y - 1, height) * heightScaler, 0.0);
                    Point3d bLocation = new Point3d(Mod(x, width) * widthScaler,      1.0 - Mod(y - 1, height) * heightScaler, 0.0);
                    Point3d cLocation = new Point3d(Mod(x + 1, width) * widthScaler,  1.0 - Mod(y - 1, height) * heightScaler, 0.0);
                    Point3d dLocation = new Point3d(Mod(x - 1, width) * widthScaler,  1.0 - Mod(y, height) * heightScaler, 0.0);
                    Point3d fLocation = new Point3d(Mod(x + 1, width) * widthScaler,  1.0 - Mod(y, height) * heightScaler, 0.0);
                    Point3d gLocation = new Point3d(Mod(x - 1, width) * widthScaler,  1.0 - Mod(y + 1, height) * heightScaler, 0.0);
                    Point3d hLocation = new Point3d(Mod(x, width) * widthScaler,      1.0 - Mod(y + 1, height) * heightScaler, 0.0);
                    Point3d iLocation = new Point3d(Mod(x + 1, width) * widthScaler,  1.0 - Mod(y + 1, height) * heightScaler, 0.0);

                    float a = evaluator.GetColor(aLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float b = evaluator.GetColor(bLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float c = evaluator.GetColor(cLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float d = evaluator.GetColor(dLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float f = evaluator.GetColor(fLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float g = evaluator.GetColor(gLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float h = evaluator.GetColor(hLocation, Vector3d.Zero, Vector3d.Zero).L;
                    float i = evaluator.GetColor(iLocation, Vector3d.Zero, Vector3d.Zero).L;

                    float dX = a - c + 2.0f * d + -2.0f * f + g - f;
                    float dY = a + 2.0f * b + c - g - 2.0f * h - i;

                    Vector3f normal = new Vector3f(dX, dY, 1.0f);
                    normal.Unitize();

                    normal = normal * 0.5f + new Vector3f(0.5f, 0.5f, 0.5f);

                    Color4f color = new Color4f(normal.X, normal.Y, normal.Z, 1.0f);

                    bmp.SetPixel(x, y, color.AsSystemColor());
                }
            }

            return bmp;
        }

        public int Mod(int x, int m)
        {
            return (x % m + m) % m;
        }

        private glTFLoader.Schema.MaterialOcclusionTextureInfo AddTextureOcclusion(string texturePath)
        {
            int textureIdx = AddTextureToBuffers(texturePath);

            return new glTFLoader.Schema.MaterialOcclusionTextureInfo() { Index = textureIdx, TexCoord = 0, Strength = 0.9f };
        }

        public glTFLoader.Schema.TextureInfo AddMetallicRoughnessTexture(Rhino.DocObjects.Material rhinoMaterial)
        {
            Rhino.DocObjects.Texture metalTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Metallic);
            Rhino.DocObjects.Texture roughnessTexture = rhinoMaterial.PhysicallyBased.GetTexture(TextureType.PBR_Roughness);

            bool hasMetalTexture = metalTexture == null ? false : metalTexture.Enabled;
            bool hasRoughnessTexture = roughnessTexture == null ? false : roughnessTexture.Enabled;

            RenderTexture renderTextureMetal = null;
            RenderTexture renderTextureRoughness = null;

            int mWidth = 0;
            int mHeight = 0;
            int rWidth = 0;
            int rHeight = 0;

            // Get the textures
            if (hasMetalTexture)
            {
                renderTextureMetal = rhinoMaterial.RenderMaterial.GetTextureFromUsage(Rhino.Render.RenderMaterial.StandardChildSlots.PbrMetallic);
                renderTextureMetal.PixelSize(out mWidth, out mHeight, out int _w0);
            }

            if (hasRoughnessTexture)
            {
                renderTextureRoughness = rhinoMaterial.RenderMaterial.GetTextureFromUsage(RenderMaterial.StandardChildSlots.PbrRoughness);
                renderTextureRoughness.PixelSize(out rWidth, out rHeight, out int _w1);
            }

            int width = Math.Max(mWidth, rWidth);
            int height = Math.Max(mHeight, rHeight);

            TextureEvaluator evalMetal = null;
            TextureEvaluator evalRoughness = null;

            // Metal
            if (hasMetalTexture)
            {
                evalMetal = renderTextureMetal.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
            }

            // Roughness
            if (hasRoughnessTexture)
            {
                evalRoughness = renderTextureRoughness.CreateEvaluator(RenderTexture.TextureEvaluatorFlags.Normal);
            }

            // Copy Metal to the blue channel, roughness to the green
            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            float metallic = (float)rhinoMaterial.PhysicallyBased.Metallic;
            float roughness = (float)rhinoMaterial.PhysicallyBased.Roughness;

            for (var j = 0; j < height - 1; j += 1)
            {
                for (var i = 0; i < width - 1; i += 1)
                {
                    double x = (double)i / (double)(width - 1);
                    double y = (double)j / (double)(height - 1);

                    Point3d uvw = new Point3d(x, y, 0.0);

                    float g = 0;
                    float b = 0;
                    if (hasMetalTexture)
                    {
                        Color4f metal = evalMetal.GetColor(uvw, Vector3d.Zero, Vector3d.Zero);
                        b = metal.L; //grayscale maps, so we want lumonosity
                    }
                    else
                    {
                        b = metallic;
                    }

                    if (hasRoughnessTexture)
                    {
                        Color4f roughnessColor = evalRoughness.GetColor(uvw, Vector3d.ZAxis, Vector3d.Zero);
                        g = roughnessColor.L; //grayscale maps, so we want lumonosity
                    }
                    else
                    {
                        g = roughness;
                    }

                    Color4f color = new Color4f(0.0f, g, b, 1.0f);
                    bitmap.SetPixel(i, height - j - 1, color.AsSystemColor());
                }
            }

            return GetTextureInfoFromBitmap(bitmap);
        }

        private int GetTextureFromBitmap(Bitmap bitmap)
        {
            var image = GetImageFromBitmap(bitmap);

            int imageIdx = dummy.Images.AddAndReturnIndex(image);

            var texture = new glTFLoader.Schema.Texture()
            {
                Source = imageIdx,
                Sampler = 0
            };

            return dummy.Textures.AddAndReturnIndex(texture);
        }

        private glTFLoader.Schema.TextureInfo GetTextureInfoFromBitmap(Bitmap bitmap)
        {
            int textureIdx = GetTextureFromBitmap(bitmap);

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
            int imageBytesOffset = (int)binaryBuffer.Count;
            binaryBuffer.AddRange(imageBytes);

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
