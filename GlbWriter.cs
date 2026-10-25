using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Writes static Unity meshes and base textures as one GLB file.
    internal sealed class GlbWriter
    {
        private readonly MemoryStream _binary = new MemoryStream();
        private readonly JArray _views = new JArray();
        private readonly JArray _accessors = new JArray();
        private readonly JArray _meshes = new JArray();
        private readonly JArray _materials = new JArray();
        private readonly JArray _images = new JArray();
        private readonly JArray _textures = new JArray();
        private readonly Dictionary<Material, int> _materialIds = new Dictionary<Material, int>();

        // Exports all readable static mesh renderers below the selected object.
        internal static void Write(GameObject root, string path)
        {
            var writer = new GlbWriter();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null && filter.sharedMesh != null && renderer.enabled && IsFirstLod(renderer))
                {
                    writer.AddMesh(root.transform, filter, renderer);
                }
            }
            if (writer._meshes.Count == 0)
            {
                throw new InvalidOperationException("No static mesh found. Animated models are not supported.");
            }
            writer.Save(path);
        }

        // Excludes lower-detail LOD renderers from the exported model.
        private static bool IsFirstLod(MeshRenderer renderer)
        {
            var group = renderer.GetComponentInParent<LODGroup>();
            if (group == null) { return true; }
            var levels = group.GetLODs();
            if (levels.Length == 0) { return true; }
            foreach (var item in levels[0].renderers)
            {
                if (item == renderer) { return true; }
            }
            foreach (var level in levels)
            {
                foreach (var item in level.renderers)
                {
                    if (item == renderer) { return false; }
                }
            }
            return true;
        }

        // Adds one mesh with one primitive per Unity submesh.
        private void AddMesh(Transform root, MeshFilter filter, MeshRenderer renderer)
        {
            var mesh = filter.sharedMesh;
            var positions = new List<float>();
            var normals = new List<float>();
            var uvs = new List<float>();
            var vertices = mesh.vertices;
            var sourceNormals = mesh.normals;
            var sourceUvs = mesh.uv;
            foreach (var vertex in vertices)
            {
                var point = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                positions.Add(-point.x); positions.Add(point.y); positions.Add(point.z);
            }
            foreach (var normal in sourceNormals)
            {
                var direction = root.InverseTransformDirection(filter.transform.TransformDirection(normal));
                normals.Add(-direction.x); normals.Add(direction.y); normals.Add(direction.z);
            }
            foreach (var uv in sourceUvs)
            {
                uvs.Add(uv.x); uvs.Add(1f - uv.y);
            }
            var positionId = AddFloats(positions, "VEC3", true);
            var normalId = sourceNormals.Length == vertices.Length ? AddFloats(normals, "VEC3", false) : -1;
            var uvId = sourceUvs.Length == vertices.Length ? AddFloats(uvs, "VEC2", false) : -1;
            var primitives = new JArray();
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var indices = mesh.GetIndices(sub);
                var flipped = new List<uint>();
                for (var i = 0; i + 2 < indices.Length; i += 3)
                {
                    flipped.Add((uint)indices[i]); flipped.Add((uint)indices[i + 2]); flipped.Add((uint)indices[i + 1]);
                }
                var attributes = new JObject { ["POSITION"] = positionId };
                if (normalId >= 0) { attributes["NORMAL"] = normalId; }
                if (uvId >= 0) { attributes["TEXCOORD_0"] = uvId; }
                var primitive = new JObject { ["attributes"] = attributes, ["indices"] = AddIndices(flipped) };
                var materials = renderer.sharedMaterials;
                if (sub < materials.Length && materials[sub] != null)
                {
                    primitive["material"] = AddMaterial(materials[sub]);
                }
                primitives.Add(primitive);
            }
            _meshes.Add(new JObject { ["name"] = filter.name, ["primitives"] = primitives });
        }

        // Appends an aligned binary view and returns its index.
        private int AddView(byte[] bytes)
        {
            while (_binary.Length % 4 != 0) { _binary.WriteByte(0); }
            var offset = _binary.Position;
            _binary.Write(bytes, 0, bytes.Length);
            _views.Add(new JObject { ["buffer"] = 0, ["byteOffset"] = offset, ["byteLength"] = bytes.Length });
            return _views.Count - 1;
        }

        // Appends float geometry and an accessor.
        private int AddFloats(List<float> values, string type, bool bounds)
        {
            var bytes = new byte[values.Count * 4];
            Buffer.BlockCopy(values.ToArray(), 0, bytes, 0, bytes.Length);
            var width = type == "VEC3" ? 3 : 2;
            var accessor = new JObject { ["bufferView"] = AddView(bytes), ["componentType"] = 5126,
                ["count"] = values.Count / width, ["type"] = type };
            if (bounds && values.Count > 0)
            {
                var min = new JArray(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new JArray(float.MinValue, float.MinValue, float.MinValue);
                for (var i = 0; i < values.Count; i++)
                {
                    var axis = i % 3;
                    min[axis] = Math.Min((float)min[axis], values[i]);
                    max[axis] = Math.Max((float)max[axis], values[i]);
                }
                accessor["min"] = min; accessor["max"] = max;
            }
            _accessors.Add(accessor);
            return _accessors.Count - 1;
        }

        // Appends triangle indices as unsigned 32-bit integers.
        private int AddIndices(List<uint> values)
        {
            var bytes = new byte[values.Count * 4];
            Buffer.BlockCopy(values.ToArray(), 0, bytes, 0, bytes.Length);
            _accessors.Add(new JObject { ["bufferView"] = AddView(bytes), ["componentType"] = 5125,
                ["count"] = values.Count, ["type"] = "SCALAR" });
            return _accessors.Count - 1;
        }

        // Converts the diffuse color and base texture to a glTF material.
        private int AddMaterial(Material material)
        {
            if (_materialIds.TryGetValue(material, out var id)) { return id; }
            var color = material.HasProperty("_Color") ? material.color : Color.white;
            var pbr = new JObject { ["baseColorFactor"] = new JArray(color.r, color.g, color.b, color.a),
                ["metallicFactor"] = 0, ["roughnessFactor"] = 1 };
            var texture = material.mainTexture;
            if (texture != null)
            {
                var png = TextureBytes(texture);
                var imageId = _images.Count;
                _images.Add(new JObject { ["bufferView"] = AddView(png), ["mimeType"] = "image/png" });
                var textureId = _textures.Count;
                _textures.Add(new JObject { ["source"] = imageId });
                pbr["baseColorTexture"] = new JObject { ["index"] = textureId };
            }
            id = _materials.Count;
            _materials.Add(new JObject { ["name"] = material.name, ["pbrMetallicRoughness"] = pbr,
                ["alphaMode"] = color.a < .999f ? "BLEND" : "OPAQUE", ["doubleSided"] = true });
            _materialIds[material] = id;
            return id;
        }

        // Reads a GPU texture into PNG bytes without changing the game asset.
        internal static byte[] TextureBytes(Texture texture)
        {
            var width = Math.Min(texture.width, 2048);
            var height = Math.Min(texture.height, 2048);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply();
                return readable.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(readable);
            }
        }

        // Writes the glTF JSON and binary chunks in GLB 2.0 layout.
        private void Save(string path)
        {
            var nodes = new JArray();
            for (var i = 0; i < _meshes.Count; i++) { nodes.Add(new JObject { ["mesh"] = i }); }
            var roots = new JArray();
            for (var i = 0; i < nodes.Count; i++) { roots.Add(i); }
            var json = new JObject { ["asset"] = new JObject { ["version"] = "2.0", ["generator"] = "ModelForge" },
                ["scene"] = 0, ["scenes"] = new JArray(new JObject { ["nodes"] = roots }),
                ["nodes"] = nodes, ["meshes"] = _meshes, ["materials"] = _materials,
                ["textures"] = _textures, ["images"] = _images, ["accessors"] = _accessors,
                ["bufferViews"] = _views, ["buffers"] = new JArray(new JObject { ["byteLength"] = _binary.Length }) };
            var jsonBytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            var jsonLength = (jsonBytes.Length + 3) & ~3;
            var binLength = ((int)_binary.Length + 3) & ~3;
            using (var file = new BinaryWriter(File.Create(path)))
            {
                file.Write(0x46546C67); file.Write(2); file.Write(12 + 8 + jsonLength + 8 + binLength);
                file.Write(jsonLength); file.Write(0x4E4F534A); file.Write(jsonBytes);
                for (var i = jsonBytes.Length; i < jsonLength; i++) { file.Write((byte)32); }
                file.Write(binLength); file.Write(0x004E4942); file.Write(_binary.ToArray());
                for (var i = (int)_binary.Length; i < binLength; i++) { file.Write((byte)0); }
            }
        }
    }
}
