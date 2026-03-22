using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleHandPanelView
    {
        public RectTransform HandPanelRect { get; private set; }
        public Transform HandContainer { get; private set; }
        public Text EnergyLabel { get; private set; }
        public Button ChatButton { get; private set; }
        public Text ChatButtonLabel { get; private set; }
        public Button DrawPileButton { get; private set; }
        public Button DiscardPileButton { get; private set; }
        public Button ExhaustPileButton { get; private set; }
        public Button EndTurnButton { get; private set; }
        public RectTransform EndTurnReadyRoot { get; private set; }
        public Text EffectTargetHintText { get; private set; }
        public RectTransform QuickChatAnchor { get; private set; }

        public BattleHandPanelView(Transform parent, UnityEngine.Events.UnityAction toggleQuickChat, UnityEngine.Events.UnityAction endTurn)
        {
            var root = UiFactory.CreatePanel(parent, "BattleHandPanel", new Color(0.05f, 0.07f, 0.1f, 0.92f), new Vector2(0f, 0f), new Vector2(1f, 0.25f), new Vector2(10f, 8f), new Vector2(-10f, -8f));
            HandPanelRect = root.GetComponent<RectTransform>();
            root.GetComponent<Image>().type = Image.Type.Sliced;

            var topGlow = UiFactory.CreatePanel(root.transform, "TopGlow", new Color(0.31f, 0.63f, 0.75f, 0.06f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            topGlow.transform.SetAsFirstSibling();
            topGlow.GetComponent<Image>().raycastTarget = false;

            var controlBar = UiFactory.CreatePanel(root.transform, "ControlBar", new Color(0.08f, 0.11f, 0.15f, 0.96f), new Vector2(0f, 0.7f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -6f));
            controlBar.GetComponent<Image>().type = Image.Type.Sliced;

            EnergyLabel = UiFactory.CreateText(controlBar.transform, "EnergyLabel", 30, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.32f, 1f), new Vector2(16f, 0f), new Vector2(0f, 0f));
            EnergyLabel.fontStyle = FontStyle.Bold;
            EnergyLabel.color = new Color(0.82f, 0.91f, 0.98f, 0.98f);

            var actionRow = UiFactory.CreatePanel(controlBar.transform, "ActionRow", Color.clear, new Vector2(0.32f, 0f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(0f, 0f));
            var actionLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
            actionLayout.childAlignment = TextAnchor.MiddleRight;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = false;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;
            actionLayout.spacing = 8f;
            actionLayout.padding = new RectOffset(0, 0, 10, 10);

            ChatButton = UiFactory.CreateButton(actionRow.transform, "QuickChatButton", "聊天", toggleQuickChat);
            ChatButton.image.color = new Color(0.13f, 0.23f, 0.31f, 0.96f);
            ChatButtonLabel = ChatButton.GetComponentInChildren<Text>();
            ChatButtonLabel.color = new Color(0.86f, 0.92f, 0.97f, 0.96f);
            ChatButton.GetComponent<RectTransform>().sizeDelta = new Vector2(112f, 46f);

            DrawPileButton = CreateUtilityButton(actionRow.transform, "DrawPile", "牌库 0", new Color(0.1f, 0.16f, 0.22f, 0.96f));
            DiscardPileButton = CreateUtilityButton(actionRow.transform, "DiscardPile", "弃牌 0", new Color(0.1f, 0.16f, 0.22f, 0.96f));
            ExhaustPileButton = CreateUtilityButton(actionRow.transform, "ExhaustPile", "消耗 0", new Color(0.1f, 0.16f, 0.22f, 0.96f));

            EndTurnButton = UiFactory.CreateButton(actionRow.transform, "EndTurn", "结束回合", endTurn);
            EndTurnButton.image.color = new Color(0.12f, 0.43f, 0.26f, 0.98f);
            var endTurnRect = EndTurnButton.GetComponent<RectTransform>();
            endTurnRect.sizeDelta = new Vector2(196f, 52f);
            var endTurnText = EndTurnButton.GetComponentInChildren<Text>();
            endTurnText.fontSize = UiFactory.ScaleFontSize(24);
            endTurnText.color = new Color(0.96f, 0.98f, 0.97f, 0.98f);

            var readyRootObj = new GameObject("ReadyMarkers");
            readyRootObj.transform.SetParent(EndTurnButton.transform, false);
            EndTurnReadyRoot = readyRootObj.AddComponent<RectTransform>();
            EndTurnReadyRoot.anchorMin = new Vector2(1f, 0f);
            EndTurnReadyRoot.anchorMax = new Vector2(1f, 0f);
            EndTurnReadyRoot.pivot = new Vector2(1f, 0f);
            EndTurnReadyRoot.anchoredPosition = new Vector2(-8f, 8f);
            EndTurnReadyRoot.sizeDelta = new Vector2(110f, 24f);

            var handShelf = UiFactory.CreatePanel(root.transform, "HandShelf", new Color(0.07f, 0.1f, 0.13f, 0.62f), new Vector2(0f, 0f), new Vector2(1f, 0.7f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            handShelf.GetComponent<Image>().raycastTarget = false;
            handShelf.transform.SetAsFirstSibling();

            var handViewport = new GameObject("HandViewport");
            handViewport.transform.SetParent(root.transform, false);
            var viewportRect = handViewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 0.7f);
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            handViewport.AddComponent<RectMask2D>();

            var handContent = UiFactory.CreatePanel(handViewport.transform, "HandContent", Color.clear, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var handContentRect = handContent.GetComponent<RectTransform>();
            handContentRect.pivot = new Vector2(0.5f, 0.5f);
            var handLayout = handContent.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = -18f;
            handLayout.padding = new RectOffset(26, 26, 10, 4);
            handLayout.childAlignment = TextAnchor.LowerCenter;
            handLayout.childControlWidth = false;
            handLayout.childControlHeight = false;
            handLayout.childForceExpandWidth = false;
            handLayout.childForceExpandHeight = false;
            HandContainer = handContent.transform;

            EffectTargetHintText = UiFactory.CreateText(parent, "EffectTargetHint", 16, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.286f), new Vector2(0.82f, 0.312f), Vector2.zero, Vector2.zero);
            EffectTargetHintText.color = new Color(0.84f, 0.9f, 0.96f, 0.94f);
            EffectTargetHintText.text = string.Empty;
            EffectTargetHintText.raycastTarget = false;

            var quickChatAnchorObj = new GameObject("QuickChatAnchor");
            quickChatAnchorObj.transform.SetParent(controlBar.transform, false);
            QuickChatAnchor = quickChatAnchorObj.AddComponent<RectTransform>();
            QuickChatAnchor.anchorMin = new Vector2(0.5f, 1f);
            QuickChatAnchor.anchorMax = new Vector2(0.5f, 1f);
            QuickChatAnchor.pivot = new Vector2(0.5f, 0f);
            QuickChatAnchor.anchoredPosition = new Vector2(0f, 6f);
            QuickChatAnchor.sizeDelta = new Vector2(460f, 188f);
        }

        private static Button CreateUtilityButton(Transform parent, string name, string label, Color color)
        {
            var button = UiFactory.CreateButton(parent, name, label, null);
            button.image.color = color;
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(118f, 46f);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = UiFactory.ScaleFontSize(15);
            text.color = new Color(0.82f, 0.89f, 0.94f, 0.94f);
            return button;
        }
    }
}
