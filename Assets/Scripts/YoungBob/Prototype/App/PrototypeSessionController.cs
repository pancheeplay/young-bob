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
        public string encounterId;
        public string starterDeckId;
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
    {
        private readonly IMultiplayerService _multiplayer;
        private readonly BattleEngine _battleEngine;
        private readonly GameDataRepository _dataRepository;

        private RoomJoinedEvent _room;
        private int _seq;

        public PrototypeSessionController(IMultiplayerService multiplayer, BattleEngine battleEngine, GameDataRepository dataRepository)
        {
            _multiplayer = multiplayer;
            _battleEngine = battleEngine;
            _dataRepository = dataRepository;

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

        public string AvailabilityText
        {
            get
            {
                return _multiplayer.IsAvailable
                    ? _multiplayer.ServiceName + " service ready"
                    : _multiplayer.ServiceName + " service unavailable";
            }
        }

        public void StartSession(string displayNameOverride = null)
        {
            var suffix = UnityEngine.Random.Range(1000, 9999);
            var playerId = "player_" + suffix;
            var displayName = string.IsNullOrWhiteSpace(displayNameOverride) ? SystemInfo.deviceName : displayNameOverride.Trim();
            StatusChanged?.Invoke("Connecting with " + _multiplayer.ServiceName + "...");
            _multiplayer.Connect(playerId, displayName);
        }

        public void BeginMatchmaking()
        {
            StatusChanged?.Invoke("Matching room...");
            _multiplayer.MatchOrCreateRoom();
        }

        public void CreateRoom()
        {
            StatusChanged?.Invoke("Creating room...");
            _multiplayer.CreateRoom();
        }

        public void LeaveRoom()
        {
            StatusChanged?.Invoke("Leaving room...");
            _multiplayer.LeaveRoom();
        }

        public void Disconnect()
        {
            StatusChanged?.Invoke("Disconnecting...");
            _multiplayer.Disconnect();
        }

        public void RefreshRoomList()
        {
            _multiplayer.RefreshRoomList();
        }

        public void JoinRoom(string roomId)
        {
            StatusChanged?.Invoke("Joining room...");
            _multiplayer.JoinRoom(roomId);
        }

        public void StartBattle()
        {
            LogAdded?.Invoke("StartBattle clicked.");
            if (_room == null)
            {
                StatusChanged?.Invoke("Join a room first.");
                LogAdded?.Invoke("StartBattle aborted: room is null.");
                return;
            }

            if (!IsLocalHost)
            {
                StatusChanged?.Invoke("Only the host can start battle.");
                LogAdded?.Invoke("StartBattle aborted: local player is not host.");
                return;
            }

            var payload = new BattleStartPayload
            {
                randomSeed = 24681357,
                encounterId = "slime_intro",
                starterDeckId = "co_op_starter"
            };

            LogAdded?.Invoke("Broadcasting battle.start for room " + _room.roomId + ".");
            Broadcast("battle.start", JsonUtility.ToJson(payload));
        }

        public void PlayCard(string cardInstanceId, BattleTargetFaction targetFaction, string targetUnitId)
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
                targetUnitId = targetUnitId
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
            StatusChanged?.Invoke("Connected as " + localPlayerId);
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
                StatusChanged?.Invoke("Left room");
                RoomChanged?.Invoke(null);
                BattleStateChanged?.Invoke(null);
                return;
            }

            _room = room;
            CurrentRoom = room;
            StatusChanged?.Invoke("Joined room " + room.roomId + " with " + room.players.Count + " players");
            RoomChanged?.Invoke(room);
        }

        private void OnRoomListUpdated(IReadOnlyList<RoomListItem> rooms)
        {
            RoomListChanged?.Invoke(rooms);
        }

        private void OnMessageReceived(MultiplayerMessage message)
        {
            LogAdded?.Invoke("Received message: " + message.type + " seq=" + message.seq + " sender=" + message.senderPlayerId);
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
            }
        }

        private void HandleBattleStart(MultiplayerMessage message)
        {
            LogAdded?.Invoke("Handling battle.start for room " + message.roomId + ".");
            var payload = JsonUtility.FromJson<BattleStartPayload>(message.payloadJson);
            var setup = new BattleSetupDefinition
            {
                roomId = message.roomId,
                randomSeed = payload.randomSeed,
                encounterId = payload.encounterId,
                starterDeckId = payload.starterDeckId
            };

            for (var i = 0; i < _room.players.Count; i++)
            {
                var player = _room.players[i];
                setup.players.Add(new BattleParticipantDefinition
                {
                    playerId = player.playerId,
                    displayName = player.displayName
                });
            }

            CurrentBattleState = _battleEngine.CreateInitialState(setup);
            LogAdded?.Invoke("Battle started against " + CurrentBattleState.enemies.Count + " enemies.");
            BattleStateChanged?.Invoke(CurrentBattleState);
        }

        private void HandleBattleCommand(MultiplayerMessage message)
        {
            if (CurrentBattleState == null || !IsLocalHost)
            {
                return;
            }

            var payload = JsonUtility.FromJson<BattleCommandPayload>(message.payloadJson);
            var result = _battleEngine.Apply(CurrentBattleState, new BattleCommand
            {
                commandId = payload.commandId,
                actorPlayerId = payload.actorPlayerId,
                action = payload.action,
                cardInstanceId = payload.cardInstanceId,
                targetFaction = payload.targetFaction,
                targetUnitId = payload.targetUnitId
            });

            if (!result.success && !string.IsNullOrEmpty(result.error))
            {
                LogAdded?.Invoke("Rejected command: " + result.error);
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
            var payload = JsonUtility.FromJson<BattleCommitPayload>(message.payloadJson);
            CurrentBattleState = JsonUtility.FromJson<BattleState>(payload.stateJson);
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

        private void Broadcast(string type, string payloadJson)
        {
            if (_room == null)
            {
                LogAdded?.Invoke("Broadcast skipped for " + type + ": room is null.");
                return;
            }

            _seq += 1;
            LogAdded?.Invoke("Sending message " + type + " seq=" + _seq + " room=" + _room.roomId + ".");
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
    }
}
