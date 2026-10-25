using System.IO;
using BepInEx;
using HarmonyLib;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Owns the local ModelForge session and its keyboard shortcut.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string PluginGuid = "Landoria.ModelForge";
        private const string PluginName = "Landoria.ModelForge";
        private const string PluginVersion = "1.0.0";

        private static Plugin _instance;
        private Harmony _harmony;
        private ForgeSession _session;
        private ForgeWindow _window;
        internal static bool DialogOpen => _instance != null && _instance._window != null && _instance._window.IsOpen;

        // Creates the working directory and session.
        private void Awake()
        {
            _instance = this;
            Logger.LogInfo($"AssemblyVersion: {GetType().Assembly.GetName().Version}.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            var folder = Path.Combine(Paths.ConfigPath, "ModelForge", "Models");
            Directory.CreateDirectory(folder);
            _session = new ForgeSession(folder, Logger);
            _window = new ForgeWindow(_session, Logger);
            Preference.Initialize(Config);
            ConfigWatcher.Initialize(Config, Logger, "ModelForge", () => Preference.RestoreDefaults(Config));
            Logger.LogInfo($"{PluginName} {PluginVersion} is loaded. Models: {folder}");
        }

        // Opens the window and updates a pending placement.
        private void Update()
        {
            ConfigWatcher.Update();
            if (Player.m_localPlayer == null || Camera.main == null)
            {
                return;
            }
            if (Shortcut.IsDown(Preference.OpenShortcut))
            {
                _window.Toggle();
            }
            _window.UpdateCursor();
            if (!_window.IsOpen) { _session.UpdatePlacement(); }
        }

        // Removes temporary models when the plugin unloads.
        private void OnDestroy()
        {
            ConfigWatcher.Dispose();
            _window?.Dispose();
            _session?.Dispose();
            _window = null;
            _session = null;
            _harmony?.UnpatchSelf();
            _harmony = null;
            _instance = null;
            Logger.LogInfo($"{PluginName} {PluginVersion} is unloaded.");
        }
    }
}
