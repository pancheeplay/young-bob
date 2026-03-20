using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;
using YoungBob.Prototype.UI.Battle;

namespace YoungBob.Prototype.UI.Pages
{
    internal sealed class BattlePage : PageBase
    {
        private const float LogBottomThreshold = 0.01f;

        private readonly Canvas _canvas;
        private readonly Text _summaryText;
        private readonly Text _battleLogText;
        private readonly ScrollRect _logScrollRect;
        private readonly Button _jumpToLatestButton;
        private readonly RectTransform _monsterPanelRect;
        private readonly Transform _monsterContainer;
        private readonly Transform _westPlayerContainer;
        private readonly Transform _eastPlayerContainer;
        private readonly RectTransform _boardPanelRect;
        private readonly List<BattleAreaDropZoneView> _areaDropZones = new List<BattleAreaDropZoneView>();
        private readonly RectTransform _cardDragGuideRoot;
        private readonly List<RectTransform> _cardDragGuideSegments = new List<RectTransform>();
        private readonly RectTransform _cardDragGuideArrow;
        private readonly RectTransform _handPanelRect;
        private readonly Transform _handContainer;
        private readonly Button _drawPileButton;
        private readonly Button _discardPileButton;
        private readonly Button _exhaustPileButton;
        private readonly GameObject _pilePopupMask;
        private readonly Text _pilePopupTitle;
        private readonly Transform _pilePopupContent;
        private readonly GridLayoutGroup _pilePopupGrid;
        private readonly RectTransform _monsterHpFillRect;
        private readonly Text _monsterHpText;
        private readonly Text _monsterActionHintText;
        private readonly Text _monsterStatusHintText;
        private readonly Text _effectTargetHintText;
        private readonly Button _statusModeButton;
        private readonly Button _chatButton;
        private readonly Text _chatButtonLabel;
        private readonly GameObject _quickChatMask;
        private readonly Button _endTurnButton;
        private readonly Button _exitBattleButton;
        private readonly List<BattleUnitSlotView> _playerSlots = new List<BattleUnitSlotView>();
        private readonly List<MonsterPartSlotView> _monsterPartSlots = new List<MonsterPartSlotView>();
        private readonly Dictionary<string, MonsterPartSlotView> _monsterPartSlotsByInstanceId = new Dictionary<string, MonsterPartSlotView>();
        private readonly List<string> _battleLogs = new List<string>();
        private readonly Text _energyLabel;
        private bool _isUserScrolling;

        private BattleState _lastState;
        private CardDefinition _draggingCardDefinition;
        private string _draggingCardInstanceId;
        private BattleHandCardDragView _draggingCardView;
        private bool _isDetailedStatusMode = true;
        private PileView _currentPileView = PileView.Draw;
        private Action<PlayerBattleState, Transform> _popupContentRenderer;

        private enum PileView
        {
            Draw,
            Discard,
            Exhaust
        }

