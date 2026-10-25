using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landoria.ModelForge
{
    // Creates Unity UI controls using Valheim's button art and text font.
    internal sealed class ForgeUiFactory
    {
        private readonly Button _sample;
        private readonly Image _sampleImage;
        private readonly TMP_Text _sampleText;
        private readonly Image _panelImage;

        // Finds styles on Valheim's existing menu controls.
        internal ForgeUiFactory(Button sample, Transform menuDialog)
        {
            _sample = sample;
            _sampleImage = sample.GetComponent<Image>();
            _sampleText = sample.GetComponentInChildren<TMP_Text>(true);
            _panelImage = menuDialog != null ? menuDialog.GetComponent<Image>() : null;
        }

        // Creates a centered modal canvas above the game UI.
        internal GameObject CreateCanvas(out RectTransform panel)
        {
            var root = new GameObject("ModelForge UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var cover = Rect(root.transform, "Background", Vector2.zero, Vector2.zero);
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = Vector2.one;
            cover.offsetMin = Vector2.zero;
            cover.offsetMax = Vector2.zero;
            cover.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .68f);
            panel = Rect(root.transform, "Panel", Vector2.zero, new Vector2(610, 640));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(.5f, .5f);
            var image = panel.gameObject.AddComponent<Image>();
            StylePanel(image);
            return root;
        }

        // Adds a native-looking button with its own action callback.
        internal Button Button(Transform parent, string title, float y, Action callback)
        {
            var rect = Rect(parent, title, new Vector2(0, y), new Vector2(520, 42));
            var image = rect.gameObject.AddComponent<Image>();
            if (_sampleImage != null)
            {
                image.sprite = _sampleImage.sprite;
                image.type = _sampleImage.type;
                image.color = _sampleImage.color;
            }
            else
            {
                image.color = new Color(.31f, .24f, .17f, 1f);
            }
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = _sample.transition;
            button.colors = _sample.colors;
            button.spriteState = _sample.spriteState;
            button.onClick.AddListener(() => callback());
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            Label(rect, title, 0, -3, 500, 38, 19, TextAlignmentOptions.Center);
            return button;
        }

        // Adds text using Valheim's current TextMeshPro font.
        internal TMP_Text Label(Transform parent, string value, float x, float y,
            float width, float height, float size, TextAlignmentOptions alignment)
        {
            var rect = Rect(parent, "Text", new Vector2(x, y), new Vector2(width, height));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_sampleText != null)
            {
                text.font = _sampleText.font;
                text.fontSharedMaterial = _sampleText.fontSharedMaterial;
                text.color = _sampleText.color;
            }
            else
            {
                text.color = new Color(.94f, .85f, .69f);
            }
            text.fontSize = size;
            text.alignment = alignment;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        // Creates a top-anchored UI rectangle.
        internal static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var item = new GameObject(name, typeof(RectTransform));
            var rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // Applies the game's panel sprite when available.
        private void StylePanel(Image image)
        {
            if (_panelImage != null && _panelImage.sprite != null)
            {
                image.sprite = _panelImage.sprite;
                image.type = _panelImage.type;
                image.color = _panelImage.color;
            }
            else
            {
                image.color = new Color(.15f, .13f, .1f, .98f);
            }
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.58f, .42f, .23f, .9f);
            outline.effectDistance = new Vector2(3, -3);
        }
    }
}
