using System;
using System.IO;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.FileIO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace glTF_BinExporter.glTF
{
    /// <summary>
    /// A gl Buffer holds raw data such as vertices, normals, etc. In "TextMode" the data is encoded in Base64 and embedded
    /// inside the Buffer in the JSON.
    /// In Binary mode the buffer is just pointing to a specific place in the binary portion of the file. This is
    /// implemented as a two-step process: when adding data to a buffer it is collected on the object. During final
    /// serialization it is then written to the correct place and pointers are written to the buffer object in the JSON
    /// portion of the file.
    /// </summary>
    public class Buffer
    {
        public string uri;
        public int byteLength;

        [JsonIgnore]
        public int PrimitiveCount;

        [JsonIgnore]
        public bool IsGLBinaryMode;

        // dump to byte[] with ".flatten" + .ToArray();
        [JsonIgnore]
        public List<IEnumerable<byte>> RawBytes;

        public bool ShouldSerializeRawBytes()
        {
            return !IsGLBinaryMode;
        }

        [JsonIgnore]
        public int binaryOffset;

        public Buffer(bool IsGLBinaryMode)
        {
            this.IsGLBinaryMode = IsGLBinaryMode;
            RawBytes = new List<IEnumerable<byte>>();

            if (!IsGLBinaryMode)
            {
                uri = "data:application/octet-stream;base64,";
            }
        }

        public void Add(float[] floats)
        {
            // Switch GL coords for Y<=>Z
            IEnumerable<byte> byteList = floats.SelectMany(value => BitConverter.GetBytes(value));
            if (!IsGLBinaryMode)
            {
                uri += Convert.ToBase64String(byteList.ToArray());
                // 4 bytes / float * 3 (x,y,z)
                //byteLength += 4 * 3;
            }
            else
            {
                RawBytes.Add(byteList);
            }
            byteLength += byteList.Count();
        }

        public void Add(Point3d point)
        {
            // Switch GL coords for Y<=>Z
            float[] coords = new float[] { (float)point.X, (float)point.Z, -(float)point.Y };
            Add(coords);
            PrimitiveCount += 1;
        }

        public void Add(MeshFace face)
        {
            if (face.IsTriangle)
            {
                // If the face is a triangle, we serialize the 3 indices to b64
                // NOTE: A, B, C produces f
                int[] coords = new int[] { face.A, face.B, face.C };
                IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));
                if (!IsGLBinaryMode)
                {
                    uri += Convert.ToBase64String(byteList.ToArray());
                    // 4 bytes / int * 3 (A, B, C)
                    //byteLength += 4 * 3;
                }
                else
                {
                    RawBytes.Add(byteList);
                }
                byteLength += byteList.Count();
                PrimitiveCount += 3;
            }
            else
            {
                // If the face is a quad, we serialize the 4 indices in two batches of 3 to b64
                int[] coords = new int[] { face.A, face.B, face.C, face.A, face.C, face.D };
                IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));
                if (!IsGLBinaryMode)
                {
                    uri += Convert.ToBase64String(byteList.ToArray());
                    // 4 bytes / int * 6 (A, B, C, A, C, D)
                    //byteLength += 4 * 6;
                }
                else
                {
                    RawBytes.Add(byteList);
                }
                byteLength += byteList.Count();
                PrimitiveCount += 6;
            }
        }

        public class DracoGeoInfo
        {
            public bool success;

            public int verticesNum;
            public float[] verticesMin;
            public float[] verticesMax;

            public int trianglesNum;
            public int trianglesMin;
            public int trianglesMax;

            public int normalsNum;
            public float[] normalsMin;
            public float[] normalsMax;

            public int texCoordsNum;
            public float[] texCoordsMin;
            public float[] texCoordsMax;
        }

        public DracoGeoInfo Add(DracoCompression dracoCompression)
        {
            // Switch GL coords for Y<=>Z
            //float[] coords = new float[] { (float)point.X, (float)point.Z, -(float)point.Y };
            //IEnumerable<byte> byteList = coords.SelectMany(value => BitConverter.GetBytes(value));

            var dracoGeoInfo = new DracoGeoInfo();
            string filePath = Path.GetTempFileName();
            try
            {
                dracoCompression.Write(filePath);

                using (FileStream stream = File.Open(filePath, FileMode.Open))
                {
                    var bytes = new byte[stream.Length];
                    stream.Read(bytes, 0, (int)stream.Length);
                    RawBytes.Add(bytes);
                }

                // DEBUG
                //File.Copy(filePath, @"/Users/aske/Desktop/rawfile.drc");

                // Draco compression might change the number of vertices, tris, normals.
                // Decompress the file again to get the correct geometry stats.
                var geo = DracoCompression.DecompressFile(filePath);
                if (geo.ObjectType == ObjectType.Mesh)
                {
                    var mesh = (Rhino.Geometry.Mesh)geo;
                    Point2f point2f;
                    Point3f point3f;
                    Vector3f vector3f;
                    // Vertices Stats
                    dracoGeoInfo.verticesNum = mesh.Vertices.Count;
                    point3f = mesh.Vertices.Min();
                    dracoGeoInfo.verticesMin = new float[] { point3f.X, point3f.Y, point3f.Z };
                    point3f = mesh.Vertices.Max();
                    dracoGeoInfo.verticesMax = new float[] { point3f.X, point3f.Y, point3f.Z };

                    // Triangle Stats
                    dracoGeoInfo.trianglesNum = mesh.Faces.TriangleCount;
                    dracoGeoInfo.trianglesMin = 0;
                    dracoGeoInfo.trianglesMax = dracoGeoInfo.verticesNum - 1;

                    // Normals Stats
                    dracoGeoInfo.normalsNum = mesh.Normals.Count;
                    vector3f = mesh.Normals.Min();
                    dracoGeoInfo.normalsMin = new float[] { vector3f.X, vector3f.Y, vector3f.Z };
                    vector3f = mesh.Normals.Max();
                    dracoGeoInfo.normalsMax = new float[] { vector3f.X, vector3f.Y, vector3f.Z };

                    // TexCoord Stats
                    dracoGeoInfo.texCoordsNum = mesh.TextureCoordinates.Count;
                    point2f = mesh.TextureCoordinates.Min();
                    dracoGeoInfo.texCoordsMin = new float[] { point2f.X, point2f.Y };
                    point2f = mesh.TextureCoordinates.Max();
                    dracoGeoInfo.texCoordsMax = new float[] { point2f.X, point2f.Y };

                    dracoGeoInfo.success = true;
                }
                geo.Dispose();
                dracoCompression.Dispose();
            }
            finally
            {
                File.Delete(filePath);
            }

            byteLength += RawBytes.Count;
            PrimitiveCount += 1;

            return dracoGeoInfo;
        }

        public void ReadFileFromPath(string filePath)
        {
            using (FileStream stream = File.Open(filePath, FileMode.Open))
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                RawBytes.Add(bytes);
            }

            byteLength += RawBytes.Count;
            PrimitiveCount += 1;
        }

        public void ReadPNGFromStream(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            RawBytes.Add(bytes);

            byteLength += RawBytes.Count;
            PrimitiveCount += 1;
        }
    }
}
