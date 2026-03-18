using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Data;
using YoungBob.Prototype.Multiplayer;
using YoungBob.Prototype.UI;

namespace YoungBob.Prototype.UI.Pages
{
    internal sealed class RoomPage : PageBase
    {
        private enum SelectorMode
        {
            None,
            Stage,
            Deck
        }

        private readonly Text _roomText;
        private readonly Text _playersText;
        private readonly Text _chatText;
        private readonly Button _stageSelectButton;
        private readonly Text _stageSelectLabel;
        private readonly Text _stageSummaryText;
        private readonly Button _deckSelectButton;
        private readonly Text _deckSelectLabel;
        private readonly Text _deckSummaryText;
        private readonly Button _chatButton;
        private readonly GameObject _quickChatMask;
        private readonly Button _startBattleButton;
        private readonly Button _leaveRoomButton;
        private readonly SelectionPopupView _selectionPopup;
        private readonly List<string> _chatMessages = new List<string>();

        private SelectorMode _selectorMode = SelectorMode.None;
        private RoomJoinedEvent _lastRoom;

        public RoomPage(Transform parent, PrototypeSessionController session)
            : base(parent, "RoomPage", session, new Color(0.14f, 0.17f, 0.2f), new Vector2(0f, 0f), new Vector2(1f, 0.8f))
        {
            var title = UiFactory.CreateText(Root.transform, "Title", 36, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -120f), new Vector2(-40f, -40f));
            title.text = "房间";

            _roomText = UiFactory.CreateText(Root.transform, "RoomInfo", 24, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -220f), new Vector2(-40f, -140f));

