using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Multiplayer
{
    [Serializable]
    public sealed class MultiplayerPlayer
    {
        public string playerId;
        public string displayName;
        public bool isLocal;
        public bool isHost;
    }

    [Serializable]
    public sealed class RoomJoinedEvent
    {
        public string roomId;
        public List<MultiplayerPlayer> players = new List<MultiplayerPlayer>();
        public string localPlayerId;
        public string hostPlayerId;
    }

    [Serializable]
    public sealed class MultiplayerMessage
    {
        public string messageId;
        public string type;
        public string senderPlayerId;
        public string roomId;
        public int seq;
        public string payloadJson;
    }

    [Serializable]
    public sealed class RoomListItem
    {
        public string roomId;
        public string roomName;
        public string roomType;
        public int playerCount;
        public int maxPlayerCount;
    }

    public interface IMultiplayerService
    {
        event Action<string> Connected;
        event Action<string> TransportError;
        event Action<RoomJoinedEvent> RoomJoined;
        event Action<IReadOnlyList<RoomListItem>> RoomListUpdated;
        event Action<MultiplayerMessage> MessageReceived;

        bool IsAvailable { get; }
        string ServiceName { get; }

        void Connect(string playerId, string displayName);
        void Disconnect();
        void CreateRoom();
        void MatchOrCreateRoom();
        void RefreshRoomList();
        void JoinRoom(string roomId);
        void LeaveRoom();
        void Send(MultiplayerMessage message);
    }
}
