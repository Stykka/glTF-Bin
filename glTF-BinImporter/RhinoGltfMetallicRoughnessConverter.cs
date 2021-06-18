using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinImporter
{
    class RhinoGltfMetallicRoughnessConverter
    {
        public RhinoGltfMetallicRoughnessConverter(System.Drawing.Bitmap bmp, Rhino.RhinoDoc doc)
        {
            System.Drawing.Bitmap metalnessBitmap = new System.Drawing.Bitmap(bmp.Width, bmp.Height);

            System.Drawing.Bitmap roughnessBitmap = new System.Drawing.Bitmap(bmp.Width, bmp.Height);
            
            for(int i = 0; i < bmp.Width; i++)
            {
                for(int j = 0; j < bmp.Height; j++)
                {
                    System.Drawing.Color color = bmp.GetPixel(i, j);

                    byte metalness = color.B;
                    byte roughness = color.G;

                    HasMetalness = metalness > 0 || HasMetalness;
                    HasRoughness = roughness > 0 || HasRoughness;

                    metalnessBitmap.SetPixel(i, j, System.Drawing.Color.FromArgb(metalness, metalness, metalness));
                    roughnessBitmap.SetPixel(i, j, System.Drawing.Color.FromArgb(roughness, roughness, roughness));
                }
            }

            if(HasMetalness)
            {
                MetallicTexture = Rhino.Render.RenderTexture.NewBitmapTexture(metalnessBitmap, doc);
            }

            if(HasRoughness)
            {
                RoughnessTexture = Rhino.Render.RenderTexture.NewBitmapTexture(roughnessBitmap, doc);
            }
        }

        public bool HasMetalness
        {
            get;
            private set;
        } = false;

        public Rhino.Render.RenderTexture MetallicTexture
        {
            get;
            private set;
        } = null;

        public bool HasRoughness
        {
            get;
            private set;
        } = false;

        public Rhino.Render.RenderTexture RoughnessTexture
        {
            get;
            private set;
        } = null;

    }
}
