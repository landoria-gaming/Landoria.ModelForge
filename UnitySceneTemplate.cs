namespace Landoria.ModelForge
{
    // Text files written into each generated Unity project.
    internal static class UnitySceneTemplate
    {
        internal const string SceneBuilder = @"using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class ModelForgeSceneBuilder
{
    static ModelForgeSceneBuilder()
    {
        EditorApplication.delayCall += BuildMissingScenes;
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
            if (File.Exists(scenePath)) { continue; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) { continue; }
            BuildScene(prefab, scenePath);
        }
    }

    private static void BuildScene(GameObject prefab, string path)
    {
        var mode = string.IsNullOrEmpty(SceneManager.GetActiveScene().path)
            ? NewSceneMode.Single : NewSceneMode.Additive;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        model.name = prefab.name;
        var bounds = ModelBounds(model);
        var cameraObject = new GameObject(""Preview Camera"");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.tag = ""MainCamera"";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.16f, .18f, .2f);
        cameraObject.transform.position = bounds.center + new Vector3(
            bounds.extents.x + bounds.extents.z + 2f,
            bounds.extents.y + 1f,
            -(bounds.extents.x + bounds.extents.z + 2f));
        cameraObject.transform.LookAt(bounds.center);
        var lightObject = new GameObject(""Preview Light"");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        EditorSceneManager.SaveScene(scene, path);
        SaveJpeg(camera, Path.GetFileNameWithoutExtension(path));
        if (mode == NewSceneMode.Additive) { EditorSceneManager.CloseScene(scene, true); }
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
    ""com.unity.cloud.gltfast"": ""https://github.com/Unity-Technologies/com.unity.cloud.gltfast.git?path=/Packages/com.unity.cloud.gltfast""
  }
}
";
        internal const string ProjectVersion = @"m_EditorVersion: 6000.0.75f1
";
    }
}
