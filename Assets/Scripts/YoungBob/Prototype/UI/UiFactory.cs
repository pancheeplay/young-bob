using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI
{
    internal static class UiFactory
    {
        private const float GlobalFontScale = 1.5f;
        private static Font _defaultFont;

        public static int ScaleFontSize(int fontSize)
        {
            return Mathf.Max(1, Mathf.RoundToInt(fontSize * GlobalFontScale));
        }

        public static void SetDefaultFont(Font font)
        {
            _defaultFont = font;
        }

        private static Font GetDefaultFont()
        {
            if (_defaultFont == null)
            {
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _defaultFont;
        }

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
            text.font = GetDefaultFont();
            text.fontSize = ScaleFontSize(fontSize);
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

        public static GameObject CreateCard(Transform parent, string name, Data.CardDefinition cardDef, bool isPlayable, int? energyCostOverride = null)
        {
            if (parent == null) return null;

            var cardRoot = new GameObject(name);
            cardRoot.transform.SetParent(parent, false);
            var rect = cardRoot.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 280f);

            // Glow for playable state (purely visual)
            var glow = CreatePanel(cardRoot.transform, "Glow", new Color(1f, 1f, 1f, 0.4f), Vector2.zero, Vector2.one, new Vector2(-8f, -8f), new Vector2(8f, 8f));
            glow.GetComponent<Image>().raycastTarget = false;
            glow.SetActive(isPlayable);

            // Border (purely visual)
            var border = CreatePanel(cardRoot.transform, "Border", new Color(0.1f, 0.1f, 0.1f, 1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            border.GetComponent<Image>().raycastTarget = false;
            
            // Content Bg - Color based on class. This is the ONLY raycast target for dragging.
            Color bgColor = GetClassColor(cardDef.classTag);
            var bg = CreatePanel(border.transform, "Background", bgColor, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var bgImage = bg.GetComponent<Image>();
            bgImage.raycastTarget = true;

            // Target Labels (Top right) - decorative, no raycasts
            var targetTag = CreateText(bg.transform, "TargetTag", 12, TextAnchor.UpperRight, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(8f, -25f), new Vector2(-8f, -5f));
            targetTag.text = CardEffectTextFormatter.BuildCardTargetLabel(cardDef);
            targetTag.color = new Color(0.92f, 0.97f, 1f, 0.98f);
            targetTag.fontStyle = FontStyle.Bold;
            targetTag.raycastTarget = false;

            var targetRangeTag = CreateText(bg.transform, "TargetRangeTag", 10, TextAnchor.UpperRight, new Vector2(0.4f, 1f), new Vector2(1f, 1f), new Vector2(8f, -45f), new Vector2(-8f, -24f));
            targetRangeTag.text = CardEffectTextFormatter.BuildCardTargetRangeLabel(cardDef);
            targetRangeTag.color = new Color(0.88f, 0.9f, 0.95f, 0.78f);
            targetRangeTag.fontStyle = FontStyle.Italic;
            targetRangeTag.raycastTarget = false;
            targetRangeTag.gameObject.SetActive(!string.IsNullOrWhiteSpace(targetRangeTag.text));

            // Header/Title area - decorative
            var header = CreatePanel(bg.transform, "Header", new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, 0.72f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            header.GetComponent<Image>().raycastTarget = false;
            var titleText = CreateText(header.transform, "Title", 18, TextAnchor.MiddleCenter);
            titleText.text = cardDef.name;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.raycastTarget = false;

            // Icon area - decorative
            var iconRoot = CreatePanel(bg.transform, "IconRoot", new Color(0f, 0f, 0f, 0.2f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(-40f, -40f), new Vector2(40f, 40f));
            iconRoot.GetComponent<Image>().raycastTarget = false;

            // Description area - decorative
            var descArea = CreatePanel(bg.transform, "DescArea", new Color(0f, 0f, 0f, 0.4f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.6f), Vector2.zero, Vector2.zero);
            descArea.GetComponent<Image>().raycastTarget = false;
            var descText = CreateText(descArea.transform, "Description", 16, TextAnchor.MiddleCenter);
            descText.text = BuildEffectSummary(cardDef);
            descText.color = new Color(0.95f, 0.95f, 0.95f);
            descText.raycastTarget = false;

            // Energy Cost - decorative
            var costPanel = CreatePanel(bg.transform, "Cost", new Color(0.15f, 0.4f, 0.8f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-10f, -40f), new Vector2(30f, 0f));
            costPanel.GetComponent<Image>().raycastTarget = false;
            var effectiveCost = energyCostOverride ?? cardDef.energyCost;
            var costText = CreateText(costPanel.transform, "CostText", 22, TextAnchor.MiddleCenter);
            costText.text = effectiveCost.ToString();
            costText.fontStyle = FontStyle.Bold;
            costText.raycastTarget = false;

            if (effectiveCost != cardDef.energyCost)
            {
                var delta = effectiveCost - cardDef.energyCost;
                var deltaText = CreateText(costPanel.transform, "CostDelta", 11, TextAnchor.LowerCenter, Vector2.zero, Vector2.one, new Vector2(0f, -2f), new Vector2(0f, 2f));
                deltaText.text = delta > 0 ? $"+{delta}" : delta.ToString();
                deltaText.color = delta > 0 ? new Color(1f, 0.72f, 0.72f) : new Color(0.72f, 1f, 0.72f);
                deltaText.raycastTarget = false;

                costPanel.GetComponent<Image>().color = delta > 0
                    ? new Color(0.62f, 0.2f, 0.2f)
                    : new Color(0.17f, 0.52f, 0.22f);
            }

            return cardRoot;
        }

        private static Color GetClassColor(string classTag)
        {
            if (string.IsNullOrEmpty(classTag)) return new Color(0.25f, 0.25f, 0.28f);
            switch (classTag.ToLower())
            {
                case "warrior": return new Color(0.62f, 0.18f, 0.18f);
                case "assassin": return new Color(0.45f, 0.24f, 0.62f);
                case "rogue": return new Color(0.45f, 0.24f, 0.62f);
                case "mage": return new Color(0.15f, 0.25f, 0.5f); // Blueish
                case "priest": return new Color(0.72f, 0.64f, 0.22f);
                case "utility": return new Color(0.25f, 0.25f, 0.28f);
                default: return new Color(0.25f, 0.25f, 0.28f);
            }
        }

        private static string BuildEffectSummary(Data.CardDefinition cardDef)
        {
            return CardEffectTextFormatter.BuildEffectSummary(cardDef);
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
