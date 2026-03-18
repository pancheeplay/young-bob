using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Text;
#endif
using LitJson;
using UnityEngine;

namespace YoungBob.Prototype.Multiplayer
{
    public sealed class DebugRelayMultiplayerService : IMultiplayerService
    {
        private readonly Uri _serverUri;
        private readonly SynchronizationContext _mainThreadContext;

#if !UNITY_WEBGL || UNITY_EDITOR
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
#else
        private DebugRelayWebGlBridgeCallbacks _webGlBridge;
        private string _pendingDisplayName;
        private bool _isSocketOpen;
#endif
        private bool _hasReportedSocketNotOpen;
        private string _localPlayerId;
        private readonly Dictionary<string, MultiplayerMessage> _pendingSends = new Dictionary<string, MultiplayerMessage>();

        public DebugRelayMultiplayerService(string serverUrl)
        {
            _serverUri = ResolveServerUri(serverUrl);
            _mainThreadContext = SynchronizationContext.Current ?? new SynchronizationContext();
        }

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
            get { return "DebugRelay"; }
        }

        public async void Connect(string playerId, string displayName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _pendingDisplayName = displayName;
            _webGlBridge = DebugRelayWebGlBridgeCallbacks.Create(this);
            DebugRelayWebGlBridge.Connect(_serverUri.ToString(), _webGlBridge.gameObject.name);
            return;
#else
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            try
            {
                await _socket.ConnectAsync(_serverUri, _cts.Token);
                _ = ReceiveLoopAsync(_cts.Token);
                await SendPacketAsync("connect", new Dictionary<string, object>
                {
                    { "displayName", displayName }
                });
            }
            catch (Exception exception)
            {
                ReportError("DebugRelay connect failed: " + exception.Message);
            }
#endif
        }

        public void Disconnect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_isSocketOpen)
            {
                _ = SendPacketAsync("disconnect", new Dictionary<string, object>());
            }

            DebugRelayWebGlBridge.Close();
            _isSocketOpen = false;
            return;
#else
            if (_socket == null)
            {
                return;
            }

            _ = SendPacketAsync("disconnect", new Dictionary<string, object>());
            _cts?.Cancel();
            _socket.Dispose();
            _socket = null;
#endif
        }

        public void CreateRoom()
        {
            _ = SendPacketAsync("create_room", new Dictionary<string, object>
            {
                { "roomName", "Young Bob Debug Room" }
            });
        }

        public void MatchOrCreateRoom()
        {
            _ = SendPacketAsync("match_room", new Dictionary<string, object>());
        }

        public void RefreshRoomList()
        {
            _ = SendPacketAsync("get_room_list", new Dictionary<string, object>());
        }

        public void JoinRoom(string roomId)
        {
            _ = SendPacketAsync("join_room", new Dictionary<string, object>
            {
                { "roomId", roomId }
            });
        }

        public void LeaveRoom()
        {
            _ = SendPacketAsync("leave_room", new Dictionary<string, object>());
        }

        public void Send(MultiplayerMessage message)
        {
            // Debug.Log("[DebugRelay] Queue local echo for message " + message.type + " seq=" + message.seq);
            _pendingSends[message.messageId] = message;
            Post(() => MessageReceived?.Invoke(message));
            try
            {
                _ = SendPacketAsync("send_message", BuildMessagePayload(message));
            }
            catch (Exception exception)
            {
                ReportError("DebugRelay send_message build failed: " + exception.Message);
            }
        }

        private async Task SendPacketAsync(string type, IDictionary<string, object> payload)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_isSocketOpen)
            {
                ReportSocketNotOpenOnce();
                return;
            }

            var webPacket = new Dictionary<string, object>
            {
                { "type", type },
                { "payload", payload }
            };
            var webJson = JsonMapper.ToJson(webPacket);
            DebugRelayWebGlBridge.Send(webJson);
            _hasReportedSocketNotOpen = false;
            await System.Threading.Tasks.Task.CompletedTask;
            return;
#else
            if (_socket == null || _socket.State != WebSocketState.Open)
            {
                ReportSocketNotOpenOnce();
                return;
            }

            var packet = new Dictionary<string, object>
            {
                { "type", type },
                { "payload", payload }
            };

            var json = JsonMapper.ToJson(packet);
            var bytes = Encoding.UTF8.GetBytes(json);
            // Debug.Log("[DebugRelay] Sending packet: " + json);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                _hasReportedSocketNotOpen = false;
            }
            catch (Exception exception)
            {
                ReportError("DebugRelay send failed: " + exception.Message);
            }
#endif
        }

        private static Dictionary<string, object> BuildMessagePayload(MultiplayerMessage message)
        {
            var payload = new Dictionary<string, object>
            {
                { "messageId", message.messageId },
                { "type", message.type },
                { "senderPlayerId", message.senderPlayerId },
                { "roomId", message.roomId },
                { "seq", message.seq },
                { "payloadJson", message.payloadJson ?? string.Empty }
            };

            return payload;
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            var messageBuffer = new List<byte>(32 * 1024);
            try
            {
                while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }

                    if (result.Count > 0)
                    {
                        for (var i = 0; i < result.Count; i++)
                        {
                            messageBuffer.Add(buffer[i]);
                        }
                    }

                    if (!result.EndOfMessage)
                    {
                        continue;
                    }

                    if (messageBuffer.Count == 0)
                    {
                        continue;
                    }

                    var json = Encoding.UTF8.GetString(messageBuffer.ToArray());
                    messageBuffer.Clear();
                    // Debug.Log("[DebugRelay] Received packet: " + json);
                    HandlePacket(json);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                ReportError("DebugRelay receive failed: " + exception.Message);
            }
        }
