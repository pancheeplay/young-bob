using System;
using UnityEngine;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.App
{
    internal static class SessionPayloadParser
    {
        public static bool TryParsePayload<T>(MultiplayerMessage message, Action<string> log, out T payload)
            where T : class
        {
            payload = null;
            if (message == null || string.IsNullOrWhiteSpace(message.payloadJson))
            {
                log?.Invoke("忽略 " + (message == null ? "未知消息" : message.type) + "：payload 为空。");
                return false;
            }

            return TryParseJson(message.payloadJson, message.type + " 的 payload", log, out payload);
        }

        public static bool TryParseJson<T>(string json, string context, Action<string> log, out T payload)
            where T : class
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                log?.Invoke("忽略 " + context + "：json 为空。");
                return false;
            }

            try
            {
                payload = JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                log?.Invoke("解析 " + context + " 失败：" + ex.Message);
                return false;
            }

            if (payload == null)
            {
                log?.Invoke("解析 " + context + " 失败：结果为空。");
                return false;
            }

            return true;
        }
    }
}
