using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.ModelForge
{
    // Shows GLB choices in a scrollable Unity UI list.
    internal sealed class ForgeDropdown
    {
        private readonly ForgeUiFactory _ui;
        private readonly Action<int> _selected;
        private readonly RectTransform _viewport;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private string[] _files = Array.Empty<string>();

        // Creates a masked list below the model selector.
        internal ForgeDropdown(ForgeUiFactory ui, RectTransform panel, float top, Action<int> selected)
        {
            _ui = ui;
            _selected = selected;
            _viewport = ForgeUiFactory.Rect(panel, "Model choices", new Vector2(0, top), new Vector2(520, 145));
            _viewport.gameObject.AddComponent<Image>().color = new Color(.08f, .07f, .06f, .95f);
            _viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _content = ForgeUiFactory.Rect(_viewport, "Items", Vector2.zero, new Vector2(500, 0));
            _content.anchorMin = new Vector2(.5f, 1f);
            _content.anchorMax = new Vector2(.5f, 1f);
            _scroll.content = _content;
            _scroll.viewport = _viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.scrollSensitivity = 25;
            Hide();
        }

        // Rebuilds the list when files in the working folder change.
        internal void SetFiles(string[] files)
        {
            _files = files;
            for (var i = _content.childCount - 1; i >= 0; i--)
            {
                var oldChoice = _content.GetChild(i).gameObject;
                oldChoice.SetActive(false);
                UnityEngine.Object.Destroy(oldChoice);
            }
            _content.sizeDelta = new Vector2(500, files.Length * 42);
            for (var i = 0; i < files.Length; i++)
            {
                var index = i;
                var button = _ui.Button(_content, Path.GetFileName(files[i]), -i * 42, () => _selected(index));
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(490, 40);
            }
            _scroll.verticalNormalizedPosition = 1f;
        }

        // Opens or closes the model list.
        internal void Toggle()
        {
            _viewport.gameObject.SetActive(!_viewport.gameObject.activeSelf && _files.Length > 0);
        }

        // Closes the model list.
        internal void Hide()
        {
            _viewport.gameObject.SetActive(false);
        }
    }
}
