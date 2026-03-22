using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Multiplayer;
using YoungBob.Prototype.UI;
using YoungBob.Prototype.UI.Pages;

namespace YoungBob.Prototype.Scene
{
    public sealed class YoungBobUiManager : MonoBehaviour
    {
        private const string DefaultUiFontPath = "Assets/TapOnlineBattleDemo/Fonts/Noto Barlow SemiBold.ttf";

        [SerializeField] private Font _defaultUiFont;

        private PrototypeSessionController _session;
        private Canvas _canvas;
        private Text _statusText;
        private Coroutine _autoRefreshCoroutine;
        private Coroutine _battlePhaseAdvanceCoroutine;
        private RuntimeConsolePanel _consolePanel;

        private LobbyPage _lobbyPage;
        private RoomPage _roomPage;
        private BattlePage _battlePage;

        public void Initialize(YoungBobGameManager gameManager, PrototypeSessionController session)
        {
            _session = session;
            UiFactory.SetDefaultFont(ResolveDefaultUiFont());
            EnsureCanvas();

            _lobbyPage = new LobbyPage(_canvas.transform, _session);
            _roomPage = new RoomPage(_canvas.transform, _session);
            _battlePage = new BattlePage(_canvas.transform, _session);
            _consolePanel = new RuntimeConsolePanel(_canvas.transform);

            _session.StatusChanged += HandleStatusChanged;
            _session.BattleNarrationAdded += HandleBattleNarrationAdded;
            _session.BattleNoticeAdded += HandleBattleNoticeAdded;
            _session.RoomChatAdded += HandleRoomChatAdded;
            _session.RoomChanged += HandleRoomChanged;
            _session.RoomListChanged += HandleRoomListChanged;
            _session.BattleStateChanged += HandleBattleStateChanged;
            _session.BattleEventsCommitted += HandleBattleEventsCommitted;
            _session.StageSelectionChanged += HandleStageSelectionChanged;
            Application.logMessageReceived += HandleUnityLog;

            ShowLobby();
            HandleStatusChanged(_session.AvailabilityText);
        }

        private Font ResolveDefaultUiFont()
        {
            if (_defaultUiFont != null)
            {
                return _defaultUiFont;
            }

#if UNITY_EDITOR
            _defaultUiFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(DefaultUiFontPath);
#endif
            return _defaultUiFont;
        }

        private void OnDestroy()
        {
            StopBattlePhaseAdvance();
            if (_session != null)
            {
                _session.StatusChanged -= HandleStatusChanged;
                _session.BattleNarrationAdded -= HandleBattleNarrationAdded;
                _session.BattleNoticeAdded -= HandleBattleNoticeAdded;
                _session.RoomChatAdded -= HandleRoomChatAdded;
                _session.RoomChanged -= HandleRoomChanged;
                _session.RoomListChanged -= HandleRoomListChanged;
                _session.BattleStateChanged -= HandleBattleStateChanged;
                _session.BattleEventsCommitted -= HandleBattleEventsCommitted;
                _session.StageSelectionChanged -= HandleStageSelectionChanged;
            }

            Application.logMessageReceived -= HandleUnityLog;
        }

        private void HandleStatusChanged(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = "传输: " + _session.ServiceName + "\n状态: " + message;
            }
        }

        private void HandleBattleNarrationAdded(string message)
        {
            if (_battlePage != null && !string.IsNullOrWhiteSpace(message))
            {
                _battlePage.AppendBattleLog(message);
            }
        }

        private void HandleBattleNoticeAdded(string message)
        {
            if (_battlePage != null && !string.IsNullOrWhiteSpace(message))
            {
                _battlePage.AppendBattleLog("<color=#F6C978>[系统]</color> " + message);
            }
        }

        private void HandleRoomChanged(RoomJoinedEvent room)
        {
            _roomPage.Render(room);
            if (_session.CurrentBattleState == null)
            {
                if (room == null)
                {
                    ShowLobby();
                }
                else
                {
                    ShowRoom();
                }
            }
        }

        private void HandleRoomListChanged(IReadOnlyList<RoomListItem> rooms)
        {
            _lobbyPage.RenderRooms(rooms);
        }

        private void HandleRoomChatAdded(string message)
        {
            if (_battlePage != null && !string.IsNullOrWhiteSpace(message))
            {
                _battlePage.AppendBattleLog("<color=#7FD3FF>[聊天]</color> " + message);
            }
        }

