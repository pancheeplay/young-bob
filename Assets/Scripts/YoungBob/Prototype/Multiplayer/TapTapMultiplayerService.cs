using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Multiplayer
{
    public sealed class TapTapMultiplayerService : IMultiplayerService
    {
        public event Action<string> Connected;
        public event Action<string> TransportError;
        public event Action<RoomJoinedEvent> RoomJoined;
        public event Action<IReadOnlyList<RoomListItem>> RoomListUpdated;
        public event Action<MultiplayerMessage> MessageReceived;

        public bool IsAvailable
        {
            get { return false; }
        }

        public string ServiceName
        {
            get { return "TapTap"; }
        }

        public void Connect(string playerId, string displayName)
        {
            throw CreateUnavailableException();
        }

        public void Disconnect()
        {
            throw CreateUnavailableException();
        }

        public void CreateRoom()
        {
            throw CreateUnavailableException();
        }

        public void MatchOrCreateRoom()
        {
            throw CreateUnavailableException();
        }

        public void RefreshRoomList()
        {
            RoomListUpdated?.Invoke(Array.Empty<RoomListItem>());
            throw CreateUnavailableException();
        }

        public void JoinRoom(string roomId)
        {
            throw CreateUnavailableException();
        }

        public void LeaveRoom()
        {
            throw CreateUnavailableException();
        }

        public void Send(MultiplayerMessage message)
        {
            throw CreateUnavailableException();
        }

        public void RaiseConnected(string playerId)
        {
            Connected?.Invoke(playerId);
        }

        public void RaiseRoomJoined(RoomJoinedEvent evt)
        {
            RoomJoined?.Invoke(evt);
        }

        public void RaiseMessageReceived(MultiplayerMessage message)
        {
            MessageReceived?.Invoke(message);
        }

        private NotImplementedException CreateUnavailableException()
        {
            const string message = "TapTap runtime is not available for the current compilation target.";
            TransportError?.Invoke(message);
            return new NotImplementedException(message);
        }
    }
}
