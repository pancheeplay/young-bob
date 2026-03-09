#if ((UNITY_WEBGL || UNITY_MINIGAME || WEIXINMINIGAME) && !UNITY_EDITOR) || ((UNITY_WEBGL || UNITY_MINIGAME) && UNITY_EDITOR && TAP_DEBUG_ENABLE)
using System;
using System.Collections.Generic;
using TapTapMiniGame;
using UnityEngine;
using LitJson;

namespace YoungBob.Prototype.Multiplayer
{
    public sealed class TapTapRuntimeMultiplayerService : IMultiplayerService, ITapBattleEventHandler
    {
        private sealed class PlayerCustomProperties
        {
            public string playerName;
            public string avatarUrl;

            public PlayerCustomProperties(string playerName, string avatarUrl)
            {
                this.playerName = playerName;
                this.avatarUrl = avatarUrl;
            }
        }

        private sealed class RoomCustomProperties
        {
            public string gameMode;
            public string ownerName;
            public string roomName;
            public string ownerAvatarUrl;
            public string roomDescription;
            public string battleStatus;

            public RoomCustomProperties(string ownerName, string roomName, string ownerAvatarUrl, string roomDescription, string battleStatus)
            {
                gameMode = "young_bob_proto";
                this.ownerName = ownerName;
                this.roomName = roomName;
                this.ownerAvatarUrl = ownerAvatarUrl;
                this.roomDescription = roomDescription;
                this.battleStatus = battleStatus;
            }
        }

        private string _localPlayerId;
        private string _localDisplayName;
        private string _roomId;
        private string _hostPlayerId;
        private bool _isRoomListRequestInProgress;
        private List<MultiplayerPlayer> _players = new List<MultiplayerPlayer>();

        public event Action<string> Connected;
        public event Action<string> TransportError;
        public event Action<RoomJoinedEvent> RoomJoined;
        public event Action<IReadOnlyList<RoomListItem>> RoomListUpdated;
        public event Action<MultiplayerMessage> MessageReceived;

        public bool IsAvailable
        {
            get { return true; }
        }

        public string ServiceName
        {
            get { return "TapTap"; }
        }

        public void Connect(string playerId, string displayName)
        {
            _localDisplayName = displayName;
            TapBattleClient.Initialize(this);
            TapBattleClient.Connect(new BattleConnectOption
            {
                success = result =>
                {
                    _localPlayerId = result.playerId;
                    Connected?.Invoke(_localPlayerId);
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] Connect failed: " + error.errMsg);
                }
            });
        }

