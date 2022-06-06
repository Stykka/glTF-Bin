from Rhino import *
from System import Environment

path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

filePath = '"' + path + "\\test.gltf" + '"'

script = "_-Export all _Enter " + filePath + " _Enter"

RhinoApp.RunScript(script, True)