using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.DocObjects;
using Rhino;
using Rhino.FileIO;
using glTFLoader.Schema;
using Rhino.Geometry;

namespace glTF_BinExporter
{
    class RhinoDocGltfConverter
    {
        public RhinoDocGltfConverter(glTFExportOptions options, IEnumerable<RhinoObject> objects)
        {
            this.options = options;
            this.objects = objects;
        }

        public RhinoDocGltfConverter(glTFExportOptions options, RhinoDoc doc)
        {
            this.options = options;
            this.objects = doc.Objects;
        }

        private IEnumerable<RhinoObject> objects = null;
        private glTFExportOptions options = null;

        private Dictionary<Guid, int> materialsMap = new Dictionary<Guid, int>();

        private gltfSchemaDummy dummy = new gltfSchemaDummy();

        public Gltf ConvertToGltf()
        {
            dummy.Scene = 0;
            dummy.Scenes.Add(new gltfSchemaSceneDummy());

            var sanitized = GlTFUtils.SanitizeRhinoObjects(objects);

            foreach(Tuple<Rhino.Geometry.Mesh[], Rhino.DocObjects.Material, Guid, RhinoObject> tuple in sanitized)
            {
                AddRhinoObject(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
            }

            return dummy.ToSchemaGltf();
        }

        private void AddRhinoObject(Rhino.Geometry.Mesh[] rhinoMeshes, Rhino.DocObjects.Material material, Guid materialId, RhinoObject rhinoObject)
        {
            var primitives = new List<MeshPrimitive>();	

            foreach (var rhinoMesh in rhinoMeshes)	
            {	
                var vtxBuffer = new glTFLoader.Schema.Buffer();

                var vtxMin = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };	
                var vtxMax = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };

                foreach (var p in rhinoMesh.Vertices)	
                {	
                    vtxBuffer.Add(p);

                    vtxMin.X = Math.Min(vtxMin.X, p.X);	
                    // Switch Y<=>Z for GL coords	
                    vtxMin.Y = Math.Min(vtxMin.Y, p.Z);	
                    vtxMin.Z = Math.Min(vtxMin.Z, -p.Y);	

                    vtxMax.X = Math.Max(vtxMax.X, p.X);	
                    // Switch Y<=>Z for GL coords	
                    vtxMax.Y = Math.Max(vtxMax.Y, p.Z);	
                    vtxMax.Z = Math.Max(vtxMax.Z, -p.Y);	
                }
                	
                int vtxBufferIdx = dummy.Buffers.AddAndReturnIndex(vtxBuffer);

                var idsBuffer = new glTFLoader.Schema.Buffer();	
                foreach (var f in rhinoMesh.Faces)	
                {	
                    idsBuffer.Add(f);	
                }	
                
                int idsBufferIdx = dummy.Buffers.AddAndReturnIndex(idsBuffer);

                var normalsBuffer = new glTFLoader.Schema.Buffer();	
                var normalsMin = new Point3d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity, Z = Double.PositiveInfinity };	
                var normalsMax = new Point3d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity, Z = Double.NegativeInfinity };	
                //normalsBuffer.Add(rhinoMesh.Normals.ToFloatArray());	
                foreach (var n in rhinoMesh.Normals)	
                {	
                    normalsBuffer.Add(n);	

                    normalsMin.X = Math.Min(normalsMin.X, n.X);	
                    // Switch Y<=>Z for GL coords	
                    normalsMin.Y = Math.Min(normalsMin.Y, n.Z);	
                    normalsMin.Z = Math.Min(normalsMin.Z, -n.Y);	

                    normalsMax.X = Math.Max(normalsMax.X, n.X);	
                    // Switch Y<=>Z for GL coords	
                    normalsMax.Y = Math.Max(normalsMax.Y, n.Z);	
                    normalsMax.Z = Math.Max(normalsMax.Z, -n.Y);	
                }	
                int normalsIdx = dummy.Buffers.AddAndReturnIndex(normalsBuffer);	

                var texCoordsBuffer = new glTFLoader.Schema.Buffer();	
                var texCoordsMin = new Point2d() { X = Double.PositiveInfinity, Y = Double.PositiveInfinity };	
                var texCoordsMax = new Point2d() { X = Double.NegativeInfinity, Y = Double.NegativeInfinity };	
                foreach (var tx in rhinoMesh.TextureCoordinates)	
                {	
                    texCoordsBuffer.Add(tx);	

                    texCoordsMin.X = Math.Min(texCoordsMin.X, tx.X);	
                    // Switch Y<=>Z for GL coords	
                    texCoordsMin.Y = Math.Min(texCoordsMin.Y, -tx.Y);	

                    texCoordsMax.X = Math.Max(texCoordsMax.X, tx.X);	
                    // Switch Y<=>Z for GL coords	
                    texCoordsMax.Y = Math.Max(texCoordsMax.Y, -tx.Y);	
                }	

                int texCoordsIdx = dummy.Buffers.AddAndReturnIndex(texCoordsBuffer);	

