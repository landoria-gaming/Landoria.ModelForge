namespace Landoria.ModelForge
{
    internal static class UnityShaderTemplate
    {
        internal const string Code = @"    private static void RestoreIntegers(Material material, MaterialData data)
    {
        if (data.integers == null) { return; }
        foreach (var entry in data.integers)
        {
            if (material.HasProperty(entry.name)) { material.SetInteger(entry.name, entry.value); }
        }
    }

    private static readonly Dictionary<string, Shader> LoadedShaders = new Dictionary<string, Shader>();
    private static readonly Dictionary<Material, Material> RuntimeMaterials = new Dictionary<Material, Material>();

    private static void ApplyLiveShaders(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) { return; }
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        var snapshots = new Dictionary<string, Snapshot>();
        foreach (var root in scene.GetRootGameObjects())
        {
            var name = AssetNameForRoot(root, sceneName);
            var modelPath = ""Assets/Models/"" + name + "".glb"";
            if (!snapshots.TryGetValue(name, out var settings))
            {
                var metadataPath = Path.ChangeExtension(modelPath, "".valheim.json"");
                settings = File.Exists(metadataPath)
                    ? JsonUtility.FromJson<Snapshot>(File.ReadAllText(metadataPath)) : null;
                snapshots[name] = settings;
            }
            if (settings == null || settings.materials == null) { continue; }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (source == null) { continue; }
                    var index = MaterialIndex(source, name);
                    if (index < 0 || index >= settings.materials.Length) { continue; }
                    if (RuntimeMaterials.TryGetValue(source, out var cached))
                    {
                        materials[i] = cached;
                        changed = true;
                        continue;
                    }
                    var data = settings.materials[index];
                    if (data.shader == ""Custom/Heightmap"") { continue; }
                    var shader = FindGameShader(data.shader);
                    if (shader == null || shader.name == source.shader.name) { continue; }
                    var live = new Material(shader) { name = data.name, hideFlags = HideFlags.DontSave };
                    RestoreProperties(live, data, modelPath);
                    RuntimeMaterials[source] = live;
                    materials[i] = live;
                    changed = true;
                }
                if (changed) { renderer.sharedMaterials = materials; }
            }
        }
    }

    private static int MaterialIndex(Material material, string modelName)
    {
        if (material == null) { return -1; }
        var path = AssetDatabase.GetAssetPath(material);
        var prefix = ""Assets/Materials/"" + modelName + ""_"";
        if (!path.StartsWith(prefix) || !path.EndsWith("".mat"")) { return -1; }
        var value = path.Substring(prefix.Length, path.Length - prefix.Length - 4);
        return int.TryParse(value, out var index) ? index : -1;
    }

    private static string AssetNameForRoot(GameObject root, string fallback)
    {
        if (root.name.StartsWith(""AreaAsset_"")) { return root.name; }
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null) { continue; }
                var path = AssetDatabase.GetAssetPath(material);
                if (!path.StartsWith(""Assets/Materials/"")) { continue; }
                var file = Path.GetFileNameWithoutExtension(path);
                var last = file.LastIndexOf('_');
                if (last > 0 && int.TryParse(file.Substring(last + 1), out _))
                { return file.Substring(0, last); }
            }
        }
        return fallback;
    }

    private static void RestoreStableMaterials(Scene scene)
    {
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) { return; }
        var sceneName = Path.GetFileNameWithoutExtension(scene.path);
        var snapshots = new Dictionary<string, Snapshot>();
        foreach (var root in scene.GetRootGameObjects())
        {
            var name = AssetNameForRoot(root, sceneName);
            if (!snapshots.TryGetValue(name, out var settings))
            {
                var metadata = ""Assets/Models/"" + name + "".valheim.json"";
                settings = File.Exists(metadata)
                    ? JsonUtility.FromJson<Snapshot>(File.ReadAllText(metadata)) : null;
                snapshots[name] = settings;
            }
            if (settings == null || settings.materials == null) { continue; }
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var live = materials[i];
                    if (live == null || (live.hideFlags & HideFlags.DontSave) == 0) { continue; }
                    for (var index = 0; index < settings.materials.Length; index++)
                    {
                        if (settings.materials[index].name != live.name) { continue; }
                        var path = ""Assets/Materials/"" + name + ""_"" + index + "".mat"";
                        materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                        break;
                    }
                }
                renderer.sharedMaterials = materials;
            }
        }
    }

    private static Shader FindGameShader(string name)
    {
        if (string.IsNullOrEmpty(name)) { return null; }
        if (LoadedShaders.TryGetValue(name, out var cached)) { return cached; }
        var local = Shader.Find(name);
        if (name == ""Standard"" || name.StartsWith(""Legacy Shaders/""))
        { LoadedShaders[name] = local; return local; }
        const string setting = ""ProjectSettings/ModelForgeGameData.txt"";
        if (!File.Exists(setting)) { return local; }
        var dataPath = File.ReadAllText(setting).Trim();
        var softRef = Path.Combine(dataPath, ""StreamingAssets/SoftRef"");
        var manifest = Path.Combine(softRef, ""manifest_extended"");
        if (!File.Exists(manifest)) { return local; }
        var wanted = Path.GetFileName(name) + "".shader"";
        string bundleId = null;
        foreach (var line in File.ReadLines(manifest))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(""bundle: "")) { bundleId = trimmed.Substring(8); }
            if (!trimmed.StartsWith(""path in bundle: "")) { continue; }
            var path = trimmed.Substring(16);
            if (!path.EndsWith(""/"" + wanted, System.StringComparison.OrdinalIgnoreCase)) { continue; }
            var bundlePath = Path.Combine(softRef, ""Bundles"", bundleId);
            if (!LoadedBundles.TryGetValue(bundlePath, out var bundle))
            {
                bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null) { break; }
                LoadedBundles[bundlePath] = bundle;
            }
            var shader = bundle.LoadAsset<Shader>(path);
            if (shader != null && shader.name == name)
            { LoadedShaders[name] = shader; return shader; }
        }
        Debug.LogWarning(""ModelForge: game shader unavailable: "" + name);
        LoadedShaders[name] = local;
        return local;
    }

";
    }
}