        private void HandleStageSelectionChanged()
        {
            _roomPage.Render(_session.CurrentRoom);
        }

        private void HandleBattleStateChanged(BattleState battleState)
        {
            _battlePage.Render(battleState);
            if (battleState == null)
            {
                StopBattlePhaseAdvance();
                if (_session.CurrentRoom == null)
                {
                    ShowLobby();
                }
                else
                {
                    ShowRoom();
                }
            }
            else
            {
                ShowBattle();
                ScheduleBattlePhaseAdvance(battleState);
            }
        }

        private void HandleBattleEventsCommitted(IReadOnlyList<BattleEvent> battleEvents)
        {
            if (_battlePage != null && battleEvents != null && battleEvents.Count > 0)
            {
                _battlePage.PlayBattleEvents(battleEvents);
            }
        }

        private void ShowLobby()
        {
            _lobbyPage.Show();
            _roomPage.Hide();
            _battlePage.Hide();
            StartLobbyRefresh();
        }

        private void ShowRoom()
        {
            _lobbyPage.Hide();
            _roomPage.Show();
            _battlePage.Hide();
            StopLobbyRefresh();
        }

        private void ShowBattle()
        {
            _lobbyPage.Hide();
            _roomPage.Hide();
            _battlePage.Show();
            StopLobbyRefresh();
        }

        private void StartLobbyRefresh()
        {
            StopLobbyRefresh();
            _session.RefreshRoomList();
            _autoRefreshCoroutine = StartCoroutine(AutoRefreshLobbyRooms());
        }

        private void StopLobbyRefresh()
        {
            if (_autoRefreshCoroutine != null)
            {
                StopCoroutine(_autoRefreshCoroutine);
                _autoRefreshCoroutine = null;
            }
        }

        private void ScheduleBattlePhaseAdvance(BattleState battleState)
        {
            StopBattlePhaseAdvance();
            if (battleState == null || !_session.CanAutoAdvanceBattlePhase())
            {
                return;
            }

            var delay = ResolveBattlePhaseAdvanceDelay(battleState.phase);
            _battlePhaseAdvanceCoroutine = StartCoroutine(AdvanceBattlePhaseAfterDelay(delay, battleState.phase, battleState.turnIndex));
        }

        private void StopBattlePhaseAdvance()
        {
            if (_battlePhaseAdvanceCoroutine != null)
            {
                StopCoroutine(_battlePhaseAdvanceCoroutine);
                _battlePhaseAdvanceCoroutine = null;
            }
        }

        private IEnumerator AdvanceBattlePhaseAfterDelay(float delaySeconds, BattlePhase expectedPhase, int expectedTurnIndex)
        {
            yield return new WaitForSeconds(delaySeconds);

            var state = _session.CurrentBattleState;
            _battlePhaseAdvanceCoroutine = null;
            if (state == null)
            {
                yield break;
            }

            if (state.phase != expectedPhase || state.turnIndex != expectedTurnIndex)
            {
                yield break;
            }

            _session.AdvanceBattlePhase();
        }

        private static float ResolveBattlePhaseAdvanceDelay(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.MonsterTurnStart:
                    return 0.7f;
                case BattlePhase.MonsterTurnResolve:
                    return 0.95f;
                case BattlePhase.PlayerTurnStart:
                    return 0.6f;
                default:
                    return 0f;
            }
        }

        private IEnumerator AutoRefreshLobbyRooms()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                _session.RefreshRoomList();
            }
        }

        private void EnsureCanvas()
        {
            var canvasObject = new GameObject("YoungBobCanvas");
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // Match width for portrait
            canvasObject.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.transform.SetParent(transform, false);
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }

            var root = UiFactory.CreatePanel(_canvas.transform, "Root", new Color(0.09f, 0.11f, 0.14f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Safe area top margin for status/logs
            _statusText = UiFactory.CreateText(root.transform, "Status", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -120f), new Vector2(-40f, -20f));
            _statusText.supportRichText = true;
        }


        private bool _isLogging;
        private void HandleUnityLog(string condition, string stackTrace, LogType type)
        {
            if (_isLogging) return;
            _isLogging = true;
            try
            {
                var prefix = "[" + type + "] ";
                var message = prefix + condition;
                if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
                {
                    message += "\n" + stackTrace;
                }

                AppendConsole(message);
            }
            finally
            {
                _isLogging = false;
            }
        }

        private void AppendConsole(string message)
        {
            if (_consolePanel != null)
            {
                _consolePanel.Append(message);
            }
        }
    }
}
