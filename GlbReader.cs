using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Reads common static GLB meshes into temporary Unity objects.
    internal sealed class GlbReader
    {
        private readonly JObject _json;
        private readonly byte[] _data;
        private readonly Material[] _materials;

        // Stores parsed chunks and creates referenced materials.
        private GlbReader(JObject json, byte[] data)
        {
            _json = json;
            _data = data;
            var entries = (JArray)json["materials"];
            _materials = new Material[entries?.Count ?? 0];
            for (var i = 0; i < _materials.Length; i++) { _materials[i] = MakeMaterial((JObject)entries[i]); }
        }

        // Validates and loads one GLB file from the working folder.
        internal static GameObject Read(string path)
        {
            using (var file = new BinaryReader(File.OpenRead(path)))
            {
                if (file.ReadUInt32() != 0x46546C67 || file.ReadUInt32() != 2)
                {
                    throw new InvalidDataException("Expected a GLB 2.0 file.");
                }
                var length = file.ReadUInt32();
                if (length != file.BaseStream.Length) { throw new InvalidDataException("Invalid GLB length."); }
                var jsonLength = file.ReadInt32();
                if (file.ReadUInt32() != 0x4E4F534A) { throw new InvalidDataException("Missing GLB JSON chunk."); }
                var json = JObject.Parse(Encoding.UTF8.GetString(file.ReadBytes(jsonLength)));
                var dataLength = file.ReadInt32();
                if (file.ReadUInt32() != 0x004E4942) { throw new InvalidDataException("Missing GLB binary chunk."); }
                var data = file.ReadBytes(dataLength);
                return new GlbReader(json, data).Build(Path.GetFileNameWithoutExtension(path));
            }
        }

        // Builds each referenced mesh as a child of one local root.
        private GameObject Build(string name)
        {
            var root = new GameObject(name);
            try
            {
                var nodes = (JArray)_json["nodes"];
                var sceneIndex = (int)(_json["scene"] ?? 0);
                var scene = (JObject)_json["scenes"]?[sceneIndex];
                foreach (var index in (JArray)scene?["nodes"] ?? new JArray())
                {
                    AddNode((int)index, root.transform, nodes);
                }
                return root;
            }
            catch
            {
                UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        // Restores one node transform and its child nodes.
        private void AddNode(int index, Transform parent, JArray nodes)
        {
            var source = (JObject)nodes[index];
            var node = new GameObject((string)source["name"] ?? "Model");
            node.transform.SetParent(parent, false);
            var translation = (JArray)source["translation"];
            if (translation != null)
            {
                node.transform.localPosition = new Vector3(-(float)translation[0], (float)translation[1], (float)translation[2]);
            }
            var scale = (JArray)source["scale"];
            if (scale != null)
            {
                node.transform.localScale = new Vector3((float)scale[0], (float)scale[1], (float)scale[2]);
            }
            var rotation = (JArray)source["rotation"];
            if (rotation != null)
            {
                node.transform.localRotation = new Quaternion((float)rotation[0], -(float)rotation[1], -(float)rotation[2], (float)rotation[3]);
            }
            if (source["mesh"] != null) { AddMesh(node, (int)source["mesh"]); }
            foreach (var child in (JArray)source["children"] ?? new JArray())
            {
                AddNode((int)child, node.transform, nodes);
            }
        }

        // Combines glTF primitives into Unity submeshes.
        private void AddMesh(GameObject node, int index)
        {
            var source = (JObject)_json["meshes"]?[index];
            var primitives = (JArray)source?["primitives"];
            if (primitives == null) { return; }
            foreach (JObject primitive in primitives)
            {
                if ((int?)primitive["mode"] != null && (int)primitive["mode"] != 4) { continue; }
                var child = new GameObject("Primitive");
                child.transform.SetParent(node.transform, false);
                var attributes = (JObject)primitive["attributes"];
                var positions = ReadFloats((int)attributes["POSITION"], "VEC3");
                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                var vertices = new Vector3[positions.Length / 3];
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = new Vector3(-positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]);
                }
                mesh.vertices = vertices;
                AddAttributes(mesh, attributes, vertices.Length);
                var indices = ReadIndices((int)primitive["indices"]);
                for (var i = 0; i + 2 < indices.Length; i += 3)
                {
                    var swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap;
                }
                mesh.triangles = indices;
                if (mesh.normals.Length != vertices.Length) { mesh.RecalculateNormals(); }
                mesh.RecalculateBounds();
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var materialIndex = (int?)primitive["material"] ?? -1;
                child.AddComponent<MeshRenderer>().sharedMaterial = materialIndex >= 0 && materialIndex < _materials.Length
                    ? _materials[materialIndex] : DefaultMaterial();
            }
        }

        // Restores optional normal and UV attributes.
        private void AddAttributes(Mesh mesh, JObject attributes, int count)
        {
            if (attributes["NORMAL"] != null)
            {
                var source = ReadFloats((int)attributes["NORMAL"], "VEC3");
                if (source.Length == count * 3)
                {
                    var normals = new Vector3[count];
                    for (var i = 0; i < count; i++)
                    {
                        normals[i] = new Vector3(-source[i * 3], source[i * 3 + 1], source[i * 3 + 2]);
                    }
                    mesh.normals = normals;
                }
            }
            if (attributes["TEXCOORD_0"] != null)
            {
                var source = ReadFloats((int)attributes["TEXCOORD_0"], "VEC2");
                if (source.Length == count * 2)
                {
                    var uvs = new Vector2[count];
                    for (var i = 0; i < count; i++) { uvs[i] = new Vector2(source[i * 2], 1f - source[i * 2 + 1]); }
                    mesh.uv = uvs;
                }
            }
        }

        // Reads one float accessor with its view offset and stride.
        private float[] ReadFloats(int index, string expectedType)
        {
            var accessor = (JObject)_json["accessors"]?[index];
            if ((string)accessor?["type"] != expectedType || (int?)accessor["componentType"] != 5126)
            {
                throw new InvalidDataException("Unsupported GLB vertex format.");
            }
            var width = expectedType == "VEC3" ? 3 : 2;
            var count = (int)accessor["count"];
            var view = (JObject)_json["bufferViews"]?[(int)accessor["bufferView"]];
            var start = (int?)view?["byteOffset"] ?? 0;
            start += (int?)accessor["byteOffset"] ?? 0;
            var stride = (int?)view["byteStride"] ?? width * 4;
            var values = new float[count * width];
            for (var i = 0; i < count; i++)
            {
                for (var j = 0; j < width; j++) { values[i * width + j] = BitConverter.ToSingle(_data, start + i * stride + j * 4); }
            }
            return values;
        }

        // Reads supported unsigned index accessor types.
        private int[] ReadIndices(int index)
        {
            var accessor = (JObject)_json["accessors"]?[index];
            var count = (int)accessor["count"];
            var type = (int)accessor["componentType"];
            var view = (JObject)_json["bufferViews"]?[(int)accessor["bufferView"]];
            var start = ((int?)view?["byteOffset"] ?? 0) + ((int?)accessor["byteOffset"] ?? 0);
            var size = type == 5125 ? 4 : type == 5123 ? 2 : type == 5121 ? 1 : 0;
            if (size == 0) { throw new InvalidDataException("Unsupported GLB index format."); }
            var stride = (int?)view["byteStride"] ?? size;
            var indices = new int[count];
            for (var i = 0; i < count; i++)
            {
                var offset = start + i * stride;
                indices[i] = size == 4 ? (int)BitConverter.ToUInt32(_data, offset)
                    : size == 2 ? BitConverter.ToUInt16(_data, offset) : _data[offset];
            }
            return indices;
        }

        // Creates a local material from glTF base color data.
        private Material MakeMaterial(JObject source)
        {
            var material = DefaultMaterial();
            material.name = (string)source["name"] ?? "GLB Material";
            var pbr = (JObject)source["pbrMetallicRoughness"];
            var factor = (JArray)pbr?["baseColorFactor"];
            if (factor != null) { material.color = new Color((float)factor[0], (float)factor[1], (float)factor[2], (float)factor[3]); }
            var textureId = (int?)pbr?["baseColorTexture"]?["index"];
            if (textureId != null)
            {
                var imageId = (int)_json["textures"]?[textureId.Value]?["source"];
                var image = (JObject)_json["images"]?[imageId];
                if (image?["bufferView"] != null)
                {
                    var view = (JObject)_json["bufferViews"]?[(int)image["bufferView"]];
                    var bytes = new byte[(int)view["byteLength"]];
                    Buffer.BlockCopy(_data, (int?)view["byteOffset"] ?? 0, bytes, 0, bytes.Length);
                    var texture = new Texture2D(2, 2);
                    var method = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                    if (method == null || !(bool)method.Invoke(null, new object[] { texture, bytes, false }))
                    {
                        throw new InvalidDataException("Unsupported GLB image.");
                    }
                    material.mainTexture = texture;
                }
            }
            return material;
        }

        // Chooses a shader available in the running game.
        private static Material DefaultMaterial()
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            if (shader == null) { throw new InvalidOperationException("No compatible preview shader found."); }
            return new Material(shader);
        }
    }
}
