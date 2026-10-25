using System;
using System.IO;

namespace Landoria.ModelForge
{
    // Copies each GLB into a small Unity project that builds preview scenes.
    internal static class UnitySceneExport
    {
        private const string ProjectName = "UnityProject";

        // Adds the model and the editor scene builder to the working folder.
        internal static string Write(string glbPath)
        {
            var project = Path.Combine(Path.GetDirectoryName(glbPath), ProjectName);
            var models = Path.Combine(project, "Assets", "Models");
            var editor = Path.Combine(project, "Assets", "Editor");
            Directory.CreateDirectory(models);
            Directory.CreateDirectory(editor);
            Directory.CreateDirectory(Path.Combine(project, "Assets", "Scenes"));
            Directory.CreateDirectory(Path.Combine(project, "Packages"));
            Directory.CreateDirectory(Path.Combine(project, "ProjectSettings"));
            File.Copy(glbPath, Path.Combine(models, Path.GetFileName(glbPath)), true);
            WriteOnce(Path.Combine(editor, "ModelForgeSceneBuilder.cs"), UnitySceneTemplate.SceneBuilder);
            WriteOnce(Path.Combine(project, "Packages", "manifest.json"), UnitySceneTemplate.Manifest);
            WriteOnce(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt"),
                UnitySceneTemplate.ProjectVersion);
            return project;
        }

        // Preserves changes made later inside the generated Unity project.
        private static void WriteOnce(string path, string contents)
        {
            if (File.Exists(path)) { return; }
            File.WriteAllText(path, contents);
        }
    }
}
