using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinImporter
{
    class RhinoGltfMetallicRoughnessConverter
    {
        public RhinoGltfMetallicRoughnessConverter(System.Drawing.Bitmap bmp, Rhino.RhinoDoc doc, string name)
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

                    metalnessBitmap.SetPixel(i, j, System.Drawing.Color.FromArgb(metalness, metalness, metalness));
                    roughnessBitmap.SetPixel(i, j, System.Drawing.Color.FromArgb(roughness, roughness, roughness));
                }
            }

            MetallicTexture = Rhino.Render.RenderTexture.NewBitmapTexture(metalnessBitmap, doc);

            MetallicTexture.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Program);

            MetallicTexture.Name = name + "-Metallic";

            MetallicTexture.EndChange();

            RoughnessTexture = Rhino.Render.RenderTexture.NewBitmapTexture(roughnessBitmap, doc);

            RoughnessTexture.BeginChange(Rhino.Render.RenderContent.ChangeContexts.Program);

            RoughnessTexture.Name = name + "-Roughness";

            RoughnessTexture.EndChange();
        }

        public Rhino.Render.RenderTexture MetallicTexture
        {
            get;
            private set;
        } = null;

        public Rhino.Render.RenderTexture RoughnessTexture
        {
            get;
            private set;
        } = null;

    }
}
