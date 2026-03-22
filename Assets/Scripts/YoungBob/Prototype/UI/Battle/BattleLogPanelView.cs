using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleLogPanelView
    {
        public Text BattleLogText { get; private set; }
        public ScrollRect LogScrollRect { get; private set; }
        public Button JumpToLatestButton { get; private set; }

        public BattleLogPanelView(Transform parent)
        {
            var root = UiFactory.CreatePanel(parent, "BattleLogPanel", new Color(0.04f, 0.06f, 0.08f, 0.66f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 6f), new Vector2(-10f, -2f));
            root.GetComponent<Image>().raycastTarget = false;
            root.GetComponent<Image>().type = Image.Type.Sliced;

            var band = UiFactory.CreatePanel(root.transform, "BroadcastBand", new Color(0.08f, 0.13f, 0.17f, 0.58f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            band.transform.SetAsFirstSibling();
            band.GetComponent<Image>().raycastTarget = false;

            // var title = UiFactory.CreateText(root.transform, "LogTitle", 14, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -26f), new Vector2(-18f, -4f));
            // title.text = "战况播报";
            // title.color = new Color(0.71f, 0.82f, 0.9f, 0.78f);
            // title.fontStyle = FontStyle.Bold;
            // title.raycastTarget = false;

            var scrollView = new GameObject("LogScroll");
            scrollView.transform.SetParent(root.transform, false);
            var svRect = scrollView.AddComponent<RectTransform>();
            svRect.anchorMin = new Vector2(0f, 0f);
            svRect.anchorMax = new Vector2(1f, 1f);
            svRect.offsetMin = new Vector2(12f, 10f);
            svRect.offsetMax = new Vector2(-12f, -36f);

            LogScrollRect = scrollView.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<RectMask2D>();

            BattleLogText = UiFactory.CreateText(viewport.transform, "LogText", 18, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            BattleLogText.supportRichText = true;
            BattleLogText.color = new Color(0.89f, 0.93f, 0.96f, 0.94f);
            BattleLogText.lineSpacing = 0.6f;
            var logTextRect = BattleLogText.GetComponent<RectTransform>();
            logTextRect.pivot = new Vector2(0.5f, 1f);
            logTextRect.anchoredPosition = Vector2.zero;
            logTextRect.sizeDelta = Vector2.zero;
            var logTextFitter = BattleLogText.gameObject.AddComponent<ContentSizeFitter>();
            logTextFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LogScrollRect.viewport = vpRect;
            LogScrollRect.content = logTextRect;
            LogScrollRect.horizontal = false;
            LogScrollRect.vertical = true;

            JumpToLatestButton = UiFactory.CreateButton(root.transform, "JumpToLatest", "最新", null);
            JumpToLatestButton.image.color = new Color(0.12f, 0.21f, 0.29f, 0.96f);
            var jumpRect = JumpToLatestButton.GetComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(0.84f, 0.08f);
            jumpRect.anchorMax = new Vector2(0.98f, 0.28f);
            jumpRect.offsetMin = Vector2.zero;
            jumpRect.offsetMax = Vector2.zero;
            JumpToLatestButton.GetComponentInChildren<Text>().color = new Color(0.88f, 0.93f, 0.97f, 0.94f);
            JumpToLatestButton.gameObject.SetActive(false);
        }
    }
}
