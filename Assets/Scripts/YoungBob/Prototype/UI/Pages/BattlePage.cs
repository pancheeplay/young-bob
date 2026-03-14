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
        private readonly RectTransform _handPanelRect;
        private readonly Transform _handContainer;
        private readonly RectTransform _monsterHpFillRect;
        private readonly Text _monsterHpText;
        private readonly Text _monsterActionHintText;
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

        public BattlePage(Transform parent, PrototypeSessionController session)
            : base(parent, "BattlePage", session, new Color(0.12f, 0.14f, 0.17f), new Vector2(0f, 0f), new Vector2(1f, 1f))
        {
            _canvas = parent.GetComponent<Canvas>();

            // --- Top Header (Turn Info & End Turn) ---
            var headerPanel = UiFactory.CreatePanel(Root.transform, "Header", new Color(0.18f, 0.2f, 0.23f), new Vector2(0f, 0.9f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _summaryText = UiFactory.CreateText(headerPanel.transform, "Summary", 24, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(30f, 0f), new Vector2(-10f, 0f));
            _summaryText.fontStyle = FontStyle.Bold;
            _summaryText.supportRichText = true;

            _endTurnButton = UiFactory.CreateButton(headerPanel.transform, "EndTurn", "END", Session.EndTurn);
            var etRect = _endTurnButton.GetComponent<RectTransform>();
            etRect.anchorMin = new Vector2(0.7f, 0.2f);
            etRect.anchorMax = new Vector2(0.95f, 0.8f);
            etRect.offsetMin = Vector2.zero;
            etRect.offsetMax = Vector2.zero;
            var etText = _endTurnButton.GetComponentInChildren<Text>();
            etText.fontSize = 28;
            etText.fontStyle = FontStyle.Bold;
            _endTurnButton.image.color = new Color(0.25f, 0.55f, 0.35f);

            _exitBattleButton = UiFactory.CreateButton(Root.transform, "ExitBattle", "Exit", Session.EndBattleAndReturnToLobby);
            var exRect = _exitBattleButton.GetComponent<RectTransform>();
            exRect.anchorMin = new Vector2(0.42f, 0.94f);
            exRect.anchorMax = new Vector2(0.58f, 0.98f);
            exRect.offsetMin = Vector2.zero;
            exRect.offsetMax = Vector2.zero;
            _exitBattleButton.image.color = new Color(0.4f, 0.2f, 0.2f, 0.6f);

            // --- Board Panel (Portrait, Horizon-based) ---
            var boardPanel = UiFactory.CreatePanel(Root.transform, "BoardPanel", new Color(0.08f, 0.1f, 0.12f), new Vector2(0f, 0.45f), new Vector2(1f, 0.88f), new Vector2(20f, 0f), new Vector2(-20f, 0f));
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

            // East Players (Right side, standing on horizon)
            var eastPanel = UiFactory.CreatePanel(boardPanel.transform, "EastPlayers", Color.clear, new Vector2(0.72f, 0.3f), new Vector2(1f, 0.8f), Vector2.zero, Vector2.zero);
            eastPanel.GetComponent<Image>().raycastTarget = false;
            var eastLayout = eastPanel.AddComponent<HorizontalLayoutGroup>();
            eastLayout.childAlignment = TextAnchor.LowerCenter;
            eastLayout.spacing = 10f;
            eastLayout.childControlHeight = eastLayout.childControlWidth = false;
            _eastPlayerContainer = eastPanel.transform;

            // --- Battle Log (Scrollable) ---
            var logBase = UiFactory.CreatePanel(Root.transform, "LogBase", new Color(0.05f, 0.05f, 0.06f, 0.92f), new Vector2(0f, 0.28f), new Vector2(1f, 0.44f), new Vector2(25f, 5f), new Vector2(-25f, -5f));
            
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

            _jumpToLatestButton = UiFactory.CreateButton(logBase.transform, "JumpToLatest", "Log ↓", () => {
                _logScrollRect.verticalNormalizedPosition = 0f;
                _isUserScrolling = false;
                _jumpToLatestButton.gameObject.SetActive(false);
            });
            var jumpRect = _jumpToLatestButton.GetComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(0.82f, 0.04f);
            jumpRect.anchorMax = new Vector2(0.98f, 0.22f);
            jumpRect.offsetMin = jumpRect.offsetMax = Vector2.zero;
            _jumpToLatestButton.gameObject.SetActive(false);
            
            // --- Energy Area (Inside Hand Panel Top Left) ---
            _energyLabel = UiFactory.CreateText(Root.transform, "EnergyLabel", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0.235f), new Vector2(0.4f, 0.27f), new Vector2(35f, 0f), new Vector2(0f, -6f));
            _energyLabel.fontStyle = FontStyle.Bold;
            _energyLabel.color = new Color(0.4f, 0.7f, 1f);
            _energyLabel.raycastTarget = false;

            // --- Hand ---
            var handPanel = UiFactory.CreatePanel(Root.transform, "HandPanel", new Color(0.12f, 0.14f, 0.18f, 0.7f), new Vector2(0f, 0f), new Vector2(1f, 0.27f), new Vector2(10f, 10f), new Vector2(-10f, 10f));
            _handPanelRect = handPanel.GetComponent<RectTransform>();
            var handScrollArea = new GameObject("HandScroll");
            handScrollArea.transform.SetParent(handPanel.transform, false);
            var hsRect = handScrollArea.AddComponent<RectTransform>();
            hsRect.anchorMin = Vector2.zero;
            hsRect.anchorMax = Vector2.one;
            hsRect.offsetMin = hsRect.offsetMax = Vector2.zero;

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
            handLayout.padding = new RectOffset(20, 20, 20, 20);
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
                _summaryText.text = "Waiting for battle...";
                _endTurnButton.interactable = false;
                _exitBattleButton.interactable = false;
                _isUserScrolling = false;
                RefreshJumpToLatestButton();
                return;
            }

            var turnType = state.phase == BattlePhase.PlayerTurn ? "PLAYER TURN" : "MONSTER TURN";
            var color = state.phase == BattlePhase.PlayerTurn ? "#40FF80" : "#FF6060";
            var encounterTotal = state.stageEncounterIds == null ? 0 : state.stageEncounterIds.Length;
            var encounterText = encounterTotal > 0
                ? $"<size=20>Stage {state.stageId}  Encounter {state.stageEncounterIndex + 1}/{encounterTotal}</size>\n"
                : string.Empty;
            _summaryText.text = $"<color={color}>{turnType}</color>  -  ROUND {state.turnIndex}\n{encounterText}<size=22>{state.currentPrompt}</size>";
            
            _endTurnButton.interactable = Session.CanLocalPlayerAct();
            _exitBattleButton.interactable = true;
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
                return;
            }

            // Update Monster HP Bar
            if (state.monster != null)
            {
                float ratio = state.monster.coreMaxHp > 0 ? Mathf.Clamp01((float)state.monster.coreHp / state.monster.coreMaxHp) : 0f;
                _monsterHpFillRect.anchorMax = new Vector2(ratio, 1f);
                string pose = !string.IsNullOrEmpty(state.monster.currentPoseId) ? $" [{state.monster.currentPoseId}]" : "";
                _monsterHpText.text = $"MONSTER HP: {state.monster.coreHp} / {state.monster.coreMaxHp}{pose}";
                _monsterActionHintText.text = BuildMonsterActionHint(state.monster);
            }
            else
            {
                ClearMonsterPartViews();
                _monsterActionHintText.text = string.Empty;
            }

            // Render Players in their respective areas
            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                var container = player.area == BattleArea.East ? _eastPlayerContainer : _westPlayerContainer;
                var slot = CreateUnitSlot(container, BattleTargetFaction.Allies, player.playerId, player.displayName, player.hp, player.maxHp, player.armor, player.attackChargeStage, player.nextAttackBonus, new Color(0.2f, 0.36f, 0.31f), SlotHighlightMode.None);
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
                    slot.SetData(part, SlotHighlightMode.None);
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
                return;
            }

            var player = Session.GetLocalBattlePlayer();
            if (player == null)
            {
                _energyLabel.text = "";
                return;
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
            _energyLabel.text = $"ENERGY {energyIcons} ({currentEnergy}/{maxEnergy})";

            if (!string.IsNullOrEmpty(_draggingCardInstanceId) && !HasCardInHand(player, _draggingCardInstanceId))
            {
                ClearHighlights();
                _draggingCardDefinition = null;
                _draggingCardInstanceId = null;
            }

            var canAct = Session.CanLocalPlayerAct();

            for (var i = 0; i < player.hand.Count; i++)
            {
                var cardState = player.hand[i];
                var cardDef = Session.GetCardDefinition(cardState.cardId);
                if (cardDef == null) continue;
                
                var isPlayable = canAct && player.energy >= cardDef.energyCost;
                var cardObject = UiFactory.CreateCard(_handContainer, "Card_" + cardState.instanceId, cardDef, isPlayable);
                if (cardObject == null) continue;

                var layoutElement = cardObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 200f;
                layoutElement.preferredHeight = 280f;

                var dragView = cardObject.AddComponent<BattleHandCardDragView>();
                dragView.Initialize(_canvas);
                dragView.BeganDrag += (_, eventData) => BeginCardDrag(cardState.instanceId, cardDef);
                dragView.Dragged += (v, eventData) => UpdateCardDrag(v);
                dragView.EndedDrag += (v, eventData) => EndCardDrag(v);

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

        private void BeginCardDrag(string cardInstanceId, CardDefinition cardDef)
        {
            if (!Session.CanLocalPlayerAct())
            {
                return;
            }

            _draggingCardDefinition = cardDef;
            _draggingCardInstanceId = cardInstanceId;
            ApplyHighlight(cardDef, null, null, null);
        }

        private void UpdateCardDrag(BattleHandCardDragView view)
        {
            if (_draggingCardDefinition == null || _lastState == null)
            {
                return;
            }

            FindHoveredTargetsAtCardTop(view, out var hoveredPlayer, out var hoveredPart, out var hoveredArea);
            ApplyHighlight(_draggingCardDefinition, hoveredPlayer, hoveredPart, hoveredArea);
        }

        private void EndCardDrag(BattleHandCardDragView view)
        {
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
                return;
            }

            var targetType = ParseTargetType(_draggingCardDefinition.targetType);
            FindHoveredTargetsAtCardTop(view, out var hoveredPlayer, out var hoveredPart, out var hoveredArea);

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
                }
            }

            ClearHighlights();
            _draggingCardDefinition = null;
            _draggingCardInstanceId = null;
        }

        private void ApplyHighlight(CardDefinition cardDef, BattleUnitSlotView hoveredPlayer, MonsterPartSlotView hoveredPart, BattleAreaDropZoneView hoveredArea)
        {
            if (_lastState == null)
            {
                return;
            }

            var targetType = ParseTargetType(cardDef.targetType);
            for (var i = 0; i < _playerSlots.Count; i++)
            {
                var slot = _playerSlots[i];
                var player = _lastState.GetPlayer(slot.UnitId);
                slot.SetData(BattleTargetFaction.Allies, slot.UnitId, player.displayName, player.hp, player.maxHp, player.armor, player.attackChargeStage, player.nextAttackBonus, GetHighlightModeForPlayer(cardDef, targetType, slot, hoveredPlayer));
            }

            for (var i = 0; i < _monsterPartSlots.Count; i++)
            {
                var slot = _monsterPartSlots[i];
                var part = _lastState.GetPart(slot.InstanceId);
                if (part == null)
                {
                    continue;
                }

                slot.SetData(part, GetHighlightModeForPart(cardDef, targetType, slot, hoveredPart));
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
            if (!slot.IsAlive || !IsValidPartTarget(cardDef, targetType, slot))
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
            if (slot == null || !slot.IsAlive || _lastState == null)
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

        private void FindHoveredTargetsAtCardTop(BattleHandCardDragView view, out BattleUnitSlotView hoveredPlayer, out MonsterPartSlotView hoveredPart, out BattleAreaDropZoneView hoveredArea)
        {
            hoveredPlayer = null;
            hoveredPart = null;
            hoveredArea = null;

            var raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                return;
            }

            var results = RaycastAtCardTop(view, raycaster);
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

        private List<RaycastResult> RaycastAtCardTop(BattleHandCardDragView view, GraphicRaycaster raycaster)
        {
            var pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = view.GetTopCenterScreenPoint();
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

        private BattleUnitSlotView CreateUnitSlot(Transform parent, BattleTargetFaction faction, string unitId, string name, int hp, int maxHp, int armor, int charge, int bonus, Color color, SlotHighlightMode highlightMode)
        {
            var slotObject = new GameObject("UnitSlot_" + unitId);
            slotObject.transform.SetParent(parent, false);
            var rect = slotObject.AddComponent<RectTransform>();
            
            // Horizon Anchor
            rect.anchorMin = new Vector2(0.5f, 0.3f);
            rect.anchorMax = new Vector2(0.5f, 0.3f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(130f, 160f);

            var bg = slotObject.AddComponent<Image>();
            bg.color = color;
            bg.type = Image.Type.Sliced;

            // Info Container
            var infoBase = new GameObject("Info");
            infoBase.transform.SetParent(slotObject.transform, false);
            var infoRect = infoBase.AddComponent<RectTransform>();
            infoRect.anchorMin = new Vector2(0f, -0.7f);
            infoRect.anchorMax = new Vector2(1f, -0.05f);
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;

            // Name
            var nameLabel = UiFactory.CreateText(infoBase.transform, "Name", 20, TextAnchor.MiddleCenter, new Vector2(0f, 0.65f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            nameLabel.fontStyle = FontStyle.Bold;

            // HP Bar
            var barColor = faction == BattleTargetFaction.Allies ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.8f, 0.2f, 0.2f);
            var (hpBarBg, hpFill) = UiFactory.CreateProgressBar(infoBase.transform, "HPBar", barColor, new Vector2(110f, 14f));
            hpBarBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -15f);

            var hpLabel = UiFactory.CreateText(hpBarBg.transform, "HPNumeric", 14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hpLabel.color = Color.white;

            // Status (Charge/Bonus)
            var statusLabel = UiFactory.CreateText(infoBase.transform, "Status", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0f), new Vector2(1f, 0.35f), Vector2.zero, Vector2.zero);
            statusLabel.fontStyle = FontStyle.Bold;
            statusLabel.color = new Color(1f, 0.8f, 0.2f);

            // Armor
            var armorLabel = UiFactory.CreateText(slotObject.transform, "Armor", 18, TextAnchor.MiddleCenter, new Vector2(0f, 0.7f), new Vector2(0f, 0.7f), Vector2.zero, Vector2.zero);
            armorLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(70f, 0f);
            armorLabel.fontStyle = FontStyle.Bold;
            armorLabel.color = new Color(0.6f, 0.8f, 1f);

            // Highlight
            var borderObj = UiFactory.CreatePanel(slotObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 1f), Vector2.zero, Vector2.one, new Vector2(-5f, -5f), new Vector2(5f, 5f));
            borderObj.transform.SetAsFirstSibling();
            var highlightImage = borderObj.GetComponent<Image>();

            var slotView = slotObject.AddComponent<BattleUnitSlotView>();
            slotView.Initialize(bg, nameLabel, hpLabel, hpFill, armorLabel, statusLabel, color, highlightImage);
            slotView.SetData(faction, unitId, name, hp, maxHp, armor, charge, bonus, highlightMode);
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
            slotView.SetData(part, highlightMode);
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

            var pose = string.IsNullOrEmpty(monster.currentPoseId) ? "idle" : monster.currentPoseId;
            if (monster.hasActiveSkill && monster.activeSkill != null)
            {
                var windup = monster.activeSkill.remainingWindup > 0 ? $" (windup {monster.activeSkill.remainingWindup})" : "";
                return $"Action: {monster.activeSkill.displayName}{windup}  |  Pose: {pose}";
            }

            return $"Action: waiting  |  Pose: {pose}";
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
            return "Cost: " + cardDef.energyCost + "\nEffect: " + cardDef.effectType + "\nTarget: " + cardDef.targetType + range + "\nValue: " + cardDef.value;
        }

        private static BattleTargetType ParseTargetType(string raw)
        {
            return (BattleTargetType)System.Enum.Parse(typeof(BattleTargetType), raw, true);
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
