using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rhino.DocObjects;
using Rhino.Geometry;
using Stykka.Common.glTF;

namespace Stykka.Common
{
    public static class GlTFUtils
    {
        /// <summary>
        /// Returns the preview-meshes of the RhinoObject.
        /// TODO: Extend to handle sub-assigned materials returning a list of KV-pairs of meshes, materials.
        /// </summary>
        /// <param name="rhinoObject"></param>
        /// <returns></returns>
        public static Rhino.Geometry.Mesh[] GetMeshes(RhinoObject rhinoObject) {
            // First make sure the internal rhino mesh has been created
            rhinoObject.CreateMeshes(MeshType.Preview, MeshingParameters.FastRenderMesh, true);
            // Then get the internal rhino meshes
            var meshes = rhinoObject.GetMeshes(MeshType.Preview);

            if (meshes.Length > 0) {
                var mainMesh = meshes[0];
                mainMesh.EnsurePrivateCopy();
                foreach (var mesh in meshes.Skip(1))
                {
                    mainMesh.Append(mesh);
                }

                mainMesh.Weld(0.01);

                mainMesh.UnifyNormals();
                mainMesh.RebuildNormals();

                // Note
                return new Rhino.Geometry.Mesh[] { mainMesh };
            } else {
                return new Rhino.Geometry.Mesh[] { };
	        }
        }

        public static List<KeyValuePair<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material>> SanitizeRhinoObjects(IEnumerable<RhinoObject> rhinoObjects) {
            var rhinoObjectsRes = new List<KeyValuePair<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material>>();

            foreach (var rhinoObject in rhinoObjects)
            {
                var mat = rhinoObject.GetMaterial(true);

                var isGeometry = rhinoObject.ObjectType == ObjectType.Brep
                    || rhinoObject.ObjectType == ObjectType.Surface
                    || rhinoObject.ObjectType == ObjectType.SubD
                    || rhinoObject.ObjectType == ObjectType.Mesh
                    || rhinoObject.ObjectType == ObjectType.Extrusion;

                if (isGeometry)
                {
                    // None-block. Just add it to the result list
                    rhinoObjectsRes.Add(new KeyValuePair<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material>(GetMeshes(rhinoObject), mat));
                } else if (rhinoObject.ObjectType == ObjectType.InstanceReference) {
                    // Cast to InstanceObject/BlockInstance
                    var instanceObject = (InstanceObject)rhinoObject;
                    // Explode the Block
                    instanceObject.Explode(true, out RhinoObject[] pieces, out ObjectAttributes[] attribs, out Transform[] transforms);

                    // Transform the exploded geo into its correct place
                    foreach (var item in pieces.Zip(transforms, (rObj, trans) => (rhinoObject: rObj, trans)))
                    {
                        var meshes = GetMeshes(item.rhinoObject);

                        foreach (var mesh in meshes)
                        {
                            mesh.Transform(item.trans);
                        }

                        // Add the exploded, transformed geo to the result list
                        rhinoObjectsRes.Add(new KeyValuePair<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material>(meshes, mat));
                    }
                } else {
                    Console.WriteLine("Unknown geo type encountered.");
		        }
            }
            return rhinoObjectsRes;
        }

        public static void ExportJSON(MemoryStream outStream, IEnumerable<RhinoObject> rhinoObjects, ExportOptions exportOptions)
        {
            RootModel mdl = new RootModel(exportOptions);

            var sanitizedRhinoObjects = SanitizeRhinoObjects(rhinoObjects);

            foreach (var kv in sanitizedRhinoObjects)
            {
                // Regular objects
                if (exportOptions.UseDracoCompression)
                {
                    mdl.AddRhinoObjectDraco(kv.Key, kv.Value);
                }
                else
                {
                    mdl.AddRhinoObject(kv.Key, kv.Value);
                }
                
                foreach (var mesh in kv.Key) {
                    mesh.Dispose();
		        }
            }

            var json = mdl.SerializeToJSON();
            byte[] jsonBytes = Encoding.ASCII.GetBytes(json);

            outStream.Write(jsonBytes, 0, jsonBytes.Length);
            outStream.Flush();
        }

        public static void ExportBinary(MemoryStream outStream, IEnumerable<RhinoObject> rhinoObjects, ExportOptions exportOptions)
        {
            RootModel mdl = new RootModel(exportOptions);

            var sanitizedRhinoObjects = SanitizeRhinoObjects(rhinoObjects);

            foreach (var kv in sanitizedRhinoObjects)
            {
                // Regular objects
                if (exportOptions.UseDracoCompression) {
                    mdl.AddRhinoObjectDraco(kv.Key, kv.Value);
                } else {
                    mdl.AddRhinoObject(kv.Key, kv.Value);
                }
            }

            mdl.SerializeToGLB(outStream);

            outStream.Flush();
        }
    }
}
