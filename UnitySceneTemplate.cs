namespace Landoria.ModelForge
{
    // Text files written into each generated Unity project.
    internal static class UnitySceneTemplate
    {
        internal const string SceneBuilder = @"using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

[InitializeOnLoad]
internal sealed class ModelForgeSceneBuilder : AssetPostprocessor
{
    private static readonly Dictionary<string, AssetBundle> LoadedBundles =
        new Dictionary<string, AssetBundle>();
    [System.Serializable]
    private sealed class Snapshot
    {
        public MaterialData[] materials;
        public Color ambient;
        public Color sunColor;
        public float sunIntensity;
        public Vector3 sunRotation;
        public float cameraFov;
        public bool cameraHdr;
        public bool sunShadows;
        public bool fog;
        public Color fogColor;
        public int fogMode;
        public float fogDensity;
        public float fogStart;
        public float fogEnd;
    }

    [System.Serializable]
    private sealed class MaterialData
    {
        public string name;
        public string shader;
        public FloatData[] floats;
        public ColorData[] colors;
        public VectorData[] vectors;
        public TextureData[] textures;
        public string[] keywords;
    }

    [System.Serializable] private sealed class FloatData { public string name; public float value; }
    [System.Serializable] private sealed class ColorData { public string name; public Color value; }
    [System.Serializable] private sealed class VectorData { public string name; public Vector4 value; }
    [System.Serializable] private sealed class TextureData { public string name; public string file; }

    static ModelForgeSceneBuilder()
    {
        EditorApplication.delayCall += BuildMissingScenes;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorApplication.delayCall += RefreshOpenScenes;
    }

    private static void RefreshOpenScenes()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            ApplyLiveShaders(SceneManager.GetSceneAt(i));
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        ApplyLiveShaders(scene);
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        RestoreStableMaterials(scene);
    }

    private static void OnSceneSaved(Scene scene)
    {
        ApplyLiveShaders(scene);
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
        string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            if (path.EndsWith("".glb"") || path.EndsWith("".build""))
            {
                EditorApplication.delayCall += BuildMissingScenes;
                return;
            }
        }
    }

    [MenuItem(""ModelForge/Build missing scenes"")]
    public static void BuildMissingScenes()
    {
        const string modelFolder = ""Assets/Models"";
        const string sceneFolder = ""Assets/Scenes"";
        if (!Directory.Exists(modelFolder)) { return; }
        foreach (var file in Directory.GetFiles(modelFolder, ""*.glb""))
        {
            var modelPath = file.Replace('\\', '/');
            var scenePath = sceneFolder + ""/"" + Path.GetFileNameWithoutExtension(file) + "".unity"";
            var requestPath = Path.ChangeExtension(modelPath, "".build"");
            if (File.Exists(scenePath) && !File.Exists(requestPath)) { continue; }
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) { continue; }
            BuildScene(prefab, modelPath, scenePath);
            if (File.Exists(requestPath)) { File.Delete(requestPath); }
        }
    }

    private static void BuildScene(GameObject prefab, string modelPath, string path)
    {
        var mode = string.IsNullOrEmpty(SceneManager.GetActiveScene().path)
            ? NewSceneMode.Single : NewSceneMode.Additive;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        model.name = prefab.name;
        var metadataPath = Path.ChangeExtension(modelPath, "".valheim.json"");
        var settings = File.Exists(metadataPath)
            ? JsonUtility.FromJson<Snapshot>(File.ReadAllText(metadataPath)) : null;
        if (settings != null) { ApplyMaterials(model, modelPath, settings); }
        var bounds = ModelBounds(model);
        var cameraObject = new GameObject(""Preview Camera"");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.tag = ""MainCamera"";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.nearClipPlane = .01f;
        camera.backgroundColor = new Color(.16f, .18f, .2f);
        if (settings != null && settings.cameraFov > 0f) { camera.fieldOfView = settings.cameraFov; }
        if (settings != null) { camera.allowHDR = settings.cameraHdr; }
        var radius = Mathf.Max(bounds.extents.magnitude, .05f);
        var distance = Mathf.Max(.2f, radius / Mathf.Tan(camera.fieldOfView *
            .5f * Mathf.Deg2Rad) * 1.5f);
        cameraObject.transform.position = bounds.center +
            new Vector3(1f, .55f, -1f).normalized * distance;
        cameraObject.transform.LookAt(bounds.center);
        var lightObject = new GameObject(""Preview Light"");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = settings != null ? settings.sunIntensity : 1.2f;
        light.color = settings != null ? settings.sunColor : Color.white;
        light.shadows = settings != null && settings.sunShadows
            ? LightShadows.Soft : LightShadows.None;
        lightObject.transform.rotation = settings != null
            ? Quaternion.Euler(settings.sunRotation) : Quaternion.Euler(45f, -30f, 0f);
        if (settings != null)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = settings.ambient;
            RenderSettings.sun = light;
            RenderSettings.fog = settings.fog;
            RenderSettings.fogColor = settings.fogColor;
            RenderSettings.fogMode = (FogMode)settings.fogMode;
            RenderSettings.fogDensity = settings.fogDensity;
            RenderSettings.fogStartDistance = settings.fogStart;
            RenderSettings.fogEndDistance = settings.fogEnd;
        }
        EditorSceneManager.SaveScene(scene, path);
        ApplyLiveShaders(scene);
        SaveJpeg(camera, Path.GetFileNameWithoutExtension(path));
        if (mode == NewSceneMode.Additive) { EditorSceneManager.CloseScene(scene, true); }
    }

    private static void ApplyMaterials(GameObject model, string modelPath, Snapshot settings)
    {
        var resolved = new Dictionary<string, Material>();
        Directory.CreateDirectory(""Assets/Materials"");
        for (var i = 0; settings.materials != null && i < settings.materials.Length; i++)
        {
            var data = settings.materials[i];
            var assetPath = ""Assets/Materials/"" + Path.GetFileNameWithoutExtension(modelPath)
                + ""_"" + i + "".mat"";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            var shader = Shader.Find(data.shader);
            if (shader == null || (shader.name != ""Standard"" &&
                string.IsNullOrEmpty(AssetDatabase.GetAssetPath(shader))))
            {
                shader = Shader.Find(""Standard"");
            }
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }
            material.shader = shader;
            material.name = data.name;
            RestoreProperties(material, data, modelPath);
            EditorUtility.SetDirty(material);
            resolved[data.name] = material;
        }
        AssetDatabase.SaveAssets();
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && resolved.TryGetValue(materials[i].name, out var source))
                {
                    materials[i] = source;
                }
            }
            renderer.sharedMaterials = materials;
        }
    }

