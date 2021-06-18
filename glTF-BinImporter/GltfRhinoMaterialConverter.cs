using Rhino.Render;
using Rhino.Render.ChildSlotNames;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinImporter
{
    class GltfRhinoMaterialConverter
    {
        public GltfRhinoMaterialConverter(glTFLoader.Schema.Gltf gltf, glTFLoader.Schema.Material material, Rhino.RhinoDoc doc, GltfRhinoConverter converter)
        {
            this.gltf = gltf;
            this.material = material;
            this.doc = doc;
            this.converter = converter;
        }

        glTFLoader.Schema.Gltf gltf = null;
        glTFLoader.Schema.Material material = null;
        Rhino.RhinoDoc doc = null;
        GltfRhinoConverter converter = null;

        public Rhino.Render.RenderMaterial Convert()
        {
            RenderMaterial pbr = RenderContentType.NewContentFromTypeId(ContentUuids.PhysicallyBasedMaterialType, doc) as RenderMaterial;

            pbr.BeginChange(RenderContent.ChangeContexts.Program);

            if(!string.IsNullOrEmpty(material.Name))
            {
                pbr.Name = material.Name;
            }

            if(material.PbrMetallicRoughness != null)
            {
                Rhino.Display.Color4f baseColor = material.PbrMetallicRoughness.BaseColorFactor.ToColor4f();

                pbr.SetParameter(PhysicallyBased.BaseColor, baseColor);

                if(material.PbrMetallicRoughness.BaseColorTexture != null)
                {
                    int index = material.PbrMetallicRoughness.BaseColorTexture.Index;

                    RenderTexture texture = converter.GetRgbTexture(index);

                    pbr.SetChild(texture, Rhino.Render.ParameterNames.PhysicallyBased.BaseColor);
                    pbr.SetChildSlotOn(Rhino.Render.ParameterNames.PhysicallyBased.BaseColor, true, RenderContent.ChangeContexts.Program);
                }

                double roughness = material.PbrMetallicRoughness.RoughnessFactor;

                pbr.SetParameter(PhysicallyBased.Roughness, roughness);

                double metallic = material.PbrMetallicRoughness.MetallicFactor;

                pbr.SetParameter(PhysicallyBased.Metallic, metallic);

                if(material.PbrMetallicRoughness.MetallicRoughnessTexture != null)
                {
                    int index = material.PbrMetallicRoughness.MetallicRoughnessTexture.Index;

                    RhinoGltfMetallicRoughnessConverter metallicRoughness = converter.GetMetallicRoughnessTexture(index);

                    if(metallicRoughness.HasMetalness)
                    {
                        pbr.SetChild(metallicRoughness.MetallicTexture, PhysicallyBased.Metallic);
                        pbr.SetChildSlotOn(PhysicallyBased.Metallic, true, RenderContent.ChangeContexts.Program);
                    }

                    if(metallicRoughness.HasRoughness)
                    {
                        pbr.SetChild(metallicRoughness.RoughnessTexture, PhysicallyBased.Roughness);
                        pbr.SetChildSlotOn(PhysicallyBased.Roughness, true, RenderContent.ChangeContexts.Program);
                    }
                }
            }

            Rhino.Display.Color4f emissionColor = material.EmissiveFactor.ToColor4f();

            pbr.SetParameter(PhysicallyBased.Emission, emissionColor);

            if (material.EmissiveTexture != null)
            {
                int index = material.EmissiveTexture.Index;
                RenderTexture texture = converter.GetRgbTexture(index);

                pbr.SetChild(texture, PhysicallyBased.Emission);
                pbr.SetChildSlotOn(PhysicallyBased.Emission, true, RenderContent.ChangeContexts.Program);
            }

            if(material.OcclusionTexture != null)
            {
                int index = material.OcclusionTexture.Index;

                RenderTexture texture = converter.GetRgbTexture(index);

                pbr.SetChild(texture, PhysicallyBased.AmbientOcclusion);
                pbr.SetChildSlotOn(PhysicallyBased.AmbientOcclusion, true, RenderContent.ChangeContexts.Program);
            }

            pbr.EndChange();

            doc.RenderMaterials.Add(pbr);

            return pbr;
        }

    }
}