#endif

        private void HandlePacket(string json)
        {
            var packet = JsonMapper.ToObject(json);
            var type = packet["type"].ToString();
            var payload = packet["payload"];

            switch (type)
            {
                case "connected":
                    _localPlayerId = payload["playerId"].ToString();
                    Post(() => Connected?.Invoke(_localPlayerId));
                    break;

                case "room_joined":
                    Post(() => RoomJoined?.Invoke(ParseRoomJoined(payload)));
                    break;

                case "room_left":
                    Post(() => RoomJoined?.Invoke(null));
                    break;

                case "room_list":
                    Post(() => RoomListUpdated?.Invoke(ParseRoomList(payload)));
                    break;

                case "room_message":
                    Post(() => MessageReceived?.Invoke(ParseMessage(payload)));
                    break;

                case "send_ack":
                    if (payload.Keys.Contains("messageId"))
                    {
                        var messageId = payload["messageId"].ToString();
                        if (_pendingSends.ContainsKey(messageId))
                        {
                            _pendingSends.Remove(messageId);
                        }
                    }
                    break;

                case "error":
                    if (payload != null && payload.Keys.Contains("message"))
                    {
                        ReportError("DebugRelay server error: " + payload["message"]);
                    }
                    break;
            }
        }

        private RoomJoinedEvent ParseRoomJoined(JsonData data)
        {
            var room = new RoomJoinedEvent
            {
                roomId = data["roomId"].ToString(),
                localPlayerId = data["localPlayerId"].ToString(),
                hostPlayerId = data["hostPlayerId"].ToString()
            };

            for (var i = 0; i < data["players"].Count; i++)
            {
                var playerData = data["players"][i];
                room.players.Add(new MultiplayerPlayer
                {
                    playerId = playerData["playerId"].ToString(),
                    displayName = playerData["displayName"].ToString(),
                    isLocal = (bool)playerData["isLocal"],
                    isHost = (bool)playerData["isHost"]
                });
            }

            return room;
        }

        private static List<RoomListItem> ParseRoomList(JsonData data)
        {
            var rooms = new List<RoomListItem>();
            for (var i = 0; i < data.Count; i++)
            {
                var room = data[i];
                rooms.Add(new RoomListItem
                {
                    roomId = room["roomId"].ToString(),
                    roomName = room["roomName"].ToString(),
                    roomType = room["roomType"].ToString(),
                    playerCount = (int)room["playerCount"],
                    maxPlayerCount = (int)room["maxPlayerCount"]
                });
            }

            return rooms;
        }

        private static MultiplayerMessage ParseMessage(JsonData data)
        {
            var payloadJson = string.Empty;
            if (data.Keys.Contains("payloadJson"))
            {
                payloadJson = data["payloadJson"].ToString();
            }
            else if (data.Keys.Contains("payload"))
            {
                payloadJson = JsonMapper.ToJson(data["payload"]);
            }

            return new MultiplayerMessage
            {
                messageId = data["messageId"].ToString(),
                type = data["type"].ToString(),
                senderPlayerId = data["senderPlayerId"].ToString(),
                roomId = data["roomId"].ToString(),
                seq = (int)data["seq"],
                payloadJson = payloadJson
            };
        }

        private void Post(Action action)
        {
            _mainThreadContext.Post(_ => action(), null);
        }

        private void ReportError(string message)
        {
            Post(() => TransportError?.Invoke(message));
        }

        private void ReportSocketNotOpenOnce()
        {
            if (_hasReportedSocketNotOpen)
            {
                return;
            }

            _hasReportedSocketNotOpen = true;
            ReportError("DebugRelay send skipped because the socket is not open.");
        }

        public void HandleWebGlOpen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _isSocketOpen = true;
            _hasReportedSocketNotOpen = false;
            // Debug.Log("[DebugRelay] WebGL socket opened.");
            _ = SendPacketAsync("connect", new Dictionary<string, object>
            {
                { "displayName", _pendingDisplayName ?? "Host Bob" }
            });
#endif
        }

        public void HandleWebGlMessage(string json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HandlePacket(json);
#endif
        }

        public void HandleWebGlError(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ReportError("DebugRelay WebGL error: " + message);
#endif
        }

        public void HandleWebGlClose(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _isSocketOpen = false;
            ReportError("DebugRelay socket closed. " + message);
#endif
        }

        private static Uri ResolveServerUri(string serverUrl)
        {
            var uri = new Uri(serverUrl);
#if UNITY_WEBGL && !UNITY_EDITOR
            if (uri.IsLoopback && !string.IsNullOrEmpty(Application.absoluteURL))
            {
                var pageUri = new Uri(Application.absoluteURL);
                var scheme = pageUri.Scheme == "https" ? "wss" : "ws";
                var builder = new UriBuilder(uri)
                {
                    Scheme = scheme,
                    Host = pageUri.Host
                };
                return builder.Uri;
            }
#endif
            return uri;
        }
    }
}
