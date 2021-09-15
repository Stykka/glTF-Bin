using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace glTF_BinExporter
{
    class RhinoPointCloudGltfConverter
    {
        public RhinoPointCloudGltfConverter(Rhino.DocObjects.RhinoObject rhinoObject, glTFExportOptions options, bool binary, gltfSchemaDummy dummy, List<byte> binaryBuffer)
        {
            this.rhinoObject = rhinoObject;
            this.options = options;
            this.binary = binary;
            this.dummy = dummy;
            this.binaryBuffer = binaryBuffer;
        }

        private Rhino.DocObjects.RhinoObject rhinoObject = null;
        private glTFExportOptions options = null;
        private bool binary = false;
        private gltfSchemaDummy dummy = null;
        private List<byte> binaryBuffer = null;

        public int AddPointCloud()
        {
            Rhino.Geometry.PointCloud pointCloud = rhinoObject.Geometry.Duplicate() as Rhino.Geometry.PointCloud;

            if(pointCloud == null)
            {
                return -1;
            }

            if(options.MapRhinoZToGltfY)
            {
                pointCloud.Transform(Constants.ZtoYUp);
            }

            Rhino.Geometry.Point3d[] points = pointCloud.GetPoints();

            int vertexAccessor = GetVertexAccessor(points);

            glTFLoader.Schema.MeshPrimitive primitive = new glTFLoader.Schema.MeshPrimitive()
            {
                Mode = glTFLoader.Schema.MeshPrimitive.ModeEnum.POINTS,
                Attributes = new Dictionary<string, int>(),
            };

            primitive.Attributes.Add(Constants.PositionAttributeTag, vertexAccessor);

            if(pointCloud.ContainsColors)
            {
                System.Drawing.Color[] colors = pointCloud.GetColors();

                int colorsAccessorIdx = GetVertexColorAccessor(colors);

                primitive.Attributes.Add(Constants.VertexColorAttributeTag, colorsAccessorIdx);
            }

            glTFLoader.Schema.Mesh mesh = new glTFLoader.Schema.Mesh()
            {
                Primitives = new glTFLoader.Schema.MeshPrimitive[] { primitive },
            };

            return dummy.Meshes.AddAndReturnIndex(mesh);
        }

        private int GetVertexAccessor(Rhino.Geometry.Point3d[] points)
        {
            int bufferViewIndex = GetBufferView(points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int count);

            glTFLoader.Schema.Accessor accessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = bufferViewIndex,
                ByteOffset = 0,
                ComponentType = glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT,
                Count = count,
                Min = min.ToFloatArray(),
                Max = max.ToFloatArray(),
                Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3,
            };

            return dummy.Accessors.AddAndReturnIndex(accessor);
        }

        private int GetBufferView(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int count)
        {
            int buffer = 0;
            int byteLength = 0;
            int byteOffset = 0;

            if (binary)
            {
                byte[] bytes = GetVertexBytes(points, out min, out max);
                buffer = 0;
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                buffer = GetVertexBuffer(points, out min, out max, out byteLength);
            }

            glTFLoader.Schema.BufferView vertexBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = buffer,
                ByteOffset = byteOffset,
                ByteLength = byteLength,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
            };

            count = points.Length;

            return dummy.BufferViews.AddAndReturnIndex(vertexBufferView);
        }

        private int GetVertexBuffer(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max, out int length)
        {
            byte[] bytes = GetVertexBytes(points, out min, out max);

            length = bytes.Length;

            glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = length,
            };

            return dummy.Buffers.AddAndReturnIndex(buffer);
        }

        private byte[] GetVertexBytes(Rhino.Geometry.Point3d[] points, out Rhino.Geometry.Point3d min, out Rhino.Geometry.Point3d max)
        {
            min = new Rhino.Geometry.Point3d(Double.PositiveInfinity, Double.PositiveInfinity, Double.PositiveInfinity);
            max = new Rhino.Geometry.Point3d(Double.NegativeInfinity, Double.NegativeInfinity, Double.NegativeInfinity);

            List<float> floats = new List<float>(points.Length * 3);

            foreach (Rhino.Geometry.Point3d vertex in points)
            {
                floats.AddRange(new float[] { (float)vertex.X, (float)vertex.Y, (float)vertex.Z });

                min.X = Math.Min(min.X, vertex.X);
                max.X = Math.Max(max.X, vertex.X);

                min.Y = Math.Min(min.Y, vertex.Y);
                max.Y = Math.Max(max.Y, vertex.Y);

                min.Z = Math.Min(min.Z, vertex.Z);
                max.Z = Math.Max(max.Z, vertex.Z);
            }

            IEnumerable<byte> bytesEnumerable = floats.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

        private int GetVertexColorAccessor(System.Drawing.Color[] vertexColors)
        {
            int vertexColorsBufferViewIdx = GetVertexColorBufferView(vertexColors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max, out int countVertexColors);

            var type = options.UseDracoCompression ? glTFLoader.Schema.Accessor.ComponentTypeEnum.UNSIGNED_BYTE : glTFLoader.Schema.Accessor.ComponentTypeEnum.FLOAT;

            glTFLoader.Schema.Accessor vertexColorAccessor = new glTFLoader.Schema.Accessor()
            {
                BufferView = vertexColorsBufferViewIdx,
                ByteOffset = 0,
                Count = countVertexColors,
                ComponentType = type,
                Min = min.ToFloatArray(),
                Max = max.ToFloatArray(),
                Type = glTFLoader.Schema.Accessor.TypeEnum.VEC4,
                Normalized = options.UseDracoCompression,
            };

            return dummy.Accessors.AddAndReturnIndex(vertexColorAccessor);
        }

        int GetVertexColorBufferView(System.Drawing.Color[] colors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max, out int countVertexColors)
        {
            int buffer = 0;
            int byteLength = 0;
            int byteOffset = 0;

            if (binary)
            {
                byte[] bytes = GetVertexColorBytes(colors, out min, out max);
                byteLength = bytes.Length;
                byteOffset = binaryBuffer.Count;
                binaryBuffer.AddRange(bytes);
            }
            else
            {
                buffer = GetVertexColorBuffer(colors, out min, out max, out byteLength);
            }

            glTFLoader.Schema.BufferView vertexColorsBufferView = new glTFLoader.Schema.BufferView()
            {
                Buffer = buffer,
                ByteLength = byteLength,
                ByteOffset = byteOffset,
                Target = glTFLoader.Schema.BufferView.TargetEnum.ARRAY_BUFFER,
            };

            countVertexColors = colors.Length;

            return dummy.BufferViews.AddAndReturnIndex(vertexColorsBufferView);
        }

        int GetVertexColorBuffer(System.Drawing.Color[] colors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max, out int byteLength)
        {
            byte[] bytes = GetVertexColorBytes(colors, out min, out max);

            glTFLoader.Schema.Buffer vertexColorsBuffer = new glTFLoader.Schema.Buffer()
            {
                Uri = Constants.TextBufferHeader + Convert.ToBase64String(bytes),
                ByteLength = bytes.Length,
            };

            byteLength = bytes.Length;

            return dummy.Buffers.AddAndReturnIndex(vertexColorsBuffer);
        }

        byte[] GetVertexColorBytes(System.Drawing.Color[] colors, out Rhino.Display.Color4f min, out Rhino.Display.Color4f max)
        {
            float[] minArr = new float[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity };
            float[] maxArr = new float[] { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

            List<float> colorFloats = new List<float>(colors.Length * 4);

            for (int i = 0; i < colors.Length; i++)
            {
                Rhino.Display.Color4f color = new Rhino.Display.Color4f(colors[i]);

                colorFloats.AddRange(color.ToFloatArray());

                minArr[0] = Math.Min(minArr[0], color.R);
                minArr[1] = Math.Min(minArr[1], color.G);
                minArr[2] = Math.Min(minArr[2], color.B);
                minArr[3] = Math.Min(minArr[3], color.A);

                maxArr[0] = Math.Max(maxArr[0], color.R);
                maxArr[1] = Math.Max(maxArr[1], color.G);
                maxArr[2] = Math.Max(maxArr[2], color.B);
                maxArr[3] = Math.Max(maxArr[3], color.A);
            }

            min = new Rhino.Display.Color4f(minArr[0], minArr[1], minArr[2], minArr[3]);
            max = new Rhino.Display.Color4f(maxArr[0], maxArr[1], maxArr[2], maxArr[3]);

            IEnumerable<byte> bytesEnumerable = colorFloats.SelectMany(value => BitConverter.GetBytes(value));

            return bytesEnumerable.ToArray();
        }

    }
}
