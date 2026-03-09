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
        private readonly Transform _alliesContainer;
        private readonly Transform _enemiesContainer;
        private readonly RectTransform _handPanelRect;
        private readonly Transform _handContainer;
        private readonly Button _endTurnButton;
        private readonly Button _exitBattleButton;
        private readonly List<BattleUnitSlotView> _allySlots = new List<BattleUnitSlotView>();
        private readonly List<BattleUnitSlotView> _enemySlots = new List<BattleUnitSlotView>();
        private readonly List<string> _battleLogs = new List<string>();
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
            _summaryText = UiFactory.CreateText(headerPanel.transform, "Summary", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.65f, 1f), new Vector2(40f, 0f), new Vector2(-20f, 0f));
            _summaryText.fontStyle = FontStyle.Bold;
            _summaryText.supportRichText = true;

            _endTurnButton = UiFactory.CreateButton(headerPanel.transform, "EndTurn", "END TURN", Session.EndTurn);
            var etRect = _endTurnButton.GetComponent<RectTransform>();
            etRect.anchorMin = new Vector2(0.68f, 0.15f);
            etRect.anchorMax = new Vector2(0.96f, 0.85f);
            etRect.offsetMin = Vector2.zero;
            etRect.offsetMax = Vector2.zero;
            var etText = _endTurnButton.GetComponentInChildren<Text>();
            etText.fontSize = 34;
            etText.fontStyle = FontStyle.Bold;
            _endTurnButton.image.color = new Color(0.25f, 0.55f, 0.35f);

            _exitBattleButton = UiFactory.CreateButton(Root.transform, "ExitBattle", "Exit", Session.EndBattleAndReturnToLobby);
            var exRect = _exitBattleButton.GetComponent<RectTransform>();
            exRect.anchorMin = new Vector2(0.44f, 0.92f);
            exRect.anchorMax = new Vector2(0.56f, 0.98f);
            exRect.offsetMin = Vector2.zero;
            exRect.offsetMax = Vector2.zero;
            _exitBattleButton.image.color = new Color(0.4f, 0.2f, 0.2f, 0.6f);

            // --- Board Panel ---
            var boardPanel = UiFactory.CreatePanel(Root.transform, "BoardPanel", new Color(0.08f, 0.1f, 0.12f), new Vector2(0f, 0.55f), new Vector2(1f, 0.88f), new Vector2(30f, 10f), new Vector2(-30f, -10f));
            
            var alliesPanel = UiFactory.CreatePanel(boardPanel.transform, "AlliesPanel", Color.clear, new Vector2(0f, 0f), new Vector2(0.49f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -10f));
            var alliesLayout = alliesPanel.AddComponent<HorizontalLayoutGroup>();
            alliesLayout.spacing = 15f;
            alliesLayout.childAlignment = TextAnchor.MiddleCenter;
            alliesLayout.childControlWidth = alliesLayout.childControlHeight = false;
            alliesLayout.childForceExpandWidth = alliesLayout.childForceExpandHeight = false;
            _alliesContainer = alliesPanel.transform;

            var enemiesPanel = UiFactory.CreatePanel(boardPanel.transform, "EnemiesPanel", Color.clear, new Vector2(0.51f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-10f, -10f));
            var enemiesLayout = enemiesPanel.AddComponent<HorizontalLayoutGroup>();
            enemiesLayout.spacing = 15f;
            enemiesLayout.childAlignment = TextAnchor.MiddleCenter;
            enemiesLayout.childControlWidth = enemiesLayout.childControlHeight = false;
            enemiesLayout.childForceExpandWidth = enemiesLayout.childForceExpandHeight = false;
            _enemiesContainer = enemiesPanel.transform;

            // --- Battle Log (Scrollable) ---
            var logBase = UiFactory.CreatePanel(Root.transform, "LogBase", new Color(0.05f, 0.05f, 0.06f, 0.9f), new Vector2(0f, 0.34f), new Vector2(1f, 0.53f), new Vector2(40f, 5f), new Vector2(-40f, -5f));
            
            var scrollView = new GameObject("LogScroll");
            scrollView.transform.SetParent(logBase.transform, false);
            var svRect = scrollView.AddComponent<RectTransform>();
            svRect.anchorMin = Vector2.zero;
            svRect.anchorMax = Vector2.one;
            svRect.offsetMin = new Vector2(15f, 15f);
            svRect.offsetMax = new Vector2(-15f, -15f);

            _logScrollRect = scrollView.AddComponent<ScrollRect>();
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            
            _battleLogText = UiFactory.CreateText(viewport.transform, "LogText", 22, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
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

            _jumpToLatestButton = UiFactory.CreateButton(logBase.transform, "JumpToLatest", "↓ Latest", () => {
                _logScrollRect.verticalNormalizedPosition = 0f;
                _isUserScrolling = false;
                _jumpToLatestButton.gameObject.SetActive(false);
            });
            var jumpRect = _jumpToLatestButton.GetComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(0.85f, 0.05f);
            jumpRect.anchorMax = new Vector2(0.98f, 0.25f);
            jumpRect.offsetMin = jumpRect.offsetMax = Vector2.zero;
            _jumpToLatestButton.gameObject.SetActive(false);

            // --- Hand ---
            var handTitle = UiFactory.CreateText(Root.transform, "HandTitle", 28, TextAnchor.MiddleLeft, new Vector2(0f, 0.29f), new Vector2(1f, 0.33f), new Vector2(40f, 0f), new Vector2(-40f, 0f));
            handTitle.text = "Your Hand";
            handTitle.fontStyle = FontStyle.Bold;

            var handPanel = UiFactory.CreatePanel(Root.transform, "HandPanel", new Color(0.1f, 0.12f, 0.15f, 0.5f), new Vector2(0f, 0f), new Vector2(1f, 0.28f), new Vector2(20f, 20f), new Vector2(-20f, 20f));
            _handPanelRect = handPanel.GetComponent<RectTransform>();
            var handLayout = handPanel.AddComponent<HorizontalLayoutGroup>();
            handLayout.spacing = 20f;
            handLayout.padding = new RectOffset(20, 20, 20, 20);
            handLayout.childAlignment = TextAnchor.MiddleCenter;
            handLayout.childControlWidth = handLayout.childControlHeight = false;
            handLayout.childForceExpandWidth = handLayout.childForceExpandHeight = false;
            _handContainer = handPanel.transform;

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
            _summaryText.text = $"<color={color}>{turnType}</color>  -  ROUND {state.turnIndex}\n<size=22>{state.currentPrompt}</size>";
            
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
            ClearContainer(_alliesContainer);
            ClearContainer(_enemiesContainer);
            _allySlots.Clear();
            _enemySlots.Clear();

            if (state == null)
            {
                return;
            }

            for (var i = state.players.Count - 1; i >= 0; i--)
            {
                var player = state.players[i];
                _allySlots.Add(CreateUnitSlot(_alliesContainer, BattleTargetFaction.Allies, player.playerId, player.displayName, player.hp, player.maxHp, player.armor, new Color(0.2f, 0.36f, 0.31f), false));
            }

            for (var i = 0; i < state.enemies.Count; i++)
            {
                var enemy = state.enemies[i];
                _enemySlots.Add(CreateUnitSlot(_enemiesContainer, BattleTargetFaction.Enemies, enemy.instanceId, enemy.displayName, enemy.hp, enemy.maxHp, enemy.armor, new Color(0.42f, 0.22f, 0.22f), false));
            }
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
                return;
            }

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
                
                var cardObject = UiFactory.CreateCard(_handContainer, "Card_" + cardState.instanceId, cardDef.name, DescribeCard(cardDef), canAct);
                if (cardObject == null) continue;

                var layoutElement = cardObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 200f;
                layoutElement.preferredHeight = 280f;

                var dragView = cardObject.AddComponent<BattleHandCardDragView>();
                dragView.Initialize(_canvas);
                dragView.BeganDrag += (_, eventData) => BeginCardDrag(cardState.instanceId, cardDef);
                dragView.Dragged += (_, eventData) => UpdateCardDrag(eventData);
                dragView.EndedDrag += (_, eventData) => EndCardDrag(eventData);

                // Dim if not playable
                if (!canAct)
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
            ApplyHighlight(cardDef, null);
        }

        private void UpdateCardDrag(PointerEventData eventData)
        {
            if (_draggingCardDefinition == null || _lastState == null)
            {
                return;
            }

            var hoveredSlot = FindHoveredSlot(eventData);
            ApplyHighlight(_draggingCardDefinition, hoveredSlot);
        }

        private void EndCardDrag(PointerEventData eventData)
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
            var hoveredSlot = FindHoveredSlot(eventData);

            if (targetType == BattleTargetType.SingleEnemy || targetType == BattleTargetType.SingleAlly || 
                targetType == BattleTargetType.AllEnemies || targetType == BattleTargetType.AllAllies ||
                targetType == BattleTargetType.Self || targetType == BattleTargetType.OtherAlly ||
                targetType == BattleTargetType.SingleUnit)
            {
                if (hoveredSlot != null && IsValidTargetForType(targetType, hoveredSlot))
                {
                    var isAoe = (targetType == BattleTargetType.AllEnemies || targetType == BattleTargetType.AllAllies);
                    var unitId = isAoe ? string.Empty : hoveredSlot.UnitId;
                    var faction = hoveredSlot.Faction;
                    Session.PlayCard(_draggingCardInstanceId, faction, unitId);
                }
            }

            ClearHighlights();
            _draggingCardDefinition = null;
            _draggingCardInstanceId = null;
        }

        private void ApplyHighlight(CardDefinition cardDef, BattleUnitSlotView hoveredSlot)
        {
            if (_lastState == null)
            {
                return;
            }

            var targetType = ParseTargetType(cardDef.targetType);
            for (var i = 0; i < _allySlots.Count; i++)
            {
                var slot = _allySlots[i];
                var player = _lastState.GetPlayer(slot.UnitId);
                slot.SetData(BattleTargetFaction.Allies, slot.UnitId, player.displayName, player.hp, player.maxHp, player.armor, ShouldHighlightSlot(targetType, slot, hoveredSlot));
            }

            for (var i = 0; i < _enemySlots.Count; i++)
            {
                var slot = _enemySlots[i];
                var enemy = _lastState.GetEnemy(slot.UnitId);
                slot.SetData(BattleTargetFaction.Enemies, slot.UnitId, enemy.displayName, enemy.hp, enemy.maxHp, enemy.armor, ShouldHighlightSlot(targetType, slot, hoveredSlot));
            }
        }

        private bool ShouldHighlightSlot(BattleTargetType targetType, BattleUnitSlotView slot, BattleUnitSlotView hoveredSlot)
        {
            if (!slot.IsAlive)
            {
                return false;
            }

            switch (targetType)
            {
                case BattleTargetType.Self:
                    return slot.UnitId == Session.LocalPlayerId && hoveredSlot == slot;
                case BattleTargetType.SingleEnemy:
                    return slot.Faction == BattleTargetFaction.Enemies && hoveredSlot == slot;
                case BattleTargetType.AllEnemies:
                    return slot.Faction == BattleTargetFaction.Enemies && hoveredSlot != null && IsValidTargetForType(targetType, hoveredSlot);
                case BattleTargetType.SingleAlly:
                    return slot.Faction == BattleTargetFaction.Allies && hoveredSlot == slot;
                case BattleTargetType.AllAllies:
                    return slot.Faction == BattleTargetFaction.Allies && hoveredSlot != null && IsValidTargetForType(targetType, hoveredSlot);
                case BattleTargetType.OtherAlly:
                    return slot.Faction == BattleTargetFaction.Allies && slot.UnitId != Session.LocalPlayerId && hoveredSlot == slot;
                case BattleTargetType.SingleUnit:
                    return hoveredSlot == slot;
                default:
                    return false;
            }
        }

        private bool IsValidTargetForType(BattleTargetType targetType, BattleUnitSlotView slot)
        {
            if (slot == null || !slot.IsAlive)
            {
                return false;
            }

            switch (targetType)
            {
                case BattleTargetType.Self:
                    return slot.UnitId == Session.LocalPlayerId;
                case BattleTargetType.SingleEnemy:
                case BattleTargetType.AllEnemies:
                    return slot.Faction == BattleTargetFaction.Enemies;
                case BattleTargetType.SingleAlly:
                case BattleTargetType.AllAllies:
                    return slot.Faction == BattleTargetFaction.Allies;
                case BattleTargetType.OtherAlly:
                    return slot.Faction == BattleTargetFaction.Allies && slot.UnitId != Session.LocalPlayerId;
                case BattleTargetType.SingleUnit:
                    return true;
                default:
                    return false;
            }
        }

        private void ClearHighlights()
        {
            if (_lastState == null)
            {
                return;
            }

            RenderBoard(_lastState);
        }

        private BattleUnitSlotView FindHoveredSlot(PointerEventData eventData)
        {
            var hoveredObject = eventData.pointerEnter;
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

        private BattleUnitSlotView CreateUnitSlot(Transform parent, BattleTargetFaction faction, string unitId, string name, int hp, int maxHp, int armor, Color color, bool highlight)
        {
            var slotObject = UiFactory.CreatePanel(parent, "UnitSlot_" + unitId, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var layoutElement = slotObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 220f;
            layoutElement.preferredHeight = 160f;
            slotObject.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 160f);
            
            // Name at top
            var nameLabel = UiFactory.CreateText(slotObject.transform, "Name", 24, TextAnchor.UpperCenter, new Vector2(0f, 0.65f), new Vector2(1f, 0.95f), new Vector2(5f, 0f), new Vector2(-5f, 0f));
            nameLabel.fontStyle = FontStyle.Bold;

            // HP Bar using helper
            var barColor = faction == BattleTargetFaction.Allies ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.8f, 0.2f, 0.2f);
            var (hpBarBg, hpFill) = UiFactory.CreateProgressBar(slotObject.transform, "HPBar", barColor, new Vector2(180f, 24f));
            hpBarBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -15f);

            // HP Numeric
            var hpLabel = UiFactory.CreateText(hpBarBg.transform, "HPNumeric", 18, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hpLabel.color = Color.white;

            // Armor Display (Below HP)
            var armorLabel = UiFactory.CreateText(slotObject.transform, "Armor", 22, TextAnchor.MiddleCenter, new Vector2(0f, 0.05f), new Vector2(1f, 0.35f), Vector2.zero, Vector2.zero);
            armorLabel.fontStyle = FontStyle.Bold;
            armorLabel.color = new Color(0.6f, 0.8f, 1f);

            // Highlight Border
            var borderObj = UiFactory.CreatePanel(slotObject.transform, "Highlight", new Color(1f, 0.85f, 0f, 1f), Vector2.zero, Vector2.one, new Vector2(-5f, -5f), new Vector2(5f, 5f));
            borderObj.transform.SetAsFirstSibling();
            var highlightImage = borderObj.GetComponent<Image>();

            var slotView = slotObject.AddComponent<BattleUnitSlotView>();
            slotView.Initialize(slotObject.GetComponent<Image>(), nameLabel, hpLabel, hpFill, armorLabel, color, highlightImage);
            slotView.SetData(faction, unitId, name, hp, maxHp, armor, highlight);
            return slotView;
        }

        private static string DescribeCard(CardDefinition cardDef)
        {
            return "Effect: " + cardDef.effectType + "\nTarget: " + cardDef.targetType + "\nValue: " + cardDef.value;
        }

        private static BattleTargetType ParseTargetType(string raw)
        {
            return (BattleTargetType)System.Enum.Parse(typeof(BattleTargetType), raw, true);
        }

        private static void ClearContainer(Transform container)
        {
            for (var i = container.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(container.GetChild(i).gameObject);
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
