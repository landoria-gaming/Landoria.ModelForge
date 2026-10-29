namespace Landoria.ModelForge
{
    // Editor code that extracts animated actor prefabs from the installed game.
    internal static class UnityAnimatedTemplate
    {
        internal const string Code = @"    private static void AddAnimatedActors(AreaData area, Scene scene)
    {
        if (area.actors == null) { return; }
        foreach (var actor in area.actors)
        {
            var prefab = GetAnimatedPrefab(actor.prefab, actor.player, actor.snapshot);
            if (prefab == null) { continue; }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = actor.prefab;
            instance.transform.SetPositionAndRotation(actor.position, actor.rotation);
            var animator = instance.GetComponentInChildren<Animator>(true);
            if (animator != null) { animator.applyRootMotion = false; }
            var gameData = File.ReadAllText(""ProjectSettings/ModelForgeGameData.txt"").Trim();
            if (animator != null && TryFindPrefab(gameData, actor.prefab,
                out var animationPrefab, out var animationBundle, out var animationFile))
            {
                var loader = instance.AddComponent<ModelForgeRuntimeAnimator>();
                loader.animator = animator;
                loader.prefabPath = animationPrefab;
                loader.bundlePath = animationFile;
            }
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                if (actor.player) { Object.DestroyImmediate(body); }
                else { body.isKinematic = true; }
            }
            if (!actor.player)
            {
                var ambient = instance.AddComponent<ModelForgeAmbientActor>();
                ambient.idleSound = SaveActorIdle(gameData, actor.prefab,
                    out ambient.bundlePath, out ambient.clipPath);
                continue;
            }
            var motor = instance.GetComponent<CharacterController>();
            if (motor == null) { motor = instance.AddComponent<CharacterController>(); }
            motor.height = 1.8f;
            motor.radius = .3f;
            motor.center = new Vector3(0f, .9f, 0f);
            var control = instance.AddComponent<ModelForgePreviewController>();
            control.animator = animator;
            control.walkSound = SaveFootstep(gameData, false);
            control.runSound = SaveFootstep(gameData, true);
            control.jumpSound = control.runSound;
        }
    }

    private static GameObject GetAnimatedPrefab(string name, bool player, string snapshot)
    {
        var safe = new string(name.Where(char.IsLetterOrDigit).ToArray());
        var folder = ""Assets/Animated/"" + safe;
        var target = folder + ""/"" + safe + "".prefab"";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(target);
        if (existing != null)
        {
            ApplyActorSnapshot(folder, snapshot);
            return existing;
        }
        var gameData = File.ReadAllText(""ProjectSettings/ModelForgeGameData.txt"").Trim();
        if (!TryFindPrefab(gameData, name, out var sourcePath, out var bundle,
            out var bundleFile))
        {
            Debug.LogWarning(""ModelForge actor prefab unavailable: "" + name);
            return null;
        }
        var original = bundle.LoadAsset<GameObject>(sourcePath);
        if (original == null) { return null; }
        Directory.CreateDirectory(folder);
        var copy = Object.Instantiate(original);
        copy.name = safe;
        CleanGameScripts(copy);
        SaveActorMeshes(copy, folder);
        SaveActorMaterials(copy, folder);
        ApplyActorSnapshot(folder, snapshot);
        SaveActorAnimator(copy, original, folder, player);
        var result = PrefabUtility.SaveAsPrefabAsset(copy, target);
        Object.DestroyImmediate(copy);
        AssetDatabase.SaveAssets();
        return result;
    }

    private static bool TryFindPrefab(string data, string name,
        out string assetPath, out AssetBundle bundle, out string bundleFile)
    {
        assetPath = null;
        bundle = null;
        bundleFile = null;
        var softRef = Path.Combine(data, ""StreamingAssets/SoftRef"");
        var manifest = Path.Combine(softRef, ""manifest_extended"");
        if (!File.Exists(manifest)) { return false; }
        string bundleId = null;
        foreach (var line in File.ReadLines(manifest))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(""bundle: "")) { bundleId = trimmed.Substring(8); }
            if (!trimmed.StartsWith(""path in bundle: "")) { continue; }
            var path = trimmed.Substring(16);
            if (!path.StartsWith(""Assets/Characters/"") ||
                !path.EndsWith(""/"" + name + "".prefab"",
                    System.StringComparison.OrdinalIgnoreCase)) { continue; }
            var file = Path.Combine(softRef, ""Bundles"", bundleId);
            if (!LoadedBundles.TryGetValue(file, out bundle))
            {
                bundle = AssetBundle.LoadFromFile(file);
                if (bundle == null) { return false; }
                LoadedBundles[file] = bundle;
            }
            assetPath = path;
            bundleFile = file;
            return true;
        }
        return false;
    }

    private static void CleanGameScripts(GameObject root)
    {
        foreach (var part in root.GetComponentsInChildren<Transform>(true))
        { GameObjectUtility.RemoveMonoBehavioursWithMissingScript(part.gameObject); }
        foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
        { if (script != null) { Object.DestroyImmediate(script); } }
        foreach (var audio in root.GetComponentsInChildren<AudioSource>(true))
        { Object.DestroyImmediate(audio); }
    }

    private static void SaveActorMeshes(GameObject root, string folder)
    {
        var saved = new Dictionary<Mesh, Mesh>();
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) { continue; }
            renderer.sharedMesh = SaveMesh(renderer.sharedMesh, folder, saved);
        }
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) { continue; }
            filter.sharedMesh = SaveMesh(filter.sharedMesh, folder, saved);
        }
    }

    private static Mesh SaveMesh(Mesh source, string folder, Dictionary<Mesh, Mesh> saved)
    {
        if (saved.TryGetValue(source, out var mesh)) { return mesh; }
        mesh = Object.Instantiate(source);
        mesh.name = source.name;
        AssetDatabase.CreateAsset(mesh, folder + ""/Mesh_"" + saved.Count + "".asset"");
        saved[source] = mesh;
        return mesh;
    }

    private static void SaveActorMaterials(GameObject root, string folder)
    {
        var saved = new Dictionary<Material, Material>();
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            { continue; }
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (source == null) { continue; }
                if (!saved.TryGetValue(source, out var material))
                {
                    material = new Material(Shader.Find(""Standard""));
                    material.name = source.name;
                    if (source.HasProperty(""_Color""))
                    { material.color = source.GetColor(""_Color""); }
                    if (source.HasProperty(""_MainTex""))
                    {
                        var texture = source.GetTexture(""_MainTex"");
                        if (texture != null)
                        {
                            var path = folder + ""/Texture_"" + saved.Count + "".png"";
                            SaveActorTexture(texture, path);
                            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        }
                    }
                    AssetDatabase.CreateAsset(material,
                        folder + ""/Material_"" + saved.Count + "".mat"");
                    saved[source] = material;
                }
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static void SaveActorTexture(Texture source, string path)
    {
        var width = Mathf.Min(source.width, 2048);
        var height = Mathf.Min(source.height, 2048);
        var target = RenderTexture.GetTemporary(width, height, 0);
        var previous = RenderTexture.active;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.filterMode = source.filterMode;
                importer.mipmapEnabled = source is Texture2D t && t.mipmapCount > 1;
                importer.SaveAndReimport();
            }
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
        }
    }

    private static void SaveActorAnimator(GameObject copy, GameObject original,
        string folder, bool player)
    {
        var source = original.GetComponentInChildren<Animator>(true);
        var animator = copy.GetComponentInChildren<Animator>(true);
        if (source == null || animator == null || source.runtimeAnimatorController == null)
        { return; }
        if (source.avatar != null)
        {
            var avatar = Object.Instantiate(source.avatar);
            AssetDatabase.CreateAsset(avatar, folder + ""/Avatar.asset"");
            animator.avatar = avatar;
        }
        var controller = AnimatorController.CreateAnimatorControllerAtPath(
            folder + ""/Movement.controller"");
        var states = controller.layers[0].stateMachine;
        var clips = source.runtimeAnimatorController.animationClips;
        var idle = SaveClip(clips, player ? ""IdleTweaked"" : ""Idle"",
            ""idle"", folder, ""Idle.anim"");
        if (idle == null) { return; }
        if (!player)
        {
            var state = states.AddState(""Idle"");
            state.motion = idle;
            states.defaultState = state;
            animator.runtimeAnimatorController = controller;
            return;
        }
        var walk = SaveClip(clips, ""Walk New"", ""walk"", folder, ""Walk.anim"");
        var run = SaveClip(clips, ""Run New"", ""run"", folder, ""Run.anim"");
        var jump = SaveClip(clips, ""JumpTweaked"", ""jump"", folder, ""Jump.anim"");
        controller.AddParameter(""Speed"", AnimatorControllerParameterType.Float);
        controller.AddParameter(""Jump"", AnimatorControllerParameterType.Trigger);
        var blend = new BlendTree { name = ""Movement"", blendParameter = ""Speed"",
            useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(blend, controller);
        blend.AddChild(idle, 0f);
        blend.AddChild(walk ?? idle, 1f);
        blend.AddChild(run ?? walk ?? idle, 2f);
        var move = states.AddState(""Move"");
        move.motion = blend;
        states.defaultState = move;
        if (jump != null)
        {
            var air = states.AddState(""Jump"");
            air.motion = jump;
            var enter = move.AddTransition(air);
            enter.hasExitTime = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, ""Jump"");
            var leave = air.AddTransition(move);
            leave.hasExitTime = true;
            leave.exitTime = .9f;
        }
        animator.runtimeAnimatorController = controller;
    }

    private static AnimationClip SaveClip(AnimationClip[] clips, string exact,
        string contains, string folder, string file)
    {
        var source = clips.FirstOrDefault(c => c.name == exact) ??
            clips.FirstOrDefault(c => c.name.IndexOf(contains,
                System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (source == null) { return null; }
        var clip = Object.Instantiate(source);
        clip.name = source.name;
        AssetDatabase.CreateAsset(clip, folder + ""/"" + file);
        return clip;
    }

    private static AudioClip SaveFootstep(string gameData, bool run)
    {
        var fileName = run ? ""Player_Footstep_Grass_Run_M_09.wav"" :
            ""Player_Footstep_Grass_Walk_M_10.wav"";
        var target = ""Assets/Animated/"" + fileName;
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
        if (existing != null) { return existing; }
        if (!TryFindAudio(gameData, fileName, out var source)) { return null; }
        return SaveAudioClip(source, target);
    }

    private static AudioClip SaveActorIdle(string gameData, string actor,
        out string bundlePath, out string clipPath)
    {
        bundlePath = null;
        clipPath = null;
        var target = ""Assets/Animated/"" + actor + ""_Idle.wav"";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(target);
        if (existing != null) { return existing; }
        if (!TryFindPrefab(gameData, actor, out var path, out var bundle,
            out var bundleFile)) { return null; }
        var folder = path.Substring(0, path.LastIndexOf('/'));
        var names = bundle.GetAllAssetNames();
        var sourcePath = names.FirstOrDefault(item =>
            item.StartsWith(folder + ""/"", System.StringComparison.OrdinalIgnoreCase) &&
            (item.EndsWith("".wav"") || item.EndsWith("".ogg"")) &&
            (item.IndexOf(""Breath"", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             item.IndexOf(""Growl"", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             item.IndexOf(""Idle"", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             item.IndexOf(""Call"", System.StringComparison.OrdinalIgnoreCase) >= 0));
        if (sourcePath != null)
        {
            clipPath = sourcePath;
            bundlePath = bundleFile;
        }
        var source = sourcePath != null ? bundle.LoadAsset<AudioClip>(sourcePath) : null;
        return source != null ? SaveAudioClip(source, target) : null;
    }

    private static AudioClip SaveAudioClip(AudioClip source, string target)
    {
        var samples = new float[source.samples * source.channels];
        if (!source.GetData(samples, 0)) { return null; }
        Directory.CreateDirectory(""Assets/Animated"");
        using (var file = new BinaryWriter(File.Create(target)))
        {
            var bytes = samples.Length * 2;
            file.Write(System.Text.Encoding.ASCII.GetBytes(""RIFF""));
            file.Write(36 + bytes);
            file.Write(System.Text.Encoding.ASCII.GetBytes(""WAVEfmt ""));
            file.Write(16); file.Write((short)1); file.Write((short)source.channels);
            file.Write(source.frequency);
            file.Write(source.frequency * source.channels * 2);
            file.Write((short)(source.channels * 2)); file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes(""data"")); file.Write(bytes);
            foreach (var sample in samples)
            { file.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32767f)); }
        }
        AssetDatabase.ImportAsset(target);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(target);
    }

    private static bool TryFindAudio(string data, string fileName, out AudioClip clip)
    {
        clip = null;
        var softRef = Path.Combine(data, ""StreamingAssets/SoftRef"");
        string bundleId = null;
        foreach (var line in File.ReadLines(Path.Combine(softRef, ""manifest_extended"")))
        {
            var text = line.Trim();
            if (text.StartsWith(""bundle: "")) { bundleId = text.Substring(8); }
            if (!text.StartsWith(""path in bundle: "")) { continue; }
            var path = text.Substring(16);
            if (!path.EndsWith(""/"" + fileName,
                System.StringComparison.OrdinalIgnoreCase)) { continue; }
            var file = Path.Combine(softRef, ""Bundles"", bundleId);
            if (!LoadedBundles.TryGetValue(file, out var bundle))
            { bundle = AssetBundle.LoadFromFile(file); LoadedBundles[file] = bundle; }
            clip = bundle != null ? bundle.LoadAsset<AudioClip>(path) : null;
            return clip != null;
        }
        return false;
    }

";
    }
}
