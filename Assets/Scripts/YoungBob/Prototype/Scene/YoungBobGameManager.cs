using UnityEngine;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.Scene
{
    public sealed class YoungBobGameManager : MonoBehaviour
    {
        private enum TransportMode
        {
            TapTap = 0,
            DebugRelay = 1
        }

        [SerializeField] private YoungBobUiManager uiManager;
        [SerializeField] private TransportMode transportMode = TransportMode.DebugRelay;
        [SerializeField] private string debugRelayUrl = "ws://127.0.0.1:8787";
        [SerializeField] private string localPlayerDisplayName = "Host Bob";

        private PrototypeSessionController _session;

        public PrototypeSessionController Session
        {
            get { return _session; }
        }

        private void Awake()
        {
            if (uiManager == null)
            {
                uiManager = FindObjectOfType<YoungBobUiManager>();
            }

            var dataRepository = GameDataRepository.LoadFromResources();
            var battleEngine = new BattleEngine(dataRepository);
            IMultiplayerService multiplayer = transportMode == TransportMode.DebugRelay
                ? new DebugRelayMultiplayerService(debugRelayUrl)
                : CreateTapTransport();
            _session = new PrototypeSessionController(multiplayer, battleEngine, dataRepository);
        }

        private static IMultiplayerService CreateTapTransport()
        {
#if ((UNITY_WEBGL || UNITY_MINIGAME || WEIXINMINIGAME) && !UNITY_EDITOR) || ((UNITY_WEBGL || UNITY_MINIGAME) && UNITY_EDITOR && TAP_DEBUG_ENABLE)
            return new TapTapRuntimeMultiplayerService();
#else
            return new TapTapMultiplayerService();
#endif
        }

        private void Start()
        {
            if (uiManager == null)
            {
                throw new MissingReferenceException("YoungBobUiManager was not found in the scene.");
            }

            uiManager.Initialize(this, _session);
            _session.StartSession(localPlayerDisplayName);
        }
    }
}
