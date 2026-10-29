using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class ValheimPrefabMenu
{
    private const string BranchPath =
        "Assets/GameElements/Items/pickables/Pickable_Branch.prefab";
    static ValheimPrefabMenu()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += RefreshOpenScenes;
    }

    [MenuItem("Valheim/Add Pickable_Branch to scene")]
    public static void AddBranch()
    {
        AddPrefab(BranchPath, Vector3.zero);
    }

    internal static void AddPrefab(string assetPath, Vector3 position)
    {
        var instance = Create(assetPath);
        instance.transform.position = position;
        Undo.RegisterCreatedObjectUndo(instance, "Add Valheim prefab");
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log("Valheim prefab added: " + assetPath);
    }

    private static GameObject Create(string assetPath)
    {
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var indexPath = Path.Combine(project, "ValheimDependencies/index.json");
        var index = JsonUtility.FromJson<GameIndex>(File.ReadAllText(indexPath));
        var gameData = index.gameData;
        var manifest = Path.Combine(gameData,
            "StreamingAssets/SoftRef/manifest_extended");
        var bundleName = FindBundle(manifest, assetPath);
        var bundlePath = Path.Combine(gameData,
            "StreamingAssets/SoftRef/Bundles", bundleName);
        var bundle = ValheimBundleLibrary.Load(bundlePath, assetPath);
        if (bundle == null) { throw new InvalidOperationException(
            "Cannot load Valheim bundle: " + bundlePath); }
        var prefab = bundle.LoadAsset<GameObject>(assetPath);
        if (prefab == null) { throw new InvalidOperationException(
            "Cannot load Valheim prefab: " + assetPath); }
        var branch = UnityEngine.Object.Instantiate(prefab);
        branch.name = Path.GetFileNameWithoutExtension(assetPath);
        RepairMaterials(branch);
        branch.AddComponent<ValheimLinkedPrefab>().assetPath = assetPath;
        return branch;
    }

    private static void RepairMaterials(GameObject root)
    {
        var standard = Shader.Find("Standard");
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null || source.shader.name != "Hidden/InternalErrorShader")
                { continue; }
                var repaired = new Material(source) { shader = standard };
                materials[i] = repaired;
            }
            renderer.sharedMaterials = materials;
        }
    }

    [Serializable]
    private sealed class GameIndex { public string gameData; }

    private static string FindBundle(string manifest, string assetPath)
    {
        var bundle = "";
        foreach (var line in File.ReadLines(manifest))
        {
            var value = line.Trim();
            if (value.StartsWith("bundle: ")) { bundle = value.Substring(8); }
            if (value.Equals("path in bundle: " + assetPath,
                StringComparison.OrdinalIgnoreCase)) { return bundle; }
        }
        throw new FileNotFoundException("Valheim prefab not found", assetPath);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        Refresh(scene);
    }

    private static void RefreshOpenScenes()
    {
        for (var i = 0; i < SceneManager.sceneCount; i++)
        {
            Refresh(SceneManager.GetSceneAt(i));
        }
    }

    private static void Refresh(Scene scene)
    {
        if (!scene.IsValid()) { return; }
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var marker in root.GetComponentsInChildren<ValheimLinkedPrefab>(true))
            {
                var old = marker.gameObject;
                var parent = old.transform.parent;
                var position = old.transform.localPosition;
                var rotation = old.transform.localRotation;
                var scale = old.transform.localScale;
                var replacement = Create(marker.assetPath);
                replacement.transform.SetParent(parent, false);
                replacement.transform.localPosition = position;
                replacement.transform.localRotation = rotation;
                replacement.transform.localScale = scale;
                UnityEngine.Object.DestroyImmediate(old);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
