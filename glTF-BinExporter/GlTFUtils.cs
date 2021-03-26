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
        public static Rhino.Geometry.Mesh[] GetMeshes(RhinoObject rhinoObject)
        {
            
            if (rhinoObject.ObjectType == ObjectType.Mesh)
            {
                MeshObject meshObj = rhinoObject as MeshObject;

                return new Rhino.Geometry.Mesh[] { meshObj.MeshGeometry };
            }

            // Need to get a Mesh from the None-mesh object. Using the FastRenderMesh here. Could be made configurable.
            // First make sure the internal rhino mesh has been created
            rhinoObject.CreateMeshes(MeshType.Preview, MeshingParameters.FastRenderMesh, true);

            // Then get the internal rhino meshes
            Rhino.Geometry.Mesh[] meshes = rhinoObject.GetMeshes(MeshType.Preview);

            List<Rhino.Geometry.Mesh> validMeshes = new List<Mesh>();

            foreach(Rhino.Geometry.Mesh mesh in meshes)
            {
                if(MeshIsValidForExport(mesh))
                {
                    mesh.EnsurePrivateCopy();
                    validMeshes.Add(mesh);
                }
            }

            return validMeshes.Count == 0 ? new Rhino.Geometry.Mesh[] { } : validMeshes.ToArray();
        }

        public static bool MeshIsValidForExport(Rhino.Geometry.Mesh mesh)
        {
            if (mesh == null)
            {
                return false;
            }

            if (mesh.Vertices.Count == 0)
            {
                return false;
            }

            if (mesh.Faces.Count == 0)
            {
                return false;
            }

            return true;
        }

        private static string GetDebugName(RhinoObject rhinoObject)
        {
            if(string.IsNullOrEmpty(rhinoObject.Name))
            {
                return "(Unnamed)";
            }

            return rhinoObject.Name;
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
                if(!rhinoObject.IsMeshable(MeshType.Any))
                {
                    RhinoApp.WriteLine("Skipping " + GetDebugName(rhinoObject) + ", object is not meshable. Object is a " + rhinoObject.ObjectType.ToString());
                    continue;
                }
                
                // FIXME: This is broken. Even though objects use the same material, different Materials are returned here.
                var mat = rhinoObject.GetMaterial(true);
                var renderMatId = mat.Id;
                bool isPBR = mat.IsPhysicallyBased;

                // This is always true when called from the Main plugin command, as it uses the same ObjectType array as filter.
                // Keeping it around in case someone calls this from somewhere else.
                var isValidGeometry = Constants.ValidObjectTypes.Contains(rhinoObject.ObjectType);

                if (isValidGeometry && rhinoObject.ObjectType != ObjectType.InstanceReference)
                {
                    var meshes = GetMeshes(rhinoObject);

                    if(meshes.Length > 0) //Objects need a mesh to export
                    {
                        rhinoObjectsRes.Add(new Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject>(meshes, mat, renderMatId, rhinoObject));
                    }
                }
                else if (rhinoObject.ObjectType == ObjectType.InstanceReference)
                {
                    InstanceObject instanceObject = rhinoObject as InstanceObject;

                    List<RhinoObject> objects = new List<RhinoObject>();
                    List<Transform> transforms = new List<Transform>();

                    ExplodeRecursive(instanceObject, instanceObject.InstanceXform, objects, transforms);

                    // Transform the exploded geo into its correct place
                    foreach (var item in objects.Zip(transforms, (rObj, trans) => (rhinoObject: rObj, trans)))
                    {
                        var meshes = GetMeshes(item.rhinoObject);

                        foreach (var mesh in meshes)
                        {
                            mesh.Transform(item.trans);
                        }
                        
                        if(meshes.Length > 0) //Objects need a mesh to export
                        {
                            rhinoObjectsRes.Add(new Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject>(meshes, mat, renderMatId, item.rhinoObject));
                        }
                    }
                }
                else
                {
                    RhinoApp.WriteLine("Unknown geo type encountered.");
                }
            }

            return rhinoObjectsRes;
        }

        private static void ExplodeRecursive(InstanceObject instanceObject, Transform instanceTransform, List<RhinoObject> pieces, List<Transform> transforms)
        {
            for(int i = 0; i < instanceObject.InstanceDefinition.ObjectCount; i++)
            {
                RhinoObject rhinoObject = instanceObject.InstanceDefinition.Object(i);

                if (rhinoObject is InstanceObject nestedObject)
                {
                    Transform nestedTransform = instanceTransform * nestedObject.InstanceXform;

                    ExplodeRecursive(nestedObject, nestedTransform, pieces, transforms);
                }
                else
                {
                    pieces.Add(rhinoObject);

                    transforms.Add(instanceTransform);
                }
            }
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
