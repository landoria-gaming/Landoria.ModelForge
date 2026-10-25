using BepInEx.Configuration;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Checks the configured shortcut using Valheim's input state.
    internal static class Shortcut
    {
        // Returns true when the main key and every modifier are pressed.
        internal static bool IsDown(KeyboardShortcut shortcut)
        {
            if (shortcut.MainKey == KeyCode.None || !ZInput.GetKeyDown(shortcut.MainKey))
            {
                return false;
            }
            foreach (var modifier in shortcut.Modifiers)
            {
                if (!ZInput.GetKey(modifier))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
