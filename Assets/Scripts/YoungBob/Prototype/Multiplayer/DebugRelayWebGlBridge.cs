using System.Runtime.InteropServices;
using UnityEngine;

namespace YoungBob.Prototype.Multiplayer
{
    internal sealed class DebugRelayWebGlBridgeCallbacks : MonoBehaviour
    {
        private DebugRelayMultiplayerService _service;

        public static DebugRelayWebGlBridgeCallbacks Create(DebugRelayMultiplayerService service)
        {
            var existing = Object.FindObjectOfType<DebugRelayWebGlBridgeCallbacks>();
            if (existing != null)
            {
                existing._service = service;
                return existing;
            }

            var gameObject = new GameObject("DebugRelayWebGlBridge");
            Object.DontDestroyOnLoad(gameObject);
            var callbacks = gameObject.AddComponent<DebugRelayWebGlBridgeCallbacks>();
            callbacks._service = service;
            return callbacks;
        }

        public void OnDebugRelayOpen(string _)
        {
            _service?.HandleWebGlOpen();
        }

        public void OnDebugRelayMessage(string message)
        {
            _service?.HandleWebGlMessage(message);
        }

        public void OnDebugRelayError(string message)
        {
            _service?.HandleWebGlError(message);
        }

        public void OnDebugRelayClose(string message)
        {
            _service?.HandleWebGlClose(message);
        }
    }

    internal static class DebugRelayWebGlBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DebugRelayWebSocket_Connect(string url, string gameObjectName);

        [DllImport("__Internal")]
        private static extern void DebugRelayWebSocket_Send(string message);

        [DllImport("__Internal")]
        private static extern void DebugRelayWebSocket_Close();

        public static void Connect(string url, string gameObjectName)
        {
            DebugRelayWebSocket_Connect(url, gameObjectName);
        }

        public static void Send(string message)
        {
            DebugRelayWebSocket_Send(message);
        }

        public static void Close()
        {
            DebugRelayWebSocket_Close();
        }
#else
        public static void Connect(string url, string gameObjectName)
        {
        }

        public static void Send(string message)
        {
        }

        public static void Close()
        {
        }
#endif
    }
}
