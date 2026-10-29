using System;
using System.Collections.Generic;
using UnityEngine;

// Shares loaded AssetBundles between the shader and prefab tools.
public static class ValheimBundleLibrary
{
    private static readonly Dictionary<string, AssetBundle> Bundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    public static AssetBundle Load(string path, string assetPath)
    {
        if (Bundles.TryGetValue(path, out var bundle) && bundle != null)
        { return bundle; }
        foreach (var loaded in AssetBundle.GetAllLoadedAssetBundles())
        {
            if (loaded == null || !loaded.Contains(assetPath)) { continue; }
            Bundles[path] = loaded;
            return loaded;
        }
        bundle = AssetBundle.LoadFromFile(path);
        if (bundle != null) { Bundles[path] = bundle; }
        return bundle;
    }
}
