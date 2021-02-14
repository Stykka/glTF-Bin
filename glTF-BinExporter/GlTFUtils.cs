using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Render;

namespace glTF_BinExporter
{
    /// <summary>
    /// Functions for helping with adding RhinoObjects to the RootModel.
    /// </summary>
    public static class GlTFUtils
    {
        /// <summary>
        /// Returns the preview-meshes of the RhinoObject.
        /// TODO: Extend to handle sub-assigned materials returning a list of KV-pairs of meshes, materials.
        /// </summary>
        /// <param name="rhinoObject"></param>
        /// <returns></returns>
        public static Rhino.Geometry.Mesh[] GetMeshes(RhinoObject rhinoObject) {
            Rhino.Geometry.Mesh[] meshes;

            if (rhinoObject.ObjectType == ObjectType.Mesh) {
                // Take the Mesh directly from the geo.
                var meshObj = (MeshObject)rhinoObject;
                meshes = new Rhino.Geometry.Mesh[] { meshObj.MeshGeometry };
            } else {
                // Need to get a Mesh from the None-mesh object. Using the FastRenderMesh here. Could be made configurable.
                // First make sure the internal rhino mesh has been created
                rhinoObject.CreateMeshes(MeshType.Preview, MeshingParameters.FastRenderMesh, true);
                // Then get the internal rhino meshes
                meshes = rhinoObject.GetMeshes(MeshType.Preview);
            }

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

        /// <summary>
        /// Takes varying RhinoObjects and returns a RhinoMesh, and a RhinoMaterial for each.
        /// Handles 
        /// </summary>
        /// <param name="rhinoObjects"></param>
        /// <returns></returns>
        public static List<Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject>> SanitizeRhinoObjects(IEnumerable<RhinoObject> rhinoObjects)
        {

            var rhinoObjectsRes = new List<Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject>>();

            foreach (var rhinoObject in rhinoObjects)
            {
                // FIXME: This is broken. Even though objects use the same material, different Materials are returned here.
                var mat = rhinoObject.GetMaterial(true);
                var renderMatId = mat.Id;
                bool isPBR = mat.IsPhysicallyBased;

                // This is always true when called from the Main plugin command, as it uses the same ObjectType array as filter.
                // Keeping it around in case someone calls this from somewhere else.
                var isValidGeometry = Constants.ValidObjectTypes.Contains(rhinoObject.ObjectType);

                if (isValidGeometry && rhinoObject.ObjectType != ObjectType.InstanceReference)
                {
                    // None-block. Just add it to the result list
                    rhinoObjectsRes.Add(new Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject>(GetMeshes(rhinoObject), mat, renderMatId, rhinoObject));
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
                        rhinoObjectsRes.Add(new Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject>(meshes, mat, renderMatId, item.rhinoObject));
                    }
                } else {
                    // TODO: Should give better error message here.
                    RhinoApp.WriteLine("Unknown geo type encountered.");
                }
            }
            return rhinoObjectsRes;
        }

        public static int AddAndReturnIndex<T>(this List<T> list, T item)
        {
            list.Add(item);
            return list.Count - 1;
        }

        public static bool IsFileGltfBinary(string filename)
        {
            string extension = Path.GetExtension(filename);

            return extension.ToLower() == ".glb";
        }

    }
}
