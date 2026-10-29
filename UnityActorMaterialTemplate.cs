namespace Landoria.ModelForge
{
    // Replaces placeholder prefab textures with materials captured from the live actor.
    internal static class UnityActorMaterialTemplate
    {
        internal const string Code = @"    private static void ApplyActorSnapshot(string folder, string snapshot)
    {
        if (string.IsNullOrEmpty(snapshot)) { return; }
        var modelPath = ""Assets/Models/"" + snapshot + "".glb"";
        var metadata = Path.ChangeExtension(modelPath, "".valheim.json"");
        if (!File.Exists(metadata)) { return; }
        var captured = JsonUtility.FromJson<Snapshot>(File.ReadAllText(metadata));
        if (captured == null || captured.materials == null) { return; }
        for (var i = 0; i < captured.materials.Length; i++)
        {
            var path = folder + ""/Material_"" + i + "".mat"";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { continue; }
            RestoreProperties(material, captured.materials[i], modelPath);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
    }

";
    }
}
