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
        private readonly TMP_Text _sampleText;

        // Finds styles on Valheim's native popup button.
        internal ForgeUiFactory(Button sample)
        {
            _sample = sample;
            _sampleText = sample.GetComponentInChildren<TMP_Text>(true);
        }

        // Adds a native-looking button with its own action callback.
        internal Button Button(Transform parent, string title, float y, Action callback,
            float x = 0, float width = 520)
        {
            var copy = UnityEngine.Object.Instantiate(_sample.gameObject, parent, false);
            copy.name = title;
            var rect = copy.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, 42);
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => callback());
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            var label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.text = title; }
            copy.SetActive(true);
            return button;
        }
        // Adds text using Valheim's current TextMeshPro font.
        internal TMP_Text Label(Transform parent, string value, float x, float y,
            float width, float height, float size, TextAlignmentOptions alignment)
        {
            var rect = Rect(parent, "Text", new Vector2(x, y), new Vector2(width, height));
            rect.gameObject.SetActive(false);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            var fontSource = _sampleText;
            if (fontSource == null || fontSource.font == null)
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (candidate.font == null) { continue; }
                    fontSource = candidate;
                    break;
                }
            }
            if (fontSource != null && fontSource.font != null)
            {
                text.font = fontSource.font;
                text.fontSharedMaterial = fontSource.fontSharedMaterial;
                text.color = fontSource.color;
            }
            else
            {
                text.color = new Color(.94f, .85f, .69f);
            }
            text.fontSize = size;
            text.alignment = alignment;
            text.text = value;
            text.raycastTarget = false;
            rect.gameObject.SetActive(true);
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

    }
}
