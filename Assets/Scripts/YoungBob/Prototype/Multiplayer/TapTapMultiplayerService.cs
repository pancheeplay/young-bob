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
            throw new NotImplementedException(
                "TapTap runtime is not available for the current compilation target. Switch to the TapTap minigame target, or enable editor debug mode for the package.");
        }

        public void Disconnect()
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
        }

        public void CreateRoom()
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
        }

        public void MatchOrCreateRoom()
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
        }

        public void RefreshRoomList()
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
        }

        public void JoinRoom(string roomId)
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
        }

        public void LeaveRoom()
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
        }

        public void Send(MultiplayerMessage message)
        {
            throw new NotImplementedException("TapTap runtime is not available for the current compilation target.");
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
    }
}
