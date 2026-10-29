using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal sealed class ValheimPrefabBrowser : EditorWindow
{
    [Serializable] private sealed class GameIndex { public string gameData; }
    private sealed class Entry
    {
        public string path;
        public string bundle;
        public string name;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Entry> matches = new List<Entry>();
    private string search = "";
    private string bundleFilter = "";
    private Vector2 scroll;
    private int page;
    private const int PageSize = 100;

    [MenuItem("Valheim/Browse bundle prefabs")]
    public static void Open()
    {
        GetWindow<ValheimPrefabBrowser>("Valheim Prefabs");
    }

    private void OnEnable()
    {
        LoadIndex();
        Filter();
        Debug.Log("Valheim prefab browser indexed " + entries.Count + " prefabs");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Prefabs in installed Valheim bundles", EditorStyles.boldLabel);
        var nextSearch = EditorGUILayout.TextField("Search", search);
        var nextBundle = EditorGUILayout.TextField("Bundle ID", bundleFilter);
        if (nextSearch != search || nextBundle != bundleFilter)
        {
            search = nextSearch;
            bundleFilter = nextBundle;
            page = 0;
            Filter();
        }
        EditorGUILayout.LabelField(matches.Count + " of " + entries.Count + " prefabs");
        DrawPages();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        var start = page * PageSize;
        var end = Math.Min(start + PageSize, matches.Count);
        for (var i = start; i < end; i++) { DrawEntry(matches[i]); }
        EditorGUILayout.EndScrollView();
    }

    private void DrawPages()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Previous") && page > 0) { page--; scroll = Vector2.zero; }
        GUILayout.Label("Page " + (page + 1) + " / " +
            Math.Max(1, (matches.Count + PageSize - 1) / PageSize));
        if (GUILayout.Button("Next") && (page + 1) * PageSize < matches.Count)
        { page++; scroll = Vector2.zero; }
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawEntry(Entry entry)
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent(entry.name, entry.path),
            GUILayout.MinWidth(160)))
        {
            var view = SceneView.lastActiveSceneView;
            var position = view != null ? view.pivot : Vector3.zero;
            ValheimPrefabMenu.AddPrefab(entry.path, position);
        }
        EditorGUILayout.LabelField(entry.bundle, GUILayout.Width(85));
        EditorGUILayout.EndHorizontal();
    }

    private void LoadIndex()
    {
        entries.Clear();
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var file = Path.Combine(project, "ValheimDependencies/index.json");
        var game = JsonUtility.FromJson<GameIndex>(File.ReadAllText(file));
        var manifest = Path.Combine(game.gameData,
            "StreamingAssets/SoftRef/manifest_extended");
        var bundle = "";
        foreach (var line in File.ReadLines(manifest))
        {
            var value = line.Trim();
            if (value.StartsWith("bundle: ")) { bundle = value.Substring(8); }
            if (!value.StartsWith("path in bundle: ") ||
                !value.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            { continue; }
            var path = value.Substring(16);
            entries.Add(new Entry { path = path, bundle = bundle,
                name = Path.GetFileNameWithoutExtension(path) });
        }
        entries.Sort((a, b) => string.Compare(a.name, b.name,
            StringComparison.OrdinalIgnoreCase));
    }

    private void Filter()
    {
        matches.Clear();
        foreach (var entry in entries)
        {
            if (entry.path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 ||
                entry.bundle.IndexOf(bundleFilter, StringComparison.OrdinalIgnoreCase) < 0)
            { continue; }
            matches.Add(entry);
        }
        Repaint();
    }
}
