using BepInEx.Configuration;
using Landoria.Shared;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Stores the configurable shortcut for the local tool window.
    internal static class Preference
    {
        private static ConfigEntry<KeyboardShortcut> _openShortcut;

        internal static KeyboardShortcut OpenShortcut => _openShortcut.Value;

        // Creates the BepInEx shortcut setting.
        internal static void Initialize(ConfigFile config)
        {
            _openShortcut = config.Bind(
                "Controls", "OpenShortcut",
                new KeyboardShortcut(KeyCode.F, KeyCode.LeftShift),
                "Shortcut used to open or close ModelForge. Example: F + LeftShift.");
        }

        // Recreates the default shortcut when the config file is deleted.
        internal static void RestoreDefaults(ConfigFile config)
        {
            var saveOnConfigSet = config.SaveOnConfigSet;
            config.SaveOnConfigSet = false;
            try
            {
                _openShortcut.Value = new KeyboardShortcut(KeyCode.F, KeyCode.LeftShift);
            }
            finally
            {
                config.SaveOnConfigSet = saveOnConfigSet;
            }
            config.Save();
            ConfigWatcher.IgnoreCurrentFileVersion();
        }
    }
}
