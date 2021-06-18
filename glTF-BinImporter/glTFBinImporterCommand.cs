using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using System;
using System.Collections.Generic;

namespace glTF_BinImporter
{
    public class glTFBinImporterCommand : Command
    {
        public glTFBinImporterCommand()
        {
            Instance = this;
        }

        public static glTFBinImporterCommand Instance
        {
            get; private set;
        }

        public override string EnglishName
        {
            get { return "glTFBinImporterCommand"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            return Result.Success;
        }
    }
}
