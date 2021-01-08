using System.Collections.Generic;
using Newtonsoft.Json;

namespace glTF_BinExporter.glTF
{
    public class Asset
    {
        public string version;

        public Asset()
        {
            version = "2.0";
        }
    }

    public class Mesh
    {
        public IEnumerable<Primitive> primitives;

        public Mesh() { }
    }

    public class Primitive
    {
        public Attribute attributes;
        public int indices;
        public int material;
        public object extensions;
    }

    public class Attribute
    {
        public int POSITION;
        public int? NORMAL;
        public int? TEXCOORD_0;
    }

    public class BufferView
    {
        public int buffer;
        public int byteOffset;
        public int byteLength;
        public int? target;

        [JsonIgnore]
        public Buffer bufferRef;
    }

    public class Accessor
    {
        public int bufferView;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
    }

    public class AccessorScalar : Accessor
    {
        public int[] max;
        public int[] min;

        public AccessorScalar()
        {
            min = new int[] { 0 };
            max = new int[] { 1 };
            byteOffset = 0;
            componentType = GLConstants.UNSIGNED_INT;
            type = GLConstants.SCALAR;
        }
    }

    public class AccessorVec2 : Accessor
    {
        public float[] max;
        public float[] min;

        public AccessorVec2()
        {
            byteOffset = 0;
            componentType = GLConstants.FLOAT;
            type = GLConstants.VEC2;
            max = new float[] { 1.0f, 1.0f };
            min = new float[] { 0.0f, 0.0f };
        }
    }

    public class AccessorVec3 : Accessor
    {
        public float[] max;
        public float[] min;

        public AccessorVec3()
        {
            byteOffset = 0;
            componentType = GLConstants.FLOAT;
            type = GLConstants.VEC3;
            max = new float[] { 1.0f, 1.0f, 1.0f };
            min = new float[] { 0.0f, 0.0f, 0.0f };
        }
    }

    public class Scene
    {
        public List<int> nodes;

        public Scene()
        {
            nodes = new List<int>();
        }
    }

    public class Node
    {
        // This is this nodes assigned index in the node array.
        public int mesh;
    }

    public class Matrix
    {
        public List<double> values;
    }
}
