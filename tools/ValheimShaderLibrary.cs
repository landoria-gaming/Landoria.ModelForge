using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Installed into the generated Unity project. Shaders stay in the Valheim installation.
public static class ValheimShaderLibrary
{
    [Serializable]
    private sealed class Index
    {
        public string gameData;
        public Entry[] compiledShaders;
    }

    [Serializable]
    private sealed class Entry
    {
        public string asset;
        public string bundle;
    }

    private static readonly Dictionary<string, Shader> Shaders =
        new Dictionary<string, Shader>(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;

    public static Shader Find(string shaderName)
    {
        if (!loaded) { LoadAll(); }
        if (Shaders.TryGetValue(shaderName, out var shader)) { return shader; }
        return Shader.Find(shaderName);
    }

    public static void LoadAll()
    {
        if (loaded) { Debug.Log("Valheim shaders loaded: " + Shaders.Count); return; }
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var indexFile = Path.Combine(project, "ValheimDependencies/index.json");
        var index = JsonUtility.FromJson<Index>(File.ReadAllText(indexFile));
        var folder = Path.Combine(index.gameData, "StreamingAssets/SoftRef/Bundles");
        var missing = 0;
        foreach (var entry in index.compiledShaders)
        {
            var path = Path.Combine(folder, entry.bundle);
            var bundle = ValheimBundleLibrary.Load(path, entry.asset);
            var shader = bundle != null ? bundle.LoadAsset<Shader>(entry.asset) : null;
            if (shader == null) { missing++; continue; }
            Shaders[shader.name] = shader;
        }
        loaded = true;
        Debug.Log("Valheim shaders loaded: " + Shaders.Count + "; unavailable: " + missing);
    }
}
