using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Multiplayer
{
    public sealed class LoopbackMultiplayerService : IMultiplayerService
    {
        private const string RoomId = "loopback-room";

        private string _localPlayerId;
        private string _localDisplayName;

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
            get { return "Loopback"; }
        }

        public void Connect(string playerId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                TransportError?.Invoke("Loopback connect requires a player id.");
                return;
            }

            _localPlayerId = playerId;
            _localDisplayName = displayName;
            Connected?.Invoke(_localPlayerId);
        }

        public void MatchOrCreateRoom()
        {
            var remotePlayer = new MultiplayerPlayer
            {
                playerId = "player_2",
                displayName = "Remote Fox",
                isHost = false,
                isLocal = false
            };

            var localPlayer = new MultiplayerPlayer
            {
                playerId = _localPlayerId,
                displayName = _localDisplayName,
                isHost = true,
                isLocal = true
            };

            RoomJoined?.Invoke(new RoomJoinedEvent
            {
                roomId = RoomId,
                localPlayerId = _localPlayerId,
                hostPlayerId = _localPlayerId,
                players = new List<MultiplayerPlayer>
                {
                    localPlayer,
                    remotePlayer
                }
            });
        }

        public void CreateRoom()
        {
            MatchOrCreateRoom();
        }

        public void RefreshRoomList()
        {
            RoomListUpdated?.Invoke(new List<RoomListItem>
            {
                new RoomListItem
                {
                    roomId = RoomId,
                    roomName = "Loopback Room",
                    roomType = "young_bob_proto",
                    playerCount = 1,
                    maxPlayerCount = 2
                }
            });
        }

        public void JoinRoom(string roomId)
        {
            MatchOrCreateRoom();
        }

        public void LeaveRoom()
        {
            RoomJoined?.Invoke(null);
        }

        public void Disconnect()
        {
        }

        public void Send(MultiplayerMessage message)
        {
            MessageReceived?.Invoke(message);
        }
    }
}
