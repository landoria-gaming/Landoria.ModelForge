using HarmonyLib;
using UnityEngine;

namespace Landoria.ModelForge
{
    // Keeps the ModelForge pointer visible after Valheim updates camera capture.
    [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
    internal static class CursorPatch
    {
        // Releases the pointer only while the local dialog is open.
        [HarmonyPostfix]
        private static void KeepDialogCursorVisible()
        {
            if (!Plugin.DialogOpen) { return; }
            ZCursor.LockState = CursorLockMode.None;
            ZCursor.Show();
        }
    }
}
