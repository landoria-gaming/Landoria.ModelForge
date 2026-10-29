namespace Landoria.ModelForge
{
    // Editor code embedded in the Unity project for rebuilding local area scenes.
    internal static class UnityAreaTemplate
    {
        internal const string Code = @"    [System.Serializable]
    private sealed class AreaData
    {
        public PlacementData[] placements;
        public ActorData[] actors;
        public float radius;
        public Vector4 wind1;
        public Vector4 wind2;
        public float windAlpha;
    }
    [System.Serializable]
    private sealed class ActorData
    {
        public string prefab;
        public string snapshot;
        public Vector3 position;
        public Quaternion rotation;
        public bool player;
    }
    [System.Serializable]
    private sealed class PlacementData
    {
        public string asset;
        public string name;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    [MenuItem(""ModelForge/Build missing scenes"")]
    public static void BuildMissingScenes()
    {
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
        { PlayerSettings.colorSpace = ColorSpace.Linear; }
        const string folder = ""Assets/Models"";
        if (!Directory.Exists(folder)) { return; }
        var areaAssets = new HashSet<string>();
        foreach (var path in Directory.GetFiles(folder, ""*.area.json""))
        {
            var area = JsonUtility.FromJson<AreaData>(File.ReadAllText(path));
            if (area == null || area.placements == null) { continue; }
            foreach (var placement in area.placements)
            { areaAssets.Add(placement.asset); }
        }
        foreach (var file in Directory.GetFiles(folder, ""*.glb""))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.StartsWith(""AreaAsset_"") || areaAssets.Contains(name)) { continue; }
            var modelPath = file.Replace('\\', '/');
            var scenePath = ""Assets/Scenes/"" + name + "".unity"";
            var request = Path.ChangeExtension(modelPath, "".build"");
            if (File.Exists(scenePath) && !File.Exists(request)) { continue; }
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) { continue; }
            BuildScene(prefab, modelPath, scenePath);
            if (File.Exists(request)) { File.Delete(request); }
        }
        BuildAreaScenes(folder);
    }

    private static void BuildAreaScenes(string folder)
    {
        foreach (var file in Directory.GetFiles(folder, ""*.area.json""))
        {
            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(file));
            var scenePath = ""Assets/Scenes/"" + name + "".unity"";
            var request = folder + ""/"" + name + "".build"";
            if (File.Exists(scenePath) && !File.Exists(request)) { continue; }
            var data = JsonUtility.FromJson<AreaData>(File.ReadAllText(file));
            if (data == null || data.placements == null) { continue; }
            BuildAreaScene(data, scenePath);
            if (File.Exists(request)) { File.Delete(request); }
        }
    }

    private static void BuildAreaScene(AreaData area, string path)
    {
        var mode = string.IsNullOrEmpty(SceneManager.GetActiveScene().path)
            ? NewSceneMode.Single : NewSceneMode.Additive;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        Snapshot settings = null;
        var prepared = new HashSet<string>();
        var snapshots = new Dictionary<string, Snapshot>();
        var bounds = new Bounds(Vector3.zero, Vector3.one);
        var first = true;
        foreach (var placement in area.placements)
        {
            var modelPath = ""Assets/Models/"" + placement.asset + "".glb"";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (prefab == null) { Debug.LogWarning(""ModelForge missing asset: "" + modelPath); continue; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            instance.name = !string.IsNullOrEmpty(placement.name) ? placement.name :
                System.Text.RegularExpressions.Regex.Replace(placement.asset,
                    @""^AreaAsset_\d+m_\d{8}_\d{6}_\d{4}_"", """");
            instance.transform.SetPositionAndRotation(placement.position, placement.rotation);
            instance.transform.localScale = placement.scale;
            if (!snapshots.TryGetValue(placement.asset, out var materialData))
            {
                var metadata = Path.ChangeExtension(modelPath, "".valheim.json"");
                materialData = File.Exists(metadata)
                    ? JsonUtility.FromJson<Snapshot>(File.ReadAllText(metadata)) : null;
                snapshots[placement.asset] = materialData;
            }
            if (materialData != null && prepared.Add(placement.asset))
            { ApplyMaterials(instance, modelPath, materialData); }
            else if (materialData != null)
            { BindPreparedMaterials(instance, placement.asset, materialData); }
            AddCollision(instance, placement.asset);
            if (settings == null) { settings = materialData; }
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else { bounds.Encapsulate(renderer.bounds); }
            }
        }
        AddAnimatedActors(area, scene);
        ConfigureAreaView(scene, bounds, settings, path);
        var windObject = new GameObject(""Valheim Wind"");
        SceneManager.MoveGameObjectToScene(windObject, scene);
        var wind = windObject.AddComponent<ModelForgeWindPreview>();
        if (area.wind1.sqrMagnitude > 0f) { wind.wind1 = area.wind1; }
        if (area.wind2.sqrMagnitude > 0f) { wind.wind2 = area.wind2; }
        wind.windAlpha = area.windAlpha;
        EditorSceneManager.SaveScene(scene, path);
        if (mode == NewSceneMode.Additive) { EditorSceneManager.CloseScene(scene, true); }
    }

    private static void AddCollision(GameObject instance, string asset)
    {
        if (asset.IndexOf(""grass"", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            asset.IndexOf(""shrub"", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            asset.IndexOf(""cloud"", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            asset.IndexOf(""water"", System.StringComparison.OrdinalIgnoreCase) >= 0)
        { return; }
        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) { continue; }
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
        }
    }

    private static void BindPreparedMaterials(GameObject instance, string asset, Snapshot data)
    {
        var resolved = new Dictionary<string, Material>();
        for (var i = 0; data.materials != null && i < data.materials.Length; i++)
        {
            var path = ""Assets/Materials/"" + asset + ""_"" + i + "".mat"";
            resolved[data.materials[i].name] = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && resolved.TryGetValue(materials[i].name, out var saved))
                { materials[i] = saved; }
                else if (data.materials != null && data.materials.Length > 0)
                {
                    var index = Mathf.Min(i, data.materials.Length - 1);
                    resolved.TryGetValue(data.materials[index].name, out materials[i]);
                }
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void ConfigureAreaView(Scene scene, Bounds bounds, Snapshot settings, string path)
    {
        var cameraObject = new GameObject(""Preview Camera"");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.tag = ""MainCamera"";
        camera.clearFlags = CameraClearFlags.SolidColor;
        RenderSettings.skybox = null;
        camera.backgroundColor = new Color(.16f, .18f, .2f);
        camera.nearClipPlane = .01f;
        if (settings != null && settings.cameraFov > 0f) { camera.fieldOfView = settings.cameraFov; }
        if (settings != null) { camera.allowHDR = settings.cameraHdr; }
        var distance = Mathf.Max(2f, bounds.extents.magnitude * 2.5f);
        cameraObject.transform.position = bounds.center +
            new Vector3(1f, .65f, -1f).normalized * distance;
        cameraObject.transform.LookAt(bounds.center);
        var lightObject = new GameObject(""Clear Daylight"");
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = settings != null ? settings.sunColor : Color.white;
        light.intensity = settings != null ? settings.sunIntensity : 1.2f;
        light.shadows = LightShadows.Soft;
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
        }
        ApplyPostProcessing(cameraObject, path, settings);
    }

";
    }
}