        public void Disconnect()
        {
            TapBattleClient.Disconnect(new BattleOption
            {
                success = _ =>
                {
                    _roomId = null;
                    _players.Clear();
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] Disconnect failed: " + error.errMsg);
                }
            });
        }

        public void CreateRoom()
        {
            var roomProps = new RoomCustomProperties(_localDisplayName, "Young Bob Room", string.Empty, "Young Bob co-op test room", "idle");
            var option = new CreateRoomOption
            {
                data = new CreateRoomRequest
                {
                    roomCfg = new RoomConfig
                    {
                        maxPlayerCount = 2,
                        type = "young_bob_proto",
                        name = "Young Bob Room",
                        customProperties = JsonMapper.ToJson(roomProps),
                        matchParams = new Dictionary<string, string>
                        {
                            { "mode", "co_op" },
                            { "tier", "proto" }
                        }
                    },
                    playerCfg = new PlayerConfig
                    {
                        customStatus = 0,
                        customProperties = JsonMapper.ToJson(new PlayerCustomProperties(_localDisplayName, string.Empty))
                    }
                },
                success = response =>
                {
                    PublishRoomJoined(response.roomInfo);
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] CreateRoom failed: " + error.errMsg);
                }
            };

            Debug.Log("[TapTap] CreateRoom request: " + JsonMapper.ToJson(option.data));
            TapBattleClient.CreateRoom(option);
        }

        public void MatchOrCreateRoom()
        {
            var roomProps = new RoomCustomProperties(_localDisplayName, "Young Bob Room", string.Empty, "Young Bob co-op test room", "idle");
            var option = new MatchRoomOption
            {
                data = new MatchRoomRequest
                {
                    roomCfg = new RoomConfig
                    {
                        maxPlayerCount = 2,
                        type = "young_bob_proto",
                        customProperties = JsonMapper.ToJson(roomProps),
                        matchParams = new Dictionary<string, string>
                        {
                            { "mode", "co_op" },
                            { "tier", "proto" }
                        }
                    },
                    playerCfg = new PlayerConfig
                    {
                        customStatus = 0,
                        customProperties = JsonMapper.ToJson(new PlayerCustomProperties(_localDisplayName, string.Empty))
                    }
                },
                success = response =>
                {
                    PublishRoomJoined(response.roomInfo);
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] MatchRoom failed: " + error.errMsg);
                }
            };

            Debug.Log("[TapTap] MatchRoom request: " + JsonMapper.ToJson(option.data));
            TapBattleClient.MatchRoom(option);
        }

        public void LeaveRoom()
        {
            if (string.IsNullOrEmpty(_roomId))
            {
                RoomJoined?.Invoke(null);
                return;
            }

            TapBattleClient.LeaveRoom(new LeaveRoomOption
            {
                success = _ =>
                {
                    _roomId = null;
                    _hostPlayerId = null;
                    _players.Clear();
                    RoomJoined?.Invoke(null);
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] LeaveRoom failed: " + error.errMsg);
                }
            });
        }

        public void RefreshRoomList()
        {
            if (_isRoomListRequestInProgress)
            {
                return;
            }

            _isRoomListRequestInProgress = true;
            TapBattleClient.GetRoomList(new GetRoomListOption
            {
                data = new GetRoomListRequest
                {
                    roomType = "young_bob_proto",
                    offset = 0,
                    limit = 20
                },
                success = result =>
                {
                    var rooms = new List<RoomListItem>();
                    if (result.rooms != null)
                    {
                        for (var i = 0; i < result.rooms.Length; i++)
                        {
                            var room = result.rooms[i];
                            rooms.Add(new RoomListItem
                            {
                                roomId = room.id,
                                roomName = string.IsNullOrEmpty(room.name) ? room.id : room.name,
                                roomType = "young_bob_proto",
                                playerCount = room.playerCount,
                                maxPlayerCount = room.maxPlayerCount
                            });
                        }
                    }

                    RoomListUpdated?.Invoke(rooms);
                    _isRoomListRequestInProgress = false;
                },
                fail = error =>
                {
                    _isRoomListRequestInProgress = false;
                    Debug.LogError("[TapTap] GetRoomList failed: " + error.errMsg);
                },
                complete = _ =>
                {
                    _isRoomListRequestInProgress = false;
                }
            });
        }

        public void JoinRoom(string roomId)
        {
            TapBattleClient.JoinRoom(new JoinRoomOption
            {
                data = new JoinRoomRequest
                {
                    roomId = roomId,
                    playerCfg = new PlayerConfig
                    {
                        customStatus = 0,
                        customProperties = JsonMapper.ToJson(new PlayerCustomProperties(_localDisplayName, string.Empty))
                    }
                },
                success = result =>
                {
                    PublishRoomJoined(result.roomInfo);
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] JoinRoom failed: " + error.errMsg);
                }
            });
        }

        public void Send(MultiplayerMessage message)
        {
            JsonData payloadData = null;
            if (!string.IsNullOrEmpty(message.payloadJson))
            {
                try
                {
                    payloadData = JsonMapper.ToObject(message.payloadJson);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[TapTap] Failed to parse outgoing payloadJson: " + ex.Message + " raw=" + message.payloadJson);
                    return;
                }
            }

            var payload = new JsonData();
            payload["messageId"] = message.messageId ?? string.Empty;
            payload["type"] = message.type ?? string.Empty;
            payload["senderPlayerId"] = message.senderPlayerId ?? string.Empty;
            payload["roomId"] = message.roomId ?? string.Empty;
            payload["seq"] = message.seq;
            if (payloadData != null)
            {
                payload["payload"] = payloadData;
            }

            var json = JsonMapper.ToJson(payload);
            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Debug.LogError("[TapTap] SendCustomMessage aborted: serialized message is empty. type=" + message.type);
                return;
            }

            Debug.Log("[TapTap] SendCustomMessage payload: " + json);

            TapBattleClient.SendCustomMessage(new SendCustomMessageOption
            {
                data = new SendCustomMessageData
                {
                    msg = json,
                    type = 0,
                    receivers = new string[0]
                },
                success = _ =>
                {
                    // type=0 does not echo back to sender, so apply locally on success.
                    MessageReceived?.Invoke(message);
                },
                fail = error =>
                {
                    Debug.LogError("[TapTap] SendCustomMessage failed: " + error.errMsg);
                }
            });
        }

        public void OnDisconnected(DisconnectedInfo info)
        {
            Debug.LogWarning("[TapTap] Disconnected: " + info.reason + " code=" + info.code);
        }

        public void OnBattleServiceError(BattleServiceErrorInfo info)
        {
            Debug.LogError("[TapTap] Battle service error: " + info.errorMessage + " code=" + info.errorCode);
        }

        public void OnRoomPropertiesChange(RoomPropertiesNotification info)
        {
        }

        public void OnPlayerCustomPropertiesChange(PlayerCustomPropertiesNotification info)
        {
        }

        public void OnPlayerCustomStatusChange(PlayerCustomStatusNotification info)
        {
        }

        public void OnFrameSyncStop(FrameSyncStopInfo info)
        {
        }

        public void OnFrameInput(string frameData)
        {
        }

        public void OnFrameSyncStart(FrameSyncStartInfo info)
        {
        }

        public void OnPlayerOffline(PlayerOfflineNotification info)
        {
            Debug.LogWarning("[TapTap] Player offline: " + info.playerId);
        }

        public void OnPlayerLeaveRoom(LeaveRoomNotification info)
        {
            if (info.playerId == _localPlayerId)
            {
                _roomId = null;
                _hostPlayerId = null;
                _players.Clear();
                RoomJoined?.Invoke(null);
                return;
            }

            _hostPlayerId = info.roomOwnerId;
            _players.RemoveAll(player => player.playerId == info.playerId);
            PublishRoomJoinedSnapshot();
        }

        public void OnPlayerEnterRoom(EnterRoomNotification info)
        {
            var isKnown = false;
            for (var i = 0; i < _players.Count; i++)
            {
                if (_players[i].playerId == info.playerInfo.id)
                {
                    isKnown = true;
                    break;
                }
            }

            if (!isKnown)
            {
                _players.Add(ToPlayer(info.playerInfo, _hostPlayerId));
            }

            PublishRoomJoinedSnapshot();
        }

        public void OnCustomMessage(CustomMessageNotification info)
        {
            MultiplayerMessage message;
            try
            {
                var data = JsonMapper.ToObject(info.msg);
                message = new MultiplayerMessage
                {
                    messageId = data.Keys.Contains("messageId") ? data["messageId"].ToString() : string.Empty,
                    type = data.Keys.Contains("type") ? data["type"].ToString() : string.Empty,
                    senderPlayerId = data.Keys.Contains("senderPlayerId") ? data["senderPlayerId"].ToString() : string.Empty,
                    roomId = data.Keys.Contains("roomId") ? data["roomId"].ToString() : string.Empty,
                    seq = data.Keys.Contains("seq") ? int.Parse(data["seq"].ToString()) : 0,
                    payloadJson = data.Keys.Contains("payload") ? JsonMapper.ToJson(data["payload"]) : string.Empty
                };
            }
            catch (Exception ex)
            {
                Debug.LogError("[TapTap] Failed to parse custom message: " + ex.Message + " raw=" + info.msg);
                return;
            }

            if (message == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(message.senderPlayerId))
            {
                message.senderPlayerId = info.playerId;
            }

            if (string.IsNullOrEmpty(message.roomId))
            {
                message.roomId = _roomId;
            }

            MessageReceived?.Invoke(message);
        }

        public void OnPlayerKicked(PlayerKickedInfo info)
        {
            Debug.LogWarning("[TapTap] Player kicked: " + info.playerId);
        }

        private void PublishRoomJoined(RoomInfo roomInfo)
        {
            _roomId = roomInfo.id;
            _hostPlayerId = roomInfo.ownerId;
            _players = new List<MultiplayerPlayer>();
            if (roomInfo.players != null)
            {
                for (var i = 0; i < roomInfo.players.Length; i++)
                {
                    _players.Add(ToPlayer(roomInfo.players[i], roomInfo.ownerId));
                }
            }

            PublishRoomJoinedSnapshot();
        }

        private void PublishRoomJoinedSnapshot()
        {
            RoomJoined?.Invoke(new RoomJoinedEvent
            {
                roomId = _roomId,
                localPlayerId = _localPlayerId,
                hostPlayerId = _hostPlayerId,
                players = new List<MultiplayerPlayer>(_players)
            });
        }

        private MultiplayerPlayer ToPlayer(PlayerInfo playerInfo, string hostPlayerId)
        {
            var displayName = playerInfo.id;
            if (!string.IsNullOrEmpty(playerInfo.customProperties))
            {
                try
                {
                    var data = JsonMapper.ToObject(playerInfo.customProperties);
                    if (data.Keys.Contains("playerName"))
                    {
                        displayName = data["playerName"].ToString();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TapTap] Failed to parse player customProperties: " + ex.Message);
                }
            }

            return new MultiplayerPlayer
            {
                playerId = playerInfo.id,
                displayName = displayName,
                isLocal = playerInfo.id == _localPlayerId,
                isHost = playerInfo.id == hostPlayerId
            };
        }
    }
}
#endif
