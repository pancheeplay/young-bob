using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.UI.Pages
{
    internal sealed class LobbyPage : PageBase
    {
        private readonly Transform _roomButtonContainer;

        public LobbyPage(Transform parent, PrototypeSessionController session)
            : base(parent, "LobbyPage", session, new Color(0.13f, 0.16f, 0.19f), new Vector2(0f, 0f), new Vector2(1f, 0.8f))
        {
            var title = UiFactory.CreateText(Root.transform, "Title", 36, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -120f), new Vector2(-40f, -40f));
            title.text = "Young Bob 大厅";

            var controlsPanel = UiFactory.CreatePanel(Root.transform, "Controls", Color.clear, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -450f), new Vector2(-40f, -150f));
            var grid = controlsPanel.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(450f, 100f);
            grid.spacing = new Vector2(20f, 20f);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;

            UiFactory.CreateButton(controlsPanel.transform, "CreateButton", "创建房间", Session.CreateRoom);
            UiFactory.CreateButton(controlsPanel.transform, "MatchButton", "匹配房间", Session.BeginMatchmaking);
            UiFactory.CreateButton(controlsPanel.transform, "RefreshButton", "刷新", Session.RefreshRoomList);
            UiFactory.CreateButton(controlsPanel.transform, "DisconnectButton", "断开连接", Session.Disconnect);

            var roomsText = UiFactory.CreateText(Root.transform, "RoomsTitle", 28, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -520f), new Vector2(-40f, -470f));
            roomsText.text = "可用房间";

            var roomListPanel = UiFactory.CreatePanel(Root.transform, "RoomListPanel", new Color(0.1f, 0.12f, 0.15f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(-40f, -540f));
            var layout = roomListPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15f;
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            _roomButtonContainer = roomListPanel.transform;
        }

        public void RenderRooms(IReadOnlyList<RoomListItem> rooms)
        {
            for (var i = _roomButtonContainer.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_roomButtonContainer.GetChild(i).gameObject);
            }

            if (rooms == null || rooms.Count == 0)
            {
                var empty = UiFactory.CreateText(_roomButtonContainer, "Empty", 24, TextAnchor.MiddleCenter);
                empty.text = "未找到房间。";
                var le = empty.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 100f;
                return;
            }

            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                var button = UiFactory.CreateButton(_roomButtonContainer, "Room_" + room.roomId, room.roomName + " (" + room.playerCount + "/" + room.maxPlayerCount + ")", () => Session.JoinRoom(room.roomId));
                button.GetComponent<LayoutElement>().preferredHeight = 120f;
            }
        }
    }
}
