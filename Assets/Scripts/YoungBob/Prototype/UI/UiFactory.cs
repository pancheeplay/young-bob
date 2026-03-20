using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI
{
    internal static class UiFactory
    {
        private static Font _defaultFont;

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

            // Height/Distance Labels (Top corners) - decorative, no raycasts
            var heightTag = CreateText(bg.transform, "HeightTag", 14, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(8f, -25f), new Vector2(0f, -5f));
            heightTag.text = cardDef.rangeHeights ?? "";
            heightTag.color = new Color(0.9f, 0.9f, 1f);
            heightTag.fontStyle = FontStyle.Bold;
            heightTag.raycastTarget = false;

            var distTag = CreateText(bg.transform, "DistTag", 14, TextAnchor.UpperRight, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(-8f, -5f));
            distTag.text = cardDef.rangeDistance ?? "";
            distTag.color = new Color(1f, 0.9f, 0.7f);
            distTag.fontStyle = FontStyle.Bold;
            distTag.raycastTarget = false;

            // Header/Title area - decorative
            var header = CreatePanel(bg.transform, "Header", new Color(0f, 0f, 0f, 0.3f), new Vector2(0f, 0.72f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            header.GetComponent<Image>().raycastTarget = false;
            var titleText = CreateText(header.transform, "Title", 22, TextAnchor.MiddleCenter);
            titleText.text = cardDef.name;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = Color.white;
            titleText.raycastTarget = false;

            // Icon area - decorative
            var iconRoot = CreatePanel(bg.transform, "IconRoot", new Color(0f, 0f, 0f, 0.2f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(-40f, -40f), new Vector2(40f, 40f));
            iconRoot.GetComponent<Image>().raycastTarget = false;

            // Description area - decorative
            var descArea = CreatePanel(bg.transform, "DescArea", new Color(0f, 0f, 0f, 0.4f), new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.42f), Vector2.zero, Vector2.zero);
            descArea.GetComponent<Image>().raycastTarget = false;
            var descText = CreateText(descArea.transform, "Description", 17, TextAnchor.MiddleCenter);
            descText.text = BuildEffectSummary(cardDef);
            descText.color = new Color(0.95f, 0.95f, 0.95f);
            descText.raycastTarget = false;

            // Energy Cost - decorative
            var costPanel = CreatePanel(bg.transform, "Cost", new Color(0.15f, 0.4f, 0.8f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-10f, -40f), new Vector2(30f, 0f));
            costPanel.GetComponent<Image>().raycastTarget = false;
            var effectiveCost = energyCostOverride ?? cardDef.energyCost;
            var costText = CreateText(costPanel.transform, "CostText", 24, TextAnchor.MiddleCenter);
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
            if (cardDef == null || cardDef.effects == null || cardDef.effects.Length == 0)
            {
                return "无效果";
            }

            var lines = new System.Collections.Generic.List<string>(cardDef.effects.Length);
            for (var i = 0; i < cardDef.effects.Length; i++)
            {
                var effect = cardDef.effects[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.op))
                {
                    continue;
                }

                lines.Add(BuildEffectLine(effect));
            }

            if (lines.Count == 0)
            {
                return "无效果";
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string BuildEffectLine(Data.CardEffectDefinition effect)
        {
            var target = BuildTargetText(effect.target);
            switch (effect.op)
            {
                case "Damage":
                    return "造成" + BuildAmountText(effect.amount, effect.scaleBy, effect.ratio) + "伤害" + target;
                case "Heal":
                    return "恢复" + BuildAmountText(effect.amount, effect.scaleBy, effect.ratio) + "生命" + target;
                case "Draw":
                    return "抽" + Mathf.Max(0, effect.amount) + "张牌" + target;
                case "GainArmor":
                    return "获得" + BuildAmountText(effect.amount, effect.scaleBy, effect.ratio) + "护甲" + target;
                case "ApplyStatus":
                    return "施加" + (string.IsNullOrWhiteSpace(effect.statusId) ? "状态" : effect.statusId) + " " + Mathf.Max(0, effect.amount) + target;
                case "ApplyVulnerable":
                    return "施加易伤 " + Mathf.Max(0, effect.amount) + target;
                case "DamageByArmor":
                    return "造成护甲x" + (effect.ratio <= 0f ? "1" : effect.ratio.ToString("0.##")) + "伤害" + target;
                case "ModifyEnergy":
                    return (effect.amount >= 0 ? "获得" : "失去") + Mathf.Abs(effect.amount) + "点能量";
                case "LoseHp":
                    return "失去" + Mathf.Max(0, effect.amount) + "生命" + target;
                case "RecycleDiscardToHand":
                    return "从弃牌堆回收" + Mathf.Max(0, effect.amount) + "张到手牌"
                        + (effect.amount2 == 0 ? "" : " (费用修正 " + (effect.amount2 > 0 ? "+" : "") + effect.amount2 + ")");
                case "ExhaustFromHand":
                    return "消耗手牌中" + Mathf.Max(0, effect.amount) + "张";
                case "MoveArea":
                    return "移动到目标区域";
                case "CopyAndPlunder":
                    return "复制并掠夺目标手牌";
                default:
                    var amountPart = effect.amount == 0 ? string.Empty : " " + effect.amount;
                    return effect.op + amountPart + target;
            }
        }

        private static string BuildTargetText(string rawTarget)
        {
            if (string.IsNullOrWhiteSpace(rawTarget) || string.Equals(rawTarget, "None", System.StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (string.Equals(rawTarget, "CardTarget", System.StringComparison.OrdinalIgnoreCase))
            {
                return "（目标）";
            }

            if (string.Equals(rawTarget, "Self", System.StringComparison.OrdinalIgnoreCase))
            {
                return "（自身）";
            }

            if (string.Equals(rawTarget, "AllEnemies", System.StringComparison.OrdinalIgnoreCase))
            {
                return "（全敌方）";
            }

            if (string.Equals(rawTarget, "AllAllies", System.StringComparison.OrdinalIgnoreCase))
            {
                return "（全友方）";
            }

            return "（" + rawTarget + "）";
        }

        private static string BuildAmountText(int baseAmount, string scaleBy, float ratio)
        {
            if (string.IsNullOrWhiteSpace(scaleBy))
            {
                return Mathf.Max(0, baseAmount).ToString();
            }

            var ratioText = Mathf.Approximately(ratio, 1f) || ratio <= 0f ? string.Empty : "x" + ratio.ToString("0.##");
            if (baseAmount > 0)
            {
                return baseAmount + "+" + scaleBy + ratioText;
            }

            return scaleBy + ratioText;
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
