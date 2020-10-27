using System.Collections.Generic;
using System.IO;
using Rhino.DocObjects;
using Stykka.Common.glTF;

namespace Stykka.Common
{
    public static class GlTFUtils
    {
        //public static void ExportJSON(in GetObject go)
        //{
        //    RootModel mdl = new RootModel(false);

        //    foreach (ObjRef objRef in go.Objects())
        //    {
        //        var obj = objRef.Object();
        //        Part part;
        //        if (obj.ObjectType == ObjectType.InstanceReference)
        //        {
        //            part = new Part((InstanceObject)obj);
        //        }
        //        else
        //        {
        //            part = new Part(obj);
        //        }

        //        mdl.AddPart(part);
        //    }

        //    var json = mdl.SerializeToJSON();
        //    File.WriteAllText(@"/Users/aske/Desktop/hello.gltf", json);
        //}

        public static void ExportBinary(MemoryStream outStream, IEnumerable<RhinoObject> rhinoObjects)
        {
            RootModel mdl = new RootModel(true);

            foreach (var rhinoObject in rhinoObjects)
            {
                mdl.AddRhinoObject(rhinoObject);
            }

            mdl.SerializeToGLB(outStream);
            outStream.Flush();
        }
    }
}
