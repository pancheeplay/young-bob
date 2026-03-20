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
            _session.LogAdded += HandleLogAdded;
            _session.RoomChatAdded += HandleRoomChatAdded;
            _session.RoomChanged += HandleRoomChanged;
            _session.RoomListChanged += HandleRoomListChanged;
            _session.BattleStateChanged += HandleBattleStateChanged;
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
            if (_session != null)
            {
                _session.StatusChanged -= HandleStatusChanged;
                _session.LogAdded -= HandleLogAdded;
                _session.RoomChatAdded -= HandleRoomChatAdded;
                _session.RoomChanged -= HandleRoomChanged;
                _session.RoomListChanged -= HandleRoomListChanged;
                _session.BattleStateChanged -= HandleBattleStateChanged;
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

        private void HandleLogAdded(string message)
        {
            if (_battlePage != null && IsBattleLogMessage(message))
            {
                _battlePage.AppendBattleLog(message);
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
            if (_battlePage != null)
            {
                _battlePage.AppendBattleLog(message);
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

        private static bool IsBattleLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            // Exclude technical logs
            if (message.StartsWith("[") || message.Contains("收到消息") || message.Contains("发送消息"))
            {
                return false;
            }

            // Allow everything else as it's likely a battle event or narratively relevant
            return true;
        }
    }
}