        public BattlePage(Transform parent, PrototypeSessionController session)
            : base(parent, "BattlePage", session, new Color(0.12f, 0.14f, 0.17f), new Vector2(0f, 0f), new Vector2(1f, 1f))
        {
            _canvas = parent.GetComponent<Canvas>();

            // --- Top Header (Turn Info & End Turn) ---
            var headerPanel = UiFactory.CreatePanel(Root.transform, "Header", new Color(0.18f, 0.2f, 0.23f), new Vector2(0f, 0.92f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _summaryText = UiFactory.CreateText(headerPanel.transform, "Summary", 24, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(30f, 0f), new Vector2(-10f, 0f));
            _summaryText.fontStyle = FontStyle.Bold;
            _summaryText.supportRichText = true;

            _endTurnButton = UiFactory.CreateButton(headerPanel.transform, "EndTurn", "结束", Session.EndTurn);
            var etRect = _endTurnButton.GetComponent<RectTransform>();
            etRect.anchorMin = new Vector2(0.7f, 0.2f);
            etRect.anchorMax = new Vector2(0.95f, 0.8f);
            etRect.offsetMin = Vector2.zero;
            etRect.offsetMax = Vector2.zero;
            var etText = _endTurnButton.GetComponentInChildren<Text>();
            etText.fontSize = 28;
            etText.fontStyle = FontStyle.Bold;
            _endTurnButton.image.color = new Color(0.25f, 0.55f, 0.35f);

            _exitBattleButton = UiFactory.CreateButton(Root.transform, "ExitBattle", "退出", Session.EndBattleAndReturnToLobby);
            var exRect = _exitBattleButton.GetComponent<RectTransform>();
            exRect.anchorMin = new Vector2(0.30f, 0.94f);
            exRect.anchorMax = new Vector2(0.42f, 0.98f);
            exRect.offsetMin = Vector2.zero;
            exRect.offsetMax = Vector2.zero;
            _exitBattleButton.image.color = new Color(0.4f, 0.2f, 0.2f, 0.6f);

            _chatButton = UiFactory.CreateButton(Root.transform, "QuickChatButton", "聊天", ToggleQuickChatWheel);
            var chatRect = _chatButton.GetComponent<RectTransform>();
            chatRect.anchorMin = new Vector2(0.44f, 0.94f);
            chatRect.anchorMax = new Vector2(0.56f, 0.98f);
            chatRect.offsetMin = Vector2.zero;
            chatRect.offsetMax = Vector2.zero;
            _chatButton.image.color = new Color(0.22f, 0.36f, 0.52f, 0.75f);
            _chatButtonLabel = _chatButton.GetComponentInChildren<Text>();
            _chatButtonLabel.fontSize = 22;

            _quickChatMask = BuildQuickChatWheel(Root.transform);
            _quickChatMask.transform.SetAsLastSibling();
            _quickChatMask.SetActive(false);

            // --- Board Panel (Portrait, Horizon-based) ---
            var boardPanel = UiFactory.CreatePanel(Root.transform, "BoardPanel", new Color(0.08f, 0.1f, 0.12f), new Vector2(0f, 0.45f), new Vector2(1f, 0.91f), new Vector2(10f, 0f), new Vector2(-10f, 0f));
            _boardPanelRect = boardPanel.GetComponent<RectTransform>();

            // Invisible drop zones (Created at the back, but in front of board panel itself)
            CreateAreaDropZone(boardPanel.transform, "DropZone_West", BattleArea.West, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            CreateAreaDropZone(boardPanel.transform, "DropZone_East", BattleArea.East, new Vector2(0.5f, 0f), new Vector2(1f, 1f));

            // Global Monster HP Bar (Top of Board)
            var monsterHpBase = UiFactory.CreatePanel(boardPanel.transform, "MonsterHpBar", new Color(0.15f, 0.15f, 0.18f, 0.8f), new Vector2(0.1f, 0.9f), new Vector2(0.9f, 0.98f), Vector2.zero, Vector2.zero);
            var hpFillObj = UiFactory.CreatePanel(monsterHpBase.transform, "Fill", new Color(0.85f, 0.2f, 0.2f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _monsterHpFillRect = hpFillObj.GetComponent<RectTransform>();
            _monsterHpFillRect.pivot = new Vector2(0f, 0.5f);
            _monsterHpText = UiFactory.CreateText(monsterHpBase.transform, "Label", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _monsterHpText.fontStyle = FontStyle.Bold;

            // Ground Visual (Behind units)
            var ground = UiFactory.CreatePanel(boardPanel.transform, "Ground", new Color(0.15f, 0.17f, 0.2f, 0.8f), new Vector2(0f, 0f), new Vector2(1f, 0.3f), Vector2.zero, Vector2.zero);
            ground.GetComponent<Image>().raycastTarget = false;

            // Horizon Line
            var horizonLine = UiFactory.CreatePanel(boardPanel.transform, "Horizon", new Color(1f, 1f, 1f, 0.3f), new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.305f), Vector2.zero, Vector2.zero);
            horizonLine.GetComponent<Image>().raycastTarget = false;

            // West Players (Left side, standing on horizon)
            var westPanel = UiFactory.CreatePanel(boardPanel.transform, "WestPlayers", Color.clear, new Vector2(0f, 0.3f), new Vector2(0.28f, 0.8f), Vector2.zero, Vector2.zero);
            westPanel.GetComponent<Image>().raycastTarget = false;
            var westLayout = westPanel.AddComponent<HorizontalLayoutGroup>();
            westLayout.childAlignment = TextAnchor.LowerCenter;
            westLayout.spacing = 10f;
            westLayout.childControlHeight = westLayout.childControlWidth = false;
            _westPlayerContainer = westPanel.transform;

            // Monster Area (Center, standing on horizon)
            var monsterPanel = UiFactory.CreatePanel(boardPanel.transform, "MonsterPanel", Color.clear, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f), Vector2.zero, Vector2.zero);
            monsterPanel.GetComponent<Image>().raycastTarget = false;
            _monsterPanelRect = monsterPanel.GetComponent<RectTransform>();
            _monsterContainer = monsterPanel.transform;
            _monsterActionHintText = UiFactory.CreateText(monsterPanel.transform, "MonsterActionHint", 18, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.16f), Vector2.zero, Vector2.zero);
            _monsterActionHintText.color = new Color(0.72f, 0.72f, 0.72f, 0.75f);
            _monsterActionHintText.raycastTarget = false;
            _monsterStatusHintText = UiFactory.CreateText(monsterPanel.transform, "MonsterStatusHint", 17, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.08f), Vector2.zero, Vector2.zero);
            _monsterStatusHintText.color = new Color(0.79f, 0.82f, 0.85f, 0.72f);
            _monsterStatusHintText.raycastTarget = false;

            _effectTargetHintText = UiFactory.CreateText(Root.transform, "EffectTargetHint", 18, TextAnchor.MiddleCenter, new Vector2(0.15f, 0.425f), new Vector2(0.85f, 0.45f), Vector2.zero, Vector2.zero);
            _effectTargetHintText.color = new Color(0.9f, 0.92f, 0.95f, 0.9f);
            _effectTargetHintText.raycastTarget = false;
            _effectTargetHintText.text = string.Empty;

            // East Players (Right side, standing on horizon)
            var eastPanel = UiFactory.CreatePanel(boardPanel.transform, "EastPlayers", Color.clear, new Vector2(0.72f, 0.3f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero);
            eastPanel.GetComponent<Image>().raycastTarget = false;
            var eastLayout = eastPanel.AddComponent<HorizontalLayoutGroup>();
            eastLayout.childAlignment = TextAnchor.LowerCenter;
            eastLayout.spacing = 10f;
            eastLayout.childControlHeight = eastLayout.childControlWidth = false;
            _eastPlayerContainer = eastPanel.transform;

            // --- Battle Log (Scrollable) ---
            var logBase = UiFactory.CreatePanel(Root.transform, "LogBase", new Color(0.05f, 0.05f, 0.06f, 0.92f), new Vector2(0f, 0.28f), new Vector2(1f, 0.44f), new Vector2(15f, 5f), new Vector2(-15f, -5f));
            
            var scrollView = new GameObject("LogScroll");
            scrollView.transform.SetParent(logBase.transform, false);
            var svRect = scrollView.AddComponent<RectTransform>();
            svRect.anchorMin = Vector2.zero;
            svRect.anchorMax = Vector2.one;
            svRect.offsetMin = new Vector2(10f, 10f);
            svRect.offsetMax = new Vector2(-10f, -10f);

            _logScrollRect = scrollView.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            
            _battleLogText = UiFactory.CreateText(viewport.transform, "LogText", 20, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _battleLogText.supportRichText = true;
            var logTextRect = _battleLogText.GetComponent<RectTransform>();
            logTextRect.pivot = new Vector2(0.5f, 1f);
            logTextRect.anchoredPosition = Vector2.zero;
            logTextRect.sizeDelta = Vector2.zero;
            var logTextFitter = _battleLogText.gameObject.AddComponent<ContentSizeFitter>();
            logTextFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _logScrollRect.viewport = vpRect;
            _logScrollRect.content = logTextRect;
            _logScrollRect.horizontal = false;
            _logScrollRect.vertical = true;
            _logScrollRect.onValueChanged.AddListener(_ => OnLogScrollChanged());

            _jumpToLatestButton = UiFactory.CreateButton(logBase.transform, "JumpToLatest", "日志 ↓", () => {
                _logScrollRect.verticalNormalizedPosition = 0f;
                _isUserScrolling = false;
                _jumpToLatestButton.gameObject.SetActive(false);
            });
            var jumpRect = _jumpToLatestButton.GetComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(0.82f, 0.04f);
            jumpRect.anchorMax = new Vector2(0.98f, 0.22f);
            jumpRect.offsetMin = jumpRect.offsetMax = Vector2.zero;
            _jumpToLatestButton.gameObject.SetActive(false);
            
            // --- Hand ---
            var handPanel = UiFactory.CreatePanel(Root.transform, "HandPanel", new Color(0.12f, 0.14f, 0.18f, 0.7f), new Vector2(0f, 0.05f), new Vector2(1f, 0.27f), new Vector2(10f, 10f), new Vector2(-10f, 5f));
            _handPanelRect = handPanel.GetComponent<RectTransform>();

            var handHeader = UiFactory.CreatePanel(handPanel.transform, "HandHeader", new Color(0.10f, 0.12f, 0.16f, 0.95f), new Vector2(0f, 0.76f), new Vector2(1f, 1f), new Vector2(10f, 5f), new Vector2(-10f, -5f));
            var handHeaderRect = handHeader.GetComponent<RectTransform>();

            _energyLabel = UiFactory.CreateText(handHeader.transform, "EnergyLabel", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(18f, 0f), new Vector2(0f, 0f));
            _energyLabel.fontStyle = FontStyle.Bold;
            _energyLabel.color = new Color(0.4f, 0.7f, 1f);
            _energyLabel.raycastTarget = false;

            var handScrollArea = new GameObject("HandScroll");
            handScrollArea.transform.SetParent(handPanel.transform, false);
            var hsRect = handScrollArea.AddComponent<RectTransform>();
            hsRect.anchorMin = new Vector2(0f, 0f);
            hsRect.anchorMax = new Vector2(1f, 0.76f);
            hsRect.offsetMin = new Vector2(10f, 10f);
            hsRect.offsetMax = new Vector2(-10f, -5f);

            var handScroll = handScrollArea.AddComponent<ScrollRect>();
            var handViewport = new GameObject("Viewport");
            handViewport.transform.SetParent(handScrollArea.transform, false);
            var hvRect = handViewport.AddComponent<RectTransform>();
            hvRect.anchorMin = Vector2.zero;
            hvRect.anchorMax = Vector2.one;
            hvRect.sizeDelta = Vector2.zero;
            handViewport.AddComponent<RectMask2D>();

            var handContent = UiFactory.CreatePanel(handViewport.transform, "HandContent", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handContentRect = handContent.GetComponent<RectTransform>();
            handContentRect.pivot = new Vector2(0f, 0.5f);
            var handLayout = handContent.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 15f;
            handLayout.padding = new RectOffset(20, 20, 15, 15);
            handLayout.childAlignment = TextAnchor.MiddleLeft;
            handLayout.childControlWidth = handLayout.childControlHeight = false;
            handLayout.childForceExpandWidth = handLayout.childForceExpandHeight = false;
            var hFitter = handContent.AddComponent<ContentSizeFitter>();
            hFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            handScroll.viewport = hvRect;
            handScroll.content = handContentRect;
            handScroll.vertical = false;
            handScroll.horizontal = true;
            _handContainer = handContent.transform;

            _drawPileButton = UiFactory.CreateButton(handHeader.transform, "DrawPileButton", "牌库 0", () => OpenPilePopup(PileView.Draw));
            var drawRect = _drawPileButton.GetComponent<RectTransform>();
            drawRect.anchorMin = new Vector2(0.58f, 0.14f);
            drawRect.anchorMax = new Vector2(0.73f, 0.86f);
            drawRect.offsetMin = Vector2.zero;
            drawRect.offsetMax = Vector2.zero;
            _drawPileButton.image.color = new Color(0.2f, 0.28f, 0.45f, 0.95f);
            _drawPileButton.GetComponentInChildren<Text>().fontSize = 16;

            _discardPileButton = UiFactory.CreateButton(handHeader.transform, "DiscardPileButton", "弃牌 0", () => OpenPilePopup(PileView.Discard));
            var discardRect = _discardPileButton.GetComponent<RectTransform>();
            discardRect.anchorMin = new Vector2(0.74f, 0.14f);
            discardRect.anchorMax = new Vector2(0.86f, 0.86f);
            discardRect.offsetMin = Vector2.zero;
            discardRect.offsetMax = Vector2.zero;
            _discardPileButton.image.color = new Color(0.32f, 0.24f, 0.2f, 0.95f);
            _discardPileButton.GetComponentInChildren<Text>().fontSize = 16;

            _exhaustPileButton = UiFactory.CreateButton(handHeader.transform, "ExhaustPileButton", "消耗 0", () => OpenPilePopup(PileView.Exhaust));
            var exhaustRect = _exhaustPileButton.GetComponent<RectTransform>();
            exhaustRect.anchorMin = new Vector2(0.87f, 0.14f);
            exhaustRect.anchorMax = new Vector2(0.98f, 0.86f);
            exhaustRect.offsetMin = Vector2.zero;
            exhaustRect.offsetMax = Vector2.zero;
            _exhaustPileButton.image.color = new Color(0.26f, 0.2f, 0.36f, 0.95f);
            _exhaustPileButton.GetComponentInChildren<Text>().fontSize = 16;

            _pilePopupMask = UiFactory.CreatePanel(Root.transform, "PilePopupMask", new Color(0f, 0f, 0f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var popupWindow = UiFactory.CreatePanel(_pilePopupMask.transform, "PilePopupWindow", new Color(0.12f, 0.14f, 0.17f, 1f), new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f), Vector2.zero, Vector2.zero);
            _pilePopupTitle = UiFactory.CreateText(popupWindow.transform, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0.9f), new Vector2(0.8f, 1f), new Vector2(24f, 0f), new Vector2(0f, 0f));
            _pilePopupTitle.fontStyle = FontStyle.Bold;
            var popupClose = UiFactory.CreateButton(popupWindow.transform, "CloseButton", "关闭", ClosePilePopup);
            var popupCloseRect = popupClose.GetComponent<RectTransform>();
            popupCloseRect.anchorMin = new Vector2(0.82f, 0.91f);
            popupCloseRect.anchorMax = new Vector2(0.98f, 0.99f);
            popupCloseRect.offsetMin = Vector2.zero;
            popupCloseRect.offsetMax = Vector2.zero;

            var popupScrollObj = new GameObject("Scroll");
            popupScrollObj.transform.SetParent(popupWindow.transform, false);
            var popupScrollRect = popupScrollObj.AddComponent<RectTransform>();
            popupScrollRect.anchorMin = new Vector2(0.03f, 0.04f);
            popupScrollRect.anchorMax = new Vector2(0.97f, 0.88f);
            popupScrollRect.offsetMin = Vector2.zero;
            popupScrollRect.offsetMax = Vector2.zero;
            var popupScroll = popupScrollObj.AddComponent<ScrollRect>();

            var popupViewportObj = new GameObject("Viewport");
            popupViewportObj.transform.SetParent(popupScrollObj.transform, false);
            var popupViewportRect = popupViewportObj.AddComponent<RectTransform>();
            popupViewportRect.anchorMin = Vector2.zero;
            popupViewportRect.anchorMax = Vector2.one;
            popupViewportRect.offsetMin = Vector2.zero;
            popupViewportRect.offsetMax = Vector2.zero;
            popupViewportObj.AddComponent<RectMask2D>();

            var popupContentObj = UiFactory.CreatePanel(popupViewportObj.transform, "Content", Color.clear, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var popupContentRect = popupContentObj.GetComponent<RectTransform>();
            popupContentRect.pivot = new Vector2(0.5f, 1f);
            _pilePopupGrid = popupContentObj.AddComponent<GridLayoutGroup>();
            _pilePopupGrid.childAlignment = TextAnchor.UpperCenter;
            _pilePopupGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _pilePopupGrid.constraintCount = 3;
            _pilePopupGrid.cellSize = new Vector2(184f, 256f);
            _pilePopupGrid.spacing = new Vector2(14f, 14f);
            _pilePopupGrid.padding = new RectOffset(10, 10, 12, 12);
            var popupFitter = popupContentObj.AddComponent<ContentSizeFitter>();
            popupFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            popupScroll.viewport = popupViewportRect;
            popupScroll.content = popupContentRect;
            popupScroll.horizontal = false;
            popupScroll.vertical = true;
            _pilePopupContent = popupContentObj.transform;
            _pilePopupMask.SetActive(false);

            var settingsBar = UiFactory.CreatePanel(Root.transform, "BattleSettingsBar", new Color(0.09f, 0.11f, 0.14f, 0.95f), new Vector2(0f, 0f), new Vector2(1f, 0.045f), new Vector2(10f, 5f), new Vector2(-10f, -2f));
            var settingsLayout = settingsBar.AddComponent<HorizontalLayoutGroup>();
            settingsLayout.childAlignment = TextAnchor.MiddleLeft;
            settingsLayout.childControlWidth = false;
            settingsLayout.childControlHeight = false;
            settingsLayout.childForceExpandWidth = false;
            settingsLayout.childForceExpandHeight = false;
            settingsLayout.spacing = 10f;
            settingsLayout.padding = new RectOffset(12, 12, 4, 4);

            _statusModeButton = UiFactory.CreateButton(settingsBar.transform, "StatusMode", "模式: 详", ToggleStatusMode);
            _statusModeButton.image.color = new Color(0.24f, 0.32f, 0.46f);
            _statusModeButton.GetComponentInChildren<Text>().fontSize = 16;
            var smRect = _statusModeButton.GetComponent<RectTransform>();
            smRect.sizeDelta = new Vector2(170f, 40f);

            var dragCurveObject = new GameObject("CardDragCurve");
            dragCurveObject.transform.SetParent(_canvas.transform, false);
            var dragCurveRect = dragCurveObject.AddComponent<RectTransform>();
            dragCurveRect.anchorMin = Vector2.zero;
            dragCurveRect.anchorMax = Vector2.one;
            dragCurveRect.offsetMin = Vector2.zero;
            dragCurveRect.offsetMax = Vector2.zero;
            _cardDragGuideRoot = dragCurveRect;
            const int guideSegmentCount = 18;
            for (var i = 0; i < guideSegmentCount; i++)
            {
                var segmentObj = new GameObject("Segment_" + i);
                segmentObj.transform.SetParent(_cardDragGuideRoot, false);
                var segmentRect = segmentObj.AddComponent<RectTransform>();
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                var segmentImage = segmentObj.AddComponent<RawImage>();
                segmentImage.texture = Texture2D.whiteTexture;
                segmentImage.color = new Color(1f, 0.95f, 0.4f, 0.95f);
                segmentImage.raycastTarget = false;
                segmentObj.SetActive(false);
                _cardDragGuideSegments.Add(segmentRect);
            }

            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(_cardDragGuideRoot, false);
            _cardDragGuideArrow = arrowObj.AddComponent<RectTransform>();
            _cardDragGuideArrow.anchorMin = new Vector2(0.5f, 0.5f);
            _cardDragGuideArrow.anchorMax = new Vector2(0.5f, 0.5f);
            _cardDragGuideArrow.pivot = new Vector2(0.5f, 0.5f);
            var arrowImage = arrowObj.AddComponent<RawImage>();
            arrowImage.texture = Texture2D.whiteTexture;
            arrowImage.color = new Color(1f, 0.95f, 0.4f, 0.95f);
            arrowImage.raycastTarget = false;
            arrowObj.SetActive(false);
            _cardDragGuideRoot.gameObject.SetActive(false);

            Hide();
        }


        public void Render(BattleState state)
        {
            _lastState = state;
            RenderSummary(state);
            RenderBoard(state);
            RenderHand(state);
        }

        private void RenderSummary(BattleState state)
        {
            if (state == null)
            {
                _battleLogs.Clear();
                _battleLogText.text = string.Empty;
                _summaryText.text = "等待战斗开始...";
                _endTurnButton.interactable = false;
                _exitBattleButton.interactable = false;
                _chatButton.interactable = false;
                CloseQuickChatWheel();
                _isUserScrolling = false;
                RefreshJumpToLatestButton();
                return;
            }

            var turnType = state.phase == BattlePhase.PlayerTurn ? "我方回合" : "怪物回合";
            var color = state.phase == BattlePhase.PlayerTurn ? "#40FF80" : "#FF6060";
            var encounterTotal = state.stageEncounterIds == null ? 0 : state.stageEncounterIds.Length;
            var encounterText = encounterTotal > 0
                ? $"<size=20>关卡 {state.stageId}  遭遇 {state.stageEncounterIndex + 1}/{encounterTotal}</size>\n"
                : string.Empty;
            _summaryText.text = $"<color={color}>{turnType}</color>  -  第 {state.turnIndex} 回合\n{encounterText}<size=22>{state.currentPrompt}</size>";
            
            _endTurnButton.interactable = Session.CanLocalPlayerAct();
            _exitBattleButton.interactable = true;
            _chatButton.interactable = true;
        }

        public void AppendBattleLog(string message)
        {
            _battleLogs.Add(message);
            if (_battleLogs.Count > 100) _battleLogs.RemoveAt(0);

            _battleLogText.text = string.Join("\n\n", _battleLogs);
            
            // Auto-scroll to bottom if not manually scrolling up
            if (!_isUserScrolling)
            {
                // In Unity, verticalNormalizedPosition 0 is bottom
                Canvas.ForceUpdateCanvases();
                _logScrollRect.verticalNormalizedPosition = 0f;
            }

            RefreshJumpToLatestButton();
        }

        private void OnLogScrollChanged()
        {
            _isUserScrolling = !IsAtLatestLogPosition();
            RefreshJumpToLatestButton();
        }

        private bool IsAtLatestLogPosition()
        {
            if (_logScrollRect == null || _logScrollRect.content == null || _logScrollRect.viewport == null)
            {
                return true;
            }

            var contentHeight = _logScrollRect.content.rect.height;
            var viewportHeight = _logScrollRect.viewport.rect.height;
            if (contentHeight <= viewportHeight + 0.5f)
            {
                return true;
            }

            return _logScrollRect.verticalNormalizedPosition <= LogBottomThreshold;
        }

        private void RefreshJumpToLatestButton()
        {
            _jumpToLatestButton.gameObject.SetActive(_isUserScrolling && _battleLogs.Count > 0);
        }

        private void RenderBoard(BattleState state)
        {
            ClearContainer(_westPlayerContainer);
            ClearContainer(_eastPlayerContainer);
            _playerSlots.Clear();
            // Drop zones are static, no need to clear

            if (state == null)
            {
                ClearMonsterPartViews();
                _monsterActionHintText.text = string.Empty;
                _monsterStatusHintText.text = string.Empty;
                return;
            }

            // Update Monster HP Bar
            if (state.monster != null)
            {
                float ratio = state.monster.coreMaxHp > 0 ? Mathf.Clamp01((float)state.monster.coreHp / state.monster.coreMaxHp) : 0f;
                _monsterHpFillRect.anchorMax = new Vector2(ratio, 1f);
                string pose = !string.IsNullOrEmpty(state.monster.currentPoseId) ? $" [{state.monster.currentPoseId}]" : "";
                _monsterHpText.text = $"怪物生命：{state.monster.coreHp} / {state.monster.coreMaxHp}{pose}";
                _monsterActionHintText.text = BuildMonsterActionHint(state.monster);
                _monsterStatusHintText.text = BuildMonsterStatusesText(state.monster.statuses, _isDetailedStatusMode);
            }
            else
            {
                ClearMonsterPartViews();
                _monsterActionHintText.text = string.Empty;
                _monsterStatusHintText.text = string.Empty;
            }

            // Render Players in their respective areas
            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                var container = player.area == BattleArea.East ? _eastPlayerContainer : _westPlayerContainer;
                var secretSummary = BuildPlayerSecretSummary(player, _isDetailedStatusMode);
                var slot = CreateUnitSlot(container, BattleTargetFaction.Allies, player.playerId, player.displayName, player.hp, player.maxHp, player.armor, player.attackChargeStage, player.nextAttackBonus, player.vulnerableStacks, player.statuses, player.threatValue, player.threatTier, secretSummary, new Color(0.2f, 0.36f, 0.31f), _isDetailedStatusMode, SlotHighlightMode.None);
                _playerSlots.Add(slot);
            }

            // Render Monster Parts (manual placement)
            if (state.monster != null)
            {
                Canvas.ForceUpdateCanvases();
                var panelRect = _monsterPanelRect.rect;
                var activePartIds = new HashSet<string>();
                _monsterPartSlots.Clear();
                for (var i = 0; i < state.monster.parts.Count; i++)
                {
                    var part = state.monster.parts[i];
                    activePartIds.Add(part.instanceId);
                    MonsterPartSlotView slot;
                    if (!_monsterPartSlotsByInstanceId.TryGetValue(part.instanceId, out slot))
                    {
                        slot = CreateMonsterPartSlot(_monsterContainer, part, panelRect, state.monster.facing, state.monster.stance, SlotHighlightMode.None);
                        _monsterPartSlotsByInstanceId[part.instanceId] = slot;
                    }

                    var targetPosition = ResolvePartPosition(part, panelRect, state.monster.facing, state.monster.stance);
                    slot.SetTargetPosition(targetPosition, snapImmediately: false);
                    slot.SetData(part, _isDetailedStatusMode, SlotHighlightMode.None);
                    _monsterPartSlots.Add(slot);
                }

                var removedIds = new List<string>();
                foreach (var pair in _monsterPartSlotsByInstanceId)
                {
                    if (!activePartIds.Contains(pair.Key))
                    {
                        if (pair.Value != null)
                        {
                            UnityEngine.Object.Destroy(pair.Value.gameObject);
                        }
                        removedIds.Add(pair.Key);
                    }
                }

                for (var i = 0; i < removedIds.Count; i++)
                {
                    _monsterPartSlotsByInstanceId.Remove(removedIds[i]);
                }

                for (var i = 0; i < _monsterPartSlots.Count; i++)
                {
                    _monsterPartSlots[i].transform.SetSiblingIndex(i);
                }
            }

            // Drop zones are already created in constructor
        }

        private void RenderHand(BattleState state)
        {
            if (_handContainer == null) return;
            ClearContainer(_handContainer);

            if (state == null)
            {
                _drawPileButton.gameObject.SetActive(false);
                _discardPileButton.gameObject.SetActive(false);
                _exhaustPileButton.gameObject.SetActive(false);
                _effectTargetHintText.text = string.Empty;
                ClosePilePopup();
                return;
            }

            var player = Session.GetLocalBattlePlayer();
            if (player == null)
            {
                _energyLabel.text = "";
                _drawPileButton.gameObject.SetActive(false);
                _discardPileButton.gameObject.SetActive(false);
                _exhaustPileButton.gameObject.SetActive(false);
                _effectTargetHintText.text = string.Empty;
                ClosePilePopup();
                return;
            }

            _drawPileButton.gameObject.SetActive(true);
            _discardPileButton.gameObject.SetActive(true);
            _exhaustPileButton.gameObject.SetActive(true);
            _drawPileButton.GetComponentInChildren<Text>().text = $"牌库 {player.drawPile.Count}";
            _discardPileButton.GetComponentInChildren<Text>().text = $"弃牌 {player.discardPile.Count}";
            _exhaustPileButton.GetComponentInChildren<Text>().text = $"消耗 {player.exhaustPile.Count}";

            if (_pilePopupMask.activeSelf)
            {
                RefreshPopup(player);
            }

            // Energy display
            var currentEnergy = player.energy;
            var maxEnergy = BattleEngine.BaseEnergyPerTurn; // Default fallback
            var energyIcons = "";
            for (var i = 1; i <= Math.Max(currentEnergy, maxEnergy); i++)
            {
                if (i <= currentEnergy) energyIcons += "💎";
                else energyIcons += "<color=#2a3c50>💎</color>";
            }
            _energyLabel.text = $"能量 {energyIcons} ({currentEnergy}/{maxEnergy})";

            if (!string.IsNullOrEmpty(_draggingCardInstanceId) && !HasCardInHand(player, _draggingCardInstanceId))
            {
                if (_draggingCardView != null)
                {
                    UnityEngine.Object.Destroy(_draggingCardView.gameObject);
                    _draggingCardView = null;
                }
                HideDragGuide();
                ClearHighlights();
                _draggingCardDefinition = null;
                _draggingCardInstanceId = null;
                _effectTargetHintText.text = string.Empty;
            }

            var canAct = Session.CanLocalPlayerAct();

            for (var i = 0; i < player.hand.Count; i++)
            {
                var cardState = player.hand[i];
                var cardDef = Session.GetCardDefinition(cardState.cardId);
                if (cardDef == null) continue;
                var effectiveCost = BattleMechanics.GetEffectiveEnergyCost(cardState, cardDef);
                
                var isPlayable = canAct && player.energy >= effectiveCost;

                // Skip the card that is currently being dragged (it's in the canvas already)
                if (cardState.instanceId == _draggingCardInstanceId)
                {
                    continue;
                }

                var cardObject = UiFactory.CreateCard(_handContainer, "Card_" + cardState.instanceId, cardDef, isPlayable, effectiveCost);
                if (cardObject == null) continue;

                var layoutElement = cardObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 140f;
                layoutElement.preferredHeight = 196f;

                var dragView = cardObject.AddComponent<BattleHandCardDragView>();
                dragView.Initialize(_canvas);
                dragView.BeganDrag += (v, eventData) => BeginCardDrag(v, cardState.instanceId, cardDef, eventData);
                dragView.Dragged += (v, eventData) => UpdateCardDrag(v, eventData);
                dragView.EndedDrag += (v, eventData) => EndCardDrag(v, eventData);

                // Dim if not playable
                if (!isPlayable)
                {
                    var canvasGroup = cardObject.GetComponent<CanvasGroup>();
                    if (canvasGroup == null) canvasGroup = cardObject.AddComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 0.6f;
                    }
                    dragView.enabled = false;
                }
            }
        }

        private void OpenPilePopup(PileView view)
        {
            _currentPileView = view;
            var player = Session.GetLocalBattlePlayer();
            if (player == null)
            {
                return;
            }

            ShowPopup(player, RenderPilePopup);
        }

        private void ClosePilePopup()
        {
            _pilePopupMask.SetActive(false);
            _popupContentRenderer = null;
        }

        private void ShowPopup(PlayerBattleState player, Action<PlayerBattleState, Transform> renderer)
        {
            _popupContentRenderer = renderer;
            _pilePopupMask.SetActive(true);
            RefreshPopup(player);
        }

        private void RefreshPopup(PlayerBattleState player)
        {
            if (!_pilePopupMask.activeSelf || _popupContentRenderer == null || player == null)
            {
                return;
            }

            ClearContainer(_pilePopupContent);
            _popupContentRenderer(player, _pilePopupContent);
        }

        private void RenderPilePopup(PlayerBattleState player, Transform contentRoot)
        {
            if (player == null)
            {
                return;
            }

            var pile = GetCurrentPile(player);
            _pilePopupTitle.text = BuildPileTitle(pile.Count);

            if (pile.Count == 0)
            {
                var emptyText = UiFactory.CreateText(contentRoot, "EmptyText", 22, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 48f));
                emptyText.text = "空";
                emptyText.color = new Color(0.75f, 0.75f, 0.75f);
                var emptyLayout = emptyText.gameObject.AddComponent<LayoutElement>();
                emptyLayout.preferredWidth = 560f;
                emptyLayout.preferredHeight = 72f;
                return;
            }

            for (var i = 0; i < pile.Count; i++)
            {
                var cardState = pile[i];
                var cardDef = Session.GetCardDefinition(cardState.cardId);
                if (cardDef != null)
                {
                    var cardObj = UiFactory.CreateCard(contentRoot, "PileCard_" + cardState.instanceId, cardDef, false, BattleMechanics.GetEffectiveEnergyCost(cardState, cardDef));
                    var layout = cardObj.AddComponent<LayoutElement>();
                    layout.preferredWidth = _pilePopupGrid.cellSize.x;
                    layout.preferredHeight = _pilePopupGrid.cellSize.y;
                    var rect = cardObj.GetComponent<RectTransform>();
                    rect.localScale = Vector3.one;
                    continue;
                }

                var fallback = UiFactory.CreateText(contentRoot, "PileCardFallback_" + i, 18, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 40f));
                fallback.text = cardState.cardId;
                fallback.color = Color.white;
                var fallbackLayout = fallback.gameObject.AddComponent<LayoutElement>();
                fallbackLayout.preferredWidth = _pilePopupGrid.cellSize.x;
                fallbackLayout.preferredHeight = _pilePopupGrid.cellSize.y;
            }
        }

        private void BeginCardDrag(BattleHandCardDragView view, string cardInstanceId, CardDefinition cardDef, PointerEventData eventData)
        {
            if (!Session.CanLocalPlayerAct())
            {
                return;
            }

            _draggingCardDefinition = cardDef;
            _draggingCardInstanceId = cardInstanceId;
            _draggingCardView = view;
            view.FollowMouse = false;
            view.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -320f);
            _effectTargetHintText.text = BuildEffectsTargetHint(cardDef);

            UpdateDragCurve(view, eventData.position);
            ApplyHighlight(cardDef, null, null, null);
        }

        private void UpdateCardDrag(BattleHandCardDragView view, PointerEventData eventData)
        {
            if (_draggingCardDefinition == null || _lastState == null)
            {
                return;
            }

            UpdateDragCurve(view, eventData.position);
            FindHoveredTargetsAtScreenPosition(view, eventData.position, out var hoveredPlayer, out var hoveredPart, out var hoveredArea);
            ApplyHighlight(_draggingCardDefinition, hoveredPlayer, hoveredPart, hoveredArea);
        }

        private void EndCardDrag(BattleHandCardDragView view, PointerEventData eventData)
        {
            HideDragGuide();

            if (_draggingCardDefinition == null || string.IsNullOrEmpty(_draggingCardInstanceId))
            {
                return;
            }

            var localPlayer = Session.GetLocalBattlePlayer();
            if (localPlayer == null || !HasCardInHand(localPlayer, _draggingCardInstanceId))
            {
                ClearHighlights();
                _draggingCardDefinition = null;
                _draggingCardInstanceId = null;
                _effectTargetHintText.text = string.Empty;
                return;
            }

            if (!TryParseTargetType(_draggingCardDefinition.targetType, out var targetType))
            {
                ClearHighlights();
                _draggingCardDefinition = null;
                _draggingCardInstanceId = null;
                _draggingCardView = null;
                _effectTargetHintText.text = string.Empty;
                return;
            }
            FindHoveredTargetsAtScreenPosition(view, eventData.position, out var hoveredPlayer, out var hoveredPart, out var hoveredArea);

            if (targetType == BattleTargetType.Area)
            {
                if (hoveredArea != null
                    && BattleTargetingRules.CanTargetArea(_lastState, localPlayer, _draggingCardDefinition, hoveredArea.Area))
                {
                    Session.PlayCard(_draggingCardInstanceId, BattleTargetFaction.None, string.Empty, hoveredArea.Area);
                }
            }
            else if (targetType == BattleTargetType.MonsterPart || targetType == BattleTargetType.SingleAlly ||
                targetType == BattleTargetType.AllAllies || targetType == BattleTargetType.Self ||
                targetType == BattleTargetType.OtherAlly || targetType == BattleTargetType.SingleUnit ||
                targetType == BattleTargetType.AllMonsterParts)
            {
                if (IsValidPlayerTarget(_draggingCardDefinition, targetType, hoveredPlayer) || IsValidPartTarget(_draggingCardDefinition, targetType, hoveredPart))
                {
                    var isAoe = (targetType == BattleTargetType.AllMonsterParts || targetType == BattleTargetType.AllAllies);
                    var unitId = string.Empty;
                    var faction = BattleTargetFaction.None;
                    if (!isAoe)
                    {
                        if (hoveredPart != null && IsValidPartTarget(_draggingCardDefinition, targetType, hoveredPart))
                        {
                            unitId = hoveredPart.InstanceId;
                            faction = BattleTargetFaction.Enemies;
                        }
                        else if (hoveredPlayer != null && IsValidPlayerTarget(_draggingCardDefinition, targetType, hoveredPlayer))
                        {
                            unitId = hoveredPlayer.UnitId;
                            faction = BattleTargetFaction.Allies;
                        }
                    }
                    else
                    {
                        faction = targetType == BattleTargetType.AllAllies ? BattleTargetFaction.Allies : BattleTargetFaction.Enemies;
                    }
                    Session.PlayCard(_draggingCardInstanceId, faction, unitId, BattleArea.Middle);
                    ClearHighlights();
                    _draggingCardDefinition = null;
                    _draggingCardInstanceId = null;
                    _draggingCardView = null;
                    _effectTargetHintText.text = string.Empty;
                    return;
                }
            }

            ClearHighlights();
            _draggingCardDefinition = null;
            _draggingCardInstanceId = null;
            _draggingCardView = null;
            _effectTargetHintText.text = string.Empty;
        }

        private void ApplyHighlight(CardDefinition cardDef, BattleUnitSlotView hoveredPlayer, MonsterPartSlotView hoveredPart, BattleAreaDropZoneView hoveredArea)
        {
            if (_lastState == null)
            {
                return;
            }

            if (!TryParseTargetType(cardDef.targetType, out var targetType))
            {
                targetType = BattleTargetType.None;
            }
            for (var i = 0; i < _playerSlots.Count; i++)
            {
                var slot = _playerSlots[i];
                var player = _lastState.GetPlayer(slot.UnitId);
                var secretSummary = BuildPlayerSecretSummary(player, _isDetailedStatusMode);
                slot.SetData(BattleTargetFaction.Allies, slot.UnitId, player.displayName, player.hp, player.maxHp, player.armor, player.attackChargeStage, player.nextAttackBonus, player.vulnerableStacks, player.statuses, player.threatValue, player.threatTier, secretSummary, _isDetailedStatusMode, GetHighlightModeForPlayer(cardDef, targetType, slot, hoveredPlayer));
            }

            for (var i = 0; i < _monsterPartSlots.Count; i++)
            {
                var slot = _monsterPartSlots[i];
                var part = _lastState.GetPart(slot.InstanceId);
                if (part == null)
                {
                    continue;
                }

                slot.SetData(part, _isDetailedStatusMode, GetHighlightModeForPart(cardDef, targetType, slot, hoveredPart));
            }

            for (var i = 0; i < _areaDropZones.Count; i++)
            {
                var zone = _areaDropZones[i];
                var mode = GetHighlightModeForArea(cardDef, targetType, zone, hoveredArea);
                zone.SetHighlight(mode);
            }
        }

        private SlotHighlightMode GetHighlightModeForArea(CardDefinition cardDef, BattleTargetType targetType, BattleAreaDropZoneView zone, BattleAreaDropZoneView hoveredArea)
        {
            if (targetType != BattleTargetType.Area || _lastState == null) return SlotHighlightMode.None;
            
            var localPlayer = _lastState.GetPlayer(Session.LocalPlayerId);
            if (localPlayer == null) return SlotHighlightMode.None;

            if (!BattleTargetingRules.CanTargetArea(_lastState, localPlayer, cardDef, zone.Area))
            {
                return SlotHighlightMode.None;
            }

            return hoveredArea == zone ? SlotHighlightMode.Selected : SlotHighlightMode.Potential;
        }

        private SlotHighlightMode GetHighlightModeForPlayer(CardDefinition cardDef, BattleTargetType targetType, BattleUnitSlotView slot, BattleUnitSlotView hoveredSlot)
        {
            if (!slot.IsAlive || !IsValidPlayerTarget(cardDef, targetType, slot))
            {
                return SlotHighlightMode.None;
            }

            bool isSelected = false;
            switch (targetType)
            {
                case BattleTargetType.Self:
                case BattleTargetType.SingleAlly:
                case BattleTargetType.OtherAlly:
                case BattleTargetType.SingleUnit:
                    isSelected = (hoveredSlot == slot);
                    break;
                case BattleTargetType.AllAllies:
                    isSelected = (hoveredSlot != null && IsValidPlayerTarget(cardDef, targetType, hoveredSlot));
                    break;
            }

            return isSelected ? SlotHighlightMode.Selected : SlotHighlightMode.Potential;
        }

        private SlotHighlightMode GetHighlightModeForPart(CardDefinition cardDef, BattleTargetType targetType, MonsterPartSlotView slot, MonsterPartSlotView hoveredSlot)
        {
            if (!IsValidPartTarget(cardDef, targetType, slot))
            {
                return SlotHighlightMode.None;
            }

            bool isSelected = false;
            switch (targetType)
            {
                case BattleTargetType.MonsterPart:
                case BattleTargetType.SingleUnit:
                    isSelected = (hoveredSlot == slot);
                    break;
                case BattleTargetType.AllMonsterParts:
                    isSelected = (hoveredSlot != null && IsValidPartTarget(cardDef, targetType, hoveredSlot));
                    break;
            }

            return isSelected ? SlotHighlightMode.Selected : SlotHighlightMode.Potential;
        }


        private bool IsValidPlayerTarget(CardDefinition cardDef, BattleTargetType targetType, BattleUnitSlotView slot)
        {
            if (slot == null || !slot.IsAlive || _lastState == null)
            {
                return false;
            }

            var localPlayer = _lastState.GetPlayer(Session.LocalPlayerId);
            var targetPlayer = _lastState.GetPlayer(slot.UnitId);
            return BattleTargetingRules.CanTargetPlayer(_lastState, localPlayer, cardDef, targetType, targetPlayer);
        }

        private bool IsValidPartTarget(CardDefinition cardDef, BattleTargetType targetType, MonsterPartSlotView slot)
        {
            if (slot == null || _lastState == null)
            {
                return false;
            }

            var localPlayer = _lastState.GetPlayer(Session.LocalPlayerId);
            var targetPart = _lastState.GetPart(slot.InstanceId);
            return BattleTargetingRules.CanTargetPart(_lastState, localPlayer, cardDef, targetType, targetPart);
        }

        private void ClearHighlights()
        {
            if (_lastState == null)
            {
                return;
            }

            RenderBoard(_lastState);
            for (var i = 0; i < _areaDropZones.Count; i++)
            {
                _areaDropZones[i].SetHighlight(SlotHighlightMode.None);
            }
        }

        private void FindHoveredTargetsAtScreenPosition(BattleHandCardDragView view, Vector2 screenPosition, out BattleUnitSlotView hoveredPlayer, out MonsterPartSlotView hoveredPart, out BattleAreaDropZoneView hoveredArea)
        {
            hoveredPlayer = null;
            hoveredPart = null;
            hoveredArea = null;

            var raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                return;
            }

            var results = RaycastAtScreenPosition(screenPosition, raycaster);
            for (var i = 0; i < results.Count; i++)
            {
                var hitObject = results[i].gameObject;
                if (hitObject == view.gameObject || hitObject.transform.IsChildOf(view.transform))
                {
                    continue;
                }

                if (hoveredPlayer == null)
                {
                    hoveredPlayer = FindHoveredPlayerSlot(hitObject);
                }

                if (hoveredPart == null)
                {
                    hoveredPart = FindHoveredPartSlot(hitObject);
                }

                if (hoveredArea == null)
                {
                    hoveredArea = FindHoveredAreaDropZone(hitObject);
                }

                if (hoveredPlayer != null && hoveredPart != null && hoveredArea != null)
                {
                    break;
                }
            }
        }

