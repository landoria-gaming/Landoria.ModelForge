using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.PostProcessing;

namespace Landoria.ModelForge
{
    // Captures the selected object's live Valheim material and lighting settings.
    internal static class UnityMaterialSnapshot
    {
        // Writes editable material data beside the GLB for a Unity preview.
        internal static void Write(GameObject root, string glbPath, EnvSetup clear = null,
            float radius = 0f, int meshCount = 0)
        {
            var materials = new JArray();
            var textures = Path.Combine(Path.GetDirectoryName(glbPath),
                Path.GetFileNameWithoutExtension(glbPath) + ".textures");
            var seen = new System.Collections.Generic.HashSet<Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                { continue; }
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !seen.Add(material)) { continue; }
                    materials.Add(CaptureMaterial(material, textures, materials.Count));
                }
            }
            var sun = RenderSettings.sun;
            var camera = Camera.main;
            var post = camera != null ? camera.GetComponent<PostProcessingBehaviour>() : null;
            var data = new JObject
            {
                ["materials"] = materials,
                ["ambient"] = ColorValue(clear != null ? clear.m_ambColorDay :
                    RenderSettings.ambientLight),
                ["sunColor"] = ColorValue(clear != null ? clear.m_sunColorDay :
                    sun != null ? sun.color : Color.white),
                ["sunIntensity"] = clear != null ? clear.m_lightIntensityDay :
                    sun != null ? sun.intensity : 1.2f,
                ["sunRotation"] = VectorValue(clear != null ?
                    (Quaternion.Inverse(root.transform.rotation) * ClearSun(clear)).eulerAngles :
                    sun != null ?
                    (Quaternion.Inverse(root.transform.rotation) * sun.transform.rotation).eulerAngles :
                    new Vector3(45f, -30f, 0f)),
                ["cameraFov"] = camera != null ? camera.fieldOfView : 60f,
                ["cameraAspect"] = camera != null ? camera.aspect : 1f,
                ["hasCameraPose"] = camera != null,
                ["cameraPosition"] = VectorValue(camera != null ?
                    root.transform.InverseTransformPoint(camera.transform.position) : Vector3.zero),
                ["cameraRotation"] = QuaternionValue(camera != null ?
                    Quaternion.Inverse(root.transform.rotation) * camera.transform.rotation :
                    Quaternion.identity),
                ["cameraHdr"] = camera != null && camera.allowHDR,
                ["sunShadows"] = sun != null && sun.shadows != LightShadows.None,
                ["fog"] = RenderSettings.fog,
                ["fogColor"] = ColorValue(clear != null ? clear.m_fogColorDay :
                    RenderSettings.fogColor),
                ["fogMode"] = (int)RenderSettings.fogMode,
                ["fogDensity"] = clear != null ? clear.m_fogDensityDay :
                    RenderSettings.fogDensity,
                ["fogStart"] = RenderSettings.fogStartDistance,
                ["fogEnd"] = RenderSettings.fogEndDistance,
                ["environment"] = clear != null ? "Clear" : "Live",
                ["captureRadius"] = radius,
                ["meshCount"] = meshCount
            };
            if (post != null && post.enabled && post.profile != null)
            {
                data["postProcessingJson"] = JsonUtility.ToJson(post.profile);
            }
            File.WriteAllText(Path.ChangeExtension(glbPath, ".valheim.json"),
                data.ToString(Formatting.Indented));
        }

        // Reads every public shader property available on a live material.
        private static JObject CaptureMaterial(Material material, string folder, int index)
        {
            var floats = new JArray();
            var integers = new JArray();
            var colors = new JArray();
            var vectors = new JArray();
            var textures = new JArray();
            var shader = material.shader;
            for (var property = 0; shader != null && property < shader.GetPropertyCount(); property++)
            {
                var name = shader.GetPropertyName(property);
                if (!material.HasProperty(name)) { continue; }
                switch (shader.GetPropertyType(property))
                {
                    case ShaderPropertyType.Int:
                        integers.Add(new JObject { ["name"] = name,
                            ["value"] = material.GetInteger(name) });
                        break;
                    case ShaderPropertyType.Color:
                        colors.Add(new JObject { ["name"] = name,
                            ["value"] = ColorValue(material.GetColor(name)) });
                        break;
                    case ShaderPropertyType.Vector:
                        vectors.Add(new JObject { ["name"] = name,
                            ["value"] = VectorValue(material.GetVector(name)) });
                        break;
                    case ShaderPropertyType.Texture:
                        CaptureTexture(material, name, folder, index, textures);
                        break;
                    default:
                        floats.Add(new JObject { ["name"] = name,
                            ["value"] = material.GetFloat(name) });
                        break;
                }
            }
            return new JObject { ["name"] = material.name,
                ["shader"] = shader != null ? shader.name : "Standard",
                ["floats"] = floats, ["integers"] = integers, ["colors"] = colors,
                ["vectors"] = vectors, ["textures"] = textures,
                ["keywords"] = new JArray(material.shaderKeywords) };
        }

        // Saves a referenced shader texture as PNG for the Unity project.
        private static void CaptureTexture(Material material, string name, string folder,
            int index, JArray entries)
        {
            var texture = material.GetTexture(name);
            if (texture == null) { return; }
            try
            {
                Directory.CreateDirectory(folder);
                var file = index + "_" + name.TrimStart('_') + ".png";
                File.WriteAllBytes(Path.Combine(folder, file), GlbWriter.TextureBytes(texture));
                entries.Add(new JObject { ["name"] = name, ["file"] = file,
                    ["hasSampler"] = true, ["filterMode"] = (int)texture.filterMode,
                    ["mipmaps"] = texture is Texture2D image && image.mipmapCount > 1,
                    ["anisoLevel"] = texture.anisoLevel,
                    ["mipMapBias"] = texture.mipMapBias,
                    ["wrapMode"] = (int)texture.wrapMode,
                    ["scale"] = VectorValue(material.GetTextureScale(name)),
                    ["offset"] = VectorValue(material.GetTextureOffset(name)) });
            }
            catch (Exception error)
            {
                Debug.LogWarning("ModelForge could not capture " + name + ": " + error.Message);
            }
        }

        // Serializes colors and vectors in Unity JsonUtility's field format.
        private static JObject ColorValue(Color color)
        {
            return new JObject { ["r"] = color.r, ["g"] = color.g,
                ["b"] = color.b, ["a"] = color.a };
        }

        private static JObject VectorValue(Vector4 vector)
        {
            return new JObject { ["x"] = vector.x, ["y"] = vector.y,
                ["z"] = vector.z, ["w"] = vector.w };
        }

        private static JObject QuaternionValue(Quaternion value)
        {
            return new JObject { ["x"] = value.x, ["y"] = value.y,
                ["z"] = value.z, ["w"] = value.w };
        }

        // Match Valheim's EnvMan sun rotation at midday for Clear daylight.
        private static Quaternion ClearSun(EnvSetup clear)
        {
            return Quaternion.Euler(-90f + clear.m_sunAngle, 0f, 0f) *
                Quaternion.Euler(0f, -90f, 0f) *
                Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
