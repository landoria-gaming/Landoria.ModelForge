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
        private readonly RectTransform _root;
        private readonly RectTransform _viewport;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private string[] _files = Array.Empty<string>();

        // Creates a masked list below the model selector.
        internal ForgeDropdown(ForgeUiFactory ui, RectTransform panel, float top, Action<int> selected)
        {
            _ui = ui;
            _selected = selected;
            _root = ForgeUiFactory.Rect(panel, "Model choices", new Vector2(0, top), new Vector2(520, 145));
            _root.gameObject.AddComponent<Image>().color = new Color(.08f, .07f, .06f, .95f);
            _viewport = ForgeUiFactory.Rect(_root, "Viewport", new Vector2(-10, 0), new Vector2(490, 145));
            _viewport.gameObject.AddComponent<RectMask2D>();
            _scroll = _root.gameObject.AddComponent<ScrollRect>();
            _content = ForgeUiFactory.Rect(_viewport, "Items", Vector2.zero, new Vector2(480, 0));
            _scroll.content = _content;
            _scroll.viewport = _viewport;
            _scroll.horizontal = false;
            _scroll.scrollSensitivity = 42;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            AddScrollbar();
            Hide();
        }

        // Adds a visible native-styled scrollbar beside the clipped list.
        private void AddScrollbar()
        {
            var track = ForgeUiFactory.Rect(_root, "Scrollbar", new Vector2(251, -5), new Vector2(16, 135));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, .55f);
            var handle = ForgeUiFactory.Rect(track, "Handle", Vector2.zero, Vector2.zero);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            handle.offsetMin = Vector2.zero;
            handle.offsetMax = Vector2.zero;
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(1f, .65f, .2f, .9f);
            StyleScrollbar(trackImage, handleImage);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            _scroll.verticalScrollbar = bar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // Copies the same scrollbar artwork used by Valheim's inventory.
        private static void StyleScrollbar(Image track, Image handle)
        {
            var source = InventoryGui.instance?.m_playerGrid?.m_scrollbar;
            if (source == null) { return; }
            CopyImage(source.GetComponent<Image>(), track);
            CopyImage(source.handleRect?.GetComponent<Image>(), handle);
        }

        // Transfers native sprite and color when the source image exists.
        private static void CopyImage(Image source, Image target)
        {
            if (source == null) { return; }
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
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
            _content.sizeDelta = new Vector2(480, files.Length * 42);
            for (var i = 0; i < files.Length; i++)
            {
                var index = i;
                var button = _ui.Button(_content, Path.GetFileName(files[i]), -i * 42, () => _selected(index));
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(470, 40);
            }
            _scroll.verticalNormalizedPosition = 1f;
        }

        // Opens or closes the model list.
        internal void Toggle()
        {
            _root.gameObject.SetActive(!_root.gameObject.activeSelf && _files.Length > 0);
        }

        // Closes the model list.
        internal void Hide()
        {
            _root.gameObject.SetActive(false);
        }
    }
}