        private void UpdateDragCurve(BattleHandCardDragView view, Vector2 pointerScreenPosition)
        {
            if (_cardDragGuideRoot == null)
            {
                return;
            }

            _cardDragGuideRoot.gameObject.SetActive(true);
            _cardDragGuideRoot.SetAsLastSibling();

            var eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_cardDragGuideRoot, view.GetTopCenterScreenPoint(), eventCamera, out var startLocal)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_cardDragGuideRoot, pointerScreenPosition, eventCamera, out var endLocal))
            {
                HideDragGuide();
                return;
            }

            var delta = endLocal - startLocal;
            if (delta.sqrMagnitude < 16f)
            {
                HideDragGuide();
                return;
            }

            var control = startLocal + new Vector2(0f, Mathf.Max(90f, Mathf.Abs(delta.y) * 0.38f));
            var segmentCount = _cardDragGuideSegments.Count;
            var prev = EvaluateQuadratic(startLocal, control, endLocal, 0f);
            for (var i = 0; i < segmentCount; i++)
            {
                var t = (i + 1) / (float)segmentCount;
                var next = EvaluateQuadratic(startLocal, control, endLocal, t);
                var seg = _cardDragGuideSegments[i];
                ConfigureGuideSegment(seg, prev, next, 8f);
                prev = next;
            }

            var tangent = EvaluateQuadraticTangent(startLocal, control, endLocal, 1f);
            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = delta.normalized;
            }
            else
            {
                tangent = tangent.normalized;
            }

            ConfigureGuideArrow(endLocal, tangent, 20f, 14f);
        }

        private void HideDragGuide()
        {
            if (_cardDragGuideRoot == null)
            {
                return;
            }

            for (var i = 0; i < _cardDragGuideSegments.Count; i++)
            {
                _cardDragGuideSegments[i].gameObject.SetActive(false);
            }

            if (_cardDragGuideArrow != null)
            {
                _cardDragGuideArrow.gameObject.SetActive(false);
            }

            _cardDragGuideRoot.gameObject.SetActive(false);
        }

        private static void ConfigureGuideSegment(RectTransform segment, Vector2 from, Vector2 to, float thickness)
        {
            if (segment == null)
            {
                return;
            }

            var dir = to - from;
            var len = dir.magnitude;
            if (len < 0.5f)
            {
                segment.gameObject.SetActive(false);
                return;
            }

            segment.gameObject.SetActive(true);
            segment.anchoredPosition = (from + to) * 0.5f;
            segment.sizeDelta = new Vector2(len, thickness);
            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            segment.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void ConfigureGuideArrow(Vector2 tip, Vector2 tangent, float length, float width)
        {
            if (_cardDragGuideArrow == null)
            {
                return;
            }

            _cardDragGuideArrow.gameObject.SetActive(true);
            _cardDragGuideArrow.anchoredPosition = tip - tangent * (length * 0.5f);
            _cardDragGuideArrow.sizeDelta = new Vector2(length, width);
            var angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            _cardDragGuideArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static Vector2 EvaluateQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            var inv = 1f - t;
            return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
        }

        private static Vector2 EvaluateQuadraticTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        }

        private List<RaycastResult> RaycastAtScreenPosition(Vector2 screenPosition, GraphicRaycaster raycaster)
        {
            var pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = screenPosition;
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);
            return results;
        }

        private BattleUnitSlotView FindHoveredPlayerSlot(GameObject hoveredObject)
        {
            while (hoveredObject != null)
            {
                var slot = hoveredObject.GetComponent<BattleUnitSlotView>();
                if (slot != null)
                {
                    return slot;
                }

                hoveredObject = hoveredObject.transform.parent == null ? null : hoveredObject.transform.parent.gameObject;
            }

            return null;
        }

        private MonsterPartSlotView FindHoveredPartSlot(GameObject hoveredObject)
        {
            while (hoveredObject != null)
            {
                var slot = hoveredObject.GetComponent<MonsterPartSlotView>();
                if (slot != null)
                {
                    return slot;
                }

                hoveredObject = hoveredObject.transform.parent == null ? null : hoveredObject.transform.parent.gameObject;
            }

            return null;
        }

        private BattleAreaDropZoneView FindHoveredAreaDropZone(GameObject hoveredObject)
        {
            while (hoveredObject != null)
            {
                var slot = hoveredObject.GetComponent<BattleAreaDropZoneView>();
                if (slot != null)
                {
                    return slot;
                }

                hoveredObject = hoveredObject.transform.parent == null ? null : hoveredObject.transform.parent.gameObject;
            }

            return null;
        }

        private BattleUnitSlotView CreateUnitSlot(Transform parent, BattleTargetFaction faction, string unitId, string name, int hp, int maxHp, int armor, int charge, int bonus, int vulnerableStacks, List<BattleStatusState> statuses, int threatValue, int threatTier, string secretSummary, Color color, bool detailedMode, SlotHighlightMode highlightMode)
        {
            var slotObject = new GameObject("UnitSlot_" + unitId);
            slotObject.transform.SetParent(parent, false);
            var rect = slotObject.AddComponent<RectTransform>();
            
            // Horizon Anchor
            rect.anchorMin = new Vector2(0.5f, 0.3f);
            rect.anchorMax = new Vector2(0.5f, 0.3f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(142f, 188f);

            var bg = slotObject.AddComponent<Image>();
            bg.color = color;
            bg.type = Image.Type.Sliced;

            // Info Container
            var infoBase = new GameObject("Info");
            infoBase.transform.SetParent(slotObject.transform, false);
            var infoRect = infoBase.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, -0.92f);
            infoRect.anchorMax = new Vector2(1f, -0.05f);
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;

            // Name
            var nameLabel = UiFactory.CreateText(infoBase.transform, "Name", 20, TextAnchor.MiddleCenter, new Vector2(0f, 0.65f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            nameLabel.fontStyle = FontStyle.Bold;

            // Threat bar
            var threatBase = UiFactory.CreatePanel(infoBase.transform, "ThreatBar", new Color(0.08f, 0.1f, 0.12f, 0.92f), new Vector2(0f, 0.39f), new Vector2(1f, 0.62f), Vector2.zero, Vector2.zero);
            threatBase.GetComponent<Image>().raycastTarget = false;
            var threatLabel = UiFactory.CreateText(threatBase.transform, "ThreatLabel", 12, TextAnchor.MiddleCenter, new Vector2(0f, 0.54f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            threatLabel.fontStyle = FontStyle.Bold;
            threatLabel.raycastTarget = false;
            var threatSegmentFills = new Image[3];
            var threatSegmentLabels = new Text[3];
            for (var i = 0; i < 3; i++)
            {
                var segment = UiFactory.CreatePanel(threatBase.transform, "Segment_" + i, new Color(0.14f, 0.16f, 0.19f, 0.95f), new Vector2(i / 3f, 0.07f), new Vector2((i + 1) / 3f, 0.46f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
                segment.GetComponent<Image>().raycastTarget = false;
                var fill = UiFactory.CreatePanel(segment.transform, "Fill", new Color(0.2f, 0.8f, 0.3f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var fillImage = fill.GetComponent<Image>();
                fillImage.raycastTarget = false;
                threatSegmentFills[i] = fillImage;
                var segmentLabel = UiFactory.CreateText(segment.transform, "Label", 10, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                segmentLabel.fontStyle = FontStyle.Bold;
                segmentLabel.raycastTarget = false;
                threatSegmentLabels[i] = segmentLabel;
            }

            // HP Bar
            var barColor = faction == BattleTargetFaction.Allies ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.8f, 0.2f, 0.2f);
            var (hpBarBg, hpFill) = UiFactory.CreateProgressBar(infoBase.transform, "HPBar", barColor, new Vector2(110f, 14f));
            hpBarBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -22f);

            var hpLabel = UiFactory.CreateText(hpBarBg.transform, "HPNumeric", 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hpLabel.color = Color.white;

            // Status (Charge/Bonus)
            var statusLabel = UiFactory.CreateText(infoBase.transform, "Status", 16, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0.22f), Vector2.zero, Vector2.zero);
            statusLabel.fontStyle = FontStyle.Bold;
            statusLabel.color = new Color(1f, 0.8f, 0.2f);

            // Armor
            var armorLabel = UiFactory.CreateText(slotObject.transform, "Armor", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0.7f), new Vector2(0f, 0.7f), Vector2.zero, Vector2.zero);
            var armorRect = armorLabel.GetComponent<RectTransform>();
            armorRect.anchoredPosition = new Vector2(70f, 0f);
            armorRect.sizeDelta = new Vector2(130f, 28f);
            armorLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            armorLabel.verticalOverflow = VerticalWrapMode.Overflow;
            armorLabel.fontStyle = FontStyle.Bold;
            armorLabel.color = new Color(0.6f, 0.8f, 1f);

            // Highlight
            var borderObj = UiFactory.CreatePanel(slotObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 1f), Vector2.zero, Vector2.one, new Vector2(-5f, -5f), new Vector2(5f, 5f));
            borderObj.transform.SetAsFirstSibling();
            var highlightImage = borderObj.GetComponent<Image>();

            var slotView = slotObject.AddComponent<BattleUnitSlotView>();
            slotView.Initialize(bg, nameLabel, hpLabel, hpFill, armorLabel, statusLabel, threatBase.GetComponent<RectTransform>(), threatSegmentFills, threatSegmentLabels, threatLabel, color, highlightImage);
            slotView.SetData(faction, unitId, name, hp, maxHp, armor, charge, bonus, vulnerableStacks, statuses, threatValue, threatTier, secretSummary, detailedMode, highlightMode);
            return slotView;
        }

        private MonsterPartSlotView CreateMonsterPartSlot(Transform parent, MonsterPartState part, Rect panelRect, BattleFacing facing, BattleStance stance, SlotHighlightMode highlightMode)
        {
            var slotObject = new GameObject("MonsterPart_" + part.partId);
            slotObject.transform.SetParent(parent, false);
            var rect = slotObject.AddComponent<RectTransform>();
            
            // Horizon Anchor: Monster center is standing on the horizon
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.3f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var size = ResolvePartSize(part);
            rect.sizeDelta = size;

            var position = ResolvePartPosition(part, panelRect, facing, stance);

            var image = slotObject.AddComponent<Image>();
            image.sprite = GetPartSprite(part.shape);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;

            // Label Below Part
            var label = UiFactory.CreateText(slotObject.transform, "Label", 18, TextAnchor.MiddleCenter, new Vector2(0f, -0.5f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            label.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -20f);
            label.supportRichText = true;

            var borderObj = UiFactory.CreatePanel(slotObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 1f), Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            borderObj.transform.SetAsFirstSibling();
            var highlightImage = borderObj.GetComponent<Image>();

            var slotView = slotObject.AddComponent<MonsterPartSlotView>();
            slotView.Initialize(image, label, highlightImage);
            slotView.SetTargetPosition(position, snapImmediately: true);
            slotView.SetData(part, _isDetailedStatusMode, highlightMode);
            return slotView;
        }

        private void ClearMonsterPartViews()
        {
            foreach (var pair in _monsterPartSlotsByInstanceId)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.Destroy(pair.Value.gameObject);
                }
            }

            _monsterPartSlotsByInstanceId.Clear();
            _monsterPartSlots.Clear();
        }

        private static Vector2 ResolvePartSize(MonsterPartState part)
        {
            if (string.Equals(part.shape, "Circle", System.StringComparison.OrdinalIgnoreCase) && part.radius > 0f)
            {
                var diameter = part.radius * 2f;
                return new Vector2(diameter, diameter);
            }

            if (part.width > 0f && part.height > 0f)
            {
                return new Vector2(part.width, part.height);
            }

            return new Vector2(120f, 90f);
        }

        private static Vector2 ResolvePartPosition(MonsterPartState part, Rect panelRect, BattleFacing facing, BattleStance stance)
        {
            const float scale = 0.45f;
            const float verticalLiftRatio = 0.12f;
            var x = part.offsetX * panelRect.width * scale;
            var y = part.offsetY * panelRect.height * scale + panelRect.height * verticalLiftRatio;

            if (facing == BattleFacing.West)
            {
                x = -x;
            }

            // Keep monster body above the horizon/info area.
            var minAboveHorizon = stance == BattleStance.Prone ? panelRect.height * 0.01f : panelRect.height * 0.02f;
            y = Mathf.Max(y, minAboveHorizon);

            return new Vector2(x, y);
        }

        private static string BuildMonsterActionHint(MonsterBattleState monster)
        {
            if (monster == null)
            {
                return string.Empty;
            }

            var pose = string.IsNullOrEmpty(monster.currentPoseId) ? "待机" : monster.currentPoseId;
            if (monster.hasActiveSkill && monster.activeSkill != null)
            {
                var windup = monster.activeSkill.remainingWindup > 0 ? $"（蓄力{monster.activeSkill.remainingWindup}）" : "";
                return $"动作：{monster.activeSkill.displayName}{windup}  |  姿态：{pose}";
            }

            return $"动作：待机  |  姿态：{pose}";
        }

        private static string BuildMonsterStatusesText(List<BattleStatusState> statuses, bool detailedMode)
        {
            if (statuses == null || statuses.Count == 0)
            {
                return "<color=#9ca3af>状态：无</color>";
            }

            var parts = new List<string>();
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null || status.stacks <= 0 || string.IsNullOrWhiteSpace(status.id))
                {
                    continue;
                }

                if (string.Equals(status.id, "Poison", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add((detailedMode ? "中毒" : "☠") + status.stacks);
                }
                else if (string.Equals(status.id, "Strength", StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add((detailedMode ? "力量" : "💪") + status.stacks);
                }
                else
                {
                    parts.Add(status.id + ":" + status.stacks);
                }
            }

            if (parts.Count == 0)
            {
                return "<color=#9ca3af>状态：无</color>";
            }

            return "<color=#cbd5e1>状态：" + string.Join(" ", parts) + "</color>";
        }

        private static string BuildPlayerSecretSummary(PlayerBattleState player, bool detailedMode)
        {
            if (player == null)
            {
                return string.Empty;
            }

            if (player.statuses == null || player.statuses.Count == 0)
            {
                return string.Empty;
            }

            var secretParts = new List<string>();
            for (var i = 0; i < player.statuses.Count; i++)
            {
                var status = player.statuses[i];
                if (status == null || status.stacks <= 0 || !IsSecretStatusId(status.id))
                {
                    continue;
                }

                var label = ResolveSecretDisplayName(status.id, detailedMode);
                secretParts.Add(label + "x" + status.stacks);
            }

            if (secretParts.Count == 0)
            {
                return string.Empty;
            }

            return detailedMode
                ? "奥秘[" + string.Join(" ", secretParts) + "]"
                : "奥[" + string.Join(" ", secretParts) + "]";
        }

        private static bool IsSecretStatusId(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
            {
                return false;
            }

            return statusId.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0
                || statusId.IndexOf("奥秘", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveSecretDisplayName(string statusId, bool detailedMode)
        {
            if (string.IsNullOrWhiteSpace(statusId))
            {
                return detailedMode ? "奥秘" : "奥";
            }

            if (string.Equals(statusId, BattleStatusSystem.SecretGuardStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "护卫" : "护";
            }

            if (string.Equals(statusId, BattleStatusSystem.SecretCounterattackStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "反制" : "反";
            }

            if (string.Equals(statusId, BattleStatusSystem.SecretSidestepOnHitStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "切边" : "切";
            }

            return statusId;
        }

        private static string BuildEffectsTargetHint(CardDefinition cardDef)
        {
            if (cardDef == null || cardDef.effects == null || cardDef.effects.Length == 0)
            {
                return string.Empty;
            }

            bool hasCardTarget = false;
            bool hasSelf = false;
            bool hasAllEnemies = false;
            bool hasAllAllies = false;
            bool hasNone = false;
            bool hasOther = false;

            for (var i = 0; i < cardDef.effects.Length; i++)
            {
                var effect = cardDef.effects[i];
                if (effect == null)
                {
                    continue;
                }

                var target = string.IsNullOrWhiteSpace(effect.target) ? "CardTarget" : effect.target;
                if (string.Equals(target, "CardTarget", StringComparison.OrdinalIgnoreCase))
                {
                    hasCardTarget = true;
                }
                else if (string.Equals(target, "Self", StringComparison.OrdinalIgnoreCase))
                {
                    hasSelf = true;
                }
                else if (string.Equals(target, "AllEnemies", StringComparison.OrdinalIgnoreCase))
                {
                    hasAllEnemies = true;
                }
                else if (string.Equals(target, "AllAllies", StringComparison.OrdinalIgnoreCase))
                {
                    hasAllAllies = true;
                }
                else if (string.Equals(target, "None", StringComparison.OrdinalIgnoreCase))
                {
                    hasNone = true;
                }
                else
                {
                    hasOther = true;
                }
            }

            var parts = new List<string>();
            if (hasCardTarget) parts.Add("需指向卡牌目标");
            if (hasSelf) parts.Add("包含自身效果");
            if (hasAllEnemies) parts.Add("包含全敌方效果");
            if (hasAllAllies) parts.Add("包含全友方效果");
            if (hasNone) parts.Add("包含无目标效果");
            if (hasOther) parts.Add("包含特殊目标效果");

            return parts.Count == 0 ? string.Empty : "目标提示：" + string.Join("，", parts);
        }

        private static Sprite GetPartSprite(string shape)
        {
            // Builtin resources like "UI/Skin/Knob.psd" often fail to load in runtime environments
            // returning null here is safe; Unity will use the default white texture for the Image.
            return null;
        }

        private static string DescribeCard(CardDefinition cardDef)
        {
            var distance = string.IsNullOrEmpty(cardDef.rangeDistance) ? cardDef.rangeZones : cardDef.rangeDistance;
            var range = string.IsNullOrEmpty(cardDef.rangeHeights) && string.IsNullOrEmpty(distance)
                ? ""
                : "\nRange: " + (string.IsNullOrEmpty(distance) ? "" : distance + " ") + (string.IsNullOrEmpty(cardDef.rangeHeights) ? "" : cardDef.rangeHeights);
            var effectText = "None";
            if (cardDef.effects != null && cardDef.effects.Length > 0)
            {
                var effectLines = new System.Text.StringBuilder();
                for (var i = 0; i < cardDef.effects.Length; i++)
                {
                    var effect = cardDef.effects[i];
                    if (effect == null || string.IsNullOrWhiteSpace(effect.op))
                    {
                        continue;
                    }

                    if (effectLines.Length > 0)
                    {
                        effectLines.Append(", ");
                    }

                    effectLines.Append(effect.op);
                    if (effect.amount != 0)
                    {
                        effectLines.Append(" ");
                        effectLines.Append(effect.amount);
                    }
                }

                if (effectLines.Length > 0)
                {
                    effectText = effectLines.ToString();
                }
            }

            return "Cost: " + cardDef.energyCost + "\nEffects: " + effectText + "\nTarget: " + cardDef.targetType + range;
        }

        private static bool TryParseTargetType(string raw, out BattleTargetType targetType)
        {
            targetType = BattleTargetType.None;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            return System.Enum.TryParse(raw, true, out targetType);
        }

        private List<BattleCardState> GetCurrentPile(PlayerBattleState player)
        {
            switch (_currentPileView)
            {
                case PileView.Draw:
                    return player.drawPile;
                case PileView.Discard:
                    return player.discardPile;
                case PileView.Exhaust:
                    return player.exhaustPile;
                default:
                    return player.drawPile;
            }
        }

        private string BuildPileTitle(int count)
        {
            switch (_currentPileView)
            {
                case PileView.Draw:
                    return $"牌库 ({count})";
                case PileView.Discard:
                    return $"弃牌堆 ({count})";
                case PileView.Exhaust:
                    return $"消耗堆 ({count})";
                default:
                    return $"卡堆 ({count})";
            }
        }

        private void ToggleStatusMode()
        {
            _isDetailedStatusMode = !_isDetailedStatusMode;
            _statusModeButton.GetComponentInChildren<Text>().text = _isDetailedStatusMode ? "模式: 详" : "模式: 简";
            if (_lastState != null)
            {
                RenderBoard(_lastState);
            }
        }

        private void ToggleQuickChatWheel()
        {
            if (_quickChatMask == null)
            {
                return;
            }

            if (_quickChatMask.activeSelf)
            {
                CloseQuickChatWheel();
            }
            else
            {
                OpenQuickChatWheel();
            }
        }

        private void OpenQuickChatWheel()
        {
            if (_quickChatMask == null)
            {
                return;
            }

            _quickChatMask.transform.SetAsLastSibling();
            _quickChatMask.SetActive(true);
            _chatButtonLabel.text = "收起";
        }

        private void CloseQuickChatWheel()
        {
            if (_quickChatMask == null)
            {
                return;
            }

            _quickChatMask.SetActive(false);
            _chatButtonLabel.text = "聊天";
        }

        private void SendQuickChat(string presetId)
        {
            Session.SendQuickChat(presetId);
            CloseQuickChatWheel();
        }

        private GameObject BuildQuickChatWheel(Transform parent)
        {
            var mask = UiFactory.CreatePanel(parent, "QuickChatMask", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backgroundButton = mask.AddComponent<Button>();
            var backgroundImage = mask.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.55f);
            backgroundButton.targetGraphic = backgroundImage;
            backgroundButton.onClick.AddListener(CloseQuickChatWheel);

            var wheel = UiFactory.CreatePanel(mask.transform, "QuickChatWheel", new Color(0.12f, 0.15f, 0.19f, 0.98f), new Vector2(0.16f, 0.32f), new Vector2(0.84f, 0.64f), Vector2.zero, Vector2.zero);
            var wheelRect = wheel.GetComponent<RectTransform>();
            wheelRect.anchorMin = new Vector2(0.16f, 0.32f);
            wheelRect.anchorMax = new Vector2(0.84f, 0.64f);
            wheelRect.offsetMin = Vector2.zero;
            wheelRect.offsetMax = Vector2.zero;

            var title = UiFactory.CreateText(wheel.transform, "Title", 24, TextAnchor.MiddleCenter, new Vector2(0f, 0.72f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            title.text = "选择快捷语";
            title.fontStyle = FontStyle.Bold;

            CreateQuickChatWheelButton(wheel.transform, "Top", "打得不错", new Vector2(0f, 150f), "good_play");
            CreateQuickChatWheelButton(wheel.transform, "Left", "抱歉", new Vector2(-180f, 0f), "sorry");
            CreateQuickChatWheelButton(wheel.transform, "Right", "谢谢你", new Vector2(180f, 0f), "thanks");
            CreateQuickChatWheelButton(wheel.transform, "Bottom", "救我!", new Vector2(0f, -150f), "help");

            var cancel = UiFactory.CreateButton(wheel.transform, "Cancel", "取消", CloseQuickChatWheel);
            var cancelRect = cancel.GetComponent<RectTransform>();
            cancelRect.anchorMin = new Vector2(0.38f, 0.04f);
            cancelRect.anchorMax = new Vector2(0.62f, 0.18f);
            cancelRect.offsetMin = Vector2.zero;
            cancelRect.offsetMax = Vector2.zero;
            cancel.image.color = new Color(0.34f, 0.25f, 0.24f, 0.95f);

            return mask;
        }

        private void CreateQuickChatWheelButton(Transform parent, string name, string label, Vector2 anchoredPosition, string presetId)
        {
            var button = UiFactory.CreateButton(parent, name, label, anchoredPosition, () => SendQuickChat(presetId));
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(180f, 72f);
            button.image.color = new Color(0.22f, 0.34f, 0.48f, 0.98f);
        }

        private void CreateAreaDropZone(Transform parent, string name, BattleArea area, Vector2 anchorMin, Vector2 anchorMax)
        {
            var zoneObject = new GameObject(name);
            zoneObject.transform.SetParent(parent, false);
            var rect = zoneObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = zoneObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var border = UiFactory.CreatePanel(zoneObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 0.4f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            border.transform.SetAsFirstSibling();
            var borderImage = border.GetComponent<Image>();
            borderImage.raycastTarget = false;
            borderImage.enabled = false;

            var zoneView = zoneObject.AddComponent<BattleAreaDropZoneView>();
            zoneView.Initialize(image, borderImage, area);
            _areaDropZones.Add(zoneView);
        }

        private static void ClearContainer(Transform container)
        {
            for (var i = container.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
            }
        }

        private static bool HasCardInHand(PlayerBattleState player, string cardInstanceId)
        {
            if (player == null || string.IsNullOrEmpty(cardInstanceId))
            {
                return false;
            }

            for (var i = 0; i < player.hand.Count; i++)
            {
                if (player.hand[i].instanceId == cardInstanceId)
                {
                    return true;
                }
            }

            return false;
        }
    }

}
