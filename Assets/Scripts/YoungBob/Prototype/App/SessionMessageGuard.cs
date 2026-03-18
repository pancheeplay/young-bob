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
                log?.Invoke("忽略消息：消息为空。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentRoomId))
            {
                log?.Invoke("忽略消息 " + message.type + "：当前未加入房间。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(message.type))
            {
                log?.Invoke("忽略消息：类型为空。");
                return false;
            }

            if (!string.Equals(message.roomId, currentRoomId, StringComparison.Ordinal))
            {
                log?.Invoke("忽略消息 " + message.type + "：房间不匹配。");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(message.messageId) && !_processedMessageIds.Add(message.messageId))
            {
                log?.Invoke("忽略消息 " + message.type + "：重复的 messageId " + message.messageId + "。");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(message.senderPlayerId))
            {
                if (_lastSeqBySender.TryGetValue(message.senderPlayerId, out var lastSeq) && message.seq <= lastSeq)
                {
                    log?.Invoke("忽略消息 " + message.type + "：过期 seq " + message.seq + " <= " + lastSeq + "。");
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