            var playersPanel = UiFactory.CreatePanel(Root.transform, "PlayersPanel", new Color(0.1f, 0.12f, 0.15f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 560f), new Vector2(-40f, -240f));
            var playersTitle = UiFactory.CreateText(playersPanel.transform, "PlayersTitle", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.82f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-16f, 0f));
            playersTitle.text = "玩家列表";
            playersTitle.fontStyle = FontStyle.Bold;
            _playersText = UiFactory.CreateText(playersPanel.transform, "PlayersList", 20, TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0.82f), new Vector2(20f, 12f), new Vector2(-20f, -16f));

            var selectorsPanel = UiFactory.CreatePanel(Root.transform, "SelectorsPanel", new Color(0.1f, 0.12f, 0.15f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 240f), new Vector2(-40f, -830f));

            var stagePanel = UiFactory.CreatePanel(selectorsPanel.transform, "StagePanel", new Color(0.11f, 0.14f, 0.17f), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var stageTitle = UiFactory.CreateText(stagePanel.transform, "StageTitle", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.8f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-16f, 0f));
            stageTitle.text = "关卡选择";
            stageTitle.fontStyle = FontStyle.Bold;
            _stageSelectButton = UiFactory.CreateButton(stagePanel.transform, "StageSelectButton", "当前关卡", () => OpenSelector(SelectorMode.Stage));
            var stageBtnRect = _stageSelectButton.GetComponent<RectTransform>();
            stageBtnRect.anchorMin = new Vector2(0.05f, 0.52f);
            stageBtnRect.anchorMax = new Vector2(0.95f, 0.78f);
            stageBtnRect.offsetMin = Vector2.zero;
            stageBtnRect.offsetMax = Vector2.zero;
            _stageSelectButton.GetComponent<LayoutElement>().preferredHeight = -1f;
            _stageSelectLabel = _stageSelectButton.GetComponentInChildren<Text>();
            _stageSelectLabel.fontSize = 18;
            _stageSelectLabel.alignment = TextAnchor.MiddleCenter;
            _stageSummaryText = UiFactory.CreateText(stagePanel.transform, "StageSummary", 16, TextAnchor.UpperLeft, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.48f), Vector2.zero, Vector2.zero);
            _stageSummaryText.color = new Color(0.8f, 0.84f, 0.9f, 0.95f);

            var deckPanel = UiFactory.CreatePanel(selectorsPanel.transform, "DeckPanel", new Color(0.11f, 0.14f, 0.17f), new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
            var deckTitle = UiFactory.CreateText(deckPanel.transform, "DeckTitle", 22, TextAnchor.MiddleLeft, new Vector2(0f, 0.8f), new Vector2(1f, 1f), new Vector2(16f, 0f), new Vector2(-16f, 0f));
            deckTitle.text = "牌组选择";
            deckTitle.fontStyle = FontStyle.Bold;
            _deckSelectButton = UiFactory.CreateButton(deckPanel.transform, "DeckSelectButton", "当前牌组", () => OpenSelector(SelectorMode.Deck));
            var deckBtnRect = _deckSelectButton.GetComponent<RectTransform>();
            deckBtnRect.anchorMin = new Vector2(0.05f, 0.52f);
            deckBtnRect.anchorMax = new Vector2(0.95f, 0.78f);
            deckBtnRect.offsetMin = Vector2.zero;
            deckBtnRect.offsetMax = Vector2.zero;
            _deckSelectButton.GetComponent<LayoutElement>().preferredHeight = -1f;
            _deckSelectLabel = _deckSelectButton.GetComponentInChildren<Text>();
            _deckSelectLabel.fontSize = 18;
            _deckSelectLabel.alignment = TextAnchor.MiddleCenter;
            _deckSummaryText = UiFactory.CreateText(deckPanel.transform, "DeckSummary", 16, TextAnchor.UpperLeft, new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.48f), Vector2.zero, Vector2.zero);
            _deckSummaryText.color = new Color(0.8f, 0.84f, 0.9f, 0.95f);

            var actionsPanel = UiFactory.CreatePanel(Root.transform, "Actions", Color.clear, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 40f), new Vector2(-40f, 200f));
            var layout = actionsPanel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;

            _startBattleButton = UiFactory.CreateButton(actionsPanel.transform, "StartBattleButton", "开始关卡", Session.StartBattle);
            _startBattleButton.GetComponent<LayoutElement>().preferredHeight = 80f;
            _leaveRoomButton = UiFactory.CreateButton(actionsPanel.transform, "LeaveRoomButton", "离开房间", Session.LeaveRoom);
            _leaveRoomButton.GetComponent<LayoutElement>().preferredHeight = 80f;

            _selectionPopup = new SelectionPopupView(Root.transform, "RoomSelector");
            _selectionPopup.Closed += () => { _selectorMode = SelectorMode.None; };

            _chatText = null;
            _chatButton = null;
            _quickChatMask = null;

            Hide();
        }

        public void Render(RoomJoinedEvent room)
        {
            var previousRoomId = _lastRoom == null ? null : _lastRoom.roomId;
            _lastRoom = room;
            if (room == null)
            {
                _roomText.text = "未加入房间";
                _playersText.text = string.Empty;
                _stageSelectLabel.text = "当前关卡: -";
                _deckSelectLabel.text = "当前牌组: -";
                _stageSummaryText.text = string.Empty;
                _deckSummaryText.text = string.Empty;
                _stageSelectButton.interactable = false;
                _deckSelectButton.interactable = false;
                _startBattleButton.interactable = false;
                _leaveRoomButton.interactable = false;
                _selectionPopup.Hide();
                return;
            }

            if (!string.IsNullOrWhiteSpace(previousRoomId) && !string.Equals(previousRoomId, room.roomId, System.StringComparison.Ordinal))
            {
                _chatMessages.Clear();
                _chatText.text = string.Empty;
            }

            _roomText.text = "房间: " + room.roomId + "\n主机: " + room.hostPlayerId;
            RenderPlayers(room);
            RenderStageSection(room);
            RenderDeckSection();

            _startBattleButton.interactable = Session.CanStartBattle();
            _leaveRoomButton.interactable = true;

            if (_selectionPopup.IsOpen && _selectorMode != SelectorMode.None)
            {
                RefreshSelectorPopup();
            }
        }

        private void RenderPlayers(RoomJoinedEvent room)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < room.players.Count; i++)
            {
                var player = room.players[i];
                var tags = new List<string>();
                if (player.isHost) tags.Add("主机");
                if (player.isLocal) tags.Add("你");

                var suffix = tags.Count > 0 ? " [" + string.Join(", ", tags) + "]" : string.Empty;
                var deckId = Session.GetSelectedDeckIdForPlayer(player.playerId);
                builder.AppendLine("• " + player.displayName + suffix + "    牌组: " + deckId);
            }

            _playersText.text = builder.ToString();
        }

        private void RenderStageSection(RoomJoinedEvent room)
        {
            var stage = Session.SelectedStage;
            var isHost = room.localPlayerId == room.hostPlayerId;
            _stageSelectButton.interactable = isHost;
            _stageSelectButton.GetComponent<Image>().color = isHost
                ? new Color(0.2f, 0.33f, 0.52f, 0.95f)
                : new Color(0.24f, 0.24f, 0.26f, 0.95f);

            if (stage == null)
            {
                _stageSelectLabel.text = "当前关卡: 无";
                _stageSummaryText.text = isHost ? "没有可用关卡数据" : "等待主机选择关卡";
                return;
            }

            var count = stage.encounterIds == null ? 0 : stage.encounterIds.Length;
            _stageSelectLabel.text = "当前关卡: " + stage.name + (isHost ? "  ▼" : "");
            _stageSummaryText.text = "ID: " + stage.id + "\n战斗数: " + count + (isHost ? "\n点击上方按钮可切换" : "\n仅主机可切换");
        }

        private void RenderDeckSection()
        {
            var selectedDeckId = Session.LocalSelectedDeckId;
            _deckSelectButton.interactable = true;
            _deckSelectButton.GetComponent<Image>().color = new Color(0.26f, 0.22f, 0.42f, 0.95f);
            _deckSelectLabel.text = "当前牌组: " + selectedDeckId + "  ▼";

            var deck = TryGetDeckById(selectedDeckId);
            _deckSummaryText.text = deck == null
                ? "无法读取牌组信息"
                : BuildDeckSummary(deck, includePreview: true);
        }

        public void AppendChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _chatMessages.Add(message);
            while (_chatMessages.Count > 6)
            {
                _chatMessages.RemoveAt(0);
            }

            _chatText.text = string.Join("\n", _chatMessages.ToArray());
        }

        private void OpenSelector(SelectorMode mode)
        {
            if (_lastRoom == null)
            {
                return;
            }

            if (mode == SelectorMode.Stage && _lastRoom.localPlayerId != _lastRoom.hostPlayerId)
            {
                return;
            }

            _selectorMode = mode;
            RefreshSelectorPopup();
        }

        private void RefreshSelectorPopup()
        {
            if (_selectorMode == SelectorMode.None || _lastRoom == null)
            {
                _selectionPopup.Hide();
                return;
            }

            if (_selectorMode == SelectorMode.Stage)
            {
                BuildStageSelectorPopup();
            }
            else
            {
                BuildDeckSelectorPopup();
            }
        }

        private void BuildStageSelectorPopup()
        {
            var stages = Session.AvailableStages;
            var selectedId = Session.SelectedStage == null ? string.Empty : Session.SelectedStage.id;
            var items = new List<SelectionPopupView.Item>();

            if (stages != null)
            {
                for (var i = 0; i < stages.Count; i++)
                {
                    var stage = stages[i];
                    if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                    {
                        continue;
                    }

                    var stageId = stage.id;
                    var count = stage.encounterIds == null ? 0 : stage.encounterIds.Length;
                    items.Add(new SelectionPopupView.Item
                    {
                        id = stageId,
                        title = stage.name,
                        detail = "ID: " + stageId + "    战斗数: " + count,
                        isSelected = string.Equals(stageId, selectedId),
                        interactable = true,
                        onSelected = () =>
                        {
                            Session.SelectStageById(stageId);
                            _selectionPopup.Hide();
                        }
                    });
                }
            }

            Debug.Log("[RoomPage] Stage selector items=" + items.Count + " selectedId=" + selectedId);
            _selectionPopup.Show("选择关卡", "仅主机可切换。当前选中项已高亮并带“已选 ✓”标记。", items, "没有可用关卡");
        }

        private void BuildDeckSelectorPopup()
        {
            var decks = Session.AvailableDecks;
            var selectedDeckId = Session.LocalSelectedDeckId;
            var items = new List<SelectionPopupView.Item>();

            if (decks != null)
            {
                for (var i = 0; i < decks.Count; i++)
                {
                    var deck = decks[i];
                    if (deck == null || string.IsNullOrWhiteSpace(deck.id))
                    {
                        continue;
                    }

                    var deckId = deck.id;
                    items.Add(new SelectionPopupView.Item
                    {
                        id = deckId,
                        title = deckId,
                        detail = BuildDeckSummary(deck, includePreview: true),
                        isSelected = string.Equals(deckId, selectedDeckId),
                        interactable = true,
                        onSelected = () =>
                        {
                            Session.SelectLocalDeck(deckId);
                            _selectionPopup.Hide();
                        }
                    });
                }
            }

            Debug.Log("[RoomPage] Deck selector items=" + items.Count + " selectedId=" + selectedDeckId);
            _selectionPopup.Show("选择牌组", "每位玩家独立选择自己的牌组。当前选中项已高亮并带“已选 ✓”标记。", items, "没有可用牌组");
        }

        private void OpenQuickChatWheel()
        {
            if (_lastRoom == null)
            {
                return;
            }

            _quickChatMask.SetActive(true);
        }

        private void CloseQuickChatWheel()
        {
            _quickChatMask.SetActive(false);
        }

        private void SendQuickChat(string presetId)
        {
            Session.SendQuickChat(presetId);
            CloseQuickChatWheel();
        }

        private GameObject BuildQuickChatWheel(Transform parent)
        {
            var mask = UiFactory.CreatePanel(parent, "QuickChatMask", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backgroundButton = mask.AddComponent<Button>();
            var backgroundImage = mask.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, 0.6f);
            backgroundButton.targetGraphic = backgroundImage;
            backgroundButton.onClick.AddListener(CloseQuickChatWheel);

            var wheel = UiFactory.CreatePanel(mask.transform, "QuickChatWheel", new Color(0.12f, 0.15f, 0.19f, 0.98f), new Vector2(0.16f, 0.34f), new Vector2(0.84f, 0.66f), Vector2.zero, Vector2.zero);
            var wheelRect = wheel.GetComponent<RectTransform>();
            wheelRect.anchorMin = new Vector2(0.16f, 0.34f);
            wheelRect.anchorMax = new Vector2(0.84f, 0.66f);
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

        private DeckDefinition TryGetDeckById(string deckId)
        {
            var decks = Session.AvailableDecks;
            if (decks == null || string.IsNullOrWhiteSpace(deckId))
            {
                return null;
            }

            for (var i = 0; i < decks.Count; i++)
            {
                if (decks[i] != null && string.Equals(decks[i].id, deckId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return decks[i];
                }
            }

            return null;
        }

        private string BuildDeckSummary(DeckDefinition deck, bool includePreview)
        {
            var cards = deck.cards ?? System.Array.Empty<string>();
            var cost0 = 0;
            var cost1 = 0;
            var cost2 = 0;
            var cost3Plus = 0;
            var previewNames = new List<string>(3);

            for (var i = 0; i < cards.Length; i++)
            {
                var cardId = cards[i];
                if (string.IsNullOrWhiteSpace(cardId))
                {
                    continue;
                }

                try
                {
                    var card = Session.GetCardDefinition(cardId);
                    if (previewNames.Count < 3)
                    {
                        previewNames.Add(string.IsNullOrWhiteSpace(card.name) ? cardId : card.name);
                    }

                    if (card.energyCost <= 0) cost0 += 1;
                    else if (card.energyCost == 1) cost1 += 1;
                    else if (card.energyCost == 2) cost2 += 1;
                    else cost3Plus += 1;
                }
                catch
                {
                    if (previewNames.Count < 3)
                    {
                        previewNames.Add(cardId);
                    }
                }
            }

            var baseInfo = "卡牌: " + cards.Length + "    费用: 0费" + cost0 + "  1费" + cost1 + "  2费" + cost2 + "  3费+" + cost3Plus;
            if (!includePreview)
            {
                return baseInfo;
            }

            var preview = previewNames.Count == 0 ? "无" : string.Join(" / ", previewNames.ToArray());
            return baseInfo + "\n示例: " + preview;
        }
    }
}
