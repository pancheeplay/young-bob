using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI
{
    internal static class UiFactory
    {
        public static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var image = panel.AddComponent<Image>();
            image.color = color;
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return panel;
        }

        public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
        {
            return CreateText(parent, name, fontSize, alignment, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public static Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.93f, 0.95f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.78f, 0.44f, 0.21f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 44f;
            layoutElement.preferredHeight = 44f;

            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 44f);

            var text = CreateText(button.transform, "Label", 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        public static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateButton(parent, name, label, onClick);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            return button;
        }

        public static GameObject CreateCard(Transform parent, string name, string title, string description, bool isPlayable)
        {
            if (parent == null) return null;

            var cardRoot = new GameObject(name);
            cardRoot.transform.SetParent(parent, false);
            var rect = cardRoot.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 280f);

            // Glow for playable state
            var glow = CreatePanel(cardRoot.transform, "Glow", new Color(0.1f, 0.8f, 0.1f, 0.5f), Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
            glow.SetActive(isPlayable);

            // Border
            var border = CreatePanel(cardRoot.transform, "Border", new Color(0.5f, 0.44f, 0.35f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            
            // Content Bg
            var bg = CreatePanel(border.transform, "Background", new Color(0.15f, 0.17f, 0.2f), Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var bgImage = bg.GetComponent<Image>();
            if (bgImage != null) bgImage.raycastTarget = true; // For dragging

            // Header/Title area
            var header = CreatePanel(bg.transform, "Header", new Color(0.24f, 0.22f, 0.18f), new Vector2(0f, 0.7f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var titleText = CreateText(header.transform, "Title", 22, TextAnchor.MiddleCenter);
            titleText.text = title;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.95f, 0.85f, 0.6f);

            // Cost/Icon placeholder
            var icon = CreatePanel(bg.transform, "Icon", new Color(0.4f, 0.34f, 0.25f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), Vector2.zero, Vector2.zero);
            icon.GetComponent<RectTransform>().sizeDelta = new Vector2(60f, 60f);

            // Description area
            var descArea = CreatePanel(bg.transform, "DescArea", new Color(0.1f, 0.11f, 0.13f, 0.8f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.45f), Vector2.zero, Vector2.zero);
            var descText = CreateText(descArea.transform, "Description", 17, TextAnchor.MiddleCenter);
            descText.text = description;
            descText.color = new Color(0.85f, 0.85f, 0.85f);

            return cardRoot;
        }

        public static (GameObject root, RectTransform fillRect) CreateProgressBar(Transform parent, string name, Color fillColor, Vector2 size)
        {
            var root = CreatePanel(parent, name, new Color(0.1f, 0.1f, 0.1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -size / 2f, size / 2f);
            var fillObj = CreatePanel(root.transform, "Fill", fillColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillRect = fillObj.GetComponent<RectTransform>();
            // Anchor at left, stretch vertically, max starts at right
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.pivot = new Vector2(0f, 0.5f);
            return (root, fillRect);
        }
    }
}



