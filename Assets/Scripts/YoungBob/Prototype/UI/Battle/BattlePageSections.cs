using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleTopBarSection
    {
        private readonly BattleTopBarView _view;

        public Button StatusModeButton => _view.StatusModeButton;
        public Button ExitBattleButton => _view.ExitBattleButton;

        public BattleTopBarSection(Transform parent, Action toggleStatusMode, Action exitBattle)
        {
            _view = new BattleTopBarView(parent, () => toggleStatusMode(), () => exitBattle());
        }

        public void Render(
            BattleState state,
            PlayerBattleState localPlayer,
            string localPlayerId,
            Func<BattlePhase, string> resolvePhaseTitle,
            Func<BattlePhase, string> resolvePhaseColor,
            Func<string, string> resolvePlayerMarker)
        {
            if (state == null)
            {
                _view.SummaryText.text = "等待战斗开始...";
                _view.PlayerMarkerText.text = "●";
                _view.PlayerNameText.text = "等待接入";
                _view.PlayerHpText.text = string.Empty;
                return;
            }

            var encounterTotal = state.stageEncounterIds == null ? 0 : state.stageEncounterIds.Length;
            var mapText = string.IsNullOrWhiteSpace(state.stageName) ? state.stageId : state.stageName;
            var phaseColor = resolvePhaseColor(state.phase);
            var prompt = BattleUiTextMapper.GetTopBarPrompt(state);
            var progressText = encounterTotal > 0 ? state.stageEncounterIndex + "/" + encounterTotal : "0/0";
            _view.SummaryText.text = $"<i><color=#C6D8E6>{mapText}</color></i> <color=#D7DCE2>{progressText}</color>\n第 {state.turnIndex} 回合 · <color={phaseColor}>{prompt}</color>";
            _view.PlayerMarkerText.text = resolvePlayerMarker(localPlayerId);
            _view.PlayerNameText.text = localPlayer == null ? "未入场" : localPlayer.displayName;
            _view.PlayerHpText.text = localPlayer == null ? string.Empty : $"生命 {localPlayer.hp} / {localPlayer.maxHp}";
        }
    }

    internal sealed class BattleLogSection
    {
        private const float LogBottomThreshold = 0.01f;

        private readonly BattleLogPanelView _view;
        private readonly List<string> _battleLogs = new List<string>();
        private bool _isUserScrolling;

        public BattleLogSection(Transform parent)
        {
            _view = new BattleLogPanelView(parent);
            _view.LogScrollRect.onValueChanged.AddListener(_ => OnLogScrollChanged());
            _view.JumpToLatestButton.onClick.RemoveAllListeners();
            _view.JumpToLatestButton.onClick.AddListener(() =>
            {
                _view.LogScrollRect.verticalNormalizedPosition = 0f;
                _isUserScrolling = false;
                _view.JumpToLatestButton.gameObject.SetActive(false);
            });
        }

        public void Reset()
        {
            _battleLogs.Clear();
            _view.BattleLogText.text = string.Empty;
            _isUserScrolling = false;
            RefreshJumpToLatestButton();
        }

        public void Append(string message)
        {
            _battleLogs.Add(message);
            if (_battleLogs.Count > 100)
            {
                _battleLogs.RemoveAt(0);
            }

            _view.BattleLogText.text = string.Join("\n\n", _battleLogs);
            if (!_isUserScrolling)
            {
                Canvas.ForceUpdateCanvases();
                _view.LogScrollRect.verticalNormalizedPosition = 0f;
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
            if (_view.LogScrollRect == null || _view.LogScrollRect.content == null || _view.LogScrollRect.viewport == null)
            {
                return true;
            }

            var contentHeight = _view.LogScrollRect.content.rect.height;
            var viewportHeight = _view.LogScrollRect.viewport.rect.height;
            if (contentHeight <= viewportHeight + 0.5f)
            {
                return true;
            }

            return _view.LogScrollRect.verticalNormalizedPosition <= LogBottomThreshold;
        }

        private void RefreshJumpToLatestButton()
        {
            _view.JumpToLatestButton.gameObject.SetActive(_isUserScrolling && _battleLogs.Count > 0);
        }
    }

    internal sealed class BattlePhaseBannerSection
    {
        private readonly GameObject _mask;
        private readonly RectTransform _panelRect;
        private readonly Text _title;
        private readonly Text _detail;
        private readonly BattlePhaseBannerAnimator _animator;
        private BattlePhase _lastPhase = (BattlePhase)(-1);
        private string _overrideTitle;
        private string _overrideDetail;
        private float _overrideUntilTime;

        public BattlePhaseBannerSection(Transform parent)
        {
            _mask = UiFactory.CreatePanel(parent, "PhaseBannerMask", new Color(0f, 0f, 0f, 0f), new Vector2(0.3f, 0.922f), new Vector2(0.73f, 0.982f), Vector2.zero, Vector2.zero);
            _mask.transform.SetAsLastSibling();
            var rectMask = _mask.AddComponent<RectMask2D>();
            rectMask.padding = Vector4.zero;
            var panel = UiFactory.CreatePanel(_mask.transform, "PhaseBannerPanel", new Color(0.09f, 0.13f, 0.17f, 0.94f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _panelRect = panel.GetComponent<RectTransform>();
            _title = UiFactory.CreateText(panel.transform, "Title", 22, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
            _title.fontStyle = FontStyle.Bold;
            _title.supportRichText = true;
            _detail = UiFactory.CreateText(panel.transform, "Detail", 14, TextAnchor.MiddleCenter, new Vector2(0.1f, 0f), new Vector2(0.9f, 0f), Vector2.zero, Vector2.zero);
            _detail.supportRichText = true;
            _detail.gameObject.SetActive(false);
            _animator = _mask.AddComponent<BattlePhaseBannerAnimator>();
            _animator.Initialize(_mask.GetComponent<Image>(), panel.GetComponent<Image>(), _panelRect, _title, _detail);
            _mask.SetActive(false);
        }

        public void ShowTransientMessage(string title, string detail, float durationSeconds)
        {
            _overrideTitle = title;
            _overrideDetail = detail;
            _overrideUntilTime = Time.unscaledTime + Mathf.Max(0.1f, durationSeconds);
            _animator.Show(title, detail, true);
            _mask.transform.SetAsLastSibling();
        }

        public void Render(BattleState state, Func<BattlePhase, string> resolvePhaseTitle, Func<BattlePhase, string> resolvePhaseColor)
        {
            if (state == null)
            {
                _mask.SetActive(false);
                return;
            }

            if (!string.IsNullOrEmpty(_overrideTitle) && Time.unscaledTime <= _overrideUntilTime)
            {
                _animator.Show(_overrideTitle, _overrideDetail, false);
                _mask.transform.SetAsLastSibling();
                return;
            }

            if (!string.IsNullOrEmpty(_overrideTitle) && Time.unscaledTime > _overrideUntilTime)
            {
                _overrideTitle = null;
                _overrideDetail = null;
            }

            var showBanner = state.phase == BattlePhase.MonsterTurnStart
                || state.phase == BattlePhase.MonsterTurnResolve
                || state.phase == BattlePhase.PlayerTurnStart
                || state.phase == BattlePhase.Victory
                || state.phase == BattlePhase.Defeat;

            if (!showBanner)
            {
                _animator.Hide();
                return;
            }

            var title = $"<color={resolvePhaseColor(state.phase)}>{resolvePhaseTitle(state.phase)}</color>";
            var shouldReplay = state.phase != _lastPhase;
            _lastPhase = state.phase;
            _animator.Show(title, string.Empty, shouldReplay);
            _mask.transform.SetAsLastSibling();
        }
    }

    internal sealed class BattlePhaseBannerAnimator : MonoBehaviour
    {
        private Image _maskImage;
        private Image _panelImage;
        private RectTransform _panelRect;
        private Text _title;
        private Text _detail;
        private Image _sweepImage;
        private float _showUntilTime;
        private bool _visible;

        public void Initialize(Image maskImage, Image panelImage, RectTransform panelRect, Text title, Text detail)
        {
            _maskImage = maskImage;
            _panelImage = panelImage;
            _panelRect = panelRect;
            _title = title;
            _detail = detail;
            var sweepObj = UiFactory.CreatePanel(_panelRect.transform, "Sweep", new Color(0.92f, 0.97f, 1f, 0.14f), new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            _sweepImage = sweepObj.GetComponent<Image>();
            _sweepImage.raycastTarget = false;
            var sweepRect = sweepObj.GetComponent<RectTransform>();
            sweepRect.pivot = new Vector2(0f, 0.5f);
            sweepRect.sizeDelta = new Vector2(84f, 0f);
        }

        public void Show(string title, string detail, bool replay)
        {
            if (_title != null) _title.text = title;
            if (_detail != null) _detail.text = detail;
            if (replay || !_visible)
            {
                _showUntilTime = Time.unscaledTime + 1.1f;
            }

            _visible = true;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _visible = false;
        }

        private void Update()
        {
            if (_maskImage == null || _panelImage == null || _panelRect == null)
            {
                return;
            }

            var target = _visible && Time.unscaledTime <= _showUntilTime ? 1f : 0f;
            var current = _panelImage.color.a / 0.94f;
            var next = Mathf.Lerp(current, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));

            var panelColor = _panelImage.color;
            panelColor.a = 0.94f * next;
            _panelImage.color = panelColor;

            if (_title != null)
            {
                var titleColor = _title.color;
                titleColor.a = next;
                _title.color = titleColor;
            }

            if (_detail != null)
            {
                var detailColor = _detail.color;
                detailColor.a = 0.92f * next;
                _detail.color = detailColor;
            }

            var anchored = _panelRect.anchoredPosition;
            anchored.y = Mathf.Lerp(-8f, 0f, next);
            _panelRect.anchoredPosition = anchored;

            if (_sweepImage != null && next > 0.05f)
            {
                var sweepRect = _sweepImage.rectTransform;
                var sweepProgress = Mathf.Repeat(Time.unscaledTime * 1.4f, 1.2f);
                sweepRect.anchorMin = new Vector2(-0.22f + sweepProgress, 0f);
                sweepRect.anchorMax = new Vector2(-0.02f + sweepProgress, 1f);
                var sweepColor = _sweepImage.color;
                sweepColor.a = 0.14f * next;
                _sweepImage.color = sweepColor;
            }

            if (next < 0.02f && (!_visible || Time.unscaledTime > _showUntilTime))
            {
                gameObject.SetActive(false);
            }
        }
    }

    internal sealed class BattleHandSection
    {
        private readonly BattleHandPanelView _view;
        private readonly QuickChatTrayView _quickChatTray;
        private readonly GameObject _pilePopupMask;
        private readonly Text _pilePopupTitle;
        private readonly Transform _pilePopupContent;
        private readonly GridLayoutGroup _pilePopupGrid;
        private readonly PrototypeSessionController _session;
        private readonly Canvas _canvas;

        private enum PileView
        {
            Draw,
            Discard,
            Exhaust
        }

        private PileView _currentPileView = PileView.Draw;
        private Action<PlayerBattleState, Transform> _popupContentRenderer;

        public Transform HandContainer => _view.HandContainer;
        public Text EffectTargetHintText => _view.EffectTargetHintText;
        public Button ChatButton => _view.ChatButton;
        public Text ChatButtonLabel => _view.ChatButtonLabel;
        public Button EndTurnButton => _view.EndTurnButton;
        public RectTransform EndTurnReadyRoot => _view.EndTurnReadyRoot;

        public BattleHandSection(
            Transform parent,
            PrototypeSessionController session,
            Canvas canvas,
            Action toggleQuickChat,
            Action<string> sendQuickChat,
            Action endTurn)
        {
            _session = session;
            _canvas = canvas;
            _view = new BattleHandPanelView(parent, () => toggleQuickChat(), () => endTurn());
            _quickChatTray = new QuickChatTrayView(_view.QuickChatAnchor, CloseQuickChat, preset => sendQuickChat(preset));

            _view.DrawPileButton.onClick.RemoveAllListeners();
            _view.DrawPileButton.onClick.AddListener(() => OpenPilePopup(PileView.Draw));
            _view.DiscardPileButton.onClick.RemoveAllListeners();
            _view.DiscardPileButton.onClick.AddListener(() => OpenPilePopup(PileView.Discard));
            _view.ExhaustPileButton.onClick.RemoveAllListeners();
            _view.ExhaustPileButton.onClick.AddListener(() => OpenPilePopup(PileView.Exhaust));

            _pilePopupMask = UiFactory.CreatePanel(parent, "PilePopupMask", new Color(0f, 0f, 0f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _pilePopupMask.transform.SetAsLastSibling();
            var popupWindow = UiFactory.CreatePanel(_pilePopupMask.transform, "PilePopupWindow", new Color(0.09f, 0.12f, 0.15f, 1f), new Vector2(0.06f, 0.09f), new Vector2(0.94f, 0.89f), Vector2.zero, Vector2.zero);
            _pilePopupTitle = UiFactory.CreateText(popupWindow.transform, "Title", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0.9f), new Vector2(0.8f, 1f), new Vector2(24f, 0f), new Vector2(0f, 0f));
            _pilePopupTitle.fontStyle = FontStyle.Bold;
            var popupClose = UiFactory.CreateButton(popupWindow.transform, "CloseButton", "关闭", ClosePilePopup);
            popupClose.image.color = new Color(0.24f, 0.18f, 0.18f, 0.96f);
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
        }

        public void Render(
            BattleState state,
            PlayerBattleState player,
            bool canAct,
            string draggingCardInstanceId,
            Action<BattleHandCardDragView, string, CardDefinition, PointerEventData> onBeginDrag,
            Action<BattleHandCardDragView, PointerEventData> onDrag,
            Action<BattleHandCardDragView, PointerEventData> onEndDrag)
        {
            ClearContainer(_view.HandContainer);

            if (state == null || player == null)
            {
                _view.EnergyLabel.text = string.Empty;
                _view.DrawPileButton.gameObject.SetActive(false);
                _view.DiscardPileButton.gameObject.SetActive(false);
                _view.ExhaustPileButton.gameObject.SetActive(false);
                _view.EffectTargetHintText.text = string.Empty;
                ClosePilePopup();
                return;
            }

            _view.DrawPileButton.gameObject.SetActive(true);
            _view.DiscardPileButton.gameObject.SetActive(true);
            _view.ExhaustPileButton.gameObject.SetActive(true);
            _view.DrawPileButton.GetComponentInChildren<Text>().text = $"牌库 {player.drawPile.Count}";
            _view.DiscardPileButton.GetComponentInChildren<Text>().text = $"弃牌 {player.discardPile.Count}";
            _view.ExhaustPileButton.GetComponentInChildren<Text>().text = $"消耗 {player.exhaustPile.Count}";

            if (_pilePopupMask.activeSelf)
            {
                RefreshPopup(player);
            }

            var currentEnergy = player.energy;
            var maxEnergy = BattleEngine.BaseEnergyPerTurn;
            _view.EnergyLabel.text = $"能量 {currentEnergy}/{maxEnergy}";

            for (var i = 0; i < player.hand.Count; i++)
            {
                var cardState = player.hand[i];
                var cardDef = _session.GetCardDefinition(cardState.cardId);
                if (cardDef == null)
                {
                    continue;
                }

                var effectiveCost = BattleMechanics.GetEffectiveEnergyCost(cardState, cardDef);
                var isPlayable = canAct && player.energy >= effectiveCost;
                if (cardState.instanceId == draggingCardInstanceId)
                {
                    continue;
                }

                var cardObject = UiFactory.CreateCard(_view.HandContainer, "Card_" + cardState.instanceId, cardDef, isPlayable, effectiveCost);
                if (cardObject == null)
                {
                    continue;
                }

                var layoutElement = cardObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 140f;
                layoutElement.preferredHeight = 196f;
                var dragView = cardObject.AddComponent<BattleHandCardDragView>();
                dragView.Initialize(_canvas);
                dragView.BeganDrag += (v, eventData) => onBeginDrag(v, cardState.instanceId, cardDef, eventData);
                dragView.Dragged += onDrag;
                dragView.EndedDrag += onEndDrag;

                if (!isPlayable)
                {
                    var canvasGroup = cardObject.GetComponent<CanvasGroup>();
                    if (canvasGroup == null)
                    {
                        canvasGroup = cardObject.AddComponent<CanvasGroup>();
                    }

                    canvasGroup.alpha = 0.6f;
                    dragView.enabled = false;
                }
            }
        }

        public void SetTargetHint(string text)
        {
            _view.EffectTargetHintText.text = text ?? string.Empty;
        }

        public void ToggleQuickChat()
        {
            if (_quickChatTray.Root.activeSelf)
            {
                CloseQuickChat();
            }
            else
            {
                OpenQuickChat();
            }
        }

        public void OpenQuickChat()
        {
            _quickChatTray.Root.transform.SetAsLastSibling();
            _quickChatTray.Root.SetActive(true);
            _view.ChatButtonLabel.text = "收起";
        }

        public void CloseQuickChat()
        {
            _quickChatTray.Root.SetActive(false);
            _view.ChatButtonLabel.text = "聊天";
        }

        public void RenderEndTurnState(
            BattleState state,
            string localPlayerId,
            Func<string, string> resolvePlayerMarker,
            Func<BattlePhase, string> resolvePhaseTitle)
        {
            ClearContainer(_view.EndTurnReadyRoot);

            var localReady = false;
            var markerIndex = 0;
            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                if (player == null)
                {
                    continue;
                }

                if (string.Equals(player.playerId, localPlayerId, StringComparison.Ordinal))
                {
                    localReady = player.hasEndedTurn;
                    continue;
                }

                var marker = UiFactory.CreateText(_view.EndTurnReadyRoot, "Ready_" + i, 12, TextAnchor.MiddleCenter, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-(markerIndex + 1) * 24f, 0f), new Vector2(-markerIndex * 24f, 0f));
                marker.text = resolvePlayerMarker(player.playerId);
                marker.fontStyle = FontStyle.Bold;
                marker.color = player.hasEndedTurn ? new Color(0.95f, 0.98f, 1f, 0.98f) : new Color(0.57f, 0.63f, 0.71f, 0.75f);
                marker.raycastTarget = false;
                markerIndex++;
            }

            if (state.phase == BattlePhase.PlayerTurn && localReady)
            {
                _view.EndTurnButton.GetComponentInChildren<Text>().text = "取消结束";
            }
            else if (state.phase == BattlePhase.PlayerTurn)
            {
                _view.EndTurnButton.GetComponentInChildren<Text>().text = "结束回合";
            }
            else
            {
                _view.EndTurnButton.GetComponentInChildren<Text>().text = resolvePhaseTitle(state.phase);
            }
        }

        private void OpenPilePopup(PileView view)
        {
            _currentPileView = view;
            var player = _session.GetLocalBattlePlayer();
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
            _pilePopupMask.transform.SetAsLastSibling();
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
                var cardDef = _session.GetCardDefinition(cardState.cardId);
                if (cardDef != null)
                {
                    var cardObj = UiFactory.CreateCard(contentRoot, "PileCard_" + cardState.instanceId, cardDef, false, BattleMechanics.GetEffectiveEnergyCost(cardState, cardDef));
                    var layout = cardObj.AddComponent<LayoutElement>();
                    layout.preferredWidth = _pilePopupGrid.cellSize.x;
                    layout.preferredHeight = _pilePopupGrid.cellSize.y;
                    continue;
                }
            }
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

        private static void ClearContainer(Transform container)
        {
            for (var i = container.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(container.GetChild(i).gameObject);
            }
        }
    }

    internal sealed class BattleBoardSection
    {
        private const float ActorUnitWidth = 52f;
        private readonly PrototypeSessionController _session;
        private readonly RectTransform _monsterPanelRect;
        private readonly Transform _monsterContainer;
        private readonly Transform _westPlayerContainer;
        private readonly Transform _eastPlayerContainer;
        private readonly RectTransform _boardPanelRect;
        private readonly RectTransform _monsterHpFillRect;
        private readonly Text _monsterHpText;
        private readonly Text _monsterActionHintText;
        private readonly Text _monsterStatusHintText;
        private readonly List<BattleAreaDropZoneView> _areaDropZones = new List<BattleAreaDropZoneView>();
        private readonly List<BattleUnitSlotView> _playerSlots = new List<BattleUnitSlotView>();
        private readonly List<MonsterPartSlotView> _monsterPartSlots = new List<MonsterPartSlotView>();
        private readonly Dictionary<string, MonsterPartSlotView> _monsterPartSlotsByInstanceId = new Dictionary<string, MonsterPartSlotView>();
        private readonly Dictionary<string, int> _lastPlayerHpById = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _lastPartHpByInstanceId = new Dictionary<string, int>();

        public RectTransform BoardPanelRect => _boardPanelRect;
        public IList<BattleAreaDropZoneView> AreaDropZones => _areaDropZones;
        public IList<BattleUnitSlotView> PlayerSlots => _playerSlots;
        public IList<MonsterPartSlotView> MonsterPartSlots => _monsterPartSlots;
        public BattleUnitSlotView FindPlayerSlot(string playerId)
        {
            for (var i = 0; i < _playerSlots.Count; i++)
            {
                var slot = _playerSlots[i];
                if (slot != null && string.Equals(slot.UnitId, playerId, StringComparison.Ordinal))
                {
                    return slot;
                }
            }

            return null;
        }

        public BattleBoardSection(Transform parent, PrototypeSessionController session)
        {
            _session = session;

            var boardPanel = UiFactory.CreatePanel(parent, "BoardPanel", new Color(0.04f, 0.06f, 0.09f, 0.98f), new Vector2(0f, 0f), new Vector2(1f, 1.0f), new Vector2(10f, 0f), new Vector2(-10f, 0f));
            _boardPanelRect = boardPanel.GetComponent<RectTransform>();
            boardPanel.GetComponent<Image>().type = Image.Type.Sliced;

            // var upperGlow = UiFactory.CreatePanel(boardPanel.transform, "UpperGlow", new Color(0.3f, 0.53f, 0.64f, 0.08f), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            // upperGlow.transform.SetAsFirstSibling();
            // upperGlow.GetComponent<Image>().raycastTarget = false;

            CreateAreaDropZone(boardPanel.transform, "DropZone_West", BattleArea.West, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            CreateAreaDropZone(boardPanel.transform, "DropZone_East", BattleArea.East, new Vector2(0.5f, 0f), new Vector2(1f, 1f));

            var monsterHpBase = UiFactory.CreatePanel(boardPanel.transform, "MonsterHpBar", new Color(0.07f, 0.1f, 0.13f, 0.88f), new Vector2(0.14f, 0.89f), new Vector2(0.86f, 0.965f), Vector2.zero, Vector2.zero);
            var hpFillObj = UiFactory.CreatePanel(monsterHpBase.transform, "Fill", new Color(0.82f, 0.24f, 0.26f, 0.96f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _monsterHpFillRect = hpFillObj.GetComponent<RectTransform>();
            _monsterHpFillRect.pivot = new Vector2(0f, 0.5f);
            _monsterHpText = UiFactory.CreateText(monsterHpBase.transform, "Label", 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _monsterHpText.fontStyle = FontStyle.Bold;
            _monsterHpText.color = new Color(0.95f, 0.96f, 0.98f, 0.98f);

            var ground = UiFactory.CreatePanel(boardPanel.transform, "Ground", new Color(0.07f, 0.09f, 0.12f, 0.92f), new Vector2(0f, 0f), new Vector2(1f, 0.3f), Vector2.zero, Vector2.zero);
            ground.GetComponent<Image>().raycastTarget = false;
            var horizonLine = UiFactory.CreatePanel(boardPanel.transform, "Horizon", new Color(0.95f, 0.97f, 1f, 0.24f), new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.304f), Vector2.zero, Vector2.zero);
            horizonLine.GetComponent<Image>().raycastTarget = false;

            var westPanel = UiFactory.CreatePanel(boardPanel.transform, "WestPlayers", Color.clear, new Vector2(0f, 0.3f), new Vector2(0.28f, 0.8f), Vector2.zero, Vector2.zero);
            westPanel.GetComponent<Image>().raycastTarget = false;
            var westLayout = westPanel.AddComponent<HorizontalLayoutGroup>();
            westLayout.childAlignment = TextAnchor.LowerCenter;
            westLayout.spacing = 10f;
            westLayout.childControlHeight = westLayout.childControlWidth = false;
            _westPlayerContainer = westPanel.transform;

            var monsterPanel = UiFactory.CreatePanel(boardPanel.transform, "MonsterPanel", Color.clear, new Vector2(0.3f, 0f), new Vector2(0.7f, 1f), Vector2.zero, Vector2.zero);
            monsterPanel.GetComponent<Image>().raycastTarget = false;
            _monsterPanelRect = monsterPanel.GetComponent<RectTransform>();
            _monsterContainer = monsterPanel.transform;
            _monsterActionHintText = UiFactory.CreateText(monsterPanel.transform, "MonsterActionHint", 18, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.1f), new Vector2(0.88f, 0.17f), Vector2.zero, Vector2.zero);
            _monsterActionHintText.color = new Color(0.93f, 0.95f, 0.97f, 0.76f);
            _monsterActionHintText.raycastTarget = false;
            _monsterStatusHintText = UiFactory.CreateText(monsterPanel.transform, "MonsterStatusHint", 16, TextAnchor.MiddleCenter, new Vector2(0.12f, 0.03f), new Vector2(0.88f, 0.09f), Vector2.zero, Vector2.zero);
            _monsterStatusHintText.color = new Color(0.72f, 0.8f, 0.86f, 0.72f);
            _monsterStatusHintText.raycastTarget = false;

            var eastPanel = UiFactory.CreatePanel(boardPanel.transform, "EastPlayers", Color.clear, new Vector2(0.72f, 0.3f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero);
            eastPanel.GetComponent<Image>().raycastTarget = false;
            var eastLayout = eastPanel.AddComponent<HorizontalLayoutGroup>();
            eastLayout.childAlignment = TextAnchor.LowerCenter;
            eastLayout.spacing = 10f;
            eastLayout.childControlHeight = eastLayout.childControlWidth = false;
            _eastPlayerContainer = eastPanel.transform;
        }

        public void Render(BattleState state, bool detailedMode, Func<string, string> resolvePlayerMarker)
        {
            ClearContainer(_westPlayerContainer);
            ClearContainer(_eastPlayerContainer);
            _playerSlots.Clear();

            if (state == null)
            {
                ClearMonsterPartViews();
                _monsterActionHintText.text = string.Empty;
                _monsterStatusHintText.text = string.Empty;
                return;
            }

            if (state.monster != null)
            {
                float ratio = state.monster.coreMaxHp > 0 ? Mathf.Clamp01((float)state.monster.coreHp / state.monster.coreMaxHp) : 0f;
                _monsterHpFillRect.anchorMax = new Vector2(ratio, 1f);
                _monsterHpText.text = $"怪物生命：{state.monster.coreHp} / {state.monster.coreMaxHp}";
                _monsterActionHintText.text = BuildMonsterActionHint(state.monster);
                _monsterStatusHintText.text = BuildMonsterStatusesText(state.monster.statuses, detailedMode);
            }
            else
            {
                ClearMonsterPartViews();
                _monsterActionHintText.text = string.Empty;
                _monsterStatusHintText.text = string.Empty;
            }

            var currentThreatTargetId = state.monster == null ? null : state.monster.currentThreatTargetPlayerId;
            var currentPlayerHpById = new Dictionary<string, int>();
            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                var container = player.area == BattleArea.East ? _eastPlayerContainer : _westPlayerContainer;
                var secretSummary = BuildPlayerSecretSummary(player, detailedMode);
                var slot = CreateUnitSlot(container, BattleTargetFaction.Allies, player.playerId, player.displayName, player.hp, player.maxHp, player.armor, player.attackChargeStage, player.nextAttackBonus, 0, player.statuses, player.threatValue, player.threatTier, secretSummary, new Color(0.09f, 0.15f, 0.19f, 0.98f), detailedMode, string.Equals(currentThreatTargetId, player.playerId, StringComparison.Ordinal), SlotHighlightMode.None, resolvePlayerMarker, ActorUnitWidth);
                int previousHp;
                if (_lastPlayerHpById.TryGetValue(player.playerId, out previousHp) && player.hp < previousHp)
                {
                    slot.PlayDamagePulse();
                }
                _playerSlots.Add(slot);
                currentPlayerHpById[player.playerId] = player.hp;
            }
            _lastPlayerHpById.Clear();
            foreach (var pair in currentPlayerHpById)
            {
                _lastPlayerHpById[pair.Key] = pair.Value;
            }

            if (state.monster != null)
            {
                Canvas.ForceUpdateCanvases();
                var panelRect = _monsterPanelRect.rect;
                var activePartIds = new HashSet<string>();
                var currentPartHpByInstanceId = new Dictionary<string, int>();
                _monsterPartSlots.Clear();
                for (var i = 0; i < state.monster.parts.Count; i++)
                {
                    var part = state.monster.parts[i];
                    activePartIds.Add(part.instanceId);
                    MonsterPartSlotView slot;
                    if (!_monsterPartSlotsByInstanceId.TryGetValue(part.instanceId, out slot))
                    {
                        slot = CreateMonsterPartSlot(_monsterContainer, part, panelRect, state.monster.facing, state.monster.stance, SlotHighlightMode.None, detailedMode);
                        _monsterPartSlotsByInstanceId[part.instanceId] = slot;
                    }

                    var targetPosition = ResolvePartPosition(part, panelRect, state.monster.facing, state.monster.stance);
                    slot.SetTargetPosition(targetPosition, snapImmediately: false);
                    slot.SetData(part, detailedMode, SlotHighlightMode.None);
                    int previousHp;
                    if (_lastPartHpByInstanceId.TryGetValue(part.instanceId, out previousHp) && part.hp < previousHp)
                    {
                        slot.PlayDamagePulse();
                    }
                    _monsterPartSlots.Add(slot);
                    currentPartHpByInstanceId[part.instanceId] = part.hp;
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

                _lastPartHpByInstanceId.Clear();
                foreach (var pair in currentPartHpByInstanceId)
                {
                    _lastPartHpByInstanceId[pair.Key] = pair.Value;
                }
            }
            else
            {
                _lastPartHpByInstanceId.Clear();
            }
        }

        public void ReapplyHighlights(BattleState state, CardDefinition cardDef, BattleTargetType targetType, BattleUnitSlotView hoveredPlayer, MonsterPartSlotView hoveredPart, BattleAreaDropZoneView hoveredArea, bool detailedMode)
        {
            if (state == null)
            {
                return;
            }

            for (var i = 0; i < _playerSlots.Count; i++)
            {
                var slot = _playerSlots[i];
                var player = state.GetPlayer(slot.UnitId);
                var secretSummary = BuildPlayerSecretSummary(player, detailedMode);
                var isThreatTarget = state.monster != null && string.Equals(state.monster.currentThreatTargetPlayerId, slot.UnitId, StringComparison.Ordinal);
                slot.SetData(BattleTargetFaction.Allies, slot.UnitId, player.displayName, player.hp, player.maxHp, player.armor, player.attackChargeStage, player.nextAttackBonus, 0, player.statuses, player.threatValue, player.threatTier, secretSummary, detailedMode, isThreatTarget, GetHighlightModeForPlayer(state, _session, cardDef, targetType, slot, hoveredPlayer));
            }

            for (var i = 0; i < _monsterPartSlots.Count; i++)
            {
                var slot = _monsterPartSlots[i];
                var part = state.GetPart(slot.InstanceId);
                if (part != null)
                {
                    slot.SetData(part, detailedMode, GetHighlightModeForPart(state, _session, cardDef, targetType, slot, hoveredPart));
                }
            }

            for (var i = 0; i < _areaDropZones.Count; i++)
            {
                var zone = _areaDropZones[i];
                zone.SetHighlight(GetHighlightModeForArea(state, _session, cardDef, targetType, zone, hoveredArea));
            }
        }

        public void ClearAreaHighlights()
        {
            for (var i = 0; i < _areaDropZones.Count; i++)
            {
                _areaDropZones[i].SetHighlight(SlotHighlightMode.None);
            }
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

            var border = UiFactory.CreatePanel(zoneObject.transform, "Highlight", new Color(0.92f, 0.97f, 1f, 0.3f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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

        private BattleUnitSlotView CreateUnitSlot(Transform parent, BattleTargetFaction faction, string unitId, string name, int hp, int maxHp, int armor, int charge, int bonus, int vulnerableStacks, List<BattleStatusState> statuses, int threatValue, int threatTier, string secretSummary, Color color, bool detailedMode, bool isThreatTarget, SlotHighlightMode highlightMode, Func<string, string> resolvePlayerMarker, float actorUnitWidth)
        {
            var slotObject = new GameObject("UnitSlot_" + unitId);
            slotObject.transform.SetParent(parent, false);
            var rect = slotObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.3f);
            rect.anchorMax = new Vector2(0.5f, 0.3f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(150f, 204f);

            var hitbox = slotObject.AddComponent<Image>();
            hitbox.color = new Color(1f, 1f, 1f, 0.001f);
            hitbox.type = Image.Type.Sliced;

            var actorBody = UiFactory.CreatePanel(slotObject.transform, "ActorBody", color, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-actorUnitWidth * 0.5f, 4f), new Vector2(actorUnitWidth * 0.5f, 120f));
            var actorBodyImage = actorBody.GetComponent<Image>();
            actorBodyImage.type = Image.Type.Sliced;
            actorBodyImage.raycastTarget = false;
            actorBodyImage.color = new Color(color.r * 1.08f, color.g * 1.08f, color.b * 1.08f, color.a);

            var actorBodyShade = UiFactory.CreatePanel(actorBody.transform, "Shade", new Color(0f, 0f, 0f, 0.15f), new Vector2(0f, 0f), new Vector2(1f, 0.38f), Vector2.zero, Vector2.zero);
            actorBodyShade.GetComponent<Image>().raycastTarget = false;

            var isLocalPlayer = string.Equals(unitId, _session.LocalPlayerId, StringComparison.Ordinal);
            var actorHalfWidth = actorUnitWidth * 0.5f;
            var actorMarker = UiFactory.CreateText(actorBody.transform, "ActorMarker", 34, TextAnchor.MiddleCenter, new Vector2(0f, 0.28f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            actorMarker.text = resolvePlayerMarker(unitId);
            actorMarker.fontStyle = FontStyle.Bold;
            actorMarker.color = isLocalPlayer ? new Color(0.96f, 0.98f, 1f, 0.96f) : new Color(0.78f, 0.84f, 0.91f, 0.88f);
            actorMarker.raycastTarget = false;
            var actorNameBand = UiFactory.CreatePanel(actorBody.transform, "NameBand", new Color(0.03f, 0.05f, 0.08f, 0.68f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 24f));
            actorNameBand.GetComponent<Image>().raycastTarget = false;
            var nameLabel = UiFactory.CreateText(actorNameBand.transform, "Name", 15, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            nameLabel.color = new Color(0.94f, 0.96f, 0.98f, 0.98f);
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.raycastTarget = false;

            var localLine = UiFactory.CreatePanel(slotObject.transform, "LocalLine", isLocalPlayer ? new Color(0.98f, 0.99f, 1f, 0.82f) : new Color(1f, 1f, 1f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-actorHalfWidth, 2f), new Vector2(actorHalfWidth, 4f));
            var localLineImage = localLine.GetComponent<Image>();
            localLineImage.raycastTarget = false;
            localLineImage.type = Image.Type.Sliced;

            var infoBase = new GameObject("Info");
            infoBase.transform.SetParent(slotObject.transform, false);
            var infoRect = infoBase.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0.5f, 0f);
            infoRect.anchorMax = new Vector2(0.5f, 0f);
            infoRect.pivot = new Vector2(0.5f, 1f);
            infoRect.sizeDelta = new Vector2(150f, 118f);
            infoRect.anchoredPosition = new Vector2(0f, -8f);
            var infoBg = infoBase.AddComponent<Image>();
            infoBg.color = new Color(0.05f, 0.08f, 0.11f, 0.9f);
            infoBg.raycastTarget = false;
            var infoBorder = UiFactory.CreatePanel(infoBase.transform, "InfoBorder", new Color(1f, 1f, 1f, 0.05f), Vector2.zero, Vector2.one, new Vector2(-1f, -1f), new Vector2(1f, 1f));
            infoBorder.GetComponent<Image>().raycastTarget = false;
            infoBorder.transform.SetAsFirstSibling();

            var (hpBarBg, hpFill) = UiFactory.CreateProgressBar(infoBase.transform, "HPBar", faction == BattleTargetFaction.Allies ? new Color(0.32f, 0.85f, 0.54f) : new Color(0.84f, 0.24f, 0.26f), new Vector2(126f, 18f));
            var hpBarRect = hpBarBg.GetComponent<RectTransform>();
            hpBarRect.anchorMin = new Vector2(0.5f, 1f);
            hpBarRect.anchorMax = new Vector2(0.5f, 1f);
            hpBarRect.pivot = new Vector2(0.5f, 1f);
            hpBarRect.anchoredPosition = new Vector2(0f, -10f);
            var hpLabel = UiFactory.CreateText(hpBarBg.transform, "HPNumeric", 13, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hpLabel.color = new Color(0.96f, 0.98f, 1f, 0.98f);
            hpLabel.fontStyle = FontStyle.Bold;
            hpLabel.raycastTarget = false;

            var threatBase = UiFactory.CreatePanel(infoBase.transform, "ThreatBar", new Color(0.09f, 0.12f, 0.16f, 0.92f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-56f, -34f), new Vector2(56f, -26f));
            threatBase.GetComponent<Image>().raycastTarget = false;
            var threatSegmentFills = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                var segment = UiFactory.CreatePanel(threatBase.transform, "Segment_" + i, new Color(0.12f, 0.16f, 0.2f, 0.95f), new Vector2(i / 3f, 0f), new Vector2((i + 1) / 3f, 1f), new Vector2(1f, 1f), new Vector2(-1f, -1f));
                segment.GetComponent<Image>().raycastTarget = false;
                var fill = UiFactory.CreatePanel(segment.transform, "Fill", new Color(0.34f, 0.85f, 0.54f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                threatSegmentFills[i] = fill.GetComponent<Image>();
                threatSegmentFills[i].raycastTarget = false;
            }
            var threatArrow = UiFactory.CreateText(slotObject.transform, "ThreatArrow", 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-18f, 120f), new Vector2(18f, 146f));
            threatArrow.text = "▼";
            threatArrow.color = new Color(0.94f, 0.35f, 0.38f, 0.96f);
            threatArrow.raycastTarget = false;

            var statusLabel = UiFactory.CreateText(infoBase.transform, "Status", 12, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 12f), new Vector2(-10f, 56f));
            statusLabel.color = new Color(0.91f, 0.8f, 0.56f, 0.96f);
            statusLabel.raycastTarget = false;
            statusLabel.fontStyle = FontStyle.Normal;
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow = VerticalWrapMode.Overflow;

            var secretLabel = UiFactory.CreateText(infoBase.transform, "SecretStatus", 11, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 4f), new Vector2(-10f, 34f));
            secretLabel.color = new Color(0.76f, 0.86f, 0.95f, 0.94f);
            secretLabel.raycastTarget = false;
            secretLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            secretLabel.verticalOverflow = VerticalWrapMode.Overflow;

            var borderObj = UiFactory.CreatePanel(slotObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 1f), Vector2.zero, Vector2.one, new Vector2(-6f, -126f), new Vector2(6f, 28f));
            borderObj.transform.SetAsFirstSibling();

            var slotView = slotObject.AddComponent<BattleUnitSlotView>();
            slotView.Initialize(actorBodyImage, nameLabel, hpLabel, hpFill, null, statusLabel, secretLabel, threatBase.GetComponent<RectTransform>(), threatSegmentFills, new Text[3], null, threatArrow, color, borderObj.GetComponent<Image>(), localLine.GetComponent<Image>(), isLocalPlayer);
            slotView.SetData(faction, unitId, name, hp, maxHp, armor, charge, bonus, vulnerableStacks, statuses, threatValue, threatTier, secretSummary, detailedMode, isThreatTarget, highlightMode);
            return slotView;
        }

        private MonsterPartSlotView CreateMonsterPartSlot(Transform parent, MonsterPartState part, Rect panelRect, BattleFacing facing, BattleStance stance, SlotHighlightMode highlightMode, bool detailedMode)
        {
            var slotObject = new GameObject("MonsterPart_" + part.partId);
            slotObject.transform.SetParent(parent, false);
            var rect = slotObject.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.3f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = ResolvePartSize(part);
            var image = slotObject.AddComponent<Image>();
            var label = UiFactory.CreateText(slotObject.transform, "Label", 16, TextAnchor.MiddleCenter, new Vector2(0f, -0.5f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            label.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -20f);
            var borderObj = UiFactory.CreatePanel(slotObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 1f), Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            borderObj.transform.SetAsFirstSibling();

            var slotView = slotObject.AddComponent<MonsterPartSlotView>();
            slotView.Initialize(image, label, borderObj.GetComponent<Image>());
            slotView.SetTargetPosition(ResolvePartPosition(part, panelRect, facing, stance), true);
            slotView.SetData(part, detailedMode, highlightMode);
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
            if (string.Equals(part.shape, "Circle", StringComparison.OrdinalIgnoreCase) && part.radius > 0f)
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
            if (facing == BattleFacing.West) x = -x;
            var minAboveHorizon = stance == BattleStance.Prone ? panelRect.height * 0.01f : panelRect.height * 0.02f;
            y = Mathf.Max(y, minAboveHorizon);
            return new Vector2(x, y);
        }

        private static string BuildMonsterActionHint(MonsterBattleState monster)
        {
            if (monster == null) return string.Empty;
            var pose = string.IsNullOrEmpty(monster.currentPoseId) ? "待机" : monster.currentPoseId;
            return $"姿态：{pose}";
        }

        private static string BuildMonsterStatusesText(List<BattleStatusState> statuses, bool detailedMode)
        {
            if (statuses == null || statuses.Count == 0) return "<color=#9ca3af>状态：无</color>";
            var parts = new List<string>();
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null || status.stacks <= 0 || string.IsNullOrWhiteSpace(status.id)) continue;
                if (string.Equals(status.id, BattleStatusSystem.VulnerableStatusId, StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add((detailedMode ? "易伤" : "🎯") + status.stacks + BuildDurationSuffix(status, detailedMode));
                }
                else
                {
                    parts.Add(BattleStatusSystem.BuildStatusLabel(status, detailedMode));
                }
            }
            return parts.Count == 0 ? "<color=#9ca3af>状态：无</color>" : "<color=#cbd5e1>状态：" + string.Join(" ", parts) + "</color>";
        }

        private static string BuildPlayerSecretSummary(PlayerBattleState player, bool detailedMode)
        {
            if (player == null || player.statuses == null || player.statuses.Count == 0) return string.Empty;
            var distinctSecrets = 0;
            var totalSecretStacks = 0;
            for (var i = 0; i < player.statuses.Count; i++)
            {
                var status = player.statuses[i];
                if (status == null || status.stacks <= 0 || !IsSecretStatusId(status.id)) continue;
                distinctSecrets++;
                totalSecretStacks += status.stacks;
            }
            if (distinctSecrets <= 0 || totalSecretStacks <= 0) return string.Empty;
            return detailedMode ? $"奥秘 {distinctSecrets}种/{totalSecretStacks}层" : $"奥{totalSecretStacks}";
        }

        private static bool IsSecretStatusId(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId)) return false;
            return statusId.IndexOf("secret", StringComparison.OrdinalIgnoreCase) >= 0 || statusId.IndexOf("奥秘", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildDurationSuffix(BattleStatusState status, bool detailedMode)
        {
            if (status == null)
            {
                return string.Empty;
            }

            return BattleStatusSystem.BuildDurationSuffix(status.durationKind, status.durationTurns, detailedMode);
        }

        private static SlotHighlightMode GetHighlightModeForArea(BattleState state, PrototypeSessionController session, CardDefinition cardDef, BattleTargetType targetType, BattleAreaDropZoneView zone, BattleAreaDropZoneView hoveredArea)
        {
            if (targetType != BattleTargetType.Area || state == null) return SlotHighlightMode.None;
            var localPlayer = state.GetPlayer(session.LocalPlayerId);
            if (localPlayer == null) return SlotHighlightMode.None;
            if (!BattleTargetingRules.CanTargetArea(state, localPlayer, cardDef, zone.Area)) return SlotHighlightMode.None;
            return hoveredArea == zone ? SlotHighlightMode.Selected : SlotHighlightMode.Potential;
        }

        private static SlotHighlightMode GetHighlightModeForPlayer(BattleState state, PrototypeSessionController session, CardDefinition cardDef, BattleTargetType targetType, BattleUnitSlotView slot, BattleUnitSlotView hoveredSlot)
        {
            if (slot == null || !slot.IsAlive || state == null) return SlotHighlightMode.None;
            var localPlayer = state.GetPlayer(session.LocalPlayerId);
            var targetPlayer = state.GetPlayer(slot.UnitId);
            if (!BattleTargetingRules.CanTargetPlayer(state, localPlayer, cardDef, targetType, targetPlayer)) return SlotHighlightMode.None;
            bool isSelected = false;
            switch (targetType)
            {
                case BattleTargetType.Self:
                case BattleTargetType.SingleAlly:
                case BattleTargetType.OtherAlly:
                case BattleTargetType.SingleUnit:
                    isSelected = hoveredSlot == slot;
                    break;
                case BattleTargetType.AllAllies:
                    isSelected = hoveredSlot != null;
                    break;
            }
            return isSelected ? SlotHighlightMode.Selected : SlotHighlightMode.Potential;
        }

        private static SlotHighlightMode GetHighlightModeForPart(BattleState state, PrototypeSessionController session, CardDefinition cardDef, BattleTargetType targetType, MonsterPartSlotView slot, MonsterPartSlotView hoveredSlot)
        {
            if (slot == null || state == null) return SlotHighlightMode.None;
            var localPlayer = state.GetPlayer(session.LocalPlayerId);
            var targetPart = state.GetPart(slot.InstanceId);
            if (!BattleTargetingRules.CanTargetPart(state, localPlayer, cardDef, targetType, targetPart)) return SlotHighlightMode.None;
            bool isSelected = false;
            switch (targetType)
            {
                case BattleTargetType.MonsterPart:
                case BattleTargetType.SingleUnit:
                    isSelected = hoveredSlot == slot;
                    break;
                case BattleTargetType.AllMonsterParts:
                    isSelected = hoveredSlot != null;
                    break;
            }
            return isSelected ? SlotHighlightMode.Selected : SlotHighlightMode.Potential;
        }
    }
}
