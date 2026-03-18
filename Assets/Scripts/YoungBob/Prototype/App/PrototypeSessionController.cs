using System;
using System.Collections.Generic;
using UnityEngine;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.App
{
    [Serializable]
    public sealed class BattleStartPayload
    {
        public int randomSeed;
        public string stageId;
        public string encounterId;
        public string starterDeckId;
        public PlayerDeckChoicePayload[] playerDecks;
    }

    [Serializable]
    public sealed class PlayerDeckChoicePayload
    {
        public string playerId;
        public string deckId;
    }

    [Serializable]
    public sealed class RoomStageSelectionPayload
    {
        public string stageId;
    }

    [Serializable]
    public sealed class RoomDeckSelectionPayload
    {
        public string playerId;
        public string deckId;
    }

    [Serializable]
    public sealed class BattleCommandPayload
    {
        public string commandId;
        public string actorPlayerId;
        public string action;
        public string cardInstanceId;
        public BattleTargetFaction targetFaction;
        public string targetUnitId;
        public BattleArea targetArea;
    }

    [Serializable]
    public sealed class BattleCommitPayload
    {
        public string sourceCommandId;
        public string stateJson;
        public BattleEvent[] events;
    }

    [Serializable]
    public sealed class BattleFinishPayload
    {
        public string reason;
    }

    public sealed class PrototypeSessionController
        : IDisposable
    {
        private readonly IMultiplayerService _multiplayer;
        private readonly BattleEngine _battleEngine;
        private readonly GameDataRepository _dataRepository;
        private readonly IReadOnlyList<StageDefinition> _availableStages;
        private readonly IReadOnlyList<DeckDefinition> _availableDecks;

        private RoomJoinedEvent _room;
        private int _seq;
        private readonly LobbySelectionState _lobbySelection;
        private readonly SessionMessageGuard _messageGuard = new SessionMessageGuard();
        private bool _disposed;

        private const string QuickChatMessageType = "room.quick_chat";
        private const string QuickChatGoodPlayId = "good_play";
        private const string QuickChatSorryId = "sorry";
        private const string QuickChatThanksId = "thanks";
        private const string QuickChatHelpId = "help";

        public PrototypeSessionController(IMultiplayerService multiplayer, BattleEngine battleEngine, GameDataRepository dataRepository)
        {
            _multiplayer = multiplayer;
            _battleEngine = battleEngine;
            _dataRepository = dataRepository;
            _availableStages = _dataRepository.GetAllStages();
            _availableDecks = _dataRepository.GetAllDecks();
            _lobbySelection = new LobbySelectionState(ResolveDefaultDeckId());

            _multiplayer.Connected += OnConnected;
            _multiplayer.TransportError += OnTransportError;
            _multiplayer.RoomJoined += OnRoomJoined;
            _multiplayer.RoomListUpdated += OnRoomListUpdated;
            _multiplayer.MessageReceived += OnMessageReceived;
        }

        public event Action<string> StatusChanged;
        public event Action<RoomJoinedEvent> RoomChanged;
        public event Action<IReadOnlyList<RoomListItem>> RoomListChanged;
        public event Action<BattleState> BattleStateChanged;
        public event Action<string> LogAdded;
        public event Action<string> RoomChatAdded;
        public event Action StageSelectionChanged;

        public BattleState CurrentBattleState { get; private set; }
        public RoomJoinedEvent CurrentRoom { get; private set; }
        public string LocalPlayerId { get; private set; }
        public string ServiceName
        {
            get { return _multiplayer.ServiceName; }
        }

        public bool IsLocalHost
        {
            get { return _room != null && _room.localPlayerId == _room.hostPlayerId; }
        }

        public StageDefinition SelectedStage
        {
            get
            {
                return _lobbySelection.GetSelectedStage(_availableStages);
            }
        }

        public int AvailableStageCount
        {
            get { return _availableStages == null ? 0 : _availableStages.Count; }
        }

        public IReadOnlyList<StageDefinition> AvailableStages
        {
            get { return _availableStages; }
        }

        public IReadOnlyList<DeckDefinition> AvailableDecks
        {
            get { return _availableDecks; }
        }

        public string LocalSelectedDeckId
        {
            get { return _lobbySelection.LocalSelectedDeckId; }
        }

        public string AvailabilityText
        {
            get
            {
                return _multiplayer.IsAvailable
                    ? _multiplayer.ServiceName + " service ready"
                    : _multiplayer.ServiceName + " service unavailable";
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _multiplayer.Connected -= OnConnected;
            _multiplayer.TransportError -= OnTransportError;
            _multiplayer.RoomJoined -= OnRoomJoined;
            _multiplayer.RoomListUpdated -= OnRoomListUpdated;
            _multiplayer.MessageReceived -= OnMessageReceived;
        }

        public void StartSession(string displayNameOverride = null)
        {
            var suffix = UnityEngine.Random.Range(1000, 9999);
            var playerId = "player_" + suffix;
            var displayName = string.IsNullOrWhiteSpace(displayNameOverride) ? SystemInfo.deviceName : displayNameOverride.Trim();
            StatusChanged?.Invoke("正在连接 " + _multiplayer.ServiceName + "...");
            _multiplayer.Connect(playerId, displayName);
        }

        public void BeginMatchmaking()
        {
            StatusChanged?.Invoke("正在匹配房间...");
            _multiplayer.MatchOrCreateRoom();
        }

        public void CreateRoom()
        {
            StatusChanged?.Invoke("正在创建房间...");
            _multiplayer.CreateRoom();
        }

        public void LeaveRoom()
        {
            StatusChanged?.Invoke("正在离开房间...");
            _multiplayer.LeaveRoom();
        }

        public void Disconnect()
        {
            StatusChanged?.Invoke("正在断开连接...");
            _multiplayer.Disconnect();
        }

        public void RefreshRoomList()
        {
            _multiplayer.RefreshRoomList();
        }

        public void JoinRoom(string roomId)
        {
            StatusChanged?.Invoke("正在加入房间...");
            _multiplayer.JoinRoom(roomId);
        }

        public void StartBattle()
        {
            LogAdded?.Invoke("点击了开始关卡。");
            if (_room == null)
            {
                StatusChanged?.Invoke("请先加入房间。");
                LogAdded?.Invoke("开始关卡已取消：房间为空。");
                return;
            }

            if (!IsLocalHost)
            {
                StatusChanged?.Invoke("只有房主才能开始关卡。");
                LogAdded?.Invoke("开始关卡已取消：本地玩家不是房主。");
                return;
            }

            var selectedStage = SelectedStage;
            if (selectedStage == null)
            {
                StatusChanged?.Invoke("没有可用关卡。");
                LogAdded?.Invoke("开始关卡已取消：没有可用关卡。");
                return;
            }

            var payload = new BattleStartPayload
            {
                randomSeed = 24681357,
                stageId = selectedStage.id,
                starterDeckId = ResolveDefaultDeckId(),
                playerDecks = BuildPlayerDeckChoices()
            };

            LogAdded?.Invoke("正在广播 battle.start，房间 " + _room.roomId + "，关卡 " + selectedStage.id + "。");
            Broadcast("battle.start", JsonUtility.ToJson(payload));
        }

        public void SelectPreviousStage()
        {
            if (!IsLocalHost)
            {
                return;
            }

            if (!_lobbySelection.SelectPreviousStage(_availableStages))
            {
                return;
            }
            StageSelectionChanged?.Invoke();
            BroadcastStageSelection();
        }

        public void SelectNextStage()
        {
            if (!IsLocalHost)
            {
                return;
            }

            if (!_lobbySelection.SelectNextStage(_availableStages))
            {
                return;
            }
            StageSelectionChanged?.Invoke();
            BroadcastStageSelection();
        }

        public void SelectStageById(string stageId)
        {
            if (!IsLocalHost)
            {
                return;
            }

            if (_lobbySelection.SelectStageById(_availableStages, stageId))
            {
                StageSelectionChanged?.Invoke();
                BroadcastStageSelection();
            }
        }

        public void SelectLocalDeck(string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId))
            {
                return;
            }

            try
            {
                _dataRepository.GetDeck(deckId);
            }
            catch (Exception ex)
            {
                LogAdded?.Invoke("本地牌组选择被拒绝：无效牌组 " + deckId + "。 " + ex.Message);
                return;
            }

            _lobbySelection.SelectLocalDeck(LocalPlayerId, deckId);

            StageSelectionChanged?.Invoke();
            BroadcastDeckSelection(LocalPlayerId, deckId);
        }

        public void SendQuickChat(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
            {
                return;
            }

            if (_room == null || string.IsNullOrWhiteSpace(LocalPlayerId))
            {
                return;
            }

            var payload = new QuickChatPayload
            {
                presetId = presetId
            };

            Broadcast(QuickChatMessageType, JsonUtility.ToJson(payload));
        }

        public string GetSelectedDeckIdForPlayer(string playerId)
        {
            return _lobbySelection.GetSelectedDeckIdForPlayer(playerId, ResolveDefaultDeckId());
        }

        public void PlayCard(string cardInstanceId, BattleTargetFaction targetFaction, string targetUnitId, BattleArea targetArea)
        {
            if (CurrentBattleState == null)
            {
                return;
            }

            if (GetLocalBattlePlayer() == null)
            {
                return;
            }

            var payload = new BattleCommandPayload
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = LocalPlayerId,
                action = "play_card",
                cardInstanceId = cardInstanceId,
                targetFaction = targetFaction,
                targetUnitId = targetUnitId,
                targetArea = targetArea
            };
            Broadcast("battle.command", JsonUtility.ToJson(payload));
        }

        public void EndTurn()
        {
            if (CurrentBattleState == null)
            {
                return;
            }

            if (GetLocalBattlePlayer() == null)
            {
                return;
            }

            var payload = new BattleCommandPayload
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = LocalPlayerId,
                action = "end_turn"
            };
            Broadcast("battle.command", JsonUtility.ToJson(payload));
        }

        public CardDefinition GetCardDefinition(string cardId)
        {
            return _dataRepository.GetCard(cardId);
        }

        public PlayerBattleState GetLocalBattlePlayer()
        {
            return CurrentBattleState == null ? null : CurrentBattleState.GetPlayer(LocalPlayerId);
        }

        public bool CanLocalPlayerAct()
        {
            var player = GetLocalBattlePlayer();
            return CurrentBattleState != null
                && CurrentBattleState.phase == BattlePhase.PlayerTurn
                && player != null
                && player.hp > 0
                && !player.hasEndedTurn;
        }

        public void EndBattleAndReturnToLobby()
        {
            if (_room != null && IsLocalHost)
            {
                Broadcast("battle.finish", JsonUtility.ToJson(new BattleFinishPayload
                {
                    reason = "host_exit"
                }));
                ApplyBattleFinished();
            }
            else if (_room != null)
            {
                LeaveRoom();
            }
            else
            {
                RoomChanged?.Invoke(null);
            }
        }

        private void OnConnected(string localPlayerId)
        {
            LocalPlayerId = localPlayerId;
            StatusChanged?.Invoke("已连接，玩家ID " + localPlayerId);
        }

        private void OnTransportError(string message)
        {
            StatusChanged?.Invoke(message);
            LogAdded?.Invoke(message);
        }

        private void OnRoomJoined(RoomJoinedEvent room)
        {
            if (room == null)
            {
                _room = null;
                CurrentRoom = null;
                CurrentBattleState = null;
                _lobbySelection.ClearRoomPlayerSelections();
                ResetMessageTracking();
                StatusChanged?.Invoke("已离开房间");
                RoomChanged?.Invoke(null);
                BattleStateChanged?.Invoke(null);
                return;
            }

            var roomChanged = _room == null || !string.Equals(_room.roomId, room.roomId, StringComparison.Ordinal);
            if (roomChanged)
            {
                ResetMessageTracking();
            }

            _room = room;
            CurrentRoom = room;
            EnsureRoomDeckSelections(room);
            StatusChanged?.Invoke("已加入房间 " + room.roomId + "，当前玩家 " + room.players.Count + " 人");
            RoomChanged?.Invoke(room);
            StageSelectionChanged?.Invoke();

            if (!string.IsNullOrWhiteSpace(LocalPlayerId))
            {
                BroadcastDeckSelection(LocalPlayerId, GetSelectedDeckIdForPlayer(LocalPlayerId));
            }

            if (IsLocalHost)
            {
                BroadcastStageSelection();
            }
        }

        private void OnRoomListUpdated(IReadOnlyList<RoomListItem> rooms)
        {
            RoomListChanged?.Invoke(rooms);
        }

        private void OnMessageReceived(MultiplayerMessage message)
        {
            if (_disposed)
            {
                return;
            }

            if (!TryAcceptMessage(message))
            {
                return;
            }

            LogAdded?.Invoke("收到消息： " + message.type + " seq=" + message.seq + " sender=" + message.senderPlayerId);
            switch (message.type)
            {
                case "battle.start":
                    HandleBattleStart(message);
                    break;
                case "battle.command":
                    HandleBattleCommand(message);
                    break;
                case "battle.commit":
                    HandleBattleCommit(message);
                    break;
                case "battle.finish":
                    HandleBattleFinish(message);
                    break;
                case "room.stage.select":
                    HandleRoomStageSelection(message);
                    break;
                case "room.deck.select":
                    HandleRoomDeckSelection(message);
                    break;
                case QuickChatMessageType:
                    HandleRoomQuickChat(message);
                    break;
                default:
                    LogAdded?.Invoke("忽略未知消息类型： " + message.type);
                    break;
            }
        }

        private void HandleBattleStart(MultiplayerMessage message)
        {
            LogAdded?.Invoke("正在处理 battle.start，房间 " + message.roomId + "。");
            if (_room == null || _room.players == null)
            {
                LogAdded?.Invoke("battle.start 已忽略：没有有效的房间上下文。");
                return;
            }

            if (!TryParsePayload(message, out BattleStartPayload payload))
            {
                return;
            }

            var setup = new BattleSetupDefinition
            {
                roomId = message.roomId,
                randomSeed = payload.randomSeed,
                stageId = payload.stageId,
                encounterId = payload.encounterId,
                starterDeckId = payload.starterDeckId
            };

            for (var i = 0; i < _room.players.Count; i++)
            {
                var player = _room.players[i];
                setup.players.Add(new BattleParticipantDefinition
                {
                    playerId = player.playerId,
                    displayName = player.displayName,
                    starterDeckId = ResolvePlayerDeckId(payload, player.playerId)
                });
            }

            try
            {
                CurrentBattleState = _battleEngine.CreateInitialState(setup);
            }
            catch (Exception ex)
            {
                LogAdded?.Invoke("battle.start 失败： " + ex.Message);
                return;
            }

            var monsterName = CurrentBattleState.monster == null ? "未知" : CurrentBattleState.monster.displayName;
            LogAdded?.Invoke("战斗开始，对手为 " + monsterName + "。");
            BattleStateChanged?.Invoke(CurrentBattleState);
        }

        private void HandleBattleCommand(MultiplayerMessage message)
        {
            if (CurrentBattleState == null || !IsLocalHost)
            {
                return;
            }

            if (!TryParsePayload(message, out BattleCommandPayload payload))
            {
                return;
            }

            if (!string.Equals(payload.actorPlayerId, message.senderPlayerId, StringComparison.Ordinal))
            {
                LogAdded?.Invoke("拒绝指令：行动者与发送者不一致。");
                return;
            }

            if (string.IsNullOrWhiteSpace(payload.action))
            {
                LogAdded?.Invoke("拒绝指令：操作为空。");
                return;
            }

            var result = _battleEngine.Apply(CurrentBattleState, new BattleCommand
            {
                commandId = payload.commandId,
                actorPlayerId = payload.actorPlayerId,
                action = payload.action,
                cardInstanceId = payload.cardInstanceId,
                targetFaction = payload.targetFaction,
                targetUnitId = payload.targetUnitId,
                targetArea = payload.targetArea
            });

            if (!result.success && !string.IsNullOrEmpty(result.error))
            {
                LogAdded?.Invoke("拒绝指令： " + result.error);
                return;
            }

            var commitPayload = new BattleCommitPayload
            {
                sourceCommandId = payload.commandId,
                stateJson = JsonUtility.ToJson(CurrentBattleState),
                events = result.events.ToArray()
            };
            Broadcast("battle.commit", JsonUtility.ToJson(commitPayload));

            if (CurrentBattleState.phase == BattlePhase.Victory || CurrentBattleState.phase == BattlePhase.Defeat)
            {
                Broadcast("battle.finish", JsonUtility.ToJson(new BattleFinishPayload
                {
                    reason = CurrentBattleState.phase.ToString()
                }));
            }
        }

        private void HandleBattleCommit(MultiplayerMessage message)
        {
            if (!TryParsePayload(message, out BattleCommitPayload payload))
            {
                return;
            }

            if (!TryParsePayloadJson(payload.stateJson, "battle.commit state", out BattleState state))
            {
                return;
            }

            CurrentBattleState = state;
            if (payload.events != null)
            {
                for (var i = 0; i < payload.events.Length; i++)
                {
                    LogAdded?.Invoke(payload.events[i].message);
                }
            }

            BattleStateChanged?.Invoke(CurrentBattleState);
        }

        private void HandleBattleFinish(MultiplayerMessage message)
        {
            ApplyBattleFinished();
        }

        private void ApplyBattleFinished()
        {
            CurrentBattleState = null;
            BattleStateChanged?.Invoke(null);
            if (_room != null)
            {
                LeaveRoom();
            }
        }

        private string ResolveDefaultDeckId()
        {
            if (_availableDecks != null)
            {
                for (var i = 0; i < _availableDecks.Count; i++)
                {
                    var deck = _availableDecks[i];
                    if (deck == null || string.IsNullOrWhiteSpace(deck.id))
                    {
                        continue;
                    }

                    if (string.Equals(deck.id, "co_op_starter", StringComparison.OrdinalIgnoreCase))
                    {
                        return deck.id;
                    }
                }

                for (var i = 0; i < _availableDecks.Count; i++)
                {
                    var deck = _availableDecks[i];
                    if (deck != null && !string.IsNullOrWhiteSpace(deck.id))
                    {
                        return deck.id;
                    }
                }
            }

            return "co_op_starter";
        }

        private void EnsureRoomDeckSelections(RoomJoinedEvent room)
        {
            _lobbySelection.EnsureRoomDeckSelections(room, LocalPlayerId, ResolveDefaultDeckId());
        }

        private PlayerDeckChoicePayload[] BuildPlayerDeckChoices()
        {
            return _lobbySelection.BuildPlayerDeckChoices(_room, ResolveDefaultDeckId());
        }

        private string ResolvePlayerDeckId(BattleStartPayload payload, string playerId)
        {
            if (payload != null && payload.playerDecks != null)
            {
                for (var i = 0; i < payload.playerDecks.Length; i++)
                {
                    var choice = payload.playerDecks[i];
                    if (choice == null || !string.Equals(choice.playerId, playerId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(choice.deckId))
                    {
                        return choice.deckId;
                    }
                }
            }

            if (payload != null && !string.IsNullOrWhiteSpace(payload.starterDeckId))
            {
                return payload.starterDeckId;
            }

            return ResolveDefaultDeckId();
        }

        private void BroadcastStageSelection()
        {
            if (_room == null || !IsLocalHost || SelectedStage == null)
            {
                return;
            }

            var payload = new RoomStageSelectionPayload
            {
                stageId = SelectedStage.id
            };
            Broadcast("room.stage.select", JsonUtility.ToJson(payload));
        }

        private void BroadcastDeckSelection(string playerId, string deckId)
        {
            if (_room == null || string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(deckId))
            {
                return;
            }

            var payload = new RoomDeckSelectionPayload
            {
                playerId = playerId,
                deckId = deckId
            };
            Broadcast("room.deck.select", JsonUtility.ToJson(payload));
        }

        private void HandleRoomStageSelection(MultiplayerMessage message)
        {
            if (_room == null || !string.Equals(message.senderPlayerId, _room.hostPlayerId, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryParsePayload(message, out RoomStageSelectionPayload payload))
            {
                return;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.stageId) || _availableStages == null)
            {
                return;
            }

            if (_lobbySelection.SelectStageById(_availableStages, payload.stageId))
            {
                StageSelectionChanged?.Invoke();
            }
        }

        private void HandleRoomDeckSelection(MultiplayerMessage message)
        {
            if (!TryParsePayload(message, out RoomDeckSelectionPayload payload))
            {
                return;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.playerId) || string.IsNullOrWhiteSpace(payload.deckId))
            {
                return;
            }

            if (!string.Equals(payload.playerId, message.senderPlayerId, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                _dataRepository.GetDeck(payload.deckId);
            }
            catch (Exception ex)
            {
                LogAdded?.Invoke("房间牌组选择被拒绝：无效牌组 " + payload.deckId + "。 " + ex.Message);
                return;
            }

            _lobbySelection.ApplyRemoteDeckSelection(payload.playerId, payload.deckId, LocalPlayerId);

            StageSelectionChanged?.Invoke();
        }

        private void HandleRoomQuickChat(MultiplayerMessage message)
        {
            if (!TryParsePayload(message, out QuickChatPayload payload))
            {
                return;
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.presetId))
            {
                return;
            }

            var speakerName = ResolvePlayerDisplayName(message.senderPlayerId);
            var chatText = ResolveQuickChatText(payload.presetId);
            if (string.IsNullOrWhiteSpace(chatText))
            {
                return;
            }

            RoomChatAdded?.Invoke(speakerName + ": " + chatText);
        }

        private void Broadcast(string type, string payloadJson)
        {
            if (_room == null)
            {
                LogAdded?.Invoke("跳过广播 " + type + "：房间为空。");
                return;
            }

            if (string.IsNullOrWhiteSpace(LocalPlayerId))
            {
                LogAdded?.Invoke("跳过广播 " + type + "：LocalPlayerId 为空。");
                return;
            }

            _seq += 1;
            LogAdded?.Invoke("发送消息 " + type + " seq=" + _seq + " room=" + _room.roomId + "。");
            _multiplayer.Send(new MultiplayerMessage
            {
                messageId = Guid.NewGuid().ToString("N"),
                type = type,
                senderPlayerId = LocalPlayerId,
                roomId = _room.roomId,
                seq = _seq,
                payloadJson = payloadJson
            });
        }

        private bool TryAcceptMessage(MultiplayerMessage message)
        {
            return _messageGuard.TryAccept(message, _room == null ? null : _room.roomId, Log);
        }

        private bool TryParsePayload<T>(MultiplayerMessage message, out T payload)
            where T : class
        {
            return SessionPayloadParser.TryParsePayload(message, Log, out payload);
        }

        private bool TryParsePayloadJson<T>(string json, string context, out T payload)
            where T : class
        {
            return SessionPayloadParser.TryParseJson(json, context, Log, out payload);
        }

        private void ResetMessageTracking()
        {
            _messageGuard.Reset();
        }

        private void Log(string message)
        {
            LogAdded?.Invoke(message);
        }

        private string ResolvePlayerDisplayName(string playerId)
        {
            if (_room != null && _room.players != null)
            {
                for (var i = 0; i < _room.players.Count; i++)
                {
                    var player = _room.players[i];
                    if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal))
                    {
                        return string.IsNullOrWhiteSpace(player.displayName) ? player.playerId : player.displayName;
                    }
                }
            }

            return string.IsNullOrWhiteSpace(playerId) ? "未知玩家" : playerId;
        }

        private static string ResolveQuickChatText(string presetId)
        {
            if (string.Equals(presetId, QuickChatGoodPlayId, StringComparison.OrdinalIgnoreCase))
            {
                return "打得不错!";
            }

            if (string.Equals(presetId, QuickChatSorryId, StringComparison.OrdinalIgnoreCase))
            {
                return "抱歉!";
            }

            if (string.Equals(presetId, QuickChatThanksId, StringComparison.OrdinalIgnoreCase))
            {
                return "谢谢你!";
            }

            if (string.Equals(presetId, QuickChatHelpId, StringComparison.OrdinalIgnoreCase))
            {
                return "救我!";
            }

            return null;
        }
    }
}