" + UnityShaderTemplate.Code + @"    private static void RestoreProperties(Material material, MaterialData data, string modelPath)
    {
        if (data.floats != null)
        {
            foreach (var entry in data.floats)
            {
                if (material.HasProperty(entry.name)) { material.SetFloat(entry.name, entry.value); }
            }
        }
        if (data.colors != null)
        {
            foreach (var entry in data.colors)
            {
                if (material.HasProperty(entry.name)) { material.SetColor(entry.name, entry.value); }
            }
        }
        if (data.vectors != null)
        {
            foreach (var entry in data.vectors)
            {
                if (material.HasProperty(entry.name)) { material.SetVector(entry.name, entry.value); }
            }
        }
        RestoreTextures(material, data, modelPath);
        if (material.shader.name == data.shader && data.keywords != null)
        {
            material.shaderKeywords = data.keywords;
        }
    }

    private static void RestoreTextures(Material material, MaterialData data, string modelPath)
    {
        if (data.textures == null) { return; }
        var folder = Path.ChangeExtension(modelPath, "".textures"");
        foreach (var entry in data.textures)
        {
            var path = folder + ""/"" + entry.file;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null && material.HasProperty(entry.name))
            {
                material.SetTexture(entry.name, texture);
            }
        }
    }

    private static void SaveJpeg(Camera camera, string name)
    {
        const string previewFolder = ""Assets/Previews"";
        Directory.CreateDirectory(previewFolder);
        var target = new RenderTexture(1024, 1024, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        try
        {
            camera.aspect = 1f;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            image.Apply();
            var previewPath = previewFolder + ""/"" + name + "".jpg"";
            File.WriteAllBytes(previewPath, image.EncodeToJPG(90));
            var workingPreview = Path.GetFullPath(Path.Combine(
                Application.dataPath, "".."", "".."", name + "".jpg""));
            File.Copy(previewPath, workingPreview, true);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }

    private static Bounds ModelBounds(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        var bounds = new Bounds(model.transform.position, Vector3.one);
        if (renderers.Length == 0) { return bounds; }
        bounds = renderers[0].bounds;
        foreach (var renderer in renderers) { bounds.Encapsulate(renderer.bounds); }
        return bounds;
    }
}
";
        internal const string Manifest = @"{
  ""dependencies"": {
    ""com.unity.cloud.gltfast"": ""https://github.com/Unity-Technologies/com.unity.cloud.gltfast.git?path=/Packages/com.unity.cloud.gltfast"",
    ""com.unity.modules.imageconversion"": ""1.0.0"",
    ""com.unity.modules.unitywebrequesttexture"": ""1.0.0""
  }
}
";
        internal const string ProjectVersion = @"m_EditorVersion: 6000.0.75f1
";
    }
}
