using System;
using System.Collections.Generic;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.App
{
    internal sealed class SessionMessageGuard
    {
        private readonly Dictionary<string, int> _lastSeqBySender = new Dictionary<string, int>();
        private readonly HashSet<string> _processedMessageIds = new HashSet<string>(StringComparer.Ordinal);

        public bool TryAccept(MultiplayerMessage message, string currentRoomId, Action<string> log)
        {
            if (message == null)
            {
                log?.Invoke("Ignored message: message is null.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentRoomId))
            {
                log?.Invoke("Ignored message " + message.type + ": no joined room.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(message.type))
            {
                log?.Invoke("Ignored message: empty type.");
                return false;
            }

            if (!string.Equals(message.roomId, currentRoomId, StringComparison.Ordinal))
            {
                log?.Invoke("Ignored message " + message.type + ": room mismatch.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(message.messageId) && !_processedMessageIds.Add(message.messageId))
            {
                log?.Invoke("Ignored message " + message.type + ": duplicate messageId " + message.messageId + ".");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(message.senderPlayerId))
            {
                if (_lastSeqBySender.TryGetValue(message.senderPlayerId, out var lastSeq) && message.seq <= lastSeq)
                {
                    log?.Invoke("Ignored message " + message.type + ": stale seq " + message.seq + " <= " + lastSeq + ".");
                    return false;
                }

                _lastSeqBySender[message.senderPlayerId] = message.seq;
            }

            return true;
        }

        public void Reset()
        {
            _processedMessageIds.Clear();
            _lastSeqBySender.Clear();
        }
    }
}
