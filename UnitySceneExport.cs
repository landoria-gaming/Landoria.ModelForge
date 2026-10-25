using System;
using System.IO;

namespace Landoria.ModelForge
{
    // Copies each GLB into a small Unity project that builds preview scenes.
    internal static class UnitySceneExport
    {
        private const string ProjectName = "UnityProject";

        // Adds the model and the editor scene builder to the working folder.
        internal static string Write(string glbPath, string gameDataPath)
        {
            var project = Path.Combine(Path.GetDirectoryName(glbPath), ProjectName);
            var models = Path.Combine(project, "Assets", "Models");
            var editor = Path.Combine(project, "Assets", "Editor");
            Directory.CreateDirectory(models);
            Directory.CreateDirectory(editor);
            Directory.CreateDirectory(Path.Combine(project, "Assets", "Scenes"));
            Directory.CreateDirectory(Path.Combine(project, "Packages"));
            Directory.CreateDirectory(Path.Combine(project, "ProjectSettings"));
            var modelName = Path.GetFileNameWithoutExtension(glbPath);
            File.Copy(glbPath, Path.Combine(models, Path.GetFileName(glbPath)), true);
            var metadata = Path.ChangeExtension(glbPath, ".valheim.json");
            if (File.Exists(metadata))
            {
                File.Copy(metadata, Path.Combine(models, Path.GetFileName(metadata)), true);
            }
            var sourceTextures = Path.Combine(Path.GetDirectoryName(glbPath), modelName + ".textures");
            if (Directory.Exists(sourceTextures))
            {
                var targetTextures = Path.Combine(models, modelName + ".textures");
                Directory.CreateDirectory(targetTextures);
                foreach (var texture in Directory.GetFiles(sourceTextures, "*.png"))
                {
                    File.Copy(texture, Path.Combine(targetTextures, Path.GetFileName(texture)), true);
                }
            }
            File.WriteAllText(Path.Combine(models, modelName + ".build"), "Build scene");
            File.WriteAllText(Path.Combine(editor, "ModelForgeSceneBuilder.cs"),
                UnitySceneTemplate.SceneBuilder);
            File.WriteAllText(Path.Combine(project, "ProjectSettings", "ModelForgeGameData.txt"),
                gameDataPath);
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
