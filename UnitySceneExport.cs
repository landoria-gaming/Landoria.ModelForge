using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Landoria.ModelForge
{
    // Copies each GLB into a small Unity project that builds preview scenes.
    internal static class UnitySceneExport
    {
        private const string ProjectName = "UnityProject";

        // Adds the model and the editor scene builder to the working folder.
        internal static string Write(string glbPath, string gameDataPath,
            bool buildScene = true, string workingFolder = null)
        {
            var project = Path.Combine(workingFolder ?? Path.GetDirectoryName(glbPath), ProjectName);
            var models = Path.Combine(project, "Assets", "Models");
            var editor = Path.Combine(project, "Assets", "Editor");
            Directory.CreateDirectory(models);
            Directory.CreateDirectory(editor);
            Directory.CreateDirectory(Path.Combine(project, "Assets", "Scenes"));
            Directory.CreateDirectory(Path.Combine(project, "Packages"));
            Directory.CreateDirectory(Path.Combine(project, "ProjectSettings"));
            var modelName = Path.GetFileNameWithoutExtension(glbPath);
            var targetModel = Path.Combine(models, Path.GetFileName(glbPath));
            if (!SamePath(glbPath, targetModel)) { File.Copy(glbPath, targetModel, true); }
            var metadata = Path.ChangeExtension(glbPath, ".valheim.json");
            if (File.Exists(metadata))
            {
                var targetMetadata = Path.Combine(models, Path.GetFileName(metadata));
                if (!SamePath(metadata, targetMetadata))
                { File.Copy(metadata, targetMetadata, true); }
            }
            var sourceTextures = Path.Combine(Path.GetDirectoryName(glbPath), modelName + ".textures");
            if (Directory.Exists(sourceTextures))
            {
                var targetTextures = Path.Combine(models, modelName + ".textures");
                Directory.CreateDirectory(targetTextures);
                foreach (var texture in Directory.GetFiles(sourceTextures, "*.png"))
                {
                    var targetTexture = Path.Combine(targetTextures, Path.GetFileName(texture));
                    if (!SamePath(texture, targetTexture))
                    { File.Copy(texture, targetTexture, true); }
                }
            }
            if (buildScene)
            {
                File.WriteAllText(Path.Combine(models, modelName + ".build"), "Build scene");
            }
            File.WriteAllText(Path.Combine(editor, "ModelForgeSceneBuilder.cs"),
                UnitySceneTemplate.SceneBuilder);
            File.WriteAllText(Path.Combine(project, "Assets", "ModelForgePreviewController.cs"),
                UnityPreviewTemplate.Controller);
            File.WriteAllText(Path.Combine(project, "Assets", "ModelForgeWindPreview.cs"),
                UnityPreviewTemplate.Wind);
            File.WriteAllText(Path.Combine(project, "Assets", "ModelForgeAmbientActor.cs"),
                UnityPreviewTemplate.Ambient);
            File.WriteAllText(Path.Combine(project, "Assets", "ModelForgeRuntimeAnimator.cs"),
                UnityPreviewTemplate.Animation);
            File.WriteAllText(Path.Combine(project, "ProjectSettings", "ModelForgeGameData.txt"),
                gameDataPath);
            WritePostProcessing(project, gameDataPath);
            WriteOnce(Path.Combine(project, "Packages", "manifest.json"), UnitySceneTemplate.Manifest);
            EnsureUnityModules(Path.Combine(project, "Packages", "manifest.json"));
            WriteOnce(Path.Combine(project, "ProjectSettings", "ProjectVersion.txt"),
                UnitySceneTemplate.ProjectVersion);
            return project;
        }

        private static void EnsureUnityModules(string path)
        {
            var manifest = JObject.Parse(File.ReadAllText(path));
            var dependencies = (JObject)manifest["dependencies"];
            foreach (var module in new[] { "animation", "audio", "physics", "assetbundle" })
            {
                var name = "com.unity.modules." + module;
                if (dependencies[name] == null) { dependencies[name] = "1.0.0"; }
            }
            File.WriteAllText(path, manifest.ToString());
        }

        private static bool SamePath(string left, string right)
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        // Supplies Valheim's post-processing runtime and the matching open-source shaders.
        private static void WritePostProcessing(string project, string gameDataPath)
        {
            var plugins = Path.Combine(project, "Assets", "Plugins");
            Directory.CreateDirectory(plugins);
            var runtime = Path.Combine(gameDataPath, "Managed", "assembly_postprocessing.dll");
            File.Copy(runtime, Path.Combine(plugins, "assembly_postprocessing.dll"), true);
            UnityPostProcessingSource.CopyToProject(project);
        }

        // Preserves changes made later inside the generated Unity project.
        private static void WriteOnce(string path, string contents)
        {
            if (File.Exists(path)) { return; }
            File.WriteAllText(path, contents);
        }
    }
}
