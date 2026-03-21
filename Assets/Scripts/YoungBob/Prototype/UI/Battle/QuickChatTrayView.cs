using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class QuickChatTrayView
    {
        public GameObject Root { get; private set; }

        public QuickChatTrayView(Transform parent, System.Action closeAction, System.Action<string> sendAction)
        {
            Root = UiFactory.CreatePanel(parent, "QuickChatMask", new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backgroundButton = Root.AddComponent<Button>();
            backgroundButton.transition = Selectable.Transition.None;
            backgroundButton.targetGraphic = Root.GetComponent<Image>();
            backgroundButton.onClick.AddListener(() => closeAction());

            var tray = UiFactory.CreatePanel(Root.transform, "QuickChatTray", new Color(0.07f, 0.1f, 0.14f, 0.96f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-230f, 16f), new Vector2(230f, 184f));
            var trayImage = tray.GetComponent<Image>();
            trayImage.raycastTarget = true;
            trayImage.type = Image.Type.Sliced;

            var trayGlow = UiFactory.CreatePanel(tray.transform, "Glow", new Color(0.38f, 0.7f, 0.82f, 0.06f), new Vector2(0f, 0.54f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            trayGlow.transform.SetAsFirstSibling();
            trayGlow.GetComponent<Image>().raycastTarget = false;

            var title = UiFactory.CreateText(tray.transform, "Title", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0.74f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            title.text = "快捷交流";
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.8f, 0.88f, 0.94f, 0.92f);

            CreateArcButton(tray.transform, "good_play", "打得不错", new Vector2(0f, 104f), sendAction);
            CreateArcButton(tray.transform, "sorry", "抱歉", new Vector2(-128f, 64f), sendAction);
            CreateArcButton(tray.transform, "thanks", "谢谢", new Vector2(128f, 64f), sendAction);
            CreateArcButton(tray.transform, "help", "帮忙", new Vector2(0f, 26f), sendAction);

            Root.SetActive(false);
        }

        private static void CreateArcButton(Transform parent, string presetId, string label, Vector2 position, System.Action<string> sendAction)
        {
            var button = UiFactory.CreateButton(parent, "Chat_" + presetId, label, position, () => sendAction(presetId));
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(148f, 46f);
            button.image.color = new Color(0.12f, 0.21f, 0.29f, 0.98f);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = UiFactory.ScaleFontSize(14);
            text.color = new Color(0.87f, 0.93f, 0.97f, 0.96f);
        }
    }
}
