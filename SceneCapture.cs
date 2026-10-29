using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Captures loaded static and instanced meshes near the player for Unity.
    internal static class SceneCapture
    {
        private const int MaxMeshes = 5000;
        private static readonly FieldInfo InstanceMatrices = typeof(InstanceRenderer)
            .GetField("m_instances", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo InstanceCount = typeof(InstanceRenderer)
            .GetField("m_instanceCount", BindingFlags.Instance | BindingFlags.NonPublic);

        // Save separate assets and build an editable Unity area project.
        internal static string Export(string folder, float radius)
        {
            var player = Player.m_localPlayer;
            if (player == null) { throw new InvalidOperationException("Join a world first."); }
            var clear = FindClear();
            if (clear == null) { throw new InvalidOperationException("Clear environment was not loaded."); }
            radius = Mathf.Clamp(radius, 5f, 100f);
            var center = player.transform.position;
            var root = new GameObject("ModelForge area capture");
            root.transform.position = center;
            root.SetActive(false);
            var materials = new Dictionary<Material, Material>();
            var baked = new List<Mesh>();
            try
            {
                var count = AddSceneMeshes(root, center, radius, materials);
                count += AddSkinned(root, center, radius, materials,
                    baked, MaxMeshes - count);
                count += AddInstances(root, center, radius, materials, MaxMeshes - count);
                if (count == 0) { throw new InvalidOperationException("No static meshes found nearby."); }
                var name = "Clear_" + Mathf.RoundToInt(radius) + "m_" +
                    DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var stamp = name.Substring(name.IndexOf("m_", StringComparison.Ordinal) + 2);
                var project = Path.Combine(folder, "UnityProject", "Assets", "Models");
                Directory.CreateDirectory(project);
                var assets = new Dictionary<string, string>();
                var placements = new JArray();
                var actors = CaptureActors(center, radius, project, clear, name);
                for (var i = 0; i < root.transform.childCount; i++)
                {
                    var proxy = root.transform.GetChild(i);
                    var filter = proxy.GetComponent<MeshFilter>();
                    var renderer = proxy.GetComponent<MeshRenderer>();
                    var key = AssetKey(filter, renderer);
                    if (!assets.TryGetValue(key, out var asset))
                    {
                        asset = SafeName(proxy.name) + "_" +
                            stamp + "_" +
                            (assets.Count + 1).ToString("D3");
                        var path = Path.Combine(project, asset + ".glb");
                        GlbWriter.Write(proxy.gameObject, path);
                        UnityMaterialSnapshot.Write(proxy.gameObject, path, clear);
                        UnitySceneExport.Write(path, Application.dataPath, false, folder);
                        assets[key] = asset;
                    }
                    placements.Add(new JObject { ["asset"] = asset,
                        ["name"] = proxy.name,
                        ["position"] = VectorJson(proxy.localPosition),
                        ["rotation"] = QuaternionJson(proxy.localRotation),
                        ["scale"] = VectorJson(proxy.localScale) });
                }
                File.WriteAllText(Path.Combine(project, name + ".area.json"),
                    new JObject { ["placements"] = placements,
                        ["actors"] = actors, ["radius"] = radius,
                        ["wind1"] = VectorJson(Shader.GetGlobalVector("_GlobalWind1")),
                        ["wind2"] = VectorJson(Shader.GetGlobalVector("_GlobalWind2")),
                        ["windAlpha"] = Shader.GetGlobalFloat("_GlobalWindAlpha")
                    }.ToString(Formatting.Indented));
                File.WriteAllText(Path.Combine(project, name + ".build"), "Build area scene");
                var build = UnityBatchBuild.Start(Path.GetDirectoryName(
                    Path.GetDirectoryName(project)), name);
                return name + ": " + assets.Count + " assets, " + count +
                    " placements. " + build;
            }
            finally
            {
                UnityEngine.Object.Destroy(root);
                foreach (var material in materials.Values) { UnityEngine.Object.Destroy(material); }
                foreach (var mesh in baked) { UnityEngine.Object.Destroy(mesh); }
            }
        }

        private static string AssetKey(MeshFilter filter, MeshRenderer renderer)
        {
            var key = filter.sharedMesh.GetInstanceID().ToString();
            foreach (var material in renderer.sharedMaterials)
            {
                key += "_" + (material != null ? material.GetInstanceID().ToString() : "0");
            }
            return key;
        }

        private static string SafeName(string name)
        {
            var clean = new System.Text.StringBuilder();
            foreach (var character in name)
            {
                clean.Append(char.IsLetterOrDigit(character) ? character : '_');
                if (clean.Length == 32) { break; }
            }
            return clean.ToString();
        }

        private static JObject VectorJson(Vector3 value)
        {
            return new JObject { ["x"] = value.x, ["y"] = value.y, ["z"] = value.z };
        }

        private static JObject VectorJson(Vector4 value)
        {
            return new JObject { ["x"] = value.x, ["y"] = value.y,
                ["z"] = value.z, ["w"] = value.w };
        }

        private static JArray CaptureActors(Vector3 center, float radius,
            string project, EnvSetup clear, string areaName)
        {
            var actors = new JArray();
            var seen = new HashSet<GameObject>();
            foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(
                FindObjectsSortMode.None))
            {
                var view = animator.GetComponentInParent<ZNetView>();
                var actor = view != null ? view.gameObject : animator.transform.root.gameObject;
                if (!seen.Add(actor) || !actor.activeInHierarchy ||
                    (actor.transform.position - center).sqrMagnitude > radius * radius) { continue; }
                var prefab = actor.name.Replace("(Clone)", "").Trim();
                var stamp = areaName.Substring(areaName.IndexOf("m_", StringComparison.Ordinal) + 2);
                var snapshot = SafeName(prefab) + "_appearance_" + stamp;
                UnityMaterialSnapshot.Write(actor, Path.Combine(project,
                    snapshot + ".glb"), clear);
                actors.Add(new JObject { ["prefab"] = prefab,
                    ["snapshot"] = snapshot,
                    ["position"] = VectorJson(actor.transform.position - center),
                    ["rotation"] = QuaternionJson(actor.transform.rotation),
                    ["player"] = actor.GetComponent<Player>() != null });
            }
            return actors;
        }

        private static JObject QuaternionJson(Quaternion value)
        {
            return new JObject { ["x"] = value.x, ["y"] = value.y,
                ["z"] = value.z, ["w"] = value.w };
        }

        // Find Valheim's loaded Clear preset without changing the game weather.
        private static EnvSetup FindClear()
        {
            if (EnvMan.instance == null) { return null; }
            foreach (var setup in EnvMan.instance.m_environments)
            {
                if (setup.m_name == "Clear") { return setup; }
            }
            return null;
        }

        // Add active scene meshes whose bounds intersect the capture sphere.
        private static int AddSceneMeshes(GameObject root, Vector3 center, float radius,
            Dictionary<Material, Material> materials)
        {
            var count = 0;
            var filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            Array.Sort(filters, (left, right) =>
                (right.GetComponent<Heightmap>() != null).CompareTo(
                    left.GetComponent<Heightmap>() != null));
            foreach (var filter in filters)
            {
                if (count >= MaxMeshes) { break; }
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null ||
                    filter.name == "lodMesh" ||
                    renderer.GetComponentInParent<Animator>() != null ||
                    !GlbWriter.IsFirstLod(renderer) ||
                    renderer.bounds.SqrDistance(center) > radius * radius) { continue; }
                AddProxy(root, filter.sharedMesh, renderer.sharedMaterials,
                    filter.transform.position, filter.transform.rotation,
                    filter.transform.lossyScale, filter.name, materials);
                count++;
            }
            return count;
        }

        // Include grass and other meshes drawn through Valheim's instancing system.
        private static int AddInstances(GameObject root, Vector3 center, float radius,
            Dictionary<Material, Material> materials, int limit)
        {
            if (InstanceMatrices == null || InstanceCount == null) { return 0; }
            var count = 0;
            foreach (var updater in InstanceRenderer.Instances)
            {
                var renderer = updater as InstanceRenderer;
                if (renderer == null || !renderer.isActiveAndEnabled ||
                    renderer.m_mesh == null || renderer.m_material == null) { continue; }
                var matrices = InstanceMatrices.GetValue(renderer) as Matrix4x4[];
                var total = (int)InstanceCount.GetValue(renderer);
                if (matrices == null) { continue; }
                for (var i = 0; i < total && i < matrices.Length && count < limit; i++)
                {
                    var matrix = matrices[i];
                    var position = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                    if ((position - center).sqrMagnitude > radius * radius) { continue; }
                    AddProxy(root, renderer.m_mesh, new[] { renderer.m_material },
                        position, matrix.rotation, matrix.lossyScale,
                        renderer.name, materials);
                    count++;
                }
                if (count >= limit) { break; }
            }
            return count;
        }

        // Freeze loaded skinned renderers in their current pose.
        private static int AddSkinned(GameObject root, Vector3 center, float radius,
            Dictionary<Material, Material> materials, List<Mesh> baked, int limit)
        {
            var count = 0;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(
                FindObjectsSortMode.None))
            {
                if (count >= limit) { break; }
                if (!renderer.enabled || renderer.sharedMesh == null ||
                    renderer.GetComponentInParent<Animator>() != null ||
                    renderer.bounds.SqrDistance(center) > radius * radius) { continue; }
                var mesh = new Mesh();
                renderer.BakeMesh(mesh);
                baked.Add(mesh);
                AddProxy(root, mesh, renderer.sharedMaterials,
                    renderer.transform.position, renderer.transform.rotation,
                    Vector3.one, renderer.name, materials);
                count++;
            }
            return count;
        }

        // Create an inactive proxy with the original mesh and a uniquely named material.
        private static void AddProxy(GameObject root, Mesh mesh, Material[] source,
            Vector3 position, Quaternion rotation, Vector3 scale, string name,
            Dictionary<Material, Material> materials)
        {
            var proxy = new GameObject(name);
            proxy.transform.SetParent(root.transform, false);
            proxy.transform.position = position;
            proxy.transform.rotation = rotation;
            proxy.transform.localScale = scale;
            proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copies = new Material[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var original = source[i];
                if (original == null) { continue; }
                if (!materials.TryGetValue(original, out var copy))
                {
                    copy = new Material(original) { name = original.name +
                        " #" + original.GetInstanceID() };
                    materials[original] = copy;
                }
                copies[i] = copy;
            }
            proxy.AddComponent<MeshRenderer>().sharedMaterials = copies;
        }
    }
}
