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
                log?.Invoke("Ignored " + (message == null ? "unknown" : message.type) + ": empty payload.");
                return false;
            }

            return TryParseJson(message.payloadJson, "payload for " + message.type, log, out payload);
        }

        public static bool TryParseJson<T>(string json, string context, Action<string> log, out T payload)
            where T : class
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                log?.Invoke("Ignored " + context + ": empty json.");
                return false;
            }

            try
            {
                payload = JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                log?.Invoke("Failed to parse " + context + ": " + ex.Message);
                return false;
            }

            if (payload == null)
            {
                log?.Invoke("Failed to parse " + context + ": parsed null.");
                return false;
            }

            return true;
        }
    }
}
