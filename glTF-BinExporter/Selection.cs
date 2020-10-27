using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Stykka.Common.Utils
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
                //doc.Views.Redraw();
            }

            //int objectCount = go.ObjectCount;

            //RhinoApp.WriteLine(string.Format("Select object count = {0}", objectCount));
            return go;
        }

        public static GetObject GetRhinoBlocksAndBreps()
        {
            return GetRhinoObjects("Select surfaces, polysurfaces, or meshes", ObjectType.Brep | ObjectType.InstanceReference);
        }

        public static Point3d GetPoint(string prompt)
        {
            GetPoint gp = new GetPoint();
            gp.SetCommandPrompt(prompt);
            gp.SetDefaultPoint(new Rhino.Geometry.Point3d(0, 0, 0));
            GetResult res = gp.Get();

            return gp.Point();
        }

        public static double GetNumber(string prompt, double defaultNumber)
        {
            GetNumber gn = new GetNumber();
            gn.SetCommandPrompt(prompt);
            gn.SetDefaultNumber(defaultNumber);
            GetResult res = gn.Get();

            return gn.Number();
        }
    }
}
