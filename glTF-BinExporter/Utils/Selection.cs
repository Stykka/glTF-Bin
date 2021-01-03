using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace glTF_BinExporter.glTF
{
    public class Selection
    {
        public static GetObject GetRhinoObjects(string prompt, ObjectType geometryFilter)
        {
            GetObject go = new GetObject();
            go.SetCommandPrompt(prompt);
            go.GeometryFilter = geometryFilter;
            go.GroupSelect = false;
            go.SubObjectSelect = false;
            go.EnableClearObjectsOnEntry(false);
            go.EnableUnselectObjectsOnExit(false);
            go.DeselectAllBeforePostSelect = false;

            bool bHavePreselectedObjects = true;

            for (; ; )
            {
                GetResult res = go.GetMultiple(1, 0);

                if (res == GetResult.Option)
                {
                    go.EnablePreSelect(false, true);
                    continue;
                }

                else if (res != GetResult.Object)
                    return go;

                if (go.ObjectsWerePreselected)
                {
                    bHavePreselectedObjects = true;
                    go.EnablePreSelect(false, true);
                    continue;
                }

                break;
            }

            if (bHavePreselectedObjects)
            {
                // Normally, pre-selected objects will remain selected, when a
                // command finishes, and post-selected objects will be unselected.
                // This this way of picking, it is possible to have a combination
                // of pre-selected and post-selected. So, to make sure everything
                // "looks the same", lets unselect everything before finishing
                // the command.
                for (int i = 0; i < go.ObjectCount; i++)
                {
                    RhinoObject rhinoObject = go.Object(i).Object();
                    if (null != rhinoObject)
                        rhinoObject.Select(false);
                }
            }

            return go;
        }

        public static GetObject GetValidExportObjects(string prompt)
        {
            var objFilter = ObjectType.None;
            foreach(var objtype in Constants.ValidObjectTypes) {
                objFilter |= objtype;
            }

            return GetRhinoObjects(prompt, objFilter);
        }
    }
}
