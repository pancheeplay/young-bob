using UnityEngine;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleStageSurfaceView
    {
        public RectTransform MonsterPanelRect { get; private set; }
        public Transform MonsterContainer { get; private set; }
        public Transform WestPlayerContainer { get; private set; }
        public Transform EastPlayerContainer { get; private set; }
        public RectTransform BoardPanelRect { get; private set; }
        public Text MonsterHpText { get; private set; }
        public RectTransform MonsterHpFillRect { get; private set; }
        public Text MonsterActionHintText { get; private set; }
        public Text MonsterStatusHintText { get; private set; }
        public GameObject PhaseBannerMask { get; private set; }
        public Text PhaseBannerTitle { get; private set; }
        public Text PhaseBannerDetail { get; private set; }

        public BattleStageSurfaceView(Transform parent)
        {
            var boardPanel = UiFactory.CreatePanel(parent, "BoardPanel", new Color(0.05f, 0.08f, 0.11f, 1f), new Vector2(0f, 0.41f), new Vector2(1f, 0.91f), new Vector2(12f, 0f), new Vector2(-12f, -2f));
            BoardPanelRect = boardPanel.GetComponent<RectTransform>();

            var atmosphere = UiFactory.CreatePanel(boardPanel.transform, "Atmosphere", new Color(0.12f, 0.19f, 0.24f, 0.22f), new Vector2(0f, 0.22f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            atmosphere.GetComponent<Image>().raycastTarget = false;
            var floor = UiFactory.CreatePanel(boardPanel.transform, "Floor", new Color(0.11f, 0.14f, 0.17f, 0.92f), new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero);
            floor.GetComponent<Image>().raycastTarget = false;
            var horizon = UiFactory.CreatePanel(boardPanel.transform, "Horizon", new Color(0.92f, 0.96f, 1f, 0.18f), new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.286f), Vector2.zero, Vector2.zero);
            horizon.GetComponent<Image>().raycastTarget = false;

            var leftZone = UiFactory.CreateText(boardPanel.transform, "LeftZoneLabel", 13, TextAnchor.MiddleCenter, new Vector2(0f, 0.29f), new Vector2(0.26f, 0.35f), Vector2.zero, Vector2.zero);
            leftZone.text = "本地战位";
            leftZone.color = new Color(0.72f, 0.82f, 0.9f, 0.5f);
            leftZone.raycastTarget = false;
            var rightZone = UiFactory.CreateText(boardPanel.transform, "RightZoneLabel", 13, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.29f), new Vector2(1f, 0.35f), Vector2.zero, Vector2.zero);
            rightZone.text = "协作移动区";
            rightZone.color = new Color(0.72f, 0.82f, 0.9f, 0.5f);
            rightZone.raycastTarget = false;

            var westPanel = UiFactory.CreatePanel(boardPanel.transform, "WestPlayers", Color.clear, new Vector2(0f, 0.28f), new Vector2(0.28f, 0.84f), Vector2.zero, Vector2.zero);
            westPanel.GetComponent<Image>().raycastTarget = false;
            var westLayout = westPanel.AddComponent<HorizontalLayoutGroup>();
            westLayout.childAlignment = TextAnchor.LowerCenter;
            westLayout.spacing = 16f;
            westLayout.childControlWidth = false;
            westLayout.childControlHeight = false;
            WestPlayerContainer = westPanel.transform;

            var monsterPanel = UiFactory.CreatePanel(boardPanel.transform, "MonsterPanel", Color.clear, new Vector2(0.28f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
            monsterPanel.GetComponent<Image>().raycastTarget = false;
            MonsterPanelRect = monsterPanel.GetComponent<RectTransform>();
            MonsterContainer = monsterPanel.transform;

            var hpBarBase = UiFactory.CreatePanel(boardPanel.transform, "MonsterHpBar", new Color(0.08f, 0.11f, 0.14f, 0.94f), new Vector2(0.24f, 0.86f), new Vector2(0.76f, 0.94f), Vector2.zero, Vector2.zero);
            var hpFillObj = UiFactory.CreatePanel(hpBarBase.transform, "Fill", new Color(0.84f, 0.24f, 0.26f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            MonsterHpFillRect = hpFillObj.GetComponent<RectTransform>();
            MonsterHpText = UiFactory.CreateText(hpBarBase.transform, "MonsterHpText", 18, TextAnchor.MiddleCenter);
            MonsterHpText.fontStyle = FontStyle.Bold;
            MonsterHpText.raycastTarget = false;

            MonsterActionHintText = UiFactory.CreateText(boardPanel.transform, "MonsterActionHint", 17, TextAnchor.MiddleCenter, new Vector2(0.24f, 0.79f), new Vector2(0.76f, 0.85f), Vector2.zero, Vector2.zero);
            MonsterActionHintText.color = new Color(0.92f, 0.95f, 0.97f, 0.82f);
            MonsterActionHintText.raycastTarget = false;

            MonsterStatusHintText = UiFactory.CreateText(boardPanel.transform, "MonsterStatusHint", 15, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.72f), new Vector2(0.82f, 0.78f), Vector2.zero, Vector2.zero);
            MonsterStatusHintText.color = new Color(0.74f, 0.81f, 0.87f, 0.76f);
            MonsterStatusHintText.raycastTarget = false;

            var eastPanel = UiFactory.CreatePanel(boardPanel.transform, "EastPlayers", Color.clear, new Vector2(0.72f, 0.28f), new Vector2(1f, 0.84f), Vector2.zero, Vector2.zero);
            eastPanel.GetComponent<Image>().raycastTarget = false;
            var eastLayout = eastPanel.AddComponent<HorizontalLayoutGroup>();
            eastLayout.childAlignment = TextAnchor.LowerCenter;
            eastLayout.spacing = 16f;
            eastLayout.childControlWidth = false;
            eastLayout.childControlHeight = false;
            EastPlayerContainer = eastPanel.transform;

            PhaseBannerMask = UiFactory.CreatePanel(boardPanel.transform, "PhaseBannerMask", new Color(0.02f, 0.04f, 0.06f, 0.68f), new Vector2(0f, 0.76f), new Vector2(1f, 0.98f), Vector2.zero, Vector2.zero);
            PhaseBannerMask.transform.SetAsLastSibling();
            var phaseBannerPanel = UiFactory.CreatePanel(PhaseBannerMask.transform, "PhaseBannerPanel", new Color(0.11f, 0.15f, 0.19f, 0.94f), new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f), Vector2.zero, Vector2.zero);
            PhaseBannerTitle = UiFactory.CreateText(phaseBannerPanel.transform, "Title", 34, TextAnchor.MiddleCenter, new Vector2(0f, 0.42f), new Vector2(1f, 0.86f), Vector2.zero, Vector2.zero);
            PhaseBannerTitle.fontStyle = FontStyle.Bold;
            PhaseBannerTitle.supportRichText = true;
            PhaseBannerDetail = UiFactory.CreateText(phaseBannerPanel.transform, "Detail", 18, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.4f), Vector2.zero, Vector2.zero);
            PhaseBannerDetail.supportRichText = true;
            PhaseBannerMask.SetActive(false);
        }
    }
}