                // Create bufferviews	
                var vtxBufferView = new BufferView()
                {
                    bufferRef = vtxBuffer,
                    buffer = vtxBufferIdx,
                    byteOffset = 0,
                    byteLength = vtxBuffer.byteLength,
                    target = GLConstants.ARRAY_BUFFER,
                };

                bufferViews.Add(vtxBufferView);	
                int vtxBufferViewIdx = bufferViews.Count - 1;	

                var idsBufferView = new BufferView()
                {
                    bufferRef = idsBuffer,
                    buffer = idsBufferIdx,
                    byteOffset = 0,
                    byteLength = idsBuffer.byteLength,
                    target = GLConstants.ELEMENT_ARRAY_BUFFER
                };
                
                bufferViews.Add(idsBufferView);	
                int idsBufferViewIdx = bufferViews.Count - 1;	

                BufferView normalsBufferView = new BufferView()	
                {	
                    bufferRef = normalsBuffer,	
                    buffer = normalsIdx,	
                    byteOffset = 0,	
                    byteLength = normalsBuffer.byteLength,	
                    target = GLConstants.ARRAY_BUFFER	
                };	
                int normalsBufferViewIdx = bufferViews.AddAndReturnIndex(normalsBufferView);	

                BufferView texCoordsBufferView = new BufferView()	
                {	
                    bufferRef = texCoordsBuffer,	
                    buffer = texCoordsIdx,	
                    byteOffset = 0,	
                    byteLength = texCoordsBuffer.byteLength,	
                    target = GLConstants.ARRAY_BUFFER	
                };	
                int texCoordsBufferViewIdx = bufferViews.AddAndReturnIndex(texCoordsBufferView);	

                // Create accessors	
                var vtxAccessor = new AccessorVec3()	
                {	
                    bufferView = vtxBufferViewIdx,	
                    count = vtxBuffer.PrimitiveCount,	
                    min = new float[] { (float)vtxMin.X, (float)vtxMin.Y, (float)vtxMin.Z },	
                    max = new float[] { (float)vtxMax.X, (float)vtxMax.Y, (float)vtxMax.Z }	
                };	

                accessors.Add(vtxAccessor);	
                int vtxAccessorIdx = accessors.Count - 1;	

                var idsAccessor = new AccessorScalar()	
                {	
                    min = new int[] { 0 },	
                    max = new int[] { rhinoMesh.Vertices.Count - 1 },	
                    bufferView = idsBufferViewIdx,	
                    count = idsBuffer.PrimitiveCount	
                };	
                accessors.Add(idsAccessor);	
                int idsAccessorIdx = accessors.Count - 1;	

                AccessorVec3 normalsAccessor = new AccessorVec3()	
                {	
                    bufferView = normalsBufferViewIdx,	
                    count = rhinoMesh.Normals.Count,	
                    min = new float[] { (float)normalsMin.X, (float)normalsMin.Y, (float)normalsMin.Z },	
                    max = new float[] { (float)normalsMax.X, (float)normalsMax.Y, (float)normalsMax.Z }	
                };	
                int normalsAccessorIdx = accessors.AddAndReturnIndex(normalsAccessor);	

                AccessorVec2 texCoordsAccessor = new AccessorVec2()	
                {	
                    bufferView = texCoordsBufferViewIdx,	
                    count = rhinoMesh.TextureCoordinates.Count,	
                    min = new float[] { (float)texCoordsMin.X, (float)texCoordsMin.Y },	
                    max = new float[] { (float)texCoordsMax.X, (float)texCoordsMax.Y }	
                };	
                int texCoordsAccessorIdx = accessors.AddAndReturnIndex(texCoordsAccessor);	

                // Create primitives	
                var attribute = new glTFLoader.Schema.A()	
                {	
                    POSITION = vtxAccessorIdx,	
                    NORMAL = normalsAccessorIdx,	
                    TEXCOORD_0 = texCoordsAccessorIdx	
                };	

                var primitive = new MeshPrimitive() { attributes = attribute, indices = idsAccessorIdx, material = currentMaterialIdx };	

                // Create mesh	
                primitives.Add(primitive);	
            }	

            var mesh = new glTFLoader.Schema.Mesh() { Primitives = primitives.ToArray() };	
            int idxMesh = dummy.Meshes.AddAndReturnIndex(mesh);	

            var node = new Node()
            {
                Mesh = idxMesh,
            };

            int idxNode = dummy.Nodes.AddAndReturnIndex(node);	

            dummy.Scenes[dummy.Scene].Nodes.Add(idxNode);	
        }

        glTFLoader.Schema.Buffer CreateVerticesBuffer(Rhino.Geometry.Collections.MeshVertexList vertices)
        {
            List<float> floats = new List<float>();

            foreach(Point3d vector in vertices)
            {
                floats.AddRange(new float[] {(float)vector.X, (float)vector.Z, (float)-vector.Y });
            }

            IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

            byte[] bytes = bytesEnumerable.ToArray();

            return new glTFLoader.Schema.Buffer()
            {
                Uri = Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };
        }

    }
}
