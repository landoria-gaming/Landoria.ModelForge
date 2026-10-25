using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.ModelForge
{
    // Hosts ModelForge controls inside Valheim's UnifiedPopup panel.
    internal sealed class NativePopupHost
    {
        private readonly ManualLogSource _log;
        private RectTransform _panel;
        private GameObject _content;
        private Vector2 _originalSize;
        internal bool IsOpen { get; private set; }

        // Stores the logger used for missing game UI references.
        internal NativePopupHost(ManualLogSource log)
        {
            _log = log;
        }

        // Pushes a native task popup and locates its panel and button art.
        internal bool Open(out RectTransform content, out Button sample)
        {
            content = null;
            sample = null;
            if (!UnifiedPopup.IsAvailable())
            {
                _log.LogWarning("Valheim's UnifiedPopup is not available yet.");
                return false;
            }
            UnifiedPopup.Push(new TaskPopup("ModelForge", "", false));
            var popup = UnityEngine.Object.FindFirstObjectByType<UnifiedPopup>();
            _panel = Find<RectTransform>(popup, "Popup");
            sample = Find<Button>(popup, "ButtonOk");
            if (_panel == null || sample == null)
            {
                _log.LogWarning("Valheim's popup layout has changed.");
                UnifiedPopup.Pop();
                return false;
            }
            _originalSize = _panel.sizeDelta;
            _panel.sizeDelta = new Vector2(610, 640);
            content = ForgeUiFactory.Rect(_panel, "ModelForge controls", Vector2.zero, new Vector2(610, 640));
            _content = content.gameObject;
            IsOpen = true;
            return true;
        }

        // Removes mod controls and pops only its native dialog.
        internal void Close()
        {
            if (!IsOpen) { return; }
            if (_content != null)
            {
                _content.SetActive(false);
                UnityEngine.Object.Destroy(_content);
            }
            if (_panel != null) { _panel.sizeDelta = _originalSize; }
            if (UnifiedPopup.IsAvailable()) { UnifiedPopup.Pop(); }
            _content = null;
            _panel = null;
            IsOpen = false;
        }

        // Finds one named component in the active native popup.
        private static T Find<T>(UnifiedPopup popup, string name) where T : Component
        {
            if (popup == null) { return null; }
            foreach (var item in popup.GetComponentsInChildren<T>(true))
            {
                if (item.name == name) { return item; }
            }
            return null;
        }
    }
}
