using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleTopBarView
    {
        public Text PlayerMarkerText { get; private set; }
        public Text PlayerNameText { get; private set; }
        public Text PlayerHpText { get; private set; }
        public Text SummaryText { get; private set; }
        public Button StatusModeButton { get; private set; }
        public Button ExitBattleButton { get; private set; }

        public BattleTopBarView(Transform parent, UnityEngine.Events.UnityAction toggleStatusMode, UnityEngine.Events.UnityAction exitBattle)
        {
            var root = UiFactory.CreatePanel(parent, "BattleTopBar", new Color(0.04f, 0.06f, 0.09f, 0.9f), new Vector2(0f, 0.91f), new Vector2(1f, 1f), new Vector2(10f, 8f), new Vector2(-10f, -10f));
            var rootImage = root.GetComponent<Image>();
            rootImage.raycastTarget = false;
            rootImage.type = Image.Type.Sliced;

            var rootGlow = UiFactory.CreatePanel(root.transform, "TopGlow", new Color(0.45f, 0.74f, 0.86f, 0.08f), new Vector2(0f, 0.54f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            rootGlow.transform.SetAsFirstSibling();
            rootGlow.GetComponent<Image>().raycastTarget = false;

            var leftPanel = UiFactory.CreatePanel(root.transform, "LocalPlayerPanel", new Color(0.07f, 0.11f, 0.15f, 0.78f), new Vector2(0f, 0f), new Vector2(0.3f, 1f), new Vector2(8f, 8f), new Vector2(-6f, -8f));
            leftPanel.GetComponent<Image>().raycastTarget = false;
            leftPanel.GetComponent<Image>().type = Image.Type.Sliced;

            var markerBase = UiFactory.CreatePanel(leftPanel.transform, "MarkerBase", new Color(0.94f, 0.97f, 1f, 0.05f), new Vector2(0f, 0.1f), new Vector2(0f, 0.9f), new Vector2(16f, 0f), new Vector2(88f, 0f));
            markerBase.GetComponent<Image>().type = Image.Type.Sliced;
            var markerOutline = UiFactory.CreatePanel(markerBase.transform, "MarkerOutline", new Color(0.94f, 0.97f, 1f, 0.18f), Vector2.zero, Vector2.one, new Vector2(-1f, -1f), new Vector2(1f, 1f));
            markerOutline.GetComponent<Image>().raycastTarget = false;
            PlayerMarkerText = UiFactory.CreateText(markerBase.transform, "Marker", 34, TextAnchor.MiddleCenter);
            PlayerMarkerText.fontStyle = FontStyle.Bold;
            PlayerMarkerText.text = "●";
            PlayerMarkerText.color = new Color(0.96f, 0.98f, 1f, 0.96f);

            PlayerNameText = UiFactory.CreateText(leftPanel.transform, "PlayerName", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.56f), new Vector2(1f, 0.9f), new Vector2(102f, 0f), new Vector2(-12f, 0f));
            PlayerNameText.fontStyle = FontStyle.Bold;
            PlayerNameText.color = new Color(0.94f, 0.96f, 0.98f, 0.98f);
            PlayerHpText = UiFactory.CreateText(leftPanel.transform, "PlayerHp", 18, TextAnchor.MiddleLeft, new Vector2(0f, 0.16f), new Vector2(1f, 0.46f), new Vector2(102f, 0f), new Vector2(-12f, 0f));
            PlayerHpText.color = new Color(0.58f, 0.89f, 0.71f, 0.96f);

            SummaryText = UiFactory.CreateText(root.transform, "Summary", 19, TextAnchor.MiddleCenter, new Vector2(0.31f, 0.12f), new Vector2(0.72f, 0.88f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            SummaryText.supportRichText = true;
            SummaryText.fontStyle = FontStyle.Normal;
            SummaryText.color = new Color(0.88f, 0.92f, 0.96f, 0.92f);

            var rightPanel = UiFactory.CreatePanel(root.transform, "SystemPanel", Color.clear, new Vector2(0.73f, 0.18f), new Vector2(1f, 0.82f), Vector2.zero, Vector2.zero);
            var rightLayout = rightPanel.AddComponent<HorizontalLayoutGroup>();
            rightLayout.childAlignment = TextAnchor.MiddleRight;
            rightLayout.childControlWidth = false;
            rightLayout.childControlHeight = false;
            rightLayout.childForceExpandWidth = false;
            rightLayout.childForceExpandHeight = false;
            rightLayout.spacing = 10f;
            rightLayout.padding = new RectOffset(0, 8, 0, 0);

            StatusModeButton = UiFactory.CreateButton(rightPanel.transform, "StatusMode", "模式: 详", toggleStatusMode);
            StatusModeButton.image.color = new Color(0.12f, 0.2f, 0.27f, 0.96f);
            StatusModeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(158f, 42f);
            StatusModeButton.GetComponentInChildren<Text>().color = new Color(0.87f, 0.93f, 0.97f, 0.96f);

            ExitBattleButton = UiFactory.CreateButton(rightPanel.transform, "ExitBattle", "退出", exitBattle);
            ExitBattleButton.image.color = new Color(0.29f, 0.13f, 0.15f, 0.96f);
            ExitBattleButton.GetComponent<RectTransform>().sizeDelta = new Vector2(104f, 42f);
            ExitBattleButton.GetComponentInChildren<Text>().color = new Color(0.98f, 0.91f, 0.91f, 0.96f);
        }
    }
}
