using System;
using System.Diagnostics;
using System.IO;

namespace Landoria.ModelForge
{
    // Builds the editable Unity scene after the mod has written every asset.
    internal static class UnityBatchBuild
    {
        private static Process _running;
        private static string _scene;
        private static string _marker;

        internal static string Status
        {
            get
            {
                if (_running == null) { return null; }
                if (!_running.HasExited) { return "Unity scene build in progress..."; }
                if (_running.ExitCode == 0 && File.Exists(_scene) && !File.Exists(_marker))
                {
                    return "Unity scene ready: " + Path.GetFileName(_scene);
                }
                return "Unity scene build failed. See UnityProject/ModelForgeBuild.log.";
            }
        }

        internal static string Start(string project, string sceneName)
        {
            _scene = Path.Combine(project, "Assets", "Scenes", sceneName + ".unity");
            _marker = Path.Combine(project, "Assets", "Models", sceneName + ".build");
            var editor = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor",
                "6000.0.75f1", "Editor", "Unity.exe");
            if (!File.Exists(editor))
            {
                return "Open UnityProject in Unity 6000.0.75f1 to build the scene.";
            }
            if (_running != null && !_running.HasExited)
            {
                return "Unity scene build already running.";
            }
            var log = Path.Combine(project, "ModelForgeBuild.log");
            var start = new ProcessStartInfo(editor)
            {
                Arguments = "-batchmode -nographics -quit -projectPath \"" + project +
                    "\" -executeMethod ModelForgeSceneBuilder.BuildMissingScenes -logFile \"" + log + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            _running = Process.Start(start);
            if (_running == null) { return "Could not start the Unity scene build."; }
            return "Unity scene build started in the background.";
        }
    }
}
